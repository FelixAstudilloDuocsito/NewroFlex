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
    private static void Invoke(object target, string method) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
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
            using (var database = PatientSession.OpenDatabase())
                PatientSession.Select(database.CreateProfile("Prueba Memoria"));
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
            Console.WriteLine(checks + " menu checks passed"); return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
