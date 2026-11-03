import copy
import json
import sqlite3
import tempfile
import unittest
import uuid
from pathlib import Path

import pool


def sample(nick="PACIENTE_01", device="d-sede-a"):
    record_id = uuid.uuid4()
    return {
        "schemaVersion": "pigeon-local-v1", "recordId": str(record_id),
        "patientNick": nick, "deviceId": device, "savedAt": "2026-11-02T12:00:00.000Z",
        "consentGranted": True,
        "request": {"contractVersion": "scenes-v1", "rounds": [{
            "roundId": "r-" + record_id.hex, "participantId": "p-" + uuid.uuid4().hex,
            "sessionId": "s-" + uuid.uuid4().hex, "scenarioId": "pigeons", "difficultyLevel": 3,
            "startedAt": "2026-11-02T11:59:00.000Z", "endedAt": "2026-11-02T12:00:00.000Z",
            "status": "completed", "actualPigeonCount": 12, "userPigeonCount": 0,
            "responseTimeMs": 3500,
        }]},
        "arrivalMetadata": {"initiallyArriving": 10, "lateArriving": 2},
    }


class PoolTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)
        self.path = Path(self.folder.name) / "pool.sqlite3"
        self.db = pool.connect(self.path)
        self.addCleanup(self.db.close)

    def test_duplicate_round_is_imported_once_even_with_reordered_keys(self):
        payload = sample()
        self.assertTrue(pool.import_payload(self.db, payload))
        self.assertFalse(pool.import_payload(self.db, dict(reversed(list(payload.items())))))
        self.assertEqual(self.db.execute("SELECT COUNT(*) FROM submissions").fetchone()[0], 1)

    def test_same_nick_on_two_devices_keeps_both_records(self):
        for device in ("d-sede-a", "d-sede-b"):
            self.assertTrue(pool.import_payload(self.db, sample(device=device)))
        self.assertEqual(self.db.execute("SELECT COUNT(*) FROM submissions").fetchone()[0], 2)

    def test_conflicting_uuid_does_not_overwrite_original(self):
        payload = sample()
        pool.import_payload(self.db, payload)
        conflict = copy.deepcopy(payload)
        conflict["request"]["rounds"][0]["userPigeonCount"] = 12
        with self.assertRaisesRegex(ValueError, "Conflicto"):
            pool.import_payload(self.db, conflict)
        self.assertEqual(self.db.execute("SELECT user_pigeon_count FROM submissions").fetchone()[0], 0)

    def test_missing_or_denied_consent_is_rejected(self):
        for value in (False, None, "true", 1):
            payload = sample()
            payload["consentGranted"] = value
            with self.assertRaises(ValueError):
                pool.import_payload(self.db, payload)
        self.assertEqual(self.db.execute("SELECT COUNT(*) FROM submissions").fetchone()[0], 0)

    def test_round_identity_mismatch_and_invalid_nicks_are_rejected(self):
        payload = sample()
        payload["recordId"] = str(uuid.uuid4())
        with self.assertRaises(ValueError):
            pool.import_payload(self.db, payload)
        for nick in ("", "A", "nombre con espacios", "x" * 33, "PAC<01>"):
            with self.assertRaises(ValueError):
                pool.import_payload(self.db, sample(nick=nick))

    def test_zero_answer_and_missing_answer_remain_distinct(self):
        pool.import_payload(self.db, sample())
        interrupted = sample()
        interrupted["request"]["rounds"][0].update(status="interrupted", userPigeonCount=None, responseTimeMs=None)
        pool.import_payload(self.db, interrupted)
        rows = list(self.db.execute("SELECT status, user_pigeon_count, absolute_error FROM results ORDER BY status"))
        self.assertEqual(tuple(rows[0]), ("completed", 0, 12))
        self.assertEqual(tuple(rows[1]), ("interrupted", None, None))

    def test_merged_databases_deduplicate_and_preserve_metadata(self):
        other_path = Path(self.folder.name) / "other.sqlite3"
        other = pool.connect(other_path)
        shared = sample()
        unique = sample(device="d-sede-b")
        pool.import_payload(self.db, shared)
        for payload in (shared, unique):
            pool.import_payload(other, payload)
        other.close()
        self.assertEqual(pool.import_sources(self.db, [other_path], self.path), (1, 1, 0))
        stored = self.db.execute("SELECT payload_json FROM submissions WHERE record_id = ?", (unique["recordId"],)).fetchone()[0]
        self.assertEqual(json.loads(stored)["arrivalMetadata"]["lateArriving"], 2)

    def test_bad_file_is_reported_and_good_files_survive_reimport(self):
        incoming = Path(self.folder.name) / "accepted"
        incoming.mkdir()
        (incoming / "good.json").write_text(json.dumps(sample()), encoding="utf-8")
        (incoming / "bad.json").write_text("{unfinished", encoding="utf-8")
        self.assertEqual(pool.import_sources(self.db, [incoming], self.path), (1, 0, 1))
        self.assertEqual(pool.import_sources(self.db, [incoming], self.path), (0, 1, 1))

    def test_csv_and_scenes_exports_preserve_values(self):
        payload = sample(nick="PAC_Ñ01")
        pool.import_payload(self.db, payload)
        csv_path = Path(self.folder.name) / "results.csv"
        pool.export_data(self.db, csv_path, "csv")
        self.assertIn("PAC_Ñ01", csv_path.read_text(encoding="utf-8"))
        json_path = Path(self.folder.name) / "scenes.json"
        pool.export_data(self.db, json_path, "scenes-v1")
        self.assertEqual(json.loads(json_path.read_text(encoding="utf-8")), payload["request"])


if __name__ == "__main__":
    unittest.main()
