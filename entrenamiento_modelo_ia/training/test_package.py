import json
import tempfile
import unittest
from pathlib import Path
from training.package_candidate import package
from training.run import sha, ROOT


class PackageTests(unittest.TestCase):
    def test_snapshot_hashes_and_rejects_changed_adapter(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            run_dir = root / "run"
            (run_dir / "adapter").mkdir(parents=True)
            (run_dir / "tokenizer").mkdir()
            weights = run_dir / "adapter" / "adapter_model.safetensors"
            weights.write_bytes(b"test weights")
            (run_dir / "run.json").write_text(json.dumps({"status": "completed", "adapter_changed": True,
                "mode": "experimental_training", "model": "test", "revision": "a" * 40}), encoding="utf-8")
            evaluation = root / "evaluation.json"
            evaluation.write_text(json.dumps({"status": "completed", "run_sha256": sha(run_dir / "run.json"),
                "adapter_sha256": sha(weights), "model": "test", "revision": "a" * 40,
                "prompt_sha256": sha(ROOT / "evaluation" / "baseline_system.txt"),
                "schema_sha256": sha(ROOT / "schemas" / "salida.schema.json"),
                "results": [{"id": "fixture"}], "summary": {"base": {"total": 1}, "adapted": {"total": 1}}}), encoding="utf-8")
            target = root / "candidate"
            manifest = package(run_dir, evaluation, target)
            self.assertFalse(manifest["approved_for_deployment"])
            self.assertTrue((target / "evaluation" / "contracts.py").exists())
            self.assertTrue((target / "evaluation" / "summary_guard.py").exists())
            self.assertEqual(manifest["summary_policy"], "exact_template_v1")
            for name, expected in manifest["files"].items():
                self.assertEqual(sha(target / name), expected)
            with self.assertRaises(ValueError):
                package(run_dir, evaluation, target)
            weights.write_bytes(b"changed")
            with self.assertRaises(ValueError):
                package(run_dir, evaluation, root / "changed")


if __name__ == "__main__":
    unittest.main()
