"""Exporta resultados completos para revisión humana; no marca aprobaciones."""
import argparse
import csv
import json
from pathlib import Path

from evaluation.evaluate import digest


def export(report_path, dataset_path, output):
    report = json.loads(report_path.read_text(encoding="utf-8"))
    if report["status"] != "completed":
        raise ValueError("La evaluación todavía no termina")
    if report["dataset_sha256"] != digest(dataset_path):
        raise ValueError("El dataset no corresponde al informe")
    rows = {row["id"]: row for row in map(json.loads, dataset_path.read_text(encoding="utf-8").splitlines())}
    if output.exists():
        raise ValueError("No se sobrescribe una revisión existente")
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("x", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=["id", "entrada_json", "referencia", "base", "ajustado", "errores_automaticos_ajustado", "estado", "revisor", "fecha", "observaciones"])
        writer.writeheader()
        for result in report["results"]:
            row = rows[result["id"]]
            writer.writerow({"id": result["id"], "entrada_json": json.dumps(row["input"], ensure_ascii=False),
                "referencia": row["expected_output"]["resumen"],
                "base": result["base"].get("output", {}).get("resumen", result["base"].get("raw_output", "")),
                "ajustado": result["adapted"].get("output", {}).get("resumen", result["adapted"].get("raw_output", "")),
                "errores_automaticos_ajustado": json.dumps(result["adapted"].get("checks", {}).get("errors", [result["adapted"].get("error", "Sin comprobaciones")]), ensure_ascii=False),
                "estado": "pendiente", "revisor": "", "fecha": "", "observaciones": ""})


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--dataset", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    export(args.report, args.dataset, args.output)
