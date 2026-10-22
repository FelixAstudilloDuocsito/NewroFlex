"""Genera contratos y un piloto sintético determinista, sin datos de pacientes."""
import hashlib
import json
from evaluation.contracts import ROOT, METRICS, reference, validate, schema


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def obj(properties):
    return {"type": "object", "properties": properties, "required": list(properties), "additionalProperties": False}


def nullable(spec):
    return {"anyOf": [spec, {"type": "null"}]}


def contracts():
    metrics = {}
    for key, (_, _, maximum) in METRICS.items():
        number = {"type": "number", "minimum": 0}
        if maximum is not None:
            number["maximum"] = maximum
        metrics[key] = nullable(number)
    session = obj({"session_number": {"type": "integer", "minimum": 1},
                   "difficulty": {"type": "string", "enum": ["easy", "medium", "hard"]},
                   "metrics": obj(metrics)})
    entry = obj({"sessions": {"type": "array", "minItems": 1, "maxItems": 100, "items": session}})
    change = obj({"metrica": {"type": "string", "enum": list(METRICS)},
                  "inicial": nullable({"type": "number"}), "final": nullable({"type": "number"}),
                  "valor": nullable({"type": "number"}),
                  "unidad": {"type": "string", "enum": ["%", "puntos_porcentuales", "indice"]}})
    output = obj({"resumen": {"type": "string", "minLength": 1, "maxLength": 3000},
                  "cambios_observados": {"type": "array", "minItems": 5, "maxItems": 5, "items": change},
                  "advertencias": {"type": "array", "minItems": 1, "maxItems": 5,
                                   "items": {"type": "string", "enum": ["sesion_unica", "dificultad_variable", "datos_faltantes", "base_cero", "sin_inferencia_clinica"]}}})
    for name, content in [("entrada", entry), ("salida", output)]:
        write(ROOT / "schemas" / f"{name}.schema.json", {"$schema": "https://json-schema.org/draft/2020-12/schema", **content})


def build():
    contracts()
    # Familias completas en una sola partición: nunca separar variantes hermanas.
    families = {
        "train": ["ascenso", "descenso", "estable", "mixto", "unica", "dificultad", "faltantes", "base_cero"],
        "validation": ["oscilacion", "extremos"],
        "test": ["faltantes_dificultad", "ceros_estables"],
    }
    manifest = {"version": "pilot-v1", "seed": 20261020, "origin": "synthetic_deterministic",
                "human_review": "pending", "clinical_validity": False, "files": {}}
    for split, names in families.items():
        rows = []
        for family in names:
            for variant in range(3):
                base = [60 + variant * 4, 5000 + variant * 250, 20 + variant, .2 + variant * .02, 500 + variant * 20]
                delta = [8, -400, -2, -.02, -20]
                count = 1 if family == "unica" else 3
                sessions = []
                for i in range(count):
                    factor = i
                    if family in ("estable", "ceros_estables"):
                        factor = 0
                    elif family == "descenso":
                        factor = -i
                    elif family == "oscilacion":
                        factor = [0, 2, 1][i]
                    values = [round(b + factor * d, 3) for b, d in zip(base, delta)]
                    if family == "mixto":
                        values[1] = base[1] + i * 200
                    if family == "extremos":
                        values[0] = [0, 50, 100][i]
                        values[2] = [100, 50, 0][i]
                    if family in ("base_cero", "ceros_estables") and i == 0:
                        values[1] = 0
                    if family == "ceros_estables":
                        values[1] = 0
                        values[4] = 0
                    if family in ("faltantes", "faltantes_dificultad") and i in (0, 1):
                        values[variant] = None
                    difficulty = ["easy", "medium", "hard"][i] if family in ("dificultad", "faltantes_dificultad") else "medium"
                    sessions.append({"session_number": i + 1, "difficulty": difficulty, "metrics": dict(zip(METRICS, values))})
                payload = {"sessions": sessions}
                answer = reference(payload)
                validate(answer, schema("salida"))
                rows.append({"id": f"{family}-{variant + 1}", "group_id": family,
                             "source": "synthetic_deterministic", "review_status": "pending",
                             "input": payload, "expected_output": answer})
        path = ROOT / "data" / f"{split}.jsonl"
        path.parent.mkdir(exist_ok=True)
        path.write_text("".join(json.dumps(r, ensure_ascii=False) + "\n" for r in rows), encoding="utf-8")
        manifest["files"][path.name] = {"count": len(rows), "groups": names, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
    write(ROOT / "data" / "manifest.json", manifest)


if __name__ == "__main__":
    build()
    print("Piloto generado: 24 entrenamiento, 6 validación, 6 prueba; revisión humana pendiente.")
