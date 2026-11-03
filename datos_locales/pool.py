#!/usr/bin/env python3
"""Pool SQLite de pruebas de Palomas. Solo usa la biblioteca estándar de Python."""
import argparse
import csv
import json
import sqlite3
import sys
import uuid
from datetime import datetime, timezone
from pathlib import Path

DEFAULT_DATABASE = Path(__file__).with_name("palomas.sqlite3")
SCHEMA = """
CREATE TABLE IF NOT EXISTS submissions (
    record_id TEXT PRIMARY KEY,
    round_id TEXT NOT NULL UNIQUE,
    patient_nick TEXT NOT NULL,
    participant_id TEXT NOT NULL,
    device_id TEXT NOT NULL,
    session_id TEXT NOT NULL,
    difficulty_level INTEGER NOT NULL CHECK(difficulty_level BETWEEN 1 AND 3),
    started_at TEXT NOT NULL,
    ended_at TEXT NOT NULL,
    status TEXT NOT NULL,
    actual_pigeon_count INTEGER NOT NULL,
    user_pigeon_count INTEGER,
    response_time_ms INTEGER,
    saved_at TEXT NOT NULL,
    imported_at TEXT NOT NULL,
    payload_json TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_patient_nick ON submissions(patient_nick);
CREATE INDEX IF NOT EXISTS idx_session ON submissions(session_id);
CREATE VIEW IF NOT EXISTS results AS
SELECT record_id, round_id, patient_nick, participant_id, device_id, session_id,
       difficulty_level, started_at, ended_at, status, actual_pigeon_count,
       user_pigeon_count, response_time_ms,
       CASE WHEN status = 'completed' AND user_pigeon_count IS NOT NULL
            THEN ABS(user_pigeon_count - actual_pigeon_count) END AS absolute_error,
       saved_at, imported_at
FROM submissions;
PRAGMA user_version = 1;
"""


def connect(database):
    database = Path(database)
    database.parent.mkdir(parents=True, exist_ok=True)
    connection = sqlite3.connect(database, timeout=30)
    if connection.execute("PRAGMA user_version").fetchone()[0] not in (0, 1):
        connection.close()
        raise ValueError("Versión de base de datos no soportada.")
    connection.executescript(SCHEMA)
    connection.row_factory = sqlite3.Row
    return connection


def required_text(obj, key):
    value = obj.get(key)
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{key}: se requiere texto no vacío")
    return value


def integer(obj, key, nullable=False):
    value = obj.get(key)
    if value is None and nullable:
        return None
    if type(value) is not int or value < 0:
        raise ValueError(f"{key}: se requiere entero no negativo")
    return value


def validate(payload):
    if not isinstance(payload, dict) or payload.get("schemaVersion") != "pigeon-local-v1":
        raise ValueError("Se requiere un registro pigeon-local-v1")
    if payload.get("consentGranted") is not True:
        raise ValueError("Registro sin aceptación de guardado")
    record_id = str(uuid.UUID(required_text(payload, "recordId")))
    nick = required_text(payload, "patientNick")
    if not 2 <= len(nick) <= 32 or any(not (c.isalnum() or c in "_-") for c in nick):
        raise ValueError("Nick inválido: usa 2–32 letras, números, guiones o guion bajo")
    required_text(payload, "deviceId")
    required_text(payload, "savedAt")
    request = payload.get("request")
    if not isinstance(request, dict) or request.get("contractVersion") != "scenes-v1":
        raise ValueError("Falta request scenes-v1")
    rounds = request.get("rounds")
    if not isinstance(rounds, list) or len(rounds) != 1 or not isinstance(rounds[0], dict):
        raise ValueError("Cada archivo debe contener exactamente una ronda")
    round_data = rounds[0]
    round_id = required_text(round_data, "roundId")
    if not round_id.startswith("r-") or str(uuid.UUID(round_id[2:])) != record_id:
        raise ValueError("recordId y roundId no identifican la misma ronda")
    if round_data.get("scenarioId") != "pigeons":
        raise ValueError("El escenario debe ser pigeons")
    if integer(round_data, "difficultyLevel") not in (1, 2, 3):
        raise ValueError("Dificultad fuera de 1–3")
    if round_data.get("status") not in ("completed", "abandoned", "interrupted"):
        raise ValueError("Estado de ronda inválido")
    for key in ("participantId", "sessionId", "startedAt", "endedAt"):
        required_text(round_data, key)
    integer(round_data, "actualPigeonCount")
    integer(round_data, "userPigeonCount", nullable=True)
    integer(round_data, "responseTimeMs", nullable=True)
    if round_data["status"] == "completed" and round_data.get("userPigeonCount") is None:
        raise ValueError("Una ronda completada requiere respuesta")
    return record_id, round_data


def import_payload(connection, payload):
    record_id, r = validate(payload)
    canonical = json.dumps(payload, sort_keys=True, ensure_ascii=False, separators=(",", ":"), allow_nan=False)
    existing = connection.execute("SELECT payload_json FROM submissions WHERE record_id = ?", (record_id,)).fetchone()
    if existing:
        if existing[0] != canonical:
            raise ValueError(f"Conflicto: UUID {record_id} ya existe con datos diferentes; no se sobrescribió")
        return False
    with connection:
        connection.execute(
            "INSERT INTO submissions VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
            (record_id, r["roundId"], payload["patientNick"], r["participantId"], payload["deviceId"],
             r["sessionId"], r["difficultyLevel"], r["startedAt"], r["endedAt"], r["status"],
             r["actualPigeonCount"], r.get("userPigeonCount"), r.get("responseTimeMs"), payload["savedAt"],
             datetime.now(timezone.utc).isoformat(), canonical),
        )
    return True


def source_payloads(path):
    if path.suffix.lower() in (".sqlite3", ".sqlite", ".db"):
        source = sqlite3.connect(path.resolve().as_uri() + "?mode=ro", uri=True)
        try:
            for (raw,) in source.execute("SELECT payload_json FROM submissions ORDER BY record_id"):
                yield json.loads(raw)
        finally:
            source.close()
    else:
        yield json.loads(path.read_text(encoding="utf-8-sig"))


def import_sources(connection, sources, database):
    inserted = duplicates = errors = 0
    for source in sources:
        source = Path(source)
        if not source.exists():
            print(f"ERROR: no existe {source}", file=sys.stderr)
            errors += 1
            continue
        paths = sorted(source.rglob("*.json")) if source.is_dir() else [source]
        if not paths:
            print(f"ERROR: no hay archivos JSON en {source}", file=sys.stderr)
            errors += 1
        for path in paths:
            if path.resolve() == Path(database).resolve():
                print(f"ERROR: el origen y destino son la misma base: {path}", file=sys.stderr)
                errors += 1
                continue
            try:
                for payload in source_payloads(path):
                    try:
                        if import_payload(connection, payload):
                            inserted += 1
                        else:
                            duplicates += 1
                    except (ValueError, TypeError, KeyError, sqlite3.Error) as exc:
                        errors += 1
                        print(f"ERROR {path}: {exc}", file=sys.stderr)
            except (OSError, ValueError, sqlite3.Error) as exc:
                errors += 1
                print(f"ERROR {path}: {exc}", file=sys.stderr)
    return inserted, duplicates, errors


def export_data(connection, output, format_name):
    with Path(output).open("w", encoding="utf-8", newline="") as stream:
        if format_name == "csv":
            cursor = connection.execute("SELECT * FROM results ORDER BY started_at, record_id")
            writer = csv.writer(stream)
            writer.writerow([column[0] for column in cursor.description])
            writer.writerows(cursor)
        else:
            rounds = [json.loads(row[0])["request"]["rounds"][0] for row in
                      connection.execute("SELECT payload_json FROM submissions ORDER BY started_at, record_id")]
            json.dump({"contractVersion": "scenes-v1", "rounds": rounds}, stream, ensure_ascii=False, indent=2)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--db", type=Path, default=DEFAULT_DATABASE, help="Base SQLite de destino")
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("init", help="Crear la base vacía")
    importer = commands.add_parser("import", help="Importar archivos/carpetas accepted o fusionar otra base SQLite")
    importer.add_argument("sources", type=Path, nargs="+")
    commands.add_parser("stats", help="Contar registros por dificultad y estado")
    exporter = commands.add_parser("export", help="Exportar resultados")
    exporter.add_argument("output", type=Path)
    exporter.add_argument("--format", choices=("csv", "scenes-v1"), default="csv")
    args = parser.parse_args(argv)
    try:
        connection = connect(args.db)
        try:
            if args.command == "import":
                inserted, duplicates, errors = import_sources(connection, args.sources, args.db)
                print(f"Nuevos: {inserted}; duplicados omitidos: {duplicates}; errores: {errors}")
                return 1 if errors else 0
            if args.command == "export":
                if args.output.resolve() == args.db.resolve():
                    raise ValueError("La exportación no puede sobrescribir la base de datos")
                export_data(connection, args.output, args.format)
                print(f"Exportado a {args.output}")
            elif args.command == "stats":
                print(f"Total: {connection.execute('SELECT COUNT(*) FROM submissions').fetchone()[0]}")
                for row in connection.execute("SELECT difficulty_level, status, COUNT(*) FROM submissions GROUP BY 1, 2"):
                    print(f"Dificultad {row[0]} | {row[1]}: {row[2]}")
            else:
                print(f"Base lista: {args.db}")
        finally:
            connection.close()
    except (OSError, ValueError, sqlite3.Error) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
