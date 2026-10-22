import copy
import json
import tempfile
import unittest
from pathlib import Path
from evaluation.contracts import ROOT
from evaluation.dataset_v2 import render, build
from evaluation.prepare_sft import audit, export


class CandidateTests(unittest.TestCase):
    def payload(self):
        return {"sessions": [
            {"session_number": i + 1, "difficulty": "medium", "metrics": {
                "accuracy_percentage": accuracy, "average_response_time_ms": speed,
                "gaze_deviation_percentage": 20, "controller_tremor_index": .2,
                "session_duration_seconds": 500}}
            for i, (accuracy, speed) in enumerate([(61, 0), (82, 4000)])]}

    def test_units_and_zero_are_not_missing(self):
        answer = render(self.payload())
        self.assertIn("61 % a 82 %", answer["resumen"])
        self.assertIn("21 puntos porcentuales", answer["resumen"])
        self.assertIn("0 ms a 4000 ms", answer["resumen"])
        self.assertNotIn("datos_faltantes", answer["advertencias"])
        self.assertIsNone(answer["cambios_observados"][1]["valor"])

    def test_missing_and_single_distinct_from_zero(self):
        payload = self.payload()
        payload["sessions"] = payload["sessions"][:1]
        payload["sessions"][0]["metrics"]["accuracy_percentage"] = None
        text = render(payload)["resumen"]
        self.assertIn("Precisión: dato no disponible", text)
        self.assertIn("Tiempo de respuesta: 0 ms", text)
        self.assertIn("Una sola sesión", text)

    def test_all_scenarios_present_and_no_legacy_duplicates(self):
        partitions = audit(ROOT / "data" / "v2")
        old = {json.dumps(row["input"], sort_keys=True) for split in ("train", "validation", "test")
               for row in map(json.loads, (ROOT / "data" / f"{split}.jsonl").read_text(encoding="utf-8").splitlines())}
        for split, rows in partitions.items():
            self.assertEqual(len({r["scenario"] for r in rows}), 10)
            for row in rows:
                self.assertNotIn(json.dumps(row["input"], sort_keys=True), old)

    def test_draft_export_excludes_test_and_blocks_unreviewed(self):
        with tempfile.TemporaryDirectory() as tmp:
            destination = Path(tmp) / "draft"
            export(ROOT / "data" / "v2", destination)
            self.assertFalse((destination / "test.jsonl").exists())
            manifest = json.loads((destination / "manifest.json").read_text(encoding="utf-8"))
            self.assertFalse(manifest["training_ready"])
            with self.assertRaises(ValueError):
                export(ROOT / "data" / "v2", Path(tmp) / "approved", approved_only=True)
            with self.assertRaises(ValueError):
                export(ROOT / "data" / "v2", destination)

    def test_reproducible_generator(self):
        with tempfile.TemporaryDirectory() as tmp:
            destination = Path(tmp) / "v2"
            build(destination)
            for split in ("train", "validation", "test"):
                self.assertEqual((destination / f"{split}.jsonl").read_bytes(), (ROOT / "data" / "v2" / f"{split}.jsonl").read_bytes())
            with self.assertRaises(ValueError):
                build(destination)


if __name__ == "__main__":
    unittest.main()
