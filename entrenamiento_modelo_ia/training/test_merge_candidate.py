"""Pruebas de empaquetado con pesos simulados; no validan la fusión numérica."""
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import MagicMock, patch
import zipfile

from training.merge_candidate import merge, sha, verify
from training.verify_merged import verify_package

ROOT = Path(__file__).resolve().parents[1]


class MergePackageTests(unittest.TestCase):
    def make_source(self, root):
        source = root / 'original'
        for directory in ('scenes', 'evaluation', 'schemas', 'data'):
            shutil.copytree(ROOT / directory, source / directory,
                            ignore=shutil.ignore_patterns('__pycache__', '*.pyc'))
        (source / 'model/adapter').mkdir(parents=True)
        (source / 'model/tokenizer').mkdir()
        (source / 'model/adapter/adapter_model.safetensors').write_bytes(b'fixture adapter')
        (source / 'model/adapter/adapter_config.json').write_text('{}')
        (source / 'model/run.json').write_text(json.dumps({
            'status': 'completed', 'adapter_changed': True, 'model': 'fixture', 'revision': 'a' * 40}))
        for name in ('metricas_unity.md', 'model_card.md', 'requirements-lock.txt', 'validation.json', 'test.json'):
            (source / name).write_text('fixture')
        manifest = {'contractVersion': 'scenes-v1', 'base_model': 'fixture', 'base_revision': 'a' * 40,
                    'files': {p.relative_to(source).as_posix(): sha(p)
                              for p in source.rglob('*') if p.is_file()}}
        (source / 'manifest.json').write_text(json.dumps(manifest))
        return source, sha(source / 'manifest.json')

    def test_rejects_tampering_and_manifest_path_escape(self):
        with tempfile.TemporaryDirectory() as tmp:
            source, expected = self.make_source(Path(tmp))
            verify(source, expected)
            weights = source / 'model/adapter/adapter_model.safetensors'
            weights.write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError, 'modificado'):
                verify(source, expected)
            manifest = json.loads((source / 'manifest.json').read_text())
            manifest['files'] = {'../outside': 'unused'}
            (source / 'manifest.json').write_text(json.dumps(manifest))
            with self.assertRaisesRegex(ValueError, 'modificado'):
                verify(source, sha(source / 'manifest.json'))
            with self.assertRaisesRegex(ValueError, 'manifiesto'):
                verify(source, expected)

    def test_single_archive_offline_export_and_preview(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            source, expected = self.make_source(root)
            output = root / 'merged'
            model = MagicMock()

            def save_model(path, **kwargs):
                path.mkdir()
                (path / 'model.safetensors').write_bytes(b'fixture merged weights')
                (path / 'config.json').write_text('{}')

            model.save_pretrained.side_effect = save_model
            modules = {name: MagicMock() for name in ('torch', 'transformers', 'peft')}
            modules['peft'].PeftModel.from_pretrained.return_value.merge_and_unload.return_value.to.return_value.eval.return_value = model
            with patch.dict(sys.modules, modules), patch.dict('os.environ'):
                merge(source, output, root / 'cache', expected)
            modules['peft'].PeftModel.from_pretrained.return_value.merge_and_unload.assert_called_once_with(safe_merge=True)
            base_call = modules['transformers'].AutoModelForCausalLM.from_pretrained.call_args
            self.assertTrue(base_call.kwargs['local_files_only'])
            self.assertEqual(base_call.kwargs['revision'], 'a' * 40)
            self.assertNotIn('quantization_config', base_call.kwargs)
            verify_package(output)
            self.assertFalse((output / 'model/adapter').exists())
            with zipfile.ZipFile(root / 'merged.zip') as bundle:
                self.assertIsNone(bundle.testzip())
                self.assertIn('merged/model/model.safetensors', bundle.namelist())
                self.assertIn('merged/infer.py', bundle.namelist())
            result = subprocess.run([
                sys.executable, '-B', str(output / 'infer.py'), '--preview',
                '--input', str(output / 'data/scenes_v1_1/example_request.json'),
            ], cwd=root, capture_output=True, text=True, check=True)
            response = json.loads(result.stdout)
            self.assertEqual(len(response['rounds']), 3)
            self.assertTrue(all(r['summarySource'] == 'deterministic_preview' for r in response['rounds']))
            with self.assertRaisesRegex(ValueError, 'sobrescriben'):
                merge(source, output, root / 'cache', expected)
            (output / 'model/model.safetensors').write_bytes(b'tampered')
            with self.assertRaisesRegex(ValueError, 'modificado'):
                verify_package(output)
            verify(source, expected)  # El candidato original sigue intacto.


if __name__ == '__main__':
    unittest.main()
