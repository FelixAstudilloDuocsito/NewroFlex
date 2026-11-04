"""Compile and exercise the production Unity SQLite store with Unity's bundled C# tools."""
import os
from pathlib import Path
import re
import sqlite3
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


class ProfileDatabaseTests(unittest.TestCase):
    def test_production_csharp_store(self):
        version_file = ROOT / "Proyecto Capstone/ProjectSettings/ProjectVersion.txt"
        version = re.search(r"m_EditorVersion: (.+)", version_file.read_text())[1].strip()
        editor = Path(os.environ.get("UNITY_EDITOR_PATH", str(Path.home() / f"Applications/Unity/{version}/Editor")))
        mono_root = editor / "Data/MonoBleedingEdge"
        mono = mono_root / "bin/mono"
        compiler = mono_root / "lib/mono/4.5/csc.exe"
        if not mono.exists() or not compiler.exists():
            self.skipTest("Set UNITY_EDITOR_PATH to the installed Unity Editor directory to run C# integration checks")
        with tempfile.TemporaryDirectory(prefix="neuroflex-profiles-") as temporary:
            program = Path(temporary) / "ProfileDatabaseChecks.exe"
            sources = [ROOT / "Proyecto Capstone/Assets/Scripts/LocalProfileDatabase.cs",
                       ROOT / "datos_locales/tests/UnityDatabaseStubs.cs",
                       ROOT / "datos_locales/tests/ProfileDatabaseChecks.cs"]
            subprocess.run([str(mono), str(compiler), "-nologo", "-langversion:9.0",
                            "-r:System.Web.Extensions.dll", f"-out:{program}", *map(str, sources)], check=True)
            database = Path(temporary) / "test.sqlite3"
            subprocess.run([str(mono), str(program), str(database)], check=True)
            subprocess.run([str(mono), str(program), str(database), "reopen"], check=True)
            with sqlite3.connect(database) as connection:
                self.assertEqual(connection.execute("PRAGMA integrity_check").fetchone()[0], "ok")
                self.assertEqual(connection.execute("SELECT COUNT(*) FROM activity_results").fetchone()[0], 4)
                self.assertEqual(connection.execute("PRAGMA foreign_key_check").fetchall(), [])
