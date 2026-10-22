"""Baseline local trazable. No modifica prompts/models.json ni entrena pesos."""
import argparse
import hashlib
import json
import re
import time
from pathlib import Path
from datetime import datetime, timezone
import requests
from evaluation.contracts import ROOT, process, schema, validate


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def score(output, expected):
    errors = []
    try:
        validate(output, schema("salida"))
    except ValueError as exc:
        return {"schema_valid": False, "structured_fidelity": False,
                "errors": [str(exc)], "human_review": "pending"}
    actual = output["cambios_observados"]
    fidelity = len({c["metrica"] for c in actual}) == 5 and sorted(actual, key=lambda c: c["metrica"]) == sorted(expected["cambios_observados"], key=lambda c: c["metrica"])
    if not fidelity:
        errors.append("Valores, unidades o métricas distintos de la referencia")
    if sorted(output["advertencias"]) != sorted(expected["advertencias"]):
        errors.append("Advertencias distintas de la referencia")
    # Señales para revisión, NO clasificador clínico ni verificación semántica completa.
    flags = re.findall(r"\b(?:mejoría|mejora|deterioro|significativ\w*|diagnóstico|progresiv\w*|sostenid\w*)\b", output["resumen"], flags=re.I)
    return {"schema_valid": True, "structured_fidelity": fidelity, "errors": errors,
            "lexical_review_flags": flags, "human_review": "pending"}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--split", choices=["validation", "test"], default="validation")
    parser.add_argument("--model", default="qwen3:8b")
    parser.add_argument("--url", default="http://localhost:11434")
    parser.add_argument("--timeout", type=int, default=180)
    args = parser.parse_args()
    source = ROOT / "data" / f"{args.split}.jsonl"
    prompt = ROOT / "evaluation" / "baseline_system.txt"
    rows = [json.loads(line) for line in source.read_text(encoding="utf-8").splitlines()]
    def get(route):
        response = requests.get(args.url + route, timeout=10)
        response.raise_for_status()
        return response.json()
    tags, version = get("/api/tags"), get("/api/version")
    model = next((m for m in tags["models"] if m["name"] == args.model), None)
    if model is None:
        raise SystemExit("Modelo no instalado; no se descargará automáticamente")
    options = {"temperature": 0, "seed": 20261020, "num_ctx": 4096, "num_predict": 1500}
    report = {"created_at_utc": datetime.now(timezone.utc).isoformat(), "split": args.split,
              "model": model, "ollama": version, "options": options, "think": False,
              "dataset_sha256": digest(source), "prompt_sha256": digest(prompt),
              "schema_sha256": digest(ROOT / "schemas" / "salida.schema.json"),
              "evaluator_sha256": digest(Path(__file__)),
              "processor_sha256": digest(ROOT / "evaluation" / "contracts.py"),
              "status": "running", "human_review": "pending", "results": []}
    target = ROOT / "results" / ("baseline_" + args.split + "_" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ") + ".json")
    target.parent.mkdir(exist_ok=True)
    def save():
        target.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    save()
    for row in rows:
        started = time.perf_counter()
        result = {"id": row["id"]}
        try:
            payload = process(row["input"])
            response = requests.post(args.url + "/api/chat", json={"model": args.model,
                "messages": [{"role": "system", "content": prompt.read_text(encoding="utf-8")},
                             {"role": "user", "content": json.dumps(payload, ensure_ascii=False)}],
                "format": schema("salida"), "stream": False, "think": False, "options": options}, timeout=args.timeout)
            response.raise_for_status()
            data = response.json()
            result["raw_output"] = data["message"]["content"]
            result["done_reason"] = data.get("done_reason")
            result["output"] = json.loads(result["raw_output"])
            result["checks"] = score(result["output"], row["expected_output"])
            if data.get("done_reason") == "length":
                result["checks"]["errors"].append("Respuesta truncada por límite de tokens")
        except (requests.RequestException, ValueError, KeyError, TypeError) as exc:
            result["error"] = str(exc)
        result["latency_seconds"] = round(time.perf_counter() - started, 3)
        report["results"].append(result)
        save()
        print(f"{row['id']}: {result.get('checks', result.get('error'))}", flush=True)
    report["status"] = "completed"
    report["summary"] = {"total": len(rows), "request_or_parse_errors": sum("error" in r for r in report["results"]),
        "schema_valid": sum(r.get("checks", {}).get("schema_valid", False) for r in report["results"]),
        "structured_fidelity": sum(r.get("checks", {}).get("structured_fidelity", False) for r in report["results"]),
        "automatic_pass": sum("checks" in r and not r["checks"]["errors"] for r in report["results"])}
    save()
    print(f"Informe: {target}", flush=True)


if __name__ == "__main__":
    main()
