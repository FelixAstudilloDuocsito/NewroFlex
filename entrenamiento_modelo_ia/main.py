import hashlib
import json
from datetime import datetime
from pathlib import Path

import requests


# ============================================================
# CONFIGURACIÓN
# ============================================================

MODEL = "qwen3:8b"
OLLAMA_URL = "http://localhost:11434/api/chat"

TEMPERATURE = 0.2

PROMPTS_DIR = Path("prompts")
RESULTS_DIR = Path("results")
MODELS_FILE = PROMPTS_DIR / "models.json"


# ============================================================
# DATOS TEMPORALES DE PRUEBA
# ============================================================

sessions = [
    {
        "session_number": 1,
        "difficulty": "medium",
        "metrics": {
            "accuracy_percentage": 61.0,
            "average_response_time_ms": 6100,
            "gaze_deviation_percentage": 21.4,
            "controller_tremor_index": 0.31,
            "session_duration_seconds": 540
        }
    },
    {
        "session_number": 2,
        "difficulty": "medium",
        "metrics": {
            "accuracy_percentage": 68.0,
            "average_response_time_ms": 5400,
            "gaze_deviation_percentage": 19.1,
            "controller_tremor_index": 0.28,
            "session_duration_seconds": 520
        }
    },
    {
        "session_number": 3,
        "difficulty": "medium",
        "metrics": {
            "accuracy_percentage": 74.0,
            "average_response_time_ms": 4800,
            "gaze_deviation_percentage": 16.5,
            "controller_tremor_index": 0.23,
            "session_duration_seconds": 500
        }
    },
    {
        "session_number": 4,
        "difficulty": "medium",
        "metrics": {
            "accuracy_percentage": 82.0,
            "average_response_time_ms": 4200,
            "gaze_deviation_percentage": 13.2,
            "controller_tremor_index": 0.18,
            "session_duration_seconds": 480
        }
    }
]


# ============================================================
# PROCESAMIENTO
# ============================================================

def calculate_change(first, last):
    if first == 0:
        return None

    return round(
        ((last - first) / first) * 100,
        2
    )


def analyze_sessions(sessions):
    if not sessions:
        raise ValueError("El caso de prueba no contiene sesiones.")

    first = sessions[0]["metrics"]
    last = sessions[-1]["metrics"]
    has_temporal_comparison = len(sessions) >= 2

    return {
        "number_of_sessions": len(sessions),
        "has_temporal_comparison": has_temporal_comparison,

        "accuracy": {
            "first": first["accuracy_percentage"],
            "last": last["accuracy_percentage"],
            "unit": "%",
            "change": {
                "type": "absolute",
                "value": round(
                    last["accuracy_percentage"]
                    - first["accuracy_percentage"],
                    2
                ),
                "unit": "percentage_points"
            }
        },

        "response_time": {
            "first": first["average_response_time_ms"],
            "last": last["average_response_time_ms"],
            "unit": "ms",
            "change": {
                "type": "percentage",
                "value": calculate_change(
                    first["average_response_time_ms"],
                    last["average_response_time_ms"]
                ),
                "unit": "%"
            }
        },

        "gaze_deviation": {
            "first": first["gaze_deviation_percentage"],
            "last": last["gaze_deviation_percentage"],
            "unit": "%",
            "change": {
                "type": "absolute",
                "value": round(
                    last["gaze_deviation_percentage"]
                    - first["gaze_deviation_percentage"],
                    2
                ),
                "unit": "percentage_points"
            }
        },

        "controller_tremor": {
            "first": first["controller_tremor_index"],
            "last": last["controller_tremor_index"],
            "unit": "index",
            "change": {
                "type": "absolute",
                "value": round(
                    last["controller_tremor_index"]
                    - first["controller_tremor_index"],
                    3
                ),
                "unit": "index"
            }
        },

        "session_duration": {
            "first": first["session_duration_seconds"],
            "last": last["session_duration_seconds"],
            "unit": "seconds",
            "change": {
                "type": "percentage",
                "value": calculate_change(
                    first["session_duration_seconds"],
                    last["session_duration_seconds"]
                ),
                "unit": "%"
            }
        }
    }


# ============================================================
# VALIDACIÓN DE RESPUESTAS
# ============================================================

def validate_output(output):
    errors = []

    changes = output.get(
        "cambios_observados",
        []
    )

    if not isinstance(changes, list):
        errors.append(
            "'cambios_observados' debe ser una lista."
        )

        return errors

    for index, change in enumerate(
        changes,
        start=1
    ):
        if not isinstance(change, dict):
            continue

        metric = change.get(
            "metrica",
            f"elemento_{index}"
        )

        # ----------------------------------------------------
        # Estructura nueva:
        #
        # {
        #   "tipo_cambio": "porcentaje",
        #   "valor": -31.15,
        #   "unidad": "%"
        # }
        # ----------------------------------------------------

        change_type = change.get(
            "tipo_cambio"
        )

        unit = change.get(
            "unidad"
        )

        if change_type in (
            "percentage",
            "porcentaje"
        ):
            if unit not in (
                "%",
                None
            ):
                errors.append(
                    f"{metric}: cambio porcentual "
                    f"con unidad incorrecta '{unit}'. "
                    f"Se esperaba '%'."
                )

        if change_type in (
            "absolute",
            "absoluto"
        ):
            if unit == "%":
                errors.append(
                    f"{metric}: cambio absoluto "
                    "marcado incorrectamente con '%'."
                )

        # ----------------------------------------------------
        # Compatibilidad con respuestas antiguas:
        #
        # {
        #   "cambio_porcentaje": -31.15,
        #   "unidad": "ms"
        # }
        # ----------------------------------------------------

        if "cambio_porcentaje" in change:
            if unit not in (
                "%",
                None
            ):
                errors.append(
                    f"{metric}: 'cambio_porcentaje' "
                    f"usa unidad incorrecta '{unit}'. "
                    f"Se esperaba '%'."
                )

        # ----------------------------------------------------
        # Accuracy y gaze_deviation:
        # sus diferencias absolutas son puntos porcentuales,
        # no porcentajes.
        # ----------------------------------------------------

        metric_normalized = (
            str(metric)
            .lower()
            .replace(" ", "_")
        )

        if (
            metric_normalized
            in (
                "accuracy",
                "precisión",
                "precision",
                "gaze_deviation",
                "desviación_de_mirada",
                "desviacion_de_mirada"
            )
            and (
                "cambio_absoluto" in change
                or change_type in (
                    "absolute",
                    "absoluto"
                )
            )
        ):
            if unit == "%":
                errors.append(
                    f"{metric}: un cambio absoluto "
                    "entre porcentajes debe expresarse "
                    "como puntos porcentuales, no '%'."
                )

    return errors


# ============================================================
# REGISTRO DE SEMILLAS
# ============================================================

def load_model_registry():
    if not MODELS_FILE.exists():
        return []

    data = json.loads(
        MODELS_FILE.read_text(
            encoding="utf-8"
        )
    )

    return data.get(
        "models",
        []
    )


def save_model_registry(models):
    PROMPTS_DIR.mkdir(
        exist_ok=True
    )

    MODELS_FILE.write_text(
        json.dumps(
            {
                "models": models
            },
            ensure_ascii=False,
            indent=2
        ),
        encoding="utf-8"
    )


def infer_seed_metadata(file_path):
    stem = file_path.stem

    parts = stem.split("_")

    seed_id = parts[0]

    readable_name = " ".join(
        parts[1:]
    )

    readable_name = readable_name.replace(
        "-",
        " "
    ).strip()

    if not readable_name:
        readable_name = seed_id

    return {
        "id": seed_id,
        "name": readable_name,
        "file": file_path.name,
        "created_at": datetime.now().isoformat(
            timespec="seconds"
        ),
        "uses": 0,
        "active": True,
        "description": ""
    }


def sync_model_registry():
    PROMPTS_DIR.mkdir(
        exist_ok=True
    )

    models = load_model_registry()

    models_by_file = {
        model["file"]: model
        for model in models
    }

    existing_prompt_files = {
        file_path.name: file_path
        for file_path
        in PROMPTS_DIR.glob("*.txt")
    }

    changed = False

    # --------------------------------------------------------
    # AGREGAR PROMPTS NUEVOS
    # --------------------------------------------------------

    for file_name, file_path in (
        existing_prompt_files.items()
    ):
        if file_name not in models_by_file:

            new_model = infer_seed_metadata(
                file_path
            )

            models.append(
                new_model
            )

            models_by_file[
                file_name
            ] = new_model

            changed = True

    # --------------------------------------------------------
    # ACTUALIZAR EXISTENCIA / VALORES POR DEFECTO
    # --------------------------------------------------------

    for model in models:

        exists = (
            model["file"]
            in existing_prompt_files
        )

        if model.get(
            "exists",
            True
        ) != exists:
            model["exists"] = exists
            changed = True

        if "active" not in model:
            model["active"] = True
            changed = True

        if "uses" not in model:
            model["uses"] = 0
            changed = True

    if changed:
        save_model_registry(
            models
        )

    return models


# ============================================================
# CARGAR SEMILLA
# ============================================================

def load_seed(model_data):
    file_path = (
        PROMPTS_DIR
        / model_data["file"]
    )

    if not file_path.exists():
        raise FileNotFoundError(
            f"No existe el prompt: {file_path}"
        )

    prompt = file_path.read_text(
        encoding="utf-8"
    ).strip()

    prompt_hash = hashlib.sha256(
        prompt.encode("utf-8")
    ).hexdigest()[:12]

    return {
        **model_data,
        "path": str(file_path),
        "hash": prompt_hash,
        "prompt": prompt
    }


# ============================================================
# OLLAMA
# ============================================================

def run_seed(
    seed,
    analysis_data
):

    user_prompt = f"""
Resultados procesados de las sesiones:

{json.dumps(
    analysis_data,
    ensure_ascii=False,
    indent=2
)}
"""

    response = requests.post(
        OLLAMA_URL,

        json={
            "model": MODEL,

            "messages": [
                {
                    "role": "system",
                    "content": seed["prompt"]
                },
                {
                    "role": "user",
                    "content": user_prompt
                }
            ],

            "stream": False,

            "format": "json",

            "options": {
                "temperature": TEMPERATURE
            }
        },

        timeout=120
    )

    response.raise_for_status()

    ollama_result = response.json()

    content = (
        ollama_result[
            "message"
        ]["content"]
    )

    try:
        parsed_content = json.loads(
            content
        )

        validation_errors = validate_output(
            parsed_content
        )

        return {
            "seed_id": seed["id"],
            "seed_name": seed["name"],
            "seed_file": seed["file"],
            "seed_hash": seed["hash"],

            "success": True,

            "validation": {
                "valid": len(
                    validation_errors
                ) == 0,

                "error_count": len(
                    validation_errors
                ),

                "errors": validation_errors
            },

            "output": parsed_content
        }

    except json.JSONDecodeError:

        return {
            "seed_id": seed["id"],
            "seed_name": seed["name"],
            "seed_file": seed["file"],
            "seed_hash": seed["hash"],

            "success": False,

            "validation": {
                "valid": False,
                "error_count": 1,
                "errors": [
                    "El modelo no devolvió JSON válido."
                ]
            },

            "raw_output": content,

            "error": (
                "El modelo no devolvió JSON válido."
            )
        }


# ============================================================
# GUARDAR RESULTADOS
# ============================================================

def save_experiment(
    analysis_data,
    selected_models,
    results,
    sessions_used,
    test_case_id=None,
    test_case_name=None
):
    RESULTS_DIR.mkdir(
        exist_ok=True
    )

    timestamp = datetime.now().strftime(
        "%Y-%m-%d_%H-%M-%S"
    )

    total_validation_errors = sum(
        result.get(
            "validation",
            {}
        ).get(
            "error_count",
            0
        )
        for result in results
    )

    valid_results = sum(
        1
        for result in results
        if result.get(
            "validation",
            {}
        ).get(
            "valid",
            False
        )
    )

    experiment = {
        "experiment": {
            "timestamp": timestamp,
            "llm": MODEL,
            "temperature": TEMPERATURE,

            "selected_seeds": [
                model["id"]
                for model in selected_models
            ]
        },

        "test_case": {
            "id": test_case_id,
            "name": test_case_name
        },

        "input": {
            "sessions": sessions_used,
            "processed_data": analysis_data
        },

        "summary": {
            "total_seeds": len(
                selected_models
            ),
            "valid_outputs": valid_results,
            "validation_errors": (
                total_validation_errors
            )
        },

        "results": results
    }

    case_prefix = (
        f"{test_case_id}_"
        if test_case_id
        else ""
    )

    file_path = (
        RESULTS_DIR
        / f"experiment_{case_prefix}{timestamp}.json"
    )

    file_path.write_text(
        json.dumps(
            experiment,
            ensure_ascii=False,
            indent=2
        ),
        encoding="utf-8"
    )

    return file_path


# ============================================================
# EJECUTAR EXPERIMENTO
# ============================================================

def run_experiment(
    model_ids=None,
    include_inactive=False,
    test_sessions=None,
    test_case_id=None,
    test_case_name=None
):

    registry = sync_model_registry()

    if model_ids is None:

        selected_models = [
            model
            for model in registry
            if model.get(
                "exists",
                True
            )
            and (
                include_inactive
                or model.get(
                    "active",
                    True
                )
            )
        ]

    else:

        selected_models = [
            model
            for model in registry
            if model["id"]
            in model_ids
            and model.get(
                "exists",
                True
            )
        ]

    if not selected_models:
        raise RuntimeError(
            "No hay semillas disponibles para ejecutar."
        )

    sessions_to_use = (
        test_sessions
        if test_sessions is not None
        else sessions
    )

    analysis_data = analyze_sessions(
        sessions_to_use
    )

    print(
        "\n=== DATOS CALCULADOS POR EL SISTEMA ===\n"
    )

    print(
        json.dumps(
            analysis_data,
            ensure_ascii=False,
            indent=2
        )
    )

    results = []

    for model_data in selected_models:

        seed = load_seed(
            model_data
        )

        print(
            "\n" + "=" * 60
        )

        print(
            f"EJECUTANDO {seed['id']} "
            f"- {seed['name']}"
        )

        print(
            f"Archivo: {seed['file']}"
        )

        print(
            f"Hash: {seed['hash']}"
        )

        print(
            "=" * 60
        )

        try:

            result = run_seed(
                seed,
                analysis_data
            )

        except Exception as error:

            result = {
                "seed_id": seed["id"],
                "seed_name": seed["name"],

                "success": False,

                "validation": {
                    "valid": False,
                    "error_count": 1,
                    "errors": [
                        str(error)
                    ]
                },

                "error": str(error)
            }

        results.append(
            result
        )

        model_data["uses"] = (
            model_data.get(
                "uses",
                0
            ) + 1
        )

        if result["success"]:

            print(
                json.dumps(
                    result["output"],
                    ensure_ascii=False,
                    indent=2
                )
            )

            validation = result.get(
                "validation",
                {}
            )

            if validation.get(
                "valid",
                False
            ):

                print(
                    "\n[VALIDACIÓN] "
                    "Sin errores detectados."
                )

            else:

                print(
                    "\n[VALIDACIÓN] "
                    f"{validation.get('error_count', 0)} "
                    "error(es) detectado(s):"
                )

                for error in validation.get(
                    "errors",
                    []
                ):
                    print(
                        f"  - {error}"
                    )

        else:

            print(
                f"\nERROR: "
                f"{result['error']}"
            )

    save_model_registry(
        registry
    )

    file_path = save_experiment(
        analysis_data,
        selected_models,
        results,
        sessions_to_use,
        test_case_id,
        test_case_name
    )

    print(
        "\n" + "=" * 60
    )

    print(
        "EXPERIMENTO FINALIZADO"
    )

    print(
        "=" * 60
    )

    print(
        f"\nResultados guardados en:\n"
        f"{file_path}"
    )

    return results


# ============================================================
# ACTIVAR / DESACTIVAR
# ============================================================

def set_model_active(
    model_id,
    active
):

    models = sync_model_registry()

    for model in models:

        if model["id"] == model_id:

            model["active"] = active

            save_model_registry(
                models
            )

            return True

    return False