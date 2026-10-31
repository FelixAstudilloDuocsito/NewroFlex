"""Exporta el candidato congelado de escenas como modelo completo y un ZIP."""
import argparse
import hashlib
import json
import os
import platform
from importlib import metadata
from pathlib import Path
import shutil
import zipfile


def sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(8 * 1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def verify(source, expected):
    manifest_path = source / 'manifest.json'
    if sha(manifest_path) != expected:
        raise ValueError('El manifiesto no corresponde a la entrega esperada')
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    if manifest.get('contractVersion') != 'scenes-v1':
        raise ValueError('Solo se admite el candidato scenes-v1')
    for name, digest in manifest['files'].items():
        path = (source / name).resolve()
        if not path.is_relative_to(source) or not path.is_file() or sha(path) != digest:
            raise ValueError(f'Archivo modificado o ausente: {name}')
    required = {'model/run.json', 'model/adapter/adapter_model.safetensors',
                'model/adapter/adapter_config.json'}
    if not required.issubset(manifest['files']):
        raise ValueError('El manifiesto no cubre el adaptador y su procedencia')
    run = json.loads((source / 'model/run.json').read_text(encoding='utf-8'))
    if (run.get('status') != 'completed' or not run.get('adapter_changed')
            or run['model'] != manifest['base_model'] or run['revision'] != manifest['base_revision']):
        raise ValueError('Entrenamiento y manifiesto incompatibles')
    return manifest, run


def merge(source, output, cache_dir, expected):
    source, output = source.resolve(), output.resolve()
    archive = output.with_name(output.name + '.zip')
    if output.exists() or archive.exists():
        raise ValueError('No se sobrescriben carpetas ni ZIP existentes')
    if output.is_relative_to(source) or source.is_relative_to(output):
        raise ValueError('Origen y destino deben ser carpetas independientes')
    manifest, run = verify(source, expected)
    os.environ['HF_HOME'] = str(cache_dir.resolve())
    os.environ['HF_HUB_OFFLINE'] = '1'
    # Importar después de definir HF_HOME. No descargar otra copia de la base.
    import torch
    from transformers import AutoModelForCausalLM, AutoTokenizer
    from peft import PeftModel

    print('Cargando la base exacta en FP32 sobre CPU; puede requerir bastante RAM.', flush=True)
    base = AutoModelForCausalLM.from_pretrained(
        run['model'], revision=run['revision'], local_files_only=True,
        dtype=torch.float32, device_map={'': 'cpu'}, attn_implementation='sdpa',
    )
    adapted = PeftModel.from_pretrained(base, source / 'model/adapter', is_trainable=False)
    fused = adapted.merge_and_unload(safe_merge=True).to(dtype=torch.float16).eval()
    tokenizer = AutoTokenizer.from_pretrained(source / 'model/tokenizer', local_files_only=True)
    output.mkdir(parents=True)
    # El candidato original se conserva. Si falla la exportación, el destino parcial
    # queda disponible para diagnóstico y no se crea un ZIP considerado completo.
    print('Guardando pesos fusionados FP16 y tokenizer.', flush=True)
    fused.save_pretrained(output / 'model', max_shard_size='50GB')
    tokenizer.save_pretrained(output / 'model')
    # Conservar avisos de la base cuando ya están presentes en la caché local.
    cached_license = (cache_dir / 'hub' / ('models--' + run['model'].replace('/', '--'))
                      / 'snapshots' / run['revision'] / 'LICENSE')
    if cached_license.is_file():
        shutil.copy2(cached_license, output / 'model/LICENSE')
    if not (output / 'model/model.safetensors').is_file():
        raise ValueError('Se esperaba un único archivo model.safetensors')
    root = Path(__file__).resolve().parents[1]
    for directory in ('scenes', 'evaluation', 'schemas', 'data'):
        shutil.copytree(source / directory, output / directory,
                        ignore=shutil.ignore_patterns('__pycache__', '*.pyc'))
    shutil.copy2(root / 'scenes/merged_runtime.py', output / 'scenes/runtime.py')
    shutil.copy2(root / 'training/verify_merged.py', output / 'verify_merged.py')
    for name in ('metricas_unity.md', 'model_card.md', 'requirements-lock.txt'):
        shutil.copy2(source / name, output / name)
    provenance = output / 'provenance'
    provenance.mkdir()
    for name in ('manifest.json', 'validation.json', 'test.json'):
        shutil.copy2(source / name, provenance / name)
    shutil.copy2(source / 'model/run.json', provenance / 'run.json')
    environment = {'python': platform.python_version(), 'platform': platform.platform(),
                   'packages': {d.metadata['Name']: d.version for d in metadata.distributions()
                                if d.metadata['Name']}}
    (provenance / 'export_environment.json').write_text(
        json.dumps(environment, indent=2) + '\n', encoding='utf-8')
    shutil.copy2(root / 'training/merged_infer.py', output / 'infer.py')
    (output / 'README.md').write_text(
        '# NeuroFlex escenas: modelo fusionado FP16\n\n'
        'Incluye base, ajuste entrenado, tokenizer, métricas y control de resúmenes. '
        'No necesita la caché original ni PEFT para inferir. Requiere Python, '
        'PyTorch/Transformers/Accelerate compatibles y GPU NVIDIA CUDA. '
        'requirements-lock.txt conserva el entorno ORIGINAL como referencia; '
        'hay que verificar compatibilidad en el equipo receptor y registrar su entorno.\n\n'
        'Desde esta carpeta: `python -B infer.py --input data/scenes_v1_1/example_request.json`.\n\n'
        'Sin IA: `python -B infer.py --preview --input data/scenes_v1_1/example_request.json`.\n\n'
        'Antes de entregar para despliegue, ejecutar '
        '`python -B verify_merged.py --output ../verificacion-fusion.json` con GPU. '
        'Guardar ese informe fuera del paquete. La fusión FP16 cambia la configuración '
        'numérica respecto del candidato NF4; no se garantiza igualdad de respuestas. '
        'Los informes de provenance corresponden al candidato ORIGINAL, no al fusionado.\n\n'
        'No contiene un servidor HTTP ni el entorno Python. El ZIP se debe extraer. '
        'No es un ejecutable autónomo ni una imagen de SageMaker. '
        'Conservar el modelo original para comparación.\n', encoding='utf-8')
    exported = {'stage': 'merged_candidate_pending_evaluation', 'contractVersion': 'scenes-v1',
                'approved_for_deployment': False, 'base_model': run['model'],
                'base_revision': run['revision'], 'source_manifest_sha256': expected,
                'source_adapter_sha256': manifest['files']['model/adapter/adapter_model.safetensors'],
                'merge_dtype': 'float32', 'saved_dtype': 'float16',
                'files': {p.relative_to(output).as_posix(): sha(p)
                          for p in sorted(output.rglob('*')) if p.is_file()}}
    (output / 'manifest.json').write_text(json.dumps(exported, indent=2) + '\n', encoding='utf-8')
    # ZIP64 admite pesos de más de 4 GiB. Compresión ligera para reducir espacio.
    with zipfile.ZipFile(archive, 'x', compression=zipfile.ZIP_DEFLATED,
                         compresslevel=1, allowZip64=True) as bundle:
        for path in sorted(output.rglob('*')):
            if path.is_file():
                bundle.write(path, arcname=(Path(output.name) / path.relative_to(output)).as_posix())
    print(json.dumps({'archive': str(archive), 'sha256': sha(archive),
                      'status': exported['stage']}, indent=2), flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--cache-dir', type=Path, required=True, help='Raíz HF_HOME con la base ya descargada')
    parser.add_argument('--expected-manifest-sha256', required=True)
    args = parser.parse_args()
    merge(args.package, args.output, args.cache_dir, args.expected_manifest_sha256)


if __name__ == '__main__':
    main()
