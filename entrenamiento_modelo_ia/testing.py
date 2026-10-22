import json
import os
import msvcrt
from datetime import datetime
from pathlib import Path

from main import (
    sync_model_registry,
    run_experiment,
    set_model_active
)


# ============================================================
# CONFIGURACIÓN
# ============================================================

TEST_CASES_DIR = Path("test_cases")


# ============================================================
# UTILIDADES
# ============================================================

def clear_screen():
    os.system("cls")


def wait_key():
    print(
        "\nPresiona cualquier tecla para continuar..."
    )

    msvcrt.getwch()


def format_date(date_string):
    try:
        date = datetime.fromisoformat(
            date_string
        )

        return date.strftime(
            "%d/%m/%Y %H:%M"
        )

    except ValueError:
        return date_string


# ============================================================
# CASOS DE PRUEBA
# ============================================================

def load_test_cases():

    TEST_CASES_DIR.mkdir(
        exist_ok=True
    )

    test_cases = []

    for file_path in sorted(
        TEST_CASES_DIR.glob("*.json")
    ):

        try:
            data = json.loads(
                file_path.read_text(
                    encoding="utf-8"
                )
            )

            data["_file"] = (
                file_path.name
            )

            test_cases.append(
                data
            )

        except (
            json.JSONDecodeError,
            OSError
        ) as error:

            print(
                f"Error cargando "
                f"{file_path.name}: "
                f"{error}"
            )

    return test_cases


def run_test_case(
    test_case,
    model_ids=None
):

    print(
        "\n========================================"
    )

    print(
        f"CASO: "
        f"{test_case.get('id', 'SIN ID')} "
        f"- {test_case.get('name', 'Sin nombre')}"
    )

    print(
        "========================================\n"
    )

    description = test_case.get(
        "description"
    )

    if description:

        print(
            f"{description}\n"
        )

    sessions = test_case.get(
        "sessions",
        []
    )

    if not sessions:

        print(
            "El caso no contiene sesiones."
        )

        return

    run_experiment(
        model_ids=model_ids,
        test_sessions=sessions,
        test_case_id=test_case.get(
            "id"
        ),
        test_case_name=test_case.get(
            "name"
        )
    )


# ============================================================
# MENÚ PRINCIPAL
# ============================================================

def main_menu():

    while True:

        clear_screen()

        print(
            "========================================"
        )

        print(
            "        NEUROFLEX AI TESTING"
        )

        print(
            "========================================"
        )

        print()

        print(
            "1. Probar todos los modelos activos"
        )

        print(
            "2. Lista de modelos"
        )

        print(
            "3. Casos de prueba"
        )

        print(
            "4. Ejecutar benchmark completo"
        )

        print(
            "0. Salir"
        )

        print()

        option = input(
            "Selecciona una opción: "
        ).strip()

        if option == "1":

            clear_screen()

            print(
                "Ejecutando todas las semillas "
                "activas con los datos por defecto...\n"
            )

            try:
                run_experiment()

            except RuntimeError as error:

                print(
                    f"\n{error}"
                )

            wait_key()

        elif option == "2":

            model_list_menu()

        elif option == "3":

            test_case_menu()

        elif option == "4":

            benchmark_menu()

        elif option == "0":

            clear_screen()

            print(
                "Saliendo de NeuroFlex AI Testing."
            )

            break


# ============================================================
# CASOS DE PRUEBA
# ============================================================

def test_case_menu():

    test_cases = load_test_cases()

    if not test_cases:

        clear_screen()

        print(
            "No existen casos de prueba "
            "en test_cases/."
        )

        wait_key()

        return

    cursor = 0

    while True:

        clear_screen()

        print(
            "========================================"
        )

        print(
            "          CASOS DE PRUEBA"
        )

        print(
            "========================================"
        )

        print()

        print(
            "↑ ↓     Mover"
        )

        print(
            "Enter    Ejecutar caso"
        )

        print(
            "Esc      Volver"
        )

        print(
            "\n----------------------------------------\n"
        )

        for index, test_case in enumerate(
            test_cases
        ):

            cursor_indicator = (
                ">"
                if index == cursor
                else " "
            )

            print(
                f"{cursor_indicator} "
                f"{test_case.get('id', '???')} - "
                f"{test_case.get('name', 'Sin nombre')}"
            )

            print(
                f"      Sesiones: "
                f"{len(test_case.get('sessions', []))}"
            )

            description = (
                test_case.get(
                    "description",
                    ""
                )
            )

            if description:

                print(
                    f"      {description}"
                )

            print()

        key = msvcrt.getwch()

        if key in (
            "\x00",
            "\xe0"
        ):

            special_key = (
                msvcrt.getwch()
            )

            if special_key == "H":

                cursor = max(
                    0,
                    cursor - 1
                )

            elif special_key == "P":

                cursor = min(
                    len(test_cases) - 1,
                    cursor + 1
                )

            continue

        elif key == "\r":

            clear_screen()

            run_test_case(
                test_cases[cursor]
            )

            wait_key()

        elif key == "\x1b":

            return


# ============================================================
# BENCHMARK COMPLETO
# ============================================================

def benchmark_menu():

    test_cases = load_test_cases()

    if not test_cases:

        clear_screen()

        print(
            "No existen casos de prueba."
        )

        wait_key()

        return

    models = sync_model_registry()

    active_models = [
        model
        for model in models
        if model.get(
            "active",
            True
        )
        and model.get(
            "exists",
            True
        )
    ]

    if not active_models:

        clear_screen()

        print(
            "No hay modelos activos."
        )

        wait_key()

        return

    clear_screen()

    print(
        "========================================"
    )

    print(
        "          BENCHMARK COMPLETO"
    )

    print(
        "========================================"
    )

    print()

    print(
        f"Modelos activos: "
        f"{len(active_models)}"
    )

    print(
        f"Casos de prueba: "
        f"{len(test_cases)}"
    )

    total_runs = (
        len(active_models)
        * len(test_cases)
    )

    print(
        f"Inferencias totales: "
        f"{total_runs}"
    )

    print()

    confirm = input(
        "¿Ejecutar benchmark? [S/n]: "
    ).strip().lower()

    if confirm not in (
        "",
        "s",
        "si",
        "sí"
    ):

        return

    model_ids = [
        model["id"]
        for model in active_models
    ]

    current_run = 0

    for test_case in test_cases:

        current_run += len(
            model_ids
        )

        clear_screen()

        print(
            "========================================"
        )

        print(
            "          BENCHMARK EN CURSO"
        )

        print(
            "========================================"
        )

        print()

        print(
            f"Caso: "
            f"{test_case.get('id')} - "
            f"{test_case.get('name')}"
        )

        print(
            f"Progreso aproximado: "
            f"{current_run}/{total_runs}"
        )

        print()

        run_test_case(
            test_case,
            model_ids=model_ids
        )

    print()

    print(
        "========================================"
    )

    print(
        "       BENCHMARK FINALIZADO"
    )

    print(
        "========================================"
    )

    wait_key()


# ============================================================
# LISTA DE MODELOS
# ============================================================

def model_list_menu():

    models = sync_model_registry()

    if not models:

        print(
            "No existen modelos registrados."
        )

        wait_key()

        return

    order_mode = "recent"
    selected = set()
    cursor = 0

    while True:

        models = sync_model_registry()

        # ----------------------------------------
        # ORDENAMIENTO
        # ----------------------------------------

        if order_mode == "recent":

            sorted_models = sorted(
                models,
                key=lambda model:
                    model["created_at"],
                reverse=True
            )

        elif order_mode == "uses":

            sorted_models = sorted(
                models,
                key=lambda model:
                    model.get(
                        "uses",
                        0
                    ),
                reverse=True
            )

        else:

            sorted_models = sorted(
                models,
                key=lambda model:
                    model["id"]
            )

        if cursor >= len(
            sorted_models
        ):

            cursor = max(
                0,
                len(sorted_models) - 1
            )

        clear_screen()

        print(
            "========================================"
        )

        print(
            "          LISTA DE MODELOS"
        )

        print(
            "========================================"
        )

        print()

        if order_mode == "recent":

            order_text = (
                "Más recientes"
            )

        elif order_mode == "uses":

            order_text = (
                "Más utilizados"
            )

        else:

            order_text = (
                "ID"
            )

        print(
            f"Orden actual: {order_text}"
        )

        print()

        print(
            "↑ ↓     Mover"
        )

        print(
            "Espacio  Seleccionar"
        )

        print(
            "A        Activar / Desactivar"
        )

        print(
            "O        Cambiar orden"
        )

        print(
            "Enter    Ejecutar seleccionados"
        )

        print(
            "Esc      Volver"
        )

        print(
            "\n----------------------------------------\n"
        )

        for index, model in enumerate(
            sorted_models
        ):

            cursor_indicator = (
                ">"
                if index == cursor
                else " "
            )

            selected_indicator = (
                "[X]"
                if model["id"]
                in selected
                else "[ ]"
            )

            exists = model.get(
                "exists",
                True
            )

            active = model.get(
                "active",
                True
            )

            if not exists:

                status = (
                    "NO DISPONIBLE"
                )

            elif active:

                status = (
                    "ACTIVO"
                )

            else:

                status = (
                    "INACTIVO"
                )

            print(
                f"{cursor_indicator} "
                f"{selected_indicator} "
                f"{model['id']} - "
                f"{model['name']} "
                f"[{status}]"
            )

            print(
                f"      Archivo: "
                f"{model['file']}"
            )

            print(
                f"      Creado: "
                f"{format_date(model['created_at'])}"
            )

            print(
                f"      Usos: "
                f"{model.get('uses', 0)}"
            )

            print()

        key = msvcrt.getwch()

        if key in (
            "\x00",
            "\xe0"
        ):

            special_key = (
                msvcrt.getwch()
            )

            if special_key == "H":

                cursor = max(
                    0,
                    cursor - 1
                )

            elif special_key == "P":

                cursor = min(
                    len(sorted_models) - 1,
                    cursor + 1
                )

            continue

        current_model = (
            sorted_models[cursor]
        )

        # ESPACIO
        if key == " ":

            if not current_model.get(
                "exists",
                True
            ):

                continue

            model_id = (
                current_model["id"]
            )

            if model_id in selected:

                selected.remove(
                    model_id
                )

            else:

                selected.add(
                    model_id
                )

        # ACTIVAR / DESACTIVAR
        elif key.lower() == "a":

            if not current_model.get(
                "exists",
                True
            ):

                continue

            new_status = not current_model.get(
                "active",
                True
            )

            set_model_active(
                current_model["id"],
                new_status
            )

        # CAMBIAR ORDEN
        elif key.lower() == "o":

            if order_mode == "recent":

                order_mode = "uses"

            elif order_mode == "uses":

                order_mode = "id"

            else:

                order_mode = "recent"

            cursor = 0

        # ENTER
        elif key == "\r":

            if not selected:

                continue

            valid_selected = [
                model_id
                for model_id in selected
                if any(
                    model["id"]
                    == model_id
                    and model.get(
                        "exists",
                        True
                    )
                    for model
                    in models
                )
            ]

            if not valid_selected:

                continue

            clear_screen()

            print(
                "Modelos seleccionados:"
            )

            for model_id in (
                valid_selected
            ):

                print(
                    f" - {model_id}"
                )

            print()

            run_experiment(
                valid_selected
            )

            wait_key()

        # ESC
        elif key == "\x1b":

            return


# ============================================================
# ENTRY POINT
# ============================================================

if __name__ == "__main__":

    main_menu()