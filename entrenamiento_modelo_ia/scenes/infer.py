import argparse
import hashlib
import json
import os
from pathlib import Path
from scenes.contracts import ROOT, process
from scenes.summaries import response


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--input', type=Path, required=True)
    parser.add_argument('--run', type=Path)
    parser.add_argument('--cache-dir', type=Path)
    parser.add_argument('--preview', action='store_true', help='Resumen determinista sin cargar IA')
    args = parser.parse_args()
    manifest_path = ROOT / 'manifest.json'
    if manifest_path.exists():
        manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
        for name, digest in manifest['files'].items():
            path = (ROOT / name).resolve()
            if not path.is_relative_to(ROOT) or hashlib.sha256(path.read_bytes()).hexdigest() != digest:
                raise ValueError(f'Archivo modificado: {name}')
    payload = json.loads(args.input.read_text(encoding='utf-8'))
    process(payload)  # rechazar antes de cargar GPU
    generator = None
    if not args.preview:
        if args.cache_dir: os.environ['HF_HOME'] = str(args.cache_dir.resolve())
        if not os.environ.get('HF_HOME'): raise ValueError('Indica --cache-dir o HF_HOME')
        from scenes.runtime import Generator
        generator = Generator(args.run or ROOT / 'model')
    print(json.dumps(response(payload, generator), ensure_ascii=False, indent=2))


if __name__ == '__main__': main()
