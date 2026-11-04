using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NeuroFlexVR;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class SavedDataChecks
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
        Console.WriteLine("PASS: " + message);
    }
    private static void Reject(PatientResult result)
    {
        try { SavedRoundMetrics.Read(result); }
        catch (Exception) { return; }
        throw new Exception("Invalid metrics were accepted: " + result.metricsJson);
    }
    private static JObject LoadJson(string text)
    {
        using (var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None })
            return JObject.Load(reader);
    }
    private static PatientResult Record(JObject row, string json = null) => new PatientResult
    {
        id = (string)row["roundId"], profileId = (string)row["participantId"], sessionId = (string)row["sessionId"],
        scenarioId = (string)row["scenarioId"], difficulty = (int)row["difficultyLevel"], status = (string)row["status"],
        startedAt = (string)row["startedAt"], endedAt = (string)row["endedAt"], metricsJson = json ?? row.ToString(Formatting.None)
    };
    private static Scene Load(string path)
    {
        var rows = File.ReadAllLines(path).Select(line => line.Split('\t')).ToArray();
        var scene = new Scene(); var objects = new Dictionary<string, GameObject>();
        foreach (var row in rows.Where(row => row[0] == "go"))
        {
            var go = new GameObject(Encoding.UTF8.GetString(Convert.FromBase64String(row[3]))) { scene = scene };
            objects.Add(row[1], go); scene.objects.Add(go);
            var rect = new RectTransform { gameObject = go }; go.transform = rect; go.components.Add(rect);
        }
        foreach (var row in rows.Where(row => row[0] == "go"))
            if (objects.ContainsKey(row[2])) objects[row[1]].transform.SetParent(objects[row[2]].transform);
        foreach (var row in rows.Where(row => row[0] == "component"))
        {
            var component = (Component)Activator.CreateInstance(Type.GetType(row[2], true));
            component.gameObject = objects[row[1]]; objects[row[1]].components.Add(component);
        }
        return scene;
    }
    private static SavedDataSceneMenu Menu(string fixture)
    {
        var scene = Load(fixture);
        return scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<SavedDataSceneMenu>(true)).Single();
    }
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    private static void Click(Button button)
    {
        if (button == null || !button.interactable) throw new Exception("Button not enabled: " + button?.name);
        button.onClick.Invoke();
    }
    private static string Flat(string scenario, string status = "completed") => scenario == "memory"
        ? "{\"correctPairAttempts\":3,\"incorrectPairAttempts\":2,\"pairsFound\":3,\"totalPairs\":3,\"totalCards\":6,\"activeTimeMs\":8000,\"aidsUsed\":0,\"previewDurationMs\":2000,\"deckId\":\"memory-level-2\"}"
        : "{\"totalPieces\":6,\"correctPlacements\":2,\"incorrectPlacements\":1,\"activeTimeMs\":5000,\"aidsUsed\":0,\"rotationEnabled\":false}";
    private static PatientResult Local(string id, string profile, string scenario, int difficulty, string status, string json, int second) =>
        new PatientResult { id = id, profileId = profile, sessionId = "s-browser", scenarioId = scenario, difficulty = difficulty,
            status = status, startedAt = "2026-11-04T12:00:00.000Z", endedAt = "2026-11-04T12:01:" + second.ToString("D2") + ".000Z", metricsJson = json };

    public static int Main(string[] args)
    {
        try
        {
            Application.persistentDataPath = args[1]; Application.dataPath = args[1];
            JArray cases;
            using (var reader = new JsonTextReader(new StringReader(File.ReadAllText(args[2]))) { DateParseHandling = DateParseHandling.None })
                cases = JArray.Load(reader);
            foreach (JObject example in cases)
            {
                var row = (JObject)example["row"];
                var expected = (JObject)example["expected"];
                var result = Record(row);
                foreach (string json in new[] { result.metricsJson,
                    new JObject { ["contractVersion"] = "scenes-v1", ["rounds"] = new JArray(row.DeepClone()) }.ToString(Formatting.None),
                    new JObject { ["request"] = new JObject { ["contractVersion"] = "scenes-v1", ["rounds"] = new JArray(row.DeepClone()) } }.ToString(Formatting.None) })
                {
                    result.metricsJson = json;
                    var actual = SavedRoundMetrics.Read(result).Computed;
                    if (!JToken.DeepEquals(actual, expected))
                        throw new Exception("Metrics differ from Python: " + row["roundId"] + "\n" + actual + "\n" + expected);
                }
            }
            Check(true, cases.Count + " corpus/edge cases match scenes-v1 in all three local JSON shapes");
            var partial = Local("partial", "profile", "memory", 1, "abandoned", "{}", 0);
            Check(SavedRoundMetrics.Read(partial).Computed["activeTimeSeconds"].Type == JTokenType.Null,
                "Absent fields stay absent instead of becoming zero");
            var emptyMemory = LoadJson(Flat("memory"));
            emptyMemory["correctPairAttempts"] = emptyMemory["pairsFound"] = emptyMemory["incorrectPairAttempts"] = 0;
            partial.metricsJson = emptyMemory.ToString(Formatting.None);
            var emptyMetrics = SavedRoundMetrics.Read(partial);
            Check(emptyMetrics.Computed["matchingAccuracyPercent"].Type == JTokenType.Null && emptyMetrics.Computed["resolutionTimeSeconds"].Type == JTokenType.Null,
                "Zero attempts and unfinished rounds have no precision or resolution time");
            partial.metricsJson = "{bad}"; Reject(partial);
            partial.metricsJson = "{\"activeTimeMs\":true}"; Reject(partial);
            partial.metricsJson = "{\"activeTimeMs\":-1}"; Reject(partial);
            partial.metricsJson = "{\"activeTimeMs\":1,\"activeTimeMs\":2}"; Reject(partial);
            partial.metricsJson = "{\"participantId\":\"other-profile\"}"; Reject(partial);
            Check(true, "Malformed, duplicate, boolean, negative, and mismatched-profile metrics are rejected");

            PatientProfile alpha, beta;
            using (var db = PatientSession.OpenDatabase())
            {
                alpha = db.CreateProfile("Alfa"); beta = db.CreateProfile("Beta");
                db.SaveResult(Local("bad-json", alpha.id, "memory", 2, "completed", "{bad}", 9));
                db.SaveResult(Local("memory-good", alpha.id, "memory", 2, "completed", Flat("memory"), 8));
                db.SaveResult(Local("puzzle-partial", alpha.id, "puzzle", 3, "abandoned", Flat("puzzle"), 7));
                db.SaveResult(Local("memory-empty", alpha.id, "memory", 2, "abandoned", emptyMemory.ToString(Formatting.None), 6));
                var pigeon = (JObject)((JObject)cases.First(item => (string)item["row"]["scenarioId"] == "pigeons"))["row"].DeepClone();
                pigeon["participantId"] = alpha.id; pigeon["roundId"] = "pigeon-good";
                var pigeonRecord = Record(pigeon); pigeonRecord.difficulty = 1;
                db.SaveResult(pigeonRecord);
            }
            PatientSession.Select(alpha);
            var menu = Menu(args[0]); Invoke(menu, "Start");
            Check(menu.summary.text.Contains("Alfa") && menu.summary.text.Contains("5 de 5") && menu.nextRounds.interactable,
                "The actual scene loads the active profile and paginates its saved rounds");
            Check(menu.detailText.text.Contains("inválidos"), "A corrupt round shows a local error without stopping the browser");
            Click(menu.roundRows[1]);
            Check(menu.detailText.text.Contains("60 %") && menu.detailText.text.Contains("8 s"), "Saved Memory details use attempts-based accuracy and active seconds");
            Click(menu.contextTab);
            Check(menu.detailText.text.Contains("2000 ms") && menu.detailText.text.Contains("No registrado"), "The context tab shows configured preview and missing legacy metadata");
            Click(menu.metricsTab); Click(menu.nextRounds);
            Check(menu.roundPageText.text == "2 / 2" && menu.roundRows[0].gameObject.activeSelf && !menu.roundRows[1].gameObject.activeSelf,
                "The final page hides unused round rows");
            Click(menu.profileRows[1]);
            Check(menu.summary.text.Contains("Beta") && menu.detailText.text.Contains("no tiene rondas") && PatientSession.ActiveProfile.id == alpha.id,
                "Browsing another profile leaves the active player unchanged and handles an empty history");
            Click(menu.profileRows[0]); Click(menu.scenarioFilter); Click(menu.scenarioFilter);
            Click(menu.difficultyFilter); Click(menu.difficultyFilter); Click(menu.statusFilter);
            Check(menu.summary.text.Contains("2 de 5"), "Activity, difficulty, and status filters combine in SQLite");
            using (var db = PatientSession.OpenDatabase()) db.SaveResult(Local("memory-new", alpha.id, "memory", 2, "completed", Flat("memory"), 10));
            Click(menu.refreshButton);
            Check(menu.summary.text.Contains("3 de 6"), "Refresh reads newly saved rows without changing the filters");
            Click(menu.backButton);
            Check(SceneManager.lastLoaded == "Recepción" && PatientSession.ActiveProfile.id == alpha.id, "Return goes to Reception with the original active player");
            using (var db = PatientSession.OpenDatabase())
            {
                for (int i = 0; i < 1001; i++) db.SaveResult(Local("bulk-" + i.ToString("D4"), beta.id, "memory", 1, "abandoned", "{}", 1));
                var last = db.ListResultsPage(beta.id, 1000);
                Check(db.CountResults(beta.id) == 1001 && last.Count == 1 && last[0].id == "bulk-0000", "Pagination can reach histories beyond the old 1000-row limit");
                Check(db.CountResults(alpha.id) == 6 && db.CountResults(beta.id, "memory' OR 1=1 --") == 0,
                    "Filtered queries stay parameterized and isolated by profile");
            }
            Invoke(menu, "OnDestroy");
            Application.persistentDataPath = Path.Combine(args[1], "empty");
            var empty = Menu(args[0]); Invoke(empty, "Start");
            Check(empty.summary.text.Contains("Aún no hay perfiles") && empty.profilePageText.text == "0 / 0" && !empty.metricsTab.interactable,
                "Opening the new scene with an empty database has a complete empty state");
            Invoke(empty, "OnDestroy");
            Console.WriteLine(checks + " saved-data checks passed"); return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
