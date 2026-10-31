"""Verifica el paquete fusionado y evalúa su corpus con GPU; no modifica la entrega."""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import time


def sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(8 * 1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def verify_package(root):
    root = root.resolve()
    manifest = json.loads((root / 'manifest.json').read_text(encoding='utf-8'))
    if manifest.get('stage') != 'merged_candidate_pending_evaluation':
        raise ValueError('Se requiere un paquete fusionado')
    for name, expected in manifest['files'].items():
        path = (root / name).resolve()
        if not path.is_relative_to(root) or not path.is_file() or sha(path) != expected:
            raise ValueError(f'Archivo modificado o ausente: {name}')
    return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent
    if args.output.resolve().is_relative_to(root):
        raise ValueError('Guardar el informe fuera del paquete congelado')
    verify_package(root)
    from scenes.contracts import model_input
    from scenes.summaries import guard, render
    from scenes.runtime import Generator

    report = {'status': 'running', 'manifest_sha256': sha(root / 'manifest.json'), 'splits': {}}
    # Reservar el informe antes de cargar el modelo; nunca sobrescribir evidencia.
    with args.output.open('x', encoding='utf-8') as stream:
        json.dump(report, stream, indent=2)

    def save():
        args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

    try:
        generator = Generator(root / 'model')
        report['runtime'] = {'torch': generator.torch.__version__,
                             'cuda': generator.torch.version.cuda,
                             'gpu': generator.torch.cuda.get_device_name(0)}
        for split in ('validation', 'test'):
            source = root / 'data/scenes_v1_1' / f'{split}.jsonl'
            rows = [json.loads(line) for line in source.read_text(encoding='utf-8').splitlines() if line.strip()]
            if not rows:
                raise ValueError(f'Corpus vacío: {split}')
            results = []
            report['splits'][split] = {'source_sha256': sha(source), 'results': results}
            started = time.perf_counter()
            for row in rows:
                raw = generator.batch([model_input(row['input'])])[0]
                try:
                    answer, valid = json.loads(raw), True
                except ValueError:
                    answer, valid = None, False
                delivered, trace = guard(row['input'], answer)
                results.append({'id': row['id'], 'raw_output': raw, 'json_valid': valid,
                                'raw_exact_reference': answer == row['expected_output'],
                                'trace': trace, 'delivered': delivered,
                                'delivered_exact_reference': delivered == render(row['input'])})
                save()
                if len(results) % 8 == 0 or len(results) == len(rows):
                    print(f'{split}: {len(results)}/{len(rows)}', file=sys.stderr, flush=True)
            report['splits'][split]['summary'] = {
                'total': len(results), 'json_valid': sum(r['json_valid'] for r in results),
                'raw_exact_reference': sum(r['raw_exact_reference'] for r in results),
                'fallback_count': sum(r['trace']['summarySource'] == 'deterministic_fallback' for r in results),
                'delivered_exact_reference': sum(r['delivered_exact_reference'] for r in results),
            }
            report['splits'][split]['elapsed_seconds'] = time.perf_counter() - started
        report['status'] = 'completed'
        save()
        if any(s['summary']['delivered_exact_reference'] != s['summary']['total']
               for s in report['splits'].values()):
            raise ValueError('Las respuestas entregadas no cumplen las referencias; revisar el informe')
    except Exception as exc:
        report.update(status='failed', error=str(exc))
        save()
        raise
    print(json.dumps({split: data['summary'] for split, data in report['splits'].items()}, indent=2))


if __name__ == '__main__':
    main()
