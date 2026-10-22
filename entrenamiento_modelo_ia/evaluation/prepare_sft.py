"""Audita el candidato y exporta conversaciones de desarrollo, nunca test a SFT."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
from evaluation.contracts import ROOT, process, schema, validate
from evaluation.evaluate import score


def audit(directory):
    manifest = json.loads((directory / "manifest.json").read_text(encoding="utf-8"))
    partitions = {}
    groups, inputs, identifiers = set(), set(), set()
    for split in ("train", "validation", "test"):
        path = directory / f"{split}.jsonl"
        if hashlib.sha256(path.read_bytes()).hexdigest() != manifest["files"][path.name]["sha256"]:
            raise ValueError(f"Hash inválido: {path.name}")
        rows = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines()]
        if len(rows) != manifest["files"][path.name]["count"]:
            raise ValueError("Conteo incorrecto")
        current_groups = {r["group_id"] for r in rows}
        if groups & current_groups:
            raise ValueError("Grupo compartido entre particiones")
        groups |= current_groups
        for row in rows:
            serialized = json.dumps(row["input"], sort_keys=True)
            if serialized in inputs or row["id"] in identifiers:
                raise ValueError("Entrada o identificador duplicado")
            inputs.add(serialized)
            identifiers.add(row["id"])
            expected = process(row["input"])
            validate(row["expected_output"], schema("salida"))
            checks = score(row["expected_output"], expected)
            if checks["errors"]:
                raise ValueError(f"Referencia incorrecta: {row['id']}: {checks['errors']}")
        partitions[split] = rows
    return partitions


def export(directory, destination, approved_only=False):
    rows = audit(directory)
    if destination.exists():
        raise ValueError("El destino ya existe; no se sobrescribe")
    development = rows["train"] + rows["validation"]
    if approved_only and any(r.get("review_status") != "approved" or not r.get("reviewer") or not r.get("reviewed_at") for r in development):
        raise ValueError("Falta revisión documentada de entrenamiento/validación")
    prompt = (ROOT / "evaluation" / "baseline_system.txt").read_text(encoding="utf-8")
    destination.mkdir(parents=True)
    manifest = {"training_ready": approved_only, "status": "approved" if approved_only else "draft_pending_review",
                "source_manifest_sha256": hashlib.sha256((directory / "manifest.json").read_bytes()).hexdigest(),
                "prompt_sha256": hashlib.sha256(prompt.encode()).hexdigest(), "files": {}}
    for split in ("train", "validation"):
        conversations = [{"messages": [{"role": "system", "content": prompt},
                            {"role": "user", "content": json.dumps(process(row["input"]), ensure_ascii=False)},
                            {"role": "assistant", "content": json.dumps(row["expected_output"], ensure_ascii=False)}]} for row in rows[split]]
        path = destination / f"{split}.jsonl"
        path.write_text("".join(json.dumps(c, ensure_ascii=False) + "\n" for c in conversations), encoding="utf-8")
        manifest["files"][path.name] = {"count": len(conversations), "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "source_ids": [r["id"] for r in rows[split]]}
    (destination / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    with (destination / "revision.csv").open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(["id", "split", "scenario", "resumen", "decision", "reviewer", "reviewed_at", "notes"])
        for split in ("train", "validation"):
            for row in rows[split]:
                writer.writerow([row["id"], split, row.get("scenario", ""), row["expected_output"]["resumen"], "pending", "", "", ""])
    return {split: len(value) for split, value in rows.items()}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--dataset", type=Path, default=ROOT / "data" / "v2")
    parser.add_argument("--output", type=Path, default=ROOT / "data" / "sft_v2_draft")
    parser.add_argument("--approved-only", action="store_true")
    args = parser.parse_args()
    print(export(args.dataset, args.output, args.approved_only))
