"""Regression checks for scene-authored controls and callbacks after losing Inspector bindings."""
import base64
import os
from pathlib import Path
import re
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
BLOCK = r"^--- !u!(\d+) &(\d+)(?: stripped)?\n(.*?)(?=^---|\Z)"


def scene_blocks(name):
    source = (ROOT / f"Proyecto Capstone/Assets/Scenes/{name}.unity").read_text()
    return {m[2]: (m[1], m[3]) for m in re.finditer(BLOCK, source, re.M | re.S)}


def object_name(body):
    match = re.search(r"^  m_Name: (.*)", body, re.M)
    if not match:
        return None
    value = match[1].strip('"')
    return re.sub(r"\\x([0-9a-fA-F]{2})|\\u([0-9a-fA-F]{4})",
                  lambda m: chr(int(m[1] or m[2], 16)), value)


def fixture(name, output):
    blocks = scene_blocks(name)
    objects = {i: object_name(b) for i, (kind, b) in blocks.items() if kind == "1" and object_name(b)}
    transform_to_object = {}
    for i, (kind, body) in blocks.items():
        if kind in ("4", "224"):
            owner = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)
            if owner:
                transform_to_object[i] = owner[1]
    parents = {}
    for i, go in transform_to_object.items():
        parent = re.search(r"m_Father: \{fileID: (\d+)\}", blocks[i][1])
        parents[go] = transform_to_object.get(parent[1], "0") if parent else "0"
    lines = ["\t".join(("go", i, parents.get(i, "0"), base64.b64encode(name.encode()).decode()))
             for i, name in objects.items()]
    recognized = {"NeuroFlexVR.ReceptionProfileMenu", "NeuroFlexVR.ReceptionProfileStation", "NeuroFlexVR.SavedDataSceneMenu", "NeuroFlexVR.MemorySceneMenu", "NeuroFlexVR.ThemedSceneMenu",
                  "NeuroFlexVR.PigeonDifficultyHighlight", "MemoryGameManager", "TutorialPanelController"}
    for _, (_, body) in blocks.items():
        owner = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)
        if not owner or owner[1] not in objects:
            continue
        identifier = re.search(r"m_EditorClassIdentifier: (.*)", body)
        if not identifier:
            continue
        typename = identifier[1].split("::")[-1]
        if typename == "TMPro.TextMeshProUGUI":
            typename = "TMPro.TMP_Text"
        if typename in recognized or typename in ("TMPro.TMP_Text", "TMPro.TMP_InputField", "UnityEngine.UI.Button"):
            lines.append("\t".join(("component", owner[1], typename)))
    output.write_text("\n".join(lines))


class MenuBindingsTests(unittest.TestCase):
    def test_authored_controller_bindings_and_input(self):
        reception = scene_blocks("Recepción")
        loaders = {i: b for i, (_, b) in reception.items() if "::ImageSceneLoaderVR" in b}
        self.assertEqual({re.search(r"nombreEscena: (.+)", b)[1] for b in loaders.values()},
                         {"Palomas", "Juego De Memoria", "Puzzle Libreria"})
        for component_id, body in loaders.items():
            self.assertIn("requierePerfil: 1", body)
            owner = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)[1]
            self.assertIn("m_IsActive: 1", reception[owner][1])
            button = next(b for _, b in reception.values() if "::UnityEngine.UI.Button" in b and
                          f"m_GameObject: {{fileID: {owner}}}" in b)
            self.assertIn(f"m_Target: {{fileID: {component_id}}}", button)
            self.assertIn("m_MethodName: EjecutarCarga", button)
            self.assertIn("m_Interactable: 0", button)
        station = next(b for _, b in reception.values() if "::NeuroFlexVR.ReceptionProfileStation" in b)
        for field in ["aliasInput", "activeProfileText", "frontProfileText", "refreshButton", "createProfileButton"]:
            self.assertIn(re.search(r"^  " + field + r": \{fileID: (\d+)\}", station, re.M)[1], reception)
        canvas = next(i for i, (kind, body) in reception.items() if kind == "1" and object_name(body) == "PerfilesCanvas")
        transform = next(body for kind, body in reception.values() if kind == "224" and f"m_GameObject: {{fileID: {canvas}}}" in body)
        self.assertIn("m_LocalRotation: {x: 0, y: 1, z: 0, w: 0}", transform)
        self.assertIn("m_LocalPosition: {x: 0, y: 0, z: -64.03}", transform)
        self.assertIn("m_AnchoredPosition: {x: -2.97, y: 1.94}", transform)
        puzzle = scene_blocks("Puzzle Libreria")
        managers = [body for _, body in puzzle.values() if "::SwapPuzzleManager" in body]
        recorders = [body for _, body in puzzle.values() if "::NeuroFlexVR.PuzzleProfileRecorder" in body]
        self.assertEqual(len(recorders), 3)
        for manager in managers:
            owner = re.search(r"m_GameObject: \{fileID: (\d+)\}", manager)[1]
            recorder = next(body for body in recorders if f"m_GameObject: {{fileID: {owner}}}" in body)
            count = len(re.search(r"  pieces:\n((?:  - \{fileID: \d+\}\n)+)", manager)[1].splitlines())
            self.assertIn(f"difficulty: { {6: 1, 9: 2, 12: 3}[count]}", recorder)
        for name, controller in [("Juego De Memoria", "MemorySceneMenu")]:
            blocks = scene_blocks(name)
            body = next(b for _, b in blocks.values() if f"::NeuroFlexVR.{controller}" in b)
            required = ["difficultyView", "resultsView", "tutorialButton", "retryButton"]
            for field in required:
                reference = re.search(r"^  " + field + r": \{fileID: (\d+)\}", body, re.M)[1]
                self.assertNotEqual(reference, "0", (name, field))
                self.assertIn(reference, blocks)
            xr = [b for _, b in blocks.values() if "guid: ab68ce6587aab0146b8dabefbd806791" in b]
            self.assertEqual(len(xr), 1, name)
            self.assertIn("m_EnableXRInput: 1", xr[0])
            self.assertIn("m_EnableMouseInput: 1", xr[0])
            self.assertFalse(any("guid: 01614664b831546d2ae94a42149d80ac" in b for _, b in blocks.values()), name)

    def test_production_callbacks_recover_missing_references(self):
        version = re.search(r"m_EditorVersion: (.+)", (ROOT / "Proyecto Capstone/ProjectSettings/ProjectVersion.txt").read_text())[1].strip()
        editor = Path(os.environ.get("UNITY_EDITOR_PATH", str(Path.home() / f"Applications/Unity/{version}/Editor")))
        mono_root = editor / "Data/MonoBleedingEdge"
        mono = mono_root / "bin/mono"
        compiler = mono_root / "lib/mono/4.5/csc.exe"
        if not mono.exists() or not compiler.exists():
            self.skipTest("Set UNITY_EDITOR_PATH to run C# menu callback checks")
        with tempfile.TemporaryDirectory(prefix="neuroflex-menus-") as temporary:
            temporary = Path(temporary)
            memory, reception = temporary / "memory.txt", temporary / "reception.txt"
            fixture("Juego De Memoria", memory)
            fixture("Recepción", reception)
            sources = [ROOT / "Proyecto Capstone/Assets/Scripts" / (name + ".cs") for name in
                       ["LocalProfileDatabase", "ThemedSceneMenu", "MemorySceneMenu", "ReceptionProfileStation", "PuzzleProfileRecorder"]]
            sources += [ROOT / "datos_locales/tests" / (name + ".cs") for name in
                        ["UnityDatabaseStubs", "MenuUnityStubs", "MenuBindingsChecks"]]
            program = temporary / "MenuBindingsChecks.exe"
            subprocess.run([str(mono), str(compiler), "-nologo", "-langversion:9.0", "-r:System.Web.Extensions.dll",
                            f"-out:{program}", *map(str, sources)], check=True)
            subprocess.run([str(mono), str(program), str(memory), str(temporary), str(reception)], check=True)
