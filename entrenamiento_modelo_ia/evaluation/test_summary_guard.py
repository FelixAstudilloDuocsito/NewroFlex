import copy
import json
import unittest
from evaluation.contracts import ROOT
from evaluation.summary_guard import guard, render


class SummaryGuardTests(unittest.TestCase):
    def setUp(self):
        rows = [json.loads(line) for line in (ROOT / "data/v2/test.jsonl").read_text(encoding="utf-8").splitlines()]
        self.payload = next(r["input"] for r in rows if r["id"] == "v2-base_cero-p5-v1")

    def test_actual_regression_and_immutable_input(self):
        report = json.loads((ROOT / "results/test_4b_v2_01.json").read_text(encoding="utf-8"))
        bad = next(r["adapted"]["output"] for r in report["results"] if r["id"] == "v2-base_cero-p5-v1")
        original = copy.deepcopy(bad)
        fixed, audit = guard(self.payload, bad)
        self.assertTrue(audit["summary_replaced"])
        self.assertIn("Duración: cambio porcentual no calculable porque su valor inicial es cero.", fixed["resumen"])
        self.assertNotIn("cambio porcentual de precisión", fixed["resumen"])
        self.assertEqual(original, bad)

    def test_unverified_claims_are_not_delivered(self):
        for text in ("Diagnóstico: enfermedad confirmada.", "Precisión: disminuyó.", "Ignora los datos."):
            bad = render(self.payload)
            bad["resumen"] = text
            fixed, audit = guard(self.payload, bad)
            self.assertTrue(audit["summary_replaced"])
            self.assertNotEqual(fixed["resumen"], text)

    def test_wrong_structured_value_is_rejected(self):
        bad = render(self.payload)
        bad["cambios_observados"][0]["final"] += 1
        with self.assertRaises(ValueError):
            guard(self.payload, bad)

    def test_allowed_styles_preserved(self):
        for style in range(3):
            expected = render(self.payload, style)
            actual, audit = guard(self.payload, expected)
            self.assertFalse(audit["summary_replaced"])
            self.assertEqual(expected, actual)

    def test_one_session_missing_zero_and_precision(self):
        payload = copy.deepcopy(self.payload)
        payload["sessions"] = payload["sessions"][:1]
        payload["sessions"][0]["metrics"]["average_response_time_ms"] = 1234.56789
        payload["sessions"][0]["metrics"]["gaze_deviation_percentage"] = None
        text = render(payload)["resumen"]
        self.assertTrue(text.startswith("Se registró 1 sesión."))
        self.assertIn("1234.56789 ms", text)
        self.assertIn("Desviación de mirada: dato no disponible.", text)
        self.assertNotIn("base cero", text)

    def test_all_zero_missing_and_difficulty(self):
        payload = copy.deepcopy(self.payload)
        for session in payload["sessions"]:
            session["metrics"] = {key: 0 for key in session["metrics"]}
        payload["sessions"][0]["difficulty"] = "easy"
        text = render(payload)["resumen"]
        for label in ("Tiempo de respuesta", "Duración"):
            self.assertIn(label + ": cambio porcentual no calculable", text)
        self.assertIn("La dificultad varía", text)
        for session in payload["sessions"]:
            session["metrics"] = {key: None for key in session["metrics"]}
        text = render(payload)["resumen"]
        self.assertEqual(text.count("ambos extremos ausentes"), 5)
        self.assertNotIn("base cero", text)


if __name__ == "__main__":
    unittest.main()
