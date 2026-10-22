"""Candidato sintético v2: perfiles separados, referencias con unidades explícitas."""
import argparse
import hashlib
import json
import random
from pathlib import Path
from evaluation.contracts import ROOT, METRICS, process, schema, validate

UNITS = dict(zip(METRICS, ["%", "ms", "%", "unidades de índice", "s"]))
SCENARIOS = ("ascenso", "descenso", "estable", "mixto", "unica", "dificultad",
             "faltante_inicial", "faltante_final", "faltante_intermedio", "base_cero")


def render(payload, style=0):
    data = process(payload)
    lines = [f"Se registraron {data['numero_sesiones']} sesiones."]
    for c in data["cambios_observados"]:
        label, unit = METRICS[c["metrica"]][0], UNITS[c["metrica"]]
        a, b = c["inicial"], c["final"]
        if data["numero_sesiones"] == 1:
            lines.append(f"{label}: " + ("dato no disponible." if b is None else f"{b:g} {unit}."))
            continue
        if a is None or b is None:
            known = f"valor inicial {a:g} {unit}" if a is not None else (f"valor final {b:g} {unit}" if b is not None else "ambos extremos ausentes")
            lines.append(f"{label}: {known}; comparación no disponible por datos faltantes.")
            continue
        if a == b:
            lines.append(f"{label}: sin variación entre extremos ({a:g} {unit}).")
        else:
            direction = "aumentó" if b > a else "disminuyó"
            templates = [f"{label}: {direction} de {a:g} {unit} a {b:g} {unit}.",
                         f"{label}: valor inicial {a:g} {unit} y final {b:g} {unit}; {direction}.",
                         f"{label}: pasó de {a:g} {unit} a {b:g} {unit} ({direction})."]
            lines.append(templates[style % 3])
        if c["valor"] is not None:
            change_unit = {"%": "% respecto al valor inicial", "puntos_porcentuales": "puntos porcentuales", "indice": "unidades de índice"}[c["unidad"]]
            lines.append(f"Cambio: {c['valor']:g} {change_unit}.")
        else:
            lines.append("Cambio porcentual no calculable porque el valor inicial es cero.")
    explanations = {
        "sesion_unica": "Una sola sesión no permite comparación temporal.",
        "dificultad_variable": "La dificultad varía entre sesiones y limita la comparación.",
        "datos_faltantes": "Hay datos faltantes en las sesiones registradas.",
        "base_cero": "Una base cero impide calcular el cambio porcentual; no equivale a dato ausente.",
        "sin_inferencia_clinica": "El resumen describe métricas y no establece conclusiones clínicas.",
    }
    lines.extend(explanations[c] for c in data["advertencias"])
    if data["numero_sesiones"] > 1:
        lines.append("La comparación de extremos no demuestra una tendencia continua.")
    result = {"resumen": " ".join(lines), "cambios_observados": data["cambios_observados"], "advertencias": data["advertencias"]}
    validate(result, schema("salida"))
    return result


def build(destination):
    if destination.exists():
        raise ValueError("El destino ya existe; use una nueva versión para conservar revisiones y hashes")
    rng = random.Random(20261021)
    partitions = {"train": [], "validation": [], "test": []}
    # Por escenario: 4 perfiles train, 1 validation, 1 test; 3 variantes por perfil.
    # Cada perfil tiene parámetros independientes; sus variantes permanecen juntas.
    for scenario in SCENARIOS:
        for profile in range(6):
            split = "train" if profile < 4 else ("validation" if profile == 4 else "test")
            group = f"v2-{scenario}-p{profile}"
            base = [rng.uniform(25, 70), rng.uniform(1800, 7000), rng.uniform(10, 60), rng.uniform(.1, .7), rng.uniform(200, 800)]
            deltas = [rng.uniform(1, 5), rng.uniform(100, 300), rng.uniform(1, 3), rng.uniform(.005, .02), rng.uniform(5, 20)]
            for variant in range(3):
                count = 1 if scenario == "unica" else rng.choice([3, 4, 5])
                sessions = []
                for i in range(count):
                    sign = -1 if scenario == "descenso" else 1
                    factor = 0 if scenario == "estable" else i * sign
                    values = [round(b + variant * d / 3 + factor * d * (-1 if scenario == "mixto" and j % 2 else 1), 3) for j, (b, d) in enumerate(zip(base, deltas))]
                    if scenario == "base_cero" and i == 0:
                        values[1 if variant % 2 == 0 else 4] = 0
                    missing_index = {"faltante_inicial": 0, "faltante_final": count - 1, "faltante_intermedio": 1}.get(scenario)
                    if i == missing_index:
                        values[(profile + variant) % 5] = None
                    sessions.append({"session_number": i + 1,
                        "difficulty": ["easy", "medium", "hard"][min(i, 2)] if scenario == "dificultad" else "medium",
                        "metrics": dict(zip(METRICS, values))})
                payload = {"sessions": sessions}
                partitions[split].append({"id": f"{group}-v{variant}", "group_id": group, "scenario": scenario,
                    "source": "synthetic_deterministic", "review_status": "pending", "input": payload,
                    "expected_output": render(payload, variant)})
    destination.mkdir(parents=True)
    manifest = {"version": "candidate-v2", "seed": 20261021, "human_review": "pending", "clinical_validity": False,
                "generator_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(), "files": {}}
    for split, rows in partitions.items():
        path = destination / f"{split}.jsonl"
        path.write_text("".join(json.dumps(r, ensure_ascii=False) + "\n" for r in rows), encoding="utf-8")
        manifest["files"][path.name] = {"count": len(rows), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
    (destination / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, default=ROOT / "data" / "v2")
    build(parser.parse_args().output)
