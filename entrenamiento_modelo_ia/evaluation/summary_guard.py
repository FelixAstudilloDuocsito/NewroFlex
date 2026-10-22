"""Salida descriptiva acotada; no intenta validar lenguaje natural arbitrario.

Conserva la prosa del modelo solo si coincide con una de tres realizaciones
completas verificables. En otro caso usa la realización determinista y lo informa.
"""
from copy import deepcopy
from evaluation.contracts import METRICS, process, schema, validate

UNITS = dict(zip(METRICS, ["%", "ms", "%", "unidades de índice", "s"]))


def number(value):
    # str conserva la precisión del número recibido, sin limitar a seis cifras.
    return str(value).removesuffix(".0")


def render(payload, style=0):
    data = process(payload)
    count = data["numero_sesiones"]
    lines = ["Se registró 1 sesión." if count == 1 else f"Se registraron {count} sesiones."]
    for change in data["cambios_observados"]:
        label, unit = METRICS[change["metrica"]][0], UNITS[change["metrica"]]
        a, b = change["inicial"], change["final"]
        if count == 1:
            lines.append(f"{label}: " + ("dato no disponible." if b is None else f"{number(b)} {unit}."))
            continue
        if a is None or b is None:
            known = f"valor inicial {number(a)} {unit}" if a is not None else (f"valor final {number(b)} {unit}" if b is not None else "ambos extremos ausentes")
            lines.append(f"{label}: {known}; comparación no disponible por datos faltantes.")
            continue
        if a == b:
            lines.append(f"{label}: sin variación entre extremos ({number(a)} {unit}).")
        else:
            direction = "aumentó" if b > a else "disminuyó"
            lines.append([
                f"{label}: {direction} de {number(a)} {unit} a {number(b)} {unit}.",
                f"{label}: valor inicial {number(a)} {unit} y final {number(b)} {unit}; {direction}.",
                f"{label}: pasó de {number(a)} {unit} a {number(b)} {unit} ({direction}).",
            ][style % 3])
        if change["valor"] is None:
            lines.append(f"{label}: cambio porcentual no calculable porque su valor inicial es cero.")
        else:
            change_unit = {"%": "% respecto al valor inicial", "puntos_porcentuales": "puntos porcentuales", "indice": "unidades de índice"}[change["unidad"]]
            lines.append(f"Cambio: {number(change['valor'])} {change_unit}.")
    explanations = {
        "sesion_unica": "Una sola sesión no permite comparación temporal.",
        "dificultad_variable": "La dificultad varía entre sesiones y limita la comparación.",
        "datos_faltantes": "Hay datos faltantes en las sesiones registradas.",
        "base_cero": "Una base cero impide calcular el cambio porcentual; no equivale a dato ausente.",
        "sin_inferencia_clinica": "El resumen describe métricas y no establece conclusiones clínicas.",
    }
    lines.extend(explanations[code] for code in data["advertencias"])
    if count > 1:
        lines.append("La comparación de extremos no demuestra una tendencia continua.")
    output = {"resumen": " ".join(lines), "cambios_observados": data["cambios_observados"], "advertencias": data["advertencias"]}
    validate(output, schema("salida"))
    return output


def guard(payload, answer):
    """Rechaza estructura/cifras inválidas; reemplaza prosa no verificable.

El booleano es trazabilidad del fallback, NO una clasificación de error semántico.
Una paráfrasis correcta también puede activar el fallback.
"""
    validate(answer, schema("salida"))
    expected = render(payload)
    key = lambda row: row["metrica"]
    if sorted(answer["cambios_observados"], key=key) != sorted(expected["cambios_observados"], key=key) or sorted(answer["advertencias"]) != sorted(expected["advertencias"]):
        raise ValueError("Salida rechazada: valores o advertencias inconsistentes")
    allowed = {render(payload, style)["resumen"] for style in range(3)}
    replacement = answer["resumen"] not in allowed
    result = deepcopy(answer)
    if replacement:
        result["resumen"] = expected["resumen"]
    return result, {"summary_source": "deterministic_fallback" if replacement else "model_verified_template",
                    "summary_replaced": replacement, "policy": "exact_template_v1"}
