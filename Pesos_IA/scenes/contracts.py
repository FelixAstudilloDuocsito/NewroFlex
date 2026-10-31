import json
import re
from datetime import datetime
from pathlib import Path
from evaluation.contracts import validate

VERSION = "scenes-v1"
ROOT = Path(__file__).resolve().parents[1]


def integer(minimum=0):
    return {"type": "integer", "minimum": minimum, "maximum": 2147483647}


def text():
    return {"type": "string", "minLength": 1, "maxLength": 128}


def enum(*values):
    return {"type": "string", "enum": list(values)}


def nullable(spec):
    return {"anyOf": [spec, {"type": "null"}]}


def obj(properties):
    return {"type": "object", "properties": properties, "required": list(properties), "additionalProperties": False}


TIMESTAMP = {"type": "string", "minLength": 20, "maxLength": 40, "format": "date-time"}
COMMON = {**{key: text() for key in ("participantId", "sessionId", "roundId", "scenarioVersion")},
    "difficultyLevel": integer(), "startedAt": TIMESTAMP, "endedAt": TIMESTAMP,
    "status": enum("completed", "abandoned", "interrupted"), "pausedDurationMs": integer(),
    "assessmentMode": enum("training", "comparison"),
    "distractionsEnabled": {"type": "boolean"}, "distractionType": enum("none", "visual", "audio", "mixed"),
    "distractionCount": integer(), "distractionIntensity": enum("none", "low", "medium", "high"),
    "distractionDurationMs": integer(), "comparisonGroupId": nullable(text())}
SPECIFIC = {
    "puzzle": {"totalPieces": integer(1), "correctPlacements": integer(), "incorrectPlacements": integer(),
               "aidsUsed": integer(), "activeTimeMs": integer(), "puzzleId": text(), "rotationEnabled": {"type": "boolean"}},
    "memory": {"totalCards": integer(2), "totalPairs": integer(1), "correctPairAttempts": integer(),
               "incorrectPairAttempts": integer(), "pairsFound": integer(), "activeTimeMs": integer(),
               "aidsUsed": integer(), "previewDurationMs": integer(), "deckId": text()},
    "pigeons": {"actualPigeonCount": integer(), "userPigeonCount": nullable(integer()),
                "displayDurationMs": integer(), "responseTimeMs": nullable(integer()),
                "pigeonsHiddenAt": nullable(TIMESTAMP), "answerEnabledAt": nullable(TIMESTAMP),
                "answerSubmittedAt": nullable(TIMESTAMP), "movementLevel": enum("static", "low", "medium", "high"),
                "retentionDelayMs": integer()},
}
ROUND_SCHEMA = {"anyOf": [obj({**COMMON, "scenarioId": enum(name), **fields}) for name, fields in SPECIFIC.items()]}
INPUT_SCHEMA = {"$schema": "https://json-schema.org/draft/2020-12/schema", **obj({
    "contractVersion": enum(VERSION), "rounds": {"type": "array", "minItems": 1, "maxItems": 100, "items": ROUND_SCHEMA}})}


def timestamp(value):
    # ISO 8601 completo con zona; no se admiten fechas locales ambiguas.
    if not re.fullmatch(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,6})?(?:Z|[+-]\d{2}:\d{2})", value):
        raise ValueError("Se requiere fecha ISO 8601 completa con zona")
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if parsed.tzinfo is None:
        raise ValueError("La fecha debe incluir zona horaria")
    return parsed


def milliseconds(start, end):
    delta = end - start
    return delta.days * 86400000 + delta.seconds * 1000 + delta.microseconds / 1000


def require(condition, message):
    if not condition:
        raise ValueError(message)


def validate_round(row):
    validate(row, ROUND_SCHEMA)
    start, end = timestamp(row["startedAt"]), timestamp(row["endedAt"])
    wall = milliseconds(start, end)
    require(wall >= 0, "endedAt precede startedAt")
    require(row["pausedDurationMs"] <= wall, "Las pausas exceden la ronda")
    require(row["distractionDurationMs"] <= wall, "Distracciones exceden duración de ronda")
    if not row["distractionsEnabled"]:
        require(row["distractionType"] == row["distractionIntensity"] == "none" and row["distractionCount"] == row["distractionDurationMs"] == 0,
                "Distracciones deshabilitadas deben tener none y contadores cero")
    else:
        require(row["distractionType"] != "none" and row["distractionIntensity"] != "none", "Falta configuración de distractores")
        require(row["distractionCount"] != 0 or row["distractionDurationMs"] == 0, "Duración sin distractores presentados")
    scene = row["scenarioId"]
    if scene in ("puzzle", "memory"):
        require(row["activeTimeMs"] <= wall - row["pausedDurationMs"] + 1, "Tiempo activo excede tiempo disponible")
    if scene == "puzzle":
        require(row["correctPlacements"] <= row["totalPieces"], "Piezas completadas exceden total")
        require(row["status"] != "completed" or row["correctPlacements"] == row["totalPieces"], "Puzzle completado sin todas las piezas")
    elif scene == "memory":
        require(row["totalCards"] == 2 * row["totalPairs"], "Se requieren exactamente dos cartas por pareja")
        require(row["pairsFound"] <= row["totalPairs"], "Parejas encontradas exceden total")
        require(row["correctPairAttempts"] == row["pairsFound"], "Cada pareja se completa una vez y queda bloqueada")
        require(row["status"] != "completed" or row["pairsFound"] == row["totalPairs"], "Memoria completada sin todas las parejas")
        # previewDurationMs es configuración, no tiempo necesariamente transcurrido al abandonar.
    else:
        events = [row[key] for key in ("pigeonsHiddenAt", "answerEnabledAt", "answerSubmittedAt")]
        previous, missing = start, False
        parsed = []
        for event in events:
            if event is None:
                missing = True
                parsed.append(None)
                continue
            require(not missing, "Eventos de palomas deben formar una secuencia sin huecos")
            moment = timestamp(event)
            require(previous <= moment <= end, "Eventos de palomas fuera de orden o de la ronda")
            parsed.append(moment)
            previous = moment
        answered = row["answerSubmittedAt"] is not None
        require(answered == (row["userPigeonCount"] is not None) == (row["responseTimeMs"] is not None), "Respuesta y sus tiempos deben existir juntos")
        require(row["status"] != "completed" or answered, "Ronda completada sin respuesta")
        if parsed[0] is not None and parsed[1] is not None:
            require(abs(milliseconds(parsed[0], parsed[1]) - row["retentionDelayMs"]) <= 1,
                    "retentionDelayMs no coincide con los eventos (tiempo de reloj)")
        if answered:
            elapsed = milliseconds(parsed[1], parsed[2])
            require(max(0, elapsed - row["pausedDurationMs"]) - 1 <= row["responseTimeMs"] <= elapsed + 1,
                    "responseTimeMs incompatible con eventos y pausas")
    return row


def metrics(row):
    validate_round(row)
    completed = row["status"] == "completed"
    if row["scenarioId"] == "puzzle":
        return {"resolutionTimeSeconds": row["activeTimeMs"] / 1000 if completed else None,
                "activeTimeSeconds": row["activeTimeMs"] / 1000, "incorrectMoves": row["incorrectPlacements"],
                "completedPieces": row["correctPlacements"], "totalPieces": row["totalPieces"], "aidsUsed": row["aidsUsed"]}
    if row["scenarioId"] == "memory":
        attempts = row["correctPairAttempts"] + row["incorrectPairAttempts"]
        return {"matchingAccuracyPercent": round(100 * row["correctPairAttempts"] / attempts, 2) if attempts else None,
                "incorrectAttempts": row["incorrectPairAttempts"], "resolutionTimeSeconds": row["activeTimeMs"] / 1000 if completed else None,
                "activeTimeSeconds": row["activeTimeMs"] / 1000, "cardsShown": row["totalCards"],
                "pairsFound": row["pairsFound"], "totalPairs": row["totalPairs"], "aidsUsed": row["aidsUsed"]}
    actual, answer = row["actualPigeonCount"], row["userPigeonCount"]
    return {"responseCorrect": answer == actual if answer is not None else None,
            "signedCountError": answer - actual if answer is not None else None,
            "absoluteCountError": abs(answer - actual) if answer is not None else None,
            "actualPigeonCount": actual, "userPigeonCount": answer,
            "enabledResponseActiveSeconds": row["responseTimeMs"] / 1000 if row["responseTimeMs"] is not None else None,
            "responseTimeSeconds": milliseconds(timestamp(row["pigeonsHiddenAt"]), timestamp(row["answerSubmittedAt"])) / 1000 if row["answerSubmittedAt"] else None}


CONTEXT = ["scenarioId", "difficultyLevel", "scenarioVersion", "assessmentMode", "distractionsEnabled", "distractionType", "distractionIntensity", "distractionCount", "distractionDurationMs"]
CONTEXT_SCENE = {"puzzle": ["totalPieces", "puzzleId", "rotationEnabled"],
                 "memory": ["totalCards", "totalPairs", "previewDurationMs", "deckId"],
                 "pigeons": ["actualPigeonCount", "displayDurationMs", "retentionDelayMs", "movementLevel"]}


def model_input(row):
    """Nunca envía identificadores ni texto libre al modelo."""
    return {"scenarioId": row["scenarioId"], "status": row["status"], "metrics": metrics(row)}


def process(payload):
    validate(payload, INPUT_SCHEMA)
    rows = payload["rounds"]
    require(len({row["participantId"] for row in rows}) == 1, "Un participante por solicitud")
    require(len({row["roundId"] for row in rows}) == len(rows), "roundId duplicado")
    results = []
    for row in rows:
        values = metrics(row)
        warnings = ["descriptive_only"]
        if row["status"] != "completed": warnings.append("round_not_completed")
        if row["assessmentMode"] == "training": warnings.append("training_mode")
        if row["distractionsEnabled"]: warnings.append("distractions_configured")
        if row["scenarioId"] == "memory" and values["matchingAccuracyPercent"] is None: warnings.append("no_pair_attempts")
        if row["scenarioId"] == "pigeons" and values["responseCorrect"] is None: warnings.append("no_answer")
        results.append({"roundId": row["roundId"], "sessionId": row["sessionId"], "scenarioId": row["scenarioId"],
                        "status": row["status"], "metrics": values, "warnings": warnings})
    groups = {}
    for row in rows:
        if row["comparisonGroupId"] is not None:
            groups.setdefault(row["comparisonGroupId"], []).append(row)
    comparisons = []
    for group, members in groups.items():
        reasons = []
        if len(members) < 2: reasons.append("insufficient_rounds")
        if any(r["status"] != "completed" for r in members): reasons.append("incomplete_rounds")
        if any(r["assessmentMode"] != "comparison" for r in members): reasons.append("not_comparison_mode")
        keys = CONTEXT + sorted(set(key for r in members for key in CONTEXT_SCENE[r["scenarioId"]]))
        if any(any(r.get(key) != members[0].get(key) for key in keys) for r in members[1:]): reasons.append("different_conditions")
        comparisons.append({"comparisonGroupId": group, "roundIds": [r["roundId"] for r in members],
                            "comparable": not reasons, "reasons": reasons})
    return {"contractVersion": VERSION, "participantId": rows[0]["participantId"], "rounds": results, "comparisons": comparisons}


if __name__ == "__main__":
    (ROOT / "schemas/scenes-input.schema.json").write_text(json.dumps(INPUT_SCHEMA, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
