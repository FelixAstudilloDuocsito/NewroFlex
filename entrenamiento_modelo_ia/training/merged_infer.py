"""Entrada CLI copiada al paquete fusionado."""
import argparse
import json
from pathlib import Path
from verify_merged import verify_package
from scenes.contracts import process
from scenes.summaries import response


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--input', type=Path, required=True)
    parser.add_argument('--preview', action='store_true')
    args = parser.parse_args()
    root = Path(__file__).resolve().parent
    verify_package(root)
    payload = json.loads(args.input.read_text(encoding='utf-8'))
    process(payload)
    generator = None
    if not args.preview:
        from scenes.runtime import Generator
        generator = Generator(root / 'model')
    print(json.dumps(response(payload, generator), ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
