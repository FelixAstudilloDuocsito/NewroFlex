"""Revisión técnica asistida y replay de salidas históricas, sin entrenar pesos."""
import json
from pathlib import Path
from evaluation.contracts import ROOT
from evaluation.prepare_sft import audit
from evaluation.evaluate import digest, score
from evaluation.summary_guard import guard, render


def main():
    dataset = ROOT / "data/v2"
    destination = ROOT / "data/v2_reviewed_ai"
    output = ROOT / "results/review_v0_2.json"
    if destination.exists() or output.exists():
        raise ValueError("No se sobrescriben revisiones")
    partitions = audit(dataset)
    report = {"reviewer": "Codex (AI)", "review_type": "technical_assistant_review",
              "human_review": "not_performed", "new_training": False,
              "policy": "exact_template_v1", "guard_sha256": digest(ROOT / "evaluation/summary_guard.py"),
              "source_dataset_sha256": digest(dataset / "manifest.json"),
              "reference_review": [], "replay": [], "source_reports": {},
              "limitations": ["Replay de casos ya observados; no es prueba reservada nueva.",
                              "Fallback no equivale a error semántico: también rechaza paráfrasis correctas.",
                              "La mejora corresponde al procesamiento de salida, no a nuevos pesos."]}
    destination.mkdir()
    manifest = {"version": "v2-reviewed-ai", "reviewer": "Codex (AI)", "human_review": "not_performed",
                "training_ready": False, "used_for_training": False, "files": {}}
    for split, rows in partitions.items():
        for row in rows:
            corrected = render(row["input"])
            assert not score(corrected, row["expected_output"])["errors"]
            report["reference_review"].append({"id": row["id"], "split": split,
                "structured_checks": "passed", "summary_changed": corrected["resumen"] != row["expected_output"]["resumen"]})
            row["expected_output"] = corrected
            row["review_status"] = "reviewed_by_ai"
            row["reviewer"] = "Codex (AI)"
        path = destination / f"{split}.jsonl"
        path.write_text("".join(json.dumps(row, ensure_ascii=False) + "\n" for row in rows), encoding="utf-8")
        manifest["files"][path.name] = {"count": len(rows), "sha256": digest(path)}
    (destination / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    for split in ("validation", "test"):
        source = ROOT / "results" / f"{split}_4b_v2_01.json"
        original = json.loads(source.read_text(encoding="utf-8"))
        assert original["status"] == "completed"
        assert original["dataset_sha256"] == digest(dataset / f"{split}.jsonl")
        report["source_reports"][split] = digest(source)
        by_id = {row["id"]: row for row in partitions[split]}
        for result in original["results"]:
            row = by_id[result["id"]]
            entry = {"id": row["id"], "split": split}
            for variant in ("base", "adapted"):
                fixed, trace = guard(row["input"], result[variant]["output"])
                checks = score(fixed, row["expected_output"])
                assert not checks["errors"]
                assert fixed["resumen"] in {render(row["input"], style)["resumen"] for style in range(3)}
                entry[variant] = {"trace": trace, "output": fixed, "checks": checks}
            report["replay"].append(entry)
    report["summary"] = {"references_reviewed": len(report["reference_review"]), "replayed_cases": len(report["replay"]),
        **{name: {"passed_after_guard": len(report["replay"]),
                  "summary_replacements": sum(row[name]["trace"]["summary_replaced"] for row in report["replay"])} for name in ("base", "adapted")}}
    report["status"] = "completed"
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report["summary"], indent=2))


if __name__ == "__main__":
    main()
