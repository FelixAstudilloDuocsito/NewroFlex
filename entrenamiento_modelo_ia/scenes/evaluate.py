import argparse
import json
from pathlib import Path
from training.run import ROOT, sha
from scenes.contracts import model_input
from scenes.summaries import guard, render


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--run', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--split', choices=['validation', 'test'], required=True)
    args = parser.parse_args()
    if args.output.exists(): raise ValueError('Informe ya existe')
    source = ROOT / 'data/scenes_v1_1' / f'{args.split}.jsonl'
    rows = [json.loads(line) for line in source.read_text(encoding='utf-8').splitlines()]
    report = {'status': 'running', 'split': args.split, 'source_sha256': sha(source),
        'run_sha256': sha(args.run / 'run.json'), 'adapter_sha256': sha(args.run / 'adapter/adapter_model.safetensors'),
        'code_sha256': {name: sha(ROOT / 'scenes' / name) for name in ('contracts.py', 'summaries.py', 'runtime.py', 'evaluate.py')},
        'results': [], 'reviewer': 'Codex (AI)', 'human_review': 'not_performed'}
    def save(): args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    save()
    try:
        from scenes.runtime import Generator
        generator = Generator(args.run)
        for offset in range(0, len(rows), 4):
            group = rows[offset:offset + 4]
            results = [{'id': row['id']} for row in group]
            for variant in ('base', 'adapted'):
                raw = generator.batch([model_input(row['input']) for row in group], base=variant == 'base')
                for row, result, text in zip(group, results, raw):
                    try:
                        answer = json.loads(text)
                        parsed = True
                    except ValueError:
                        answer, parsed = None, False
                    fixed, trace = guard(row['input'], answer)
                    result[variant] = {'raw_output': text, 'json_valid': parsed,
                        'exact_reference_match': answer == row['expected_output'],
                        'trace': trace, 'delivered': fixed, 'delivered_matches_reference': fixed == render(row['input'])}
            report['results'].extend(results); save()
            print(f"{args.split}: {len(report['results'])}/{len(rows)}", flush=True)
        report['summary'] = {name: {'total': len(rows),
            'json_valid': sum(r[name]['json_valid'] for r in report['results']),
            'raw_exact_reference': sum(r[name]['exact_reference_match'] for r in report['results']),
            'fallback_count': sum(r[name]['trace']['summarySource'] == 'deterministic_fallback' for r in report['results']),
            'delivered_exact_reference': sum(r[name]['delivered_matches_reference'] for r in report['results'])} for name in ('base', 'adapted')}
        report['status'] = 'completed'; save()
    except Exception as exc:
        report['status'] = 'failed'; report['error'] = str(exc); save(); raise


if __name__ == '__main__': main()
