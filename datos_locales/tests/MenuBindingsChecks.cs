using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NeuroFlexVR;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public static class MenuBindingsChecks
{
    private static int checks;
    private static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        Console.WriteLine("PASS: " + description); checks++;
    }
    private static Scene Load(string path)
    {
        // The fixture is extracted from the actual serialized scene, including inactive objects.
        var lines = File.ReadAllLines(path).Select(line => line.Split('\t')).ToArray();
        var scene = new Scene();
        var objects = new Dictionary<string, GameObject>();
        foreach (var row in lines.Where(row => row[0] == "go"))
        {
            var go = new GameObject(Encoding.UTF8.GetString(Convert.FromBase64String(row[3])));
            go.scene = scene; objects.Add(row[1], go); scene.objects.Add(go);
            var rect = new RectTransform { gameObject = go }; go.transform = rect; go.components.Add(rect);
        }
        foreach (var row in lines.Where(row => row[0] == "go"))
            if (objects.ContainsKey(row[2])) objects[row[1]].transform.SetParent(objects[row[2]].transform);
        foreach (var row in lines.Where(row => row[0] == "component"))
        {
            var component = (Component)Activator.CreateInstance(Type.GetType(row[2], true));
            component.gameObject = objects[row[1]]; objects[row[1]].components.Add(component);
        }
        return scene;
    }
    private static T Named<T>(Scene scene, string name) where T : Component =>
        scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true)).Single(c => c.name == name);
    private static void Invoke(object target, string method, params object[] arguments) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    private static void Click(Button button)
    {
        Check(button != null && button.interactable, "Clickable: " + button.name);
        button.onClick.Invoke();
    }
    public static int Main(string[] args)
    {
        try
        {
            Application.persistentDataPath = args[1]; Application.dataPath = args[1];
            var reception = Load(args[2]);
            var station = Named<ReceptionProfileStation>(reception, "PerfilesCanvas");
            Invoke(station, "Start");
            Check(station.aliasInput != null && station.keyboardButtons.Length == 37, "Rear desk recovers its authored controls");
            Check(station.sceneButtons.All(button => !button.interactable), "Scene entrances wait for a selected profile");
            Click(station.dataButton);
            Check(SceneManager.lastLoaded == "Datos Guardados", "The rear desk opens saved data even before selecting a player");
            Click(station.keyboardButtons[0]); Click(station.keyboardButtons[1]);
            Check(station.aliasInput.text == "AB", "VR keyboard enters an alias");
            Click(station.createProfileButton);
            var firstProfile = PatientSession.ActiveProfile;
            Check(firstProfile?.alias == "AB" && station.sceneButtons.All(button => button.interactable), "Creating a profile selects it and enables all original entrances");
            Check(station.frontProfileText.text.Contains("AB"), "Front status identifies whose results will be recorded");
            station.aliasInput.text = "ab"; Click(station.createProfileButton);
            Check(station.feedback.text.Contains("Ya existe") && PatientSession.ActiveProfile.id == firstProfile.id, "Duplicate aliases do not replace the active profile");
            for (int i = 0; i < 6; i++)
            {
                station.aliasInput.text = "Perfil " + i;
                Click(station.createProfileButton);
            }
            var secondProfile = PatientSession.ActiveProfile;
            Check(station.pageText.text == "2 / 2", "New profiles remain selected on their sorted page");
            Click(station.previous); Click(station.profileRows[0]);
            Check(PatientSession.ActiveProfile.id == firstProfile.id, "A saved profile can be chosen from the paginated list");
            Click(station.refreshButton);
            Check(station.activeProfileText.text.Contains("AB"), "Refreshing the list preserves profile selection");
            var reentered = Named<ReceptionProfileStation>(Load(args[2]), "PerfilesCanvas");
            Invoke(reentered, "Start");
            Check(reentered.sceneButtons.All(button => button.interactable) && reentered.activeProfileText.text.Contains("AB"), "Returning to Reception keeps the selected profile");
            var memory = Load(args[0]);
            var menu = Named<MemorySceneMenu>(memory, "Memoria - Menús");
            Invoke(menu, "Awake"); Invoke(menu, "Start");
            Check(menu.difficultyView.primaryButton.name == "Iniciar", "Recovered Memory primary starts the activity");
            Check(menu.resultsView.primaryButton.name == "VolverAlInicio", "Recovered result primary returns to difficulty");
            Click(menu.difficultyView.optionButtons[1]); Click(menu.difficultyView.primaryButton);
            Check(menu.managers[1].starts == 1 && menu.levels[1].activeSelf, "Medium starts the correct board");
            menu.managers[1].onGameWon.Invoke();
            Check(menu.resultsView.gameObject.activeSelf && menu.resultSummary.text.Contains("6 / 6"), "Winning displays results on the authored panel");
            Click(menu.resultsView.primaryButton);
            Check(menu.difficultyView.gameObject.activeSelf && menu.difficultyView.optionButtons[1].GetComponentInChildren<PigeonDifficultyHighlight>(true).enabled,
                "Return keeps selected difficulty");
            Click(menu.tutorialButton); menu.tutorial.onTutorialCerrado.Invoke();
            Check(menu.difficultyView.gameObject.activeSelf, "Tutorial returns to the difficulty menu");
            Click(menu.receptionButtons[0]);
            Check(SceneManager.lastLoaded == "Recepción", "Reception navigation works");
            PatientSession.Select(null);
            menu.ShowMenu();
            Check(menu.difficultyView.primaryButton.interactable, "Memory start stays enabled without a profile");
            for (int i = 0; i < menu.levels.Length; i++)
            {
                Click(menu.difficultyView.optionButtons[i]);
                int previousStarts = menu.managers[i].starts;
                Click(menu.difficultyView.primaryButton);
                Check(menu.managers[i].starts == previousStarts + 1 && menu.levels[i].activeSelf &&
                    !menu.difficultyView.gameObject.activeSelf, "Direct scene entry starts difficulty " + (i + 1));
                menu.ShowMenu();
            }
            // Exercise the actual recorder and SQLite store, with a small puzzle state host.
            PatientSession.Select(firstProfile);
            var owner = new SwapPuzzleManager();
            for (int i = 0; i < 6; i++) owner.pieces.Add(new PuzzlePiece { pieceID = i, currentSlotIndex = i });
            var recorder = new PuzzleProfileRecorder { difficulty = 2 };
            recorder.BeginRound(owner);
            PatientSession.Select(secondProfile);
            recorder.RecordPlacement(false);
            Time.unscaledDeltaTime = 2;
            Invoke(recorder, "Update");
            Invoke(recorder, "OnApplicationPause", true);
            Invoke(recorder, "Update");
            Invoke(recorder, "OnApplicationPause", false);
            recorder.CompleteRound(); recorder.CompleteRound(); Invoke(recorder, "OnDisable");
            using (var database = PatientSession.OpenDatabase())
            {
                var result = database.ListResults(firstProfile.id).Single();
                Check(result.scenarioId == "puzzle" && result.status == "completed" && result.difficulty == 2,
                    "Puzzle completion is saved once with the correct difficulty");
                Check(result.metricsJson.Contains("\"activeTimeMs\":2000") && result.metricsJson.Contains("\"incorrectPlacements\":1"),
                    "Puzzle recording excludes paused time and includes incorrect placements");
                Check(database.CountResults(secondProfile.id) == 0, "A round stays attached to the profile captured at its start");
            }
            recorder.BeginRound(owner); Invoke(recorder, "OnDisable");
            recorder.BeginRound(owner); Invoke(recorder, "OnApplicationQuit"); Invoke(recorder, "OnDisable");
            using (var database = PatientSession.OpenDatabase())
                Check(database.ListResults(secondProfile.id).Select(result => result.status).OrderBy(status => status)
                    .SequenceEqual(new[] { "abandoned", "interrupted" }), "Unfinished puzzle rounds are stored once per lifecycle exit");
            Console.WriteLine(checks + " menu checks passed"); return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
