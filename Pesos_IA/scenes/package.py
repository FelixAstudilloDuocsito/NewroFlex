"""Congela el candidato de escenas con comprobación de procedencia."""
import argparse
import json
import shutil
from pathlib import Path
from training.run import ROOT, sha


def package(run_dir, output):
    if output.exists(): raise ValueError('No se sobrescriben entregas')
    run = json.loads((run_dir / 'run.json').read_text(encoding='utf-8'))
    if run['status'] != 'completed' or not run.get('adapter_changed') or run['mode'] == 'technical_smoke':
        raise ValueError('Entrenamiento incompleto')
    sft = ROOT / 'data/sft_scenes_v1_1'
    if run['dataset_manifest_sha256'] != sha(sft / 'manifest.json'):
        raise ValueError('Dataset distinto del entrenamiento')
    sft_manifest = json.loads((sft / 'manifest.json').read_text(encoding='utf-8'))
    for name, metadata in sft_manifest['files'].items():
        if sha(sft / name) != metadata['sha256']: raise ValueError('SFT modificado')
    if run['code_sha256'] != sha(ROOT / 'training/run.py'): raise ValueError('Entrenador modificado')
    reports = {}
    for split in ('validation', 'test'):
        report = json.loads((run_dir / f'{split}.json').read_text(encoding='utf-8'))
        if report['status'] != 'completed' or report['run_sha256'] != sha(run_dir / 'run.json'):
            raise ValueError('Informe incompleto o entrenamiento distinto')
        if report['adapter_sha256'] != sha(run_dir / 'adapter/adapter_model.safetensors'):
            raise ValueError('Adaptador modificado')
        if report['source_sha256'] != sha(ROOT / f'data/scenes_v1_1/{split}.jsonl'):
            raise ValueError('Evaluación con datos diferentes')
        for name, digest in report['code_sha256'].items():
            if sha(ROOT / 'scenes' / name) != digest: raise ValueError(f'Código modificado: {name}')
        if len(report['results']) != 24 or report['summary']['adapted']['delivered_exact_reference'] != 24:
            raise ValueError('Evaluación final insuficiente')
        reports[split] = report['summary']
    output.mkdir(parents=True)
    for directory in ('scenes', 'training'):
        (output / directory).mkdir()
    for path in (ROOT / 'scenes').glob('*.py'):
        shutil.copy2(path, output / 'scenes' / path.name)
    for name in ('__init__.py', 'run.py'):
        shutil.copy2(ROOT / 'training' / name, output / 'training' / name)
    (output / 'evaluation').mkdir()
    for name in ('__init__.py', 'contracts.py'):
        shutil.copy2(ROOT / 'evaluation' / name, output / 'evaluation' / name)
    (output / 'schemas').mkdir()
    for name in ('scenes-input.schema.json', 'scenes-output.schema.json'):
        shutil.copy2(ROOT / 'schemas' / name, output / 'schemas' / name)
    (output / 'model').mkdir()
    for name in ('adapter', 'tokenizer'):
        shutil.copytree(run_dir / name, output / 'model' / name)
    shutil.copy2(run_dir / 'run.json', output / 'model/run.json')
    for split in ('validation', 'test'):
        shutil.copy2(run_dir / f'{split}.json', output / f'{split}.json')
    for name in ('scenes_v1_1', 'sft_scenes_v1_1'):
        shutil.copytree(ROOT / 'data' / name, output / 'data' / name)
    shutil.copy2(ROOT / 'docs/metricas_unity.md', output / 'metricas_unity.md')
    shutil.copy2(ROOT / 'docs/estado_escenas.md', output / 'model_card.md')
    shutil.copy2(ROOT / 'training/requirements-lock.txt', output / 'requirements-lock.txt')
    (output / 'infer.py').write_text('from scenes.infer import main\n\nif __name__ == "__main__":\n    main()\n', encoding='utf-8')
    (output / 'README.md').write_text(
        '# NeuroFlex escenas — candidato experimental\n\n'
        f"Base requerida: {run['model']}, revisión {run['revision']}. No incluida; usar la caché existente.\n\n"
        'Desde esta carpeta, en el entorno CUDA del proyecto: '
        '`python -B infer.py --input data/scenes_v1_1/example_request.json --cache-dir D:/NeuroFlex/cache-training`. '
        'La ruta de caché es específica de este equipo; cambiarla en otro. No se descarga la base. '
        'La salida diferencia texto del modelo y sustitución determinista mediante summarySource.\n\n'
        'Instalar PyTorch compatible desde https://download.pytorch.org/whl/cu130 y requirements-lock.txt en un entorno aislado. '
        'Entrenamiento real QLoRA con datos sintéticos; revisión técnica de IA, no validación clínica. '
        'Ver model_card.md y metricas_unity.md. No es una API desplegada. '
        'La integridad del paquete se comprueba contra manifest.json antes de inferir. No editar: crear otra versión.\n', encoding='utf-8')
    manifest = {'stage': 'experimental_frozen_candidate', 'contractVersion': 'scenes-v1', 'approved_for_deployment': False,
        'reviewer': 'Codex (AI)', 'human_review': 'not_performed', 'base_model': run['model'], 'base_revision': run['revision'],
        'evaluation': reports, 'files': {str(path.relative_to(output)).replace('\\', '/'): sha(path) for path in output.rglob('*') if path.is_file()}}
    (output / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--run', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = package(args.run, args.output)
    print(json.dumps({'stage': result['stage'], 'files': len(result['files']), 'evaluation': result['evaluation']}, indent=2))
