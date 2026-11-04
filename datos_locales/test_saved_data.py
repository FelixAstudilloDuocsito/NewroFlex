"""Check the saved-data scene and compare Unity calculations with the scenes-v1 Python contract."""
import copy
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import unittest

from test_menu_bindings import ROOT, scene_blocks, object_name, fixture


class SavedDataTests(unittest.TestCase):
    def test_authored_data_scene_and_reception_access(self):
        blocks = scene_blocks("Datos Guardados")
        controller = next(body for _, body in blocks.values() if "::NeuroFlexVR.SavedDataSceneMenu" in body)
        for field in ["summary", "detailText", "detailMeta", "scenarioFilter", "difficultyFilter", "statusFilter", "backButton", "retrySaveButton"]:
            self.assertIn(re.search(r"^  " + field + r": \{fileID: (\d+)\}", controller, re.M)[1], blocks)
        self.assertEqual(sum("guid: ab68ce6587aab0146b8dabefbd806791" in body for _, body in blocks.values()), 1)
        self.assertFalse(any("guid: 01614664b831546d2ae94a42149d80ac" in body for _, body in blocks.values()))
        self.assertFalse(any("::NeuroFlexVR.ReceptionProfileStation" in body for _, body in blocks.values()))
        for _, body in blocks.values():
            if "::TMPro.TextMeshProUGUI" in body:
                self.assertIn("m_characterHorizontalScale: 1", body)
                self.assertIn("m_isRichText: 0", body)
            for line in body.splitlines():
                if "guid:" in line:
                    continue
                for reference in re.findall(r"fileID: (\d+)", line):
                    self.assertTrue(reference == "0" or reference in blocks, reference)
        reception = scene_blocks("Recepción")
        self.assertTrue(any(object_name(body) == "VerDatosGuardados" for kind, body in reception.values() if kind == "1"))
        binding = next(body for _, body in reception.values() if "::NeuroFlexVR.ReceptionProfileStation" in body)
        self.assertIn(re.search(r"dataButton: \{fileID: (\d+)\}", binding)[1], reception)
        settings = (ROOT / "Proyecto Capstone/ProjectSettings/EditorBuildSettings.asset").read_text()
        self.assertIn("- enabled: 1\n    path: Assets/Scenes/Datos Guardados.unity", settings)

    def test_contract_metrics_and_actual_browser_callbacks(self):
        version = re.search(r"m_EditorVersion: (.+)", (ROOT / "Proyecto Capstone/ProjectSettings/ProjectVersion.txt").read_text())[1].strip()
        editor = Path(os.environ.get("UNITY_EDITOR_PATH", str(Path.home() / f"Applications/Unity/{version}/Editor")))
        mono_root = editor / "Data/MonoBleedingEdge"
        mono, compiler = mono_root / "bin/mono", mono_root / "lib/mono/4.5/csc.exe"
        if not mono.exists() or not compiler.exists():
            self.skipTest("Set UNITY_EDITOR_PATH to run saved-data C# checks")
        sys.path.insert(0, str(ROOT / "entrenamiento_modelo_ia"))
        from scenes.contracts import metrics
        cases = []
        for split in ("train", "validation", "test"):
            path = ROOT / f"entrenamiento_modelo_ia/data/scenes_v1_1/{split}.jsonl"
            for line in path.read_text().splitlines():
                row = json.loads(line)["input"]
                cases.append({"row": row, "expected": metrics(row)})
        answered = copy.deepcopy(next(case["row"] for case in cases if case["row"]["scenarioId"] == "pigeons" and case["row"]["status"] == "completed"))
        answered.update(actualPigeonCount=0, userPigeonCount=0)
        cases.append({"row": answered, "expected": metrics(answered)})
        answered = copy.deepcopy(answered)
        answered["actualPigeonCount"] = 2
        cases.append({"row": answered, "expected": metrics(answered)})
        with tempfile.TemporaryDirectory(prefix="neuroflex-saved-data-") as temporary:
            temporary = Path(temporary)
            reference = temporary / "reference.json"
            reference.write_text(json.dumps(cases, ensure_ascii=False))
            scene = temporary / "data-scene.txt"
            fixture("Datos Guardados", scene)
            json_dll = next((ROOT / "Proyecto Capstone/Library/PackageCache").glob("com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll"))
            shutil.copy2(json_dll, temporary / "Newtonsoft.Json.dll")
            sources = [ROOT / "Proyecto Capstone/Assets/Scripts" / (name + ".cs") for name in
                       ["LocalProfileDatabase", "ThemedSceneMenu", "SavedRoundMetrics", "SavedDataSceneMenu"]]
            sources += [ROOT / "datos_locales/tests" / (name + ".cs") for name in
                        ["UnityDatabaseStubs", "MenuUnityStubs", "SavedDataChecks"]]
            program = temporary / "SavedDataChecks.exe"
            subprocess.run([str(mono), str(compiler), "-nologo", "-langversion:9.0", "-r:System.Web.Extensions.dll",
                            "-r:" + str(mono_root / "lib/mono/4.5/Facades/netstandard.dll"), "-r:" + str(json_dll),
                            f"-out:{program}", *map(str, sources)], check=True)
            subprocess.run([str(mono), str(program), str(scene), str(temporary), str(reference)], check=True)
