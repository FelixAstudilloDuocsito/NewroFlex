"""Contratos estrictos y cálculos independientes del modelo (solo stdlib)."""
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
METRICS = {
    "accuracy_percentage": ("Precisión", "puntos_porcentuales", 100),
    "average_response_time_ms": ("Tiempo de respuesta", "%", None),
    "gaze_deviation_percentage": ("Desviación de mirada", "puntos_porcentuales", 100),
    "controller_tremor_index": ("Índice de temblor", "indice", None),
    "session_duration_seconds": ("Duración", "%", None),
}


def validate(value, schema, path="$"):
    """Valida el subconjunto de JSON Schema usado por estos contratos."""
    if "anyOf" in schema:
        for option in schema["anyOf"]:
            try:
                validate(value, option, path)
                return
            except ValueError:
                pass
        raise ValueError(f"{path}: no coincide con ningún tipo permitido")
    kind = schema.get("type")
    good = {"object": type(value) is dict, "array": type(value) is list,
            "string": type(value) is str, "number": type(value) in (int, float),
            "integer": type(value) is int, "null": value is None,
            "boolean": type(value) is bool}
    if kind and not good[kind]:
        raise ValueError(f"{path}: se esperaba {kind}")
    if "enum" in schema and value not in schema["enum"]:
        raise ValueError(f"{path}: valor fuera del catálogo")
    if kind == "object":
        props = schema.get("properties", {})
        if set(schema.get("required", [])) - value.keys():
            raise ValueError(f"{path}: faltan campos obligatorios")
        if schema.get("additionalProperties") is False and value.keys() - props.keys():
            raise ValueError(f"{path}: campos desconocidos")
        for key, item in value.items():
            if key in props:
                validate(item, props[key], f"{path}.{key}")
    if kind == "array":
        if not schema.get("minItems", 0) <= len(value) <= schema.get("maxItems", math.inf):
            raise ValueError(f"{path}: longitud inválida")
        for i, item in enumerate(value):
            validate(item, schema["items"], f"{path}[{i}]")
    if kind == "string" and not schema.get("minLength", 0) <= len(value) <= schema.get("maxLength", math.inf):
        raise ValueError(f"{path}: longitud inválida")
    if kind in ("number", "integer"):
        if not math.isfinite(value) or not schema.get("minimum", -math.inf) <= value <= schema.get("maximum", math.inf):
            raise ValueError(f"{path}: número fuera de rango")


def schema(name):
    return json.loads((ROOT / "schemas" / f"{name}.schema.json").read_text(encoding="utf-8"))


def process(payload):
    validate(payload, schema("entrada"))
    sessions = payload["sessions"]
    numbers = [s["session_number"] for s in sessions]
    if numbers != sorted(set(numbers)):
        raise ValueError("Las sesiones deben tener números únicos y ordenados")
    warnings = []
    changes = []
    if len(sessions) == 1:
        warnings.append("sesion_unica")
    if len({s["difficulty"] for s in sessions}) > 1:
        warnings.append("dificultad_variable")
    if any(v is None for s in sessions for v in s["metrics"].values()):
        warnings.append("datos_faltantes")
    for key, (_, unit, _) in METRICS.items():
        first, last = sessions[0]["metrics"][key], sessions[-1]["metrics"][key]
        value = None
        if len(sessions) > 1 and first is not None and last is not None:
            if unit == "%":
                if first == 0:
                    if "base_cero" not in warnings:
                        warnings.append("base_cero")
                else:
                    value = round((last - first) / first * 100, 2)
            else:
                value = round(last - first, 3)
        changes.append({"metrica": key, "inicial": first, "final": last,
                        "valor": value, "unidad": unit})
    warnings.append("sin_inferencia_clinica")
    return {"numero_sesiones": len(sessions), "cambios_observados": changes,
            "advertencias": warnings, "alcance": "Comparación de extremos; no permite afirmar tendencia continua."}


def reference(payload):
    processed = process(payload)
    sentences = []
    for change in processed["cambios_observados"]:
        label = METRICS[change["metrica"]][0]
        if processed["numero_sesiones"] == 1:
            value = change['final'] if change['final'] is not None else "no disponible"
            sentences.append(f"{label}: valor registrado {value}.")
        elif change["inicial"] is None or change["final"] is None:
            sentences.append(f"{label}: comparación no disponible por datos faltantes.")
        else:
            sentences.append(f"{label}: de {change['inicial']} a {change['final']}.")
    explanations = {
        "sesion_unica": "Una sola sesión no permite comparación temporal.",
        "dificultad_variable": "El cambio de dificultad limita la comparación.",
        "datos_faltantes": "Faltan datos en las sesiones registradas.",
        "base_cero": "No puede calcularse el cambio porcentual con base cero.",
        "sin_inferencia_clinica": "Estos datos se describen sin interpretación clínica.",
    }
    sentences.extend(explanations[code] for code in processed["advertencias"])
    return {"resumen": " ".join(sentences), "cambios_observados": processed["cambios_observados"],
            "advertencias": processed["advertencias"]}
