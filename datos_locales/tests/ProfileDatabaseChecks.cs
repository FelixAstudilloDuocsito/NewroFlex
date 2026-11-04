using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NeuroFlexVR;

public static class ProfileDatabaseChecks
{
    private static int checks;
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        checks++;
        Console.WriteLine("PASS: " + description);
    }
    private static void Reject(Action action, string description)
    {
        try { action(); } catch { Check(true, description); return; }
        throw new Exception("Was accepted: " + description);
    }
    private static PatientResult Result(string id, string profile, string scenario, string status = "completed")
    {
        return new PatientResult { id = id, profileId = profile, sessionId = "test-session", scenarioId = scenario,
            difficulty = 2, status = status, startedAt = "2026-11-03T10:00:00.000Z", endedAt = "2026-11-03T10:01:00.000Z",
            metricsJson = "{\"activeTimeMs\":60000}" };
    }
    public static int Main(string[] args)
    {
        try
        {
            string path = args[0];
            UnityEngine.Application.persistentDataPath = Path.GetDirectoryName(path);
            UnityEngine.Application.dataPath = UnityEngine.Application.persistentDataPath;
            if (args.Length > 1 && args[1] == "reopen")
            {
                using (var db = new LocalProfileDatabase(path))
                    Check(db.ListProfiles().Count == 2 && db.CountResults(db.ListProfiles().First(p => p.alias == "Ágata O'Neil").id) == 3,
                        "Profiles and results survive a fresh process");
                return 0;
            }
            string a, b;
            using (var db = new LocalProfileDatabase(path))
            {
                Check(db.ListProfiles().Count == 0, "Database initializes empty");
                var first = db.CreateProfile("  Ágata O'Neil  "); a = first.id;
                var second = db.CreateProfile("Paciente de prueba B"); b = second.id;
                Check(a != b && first.alias == "Ágata O'Neil", "Stable distinct UUIDs and trimmed Unicode alias");
                Reject(() => db.CreateProfile("ÁGATA O'NEIL"), "Duplicate alias rejected ignoring case");
                Reject(() => db.CreateProfile("A\u0301gata O'Neil"), "Unicode normalization prevents duplicate alias");
                Reject(() => db.CreateProfile("x"), "Short alias rejected");
                Reject(() => db.CreateProfile(new string('x', 41)), "Long alias rejected");
                Reject(() => db.CreateProfile("<b>Paciente</b>"), "Markup and injection characters rejected");
                db.SaveResult(Result("round-pigeons", a, "pigeons"));
                db.SaveResult(Result("round-memory", a, "memory", "abandoned"));
                db.SaveResult(Result("round-puzzle", a, "puzzle", "interrupted"));
                db.SaveResult(Result("round-pigeons", a, "pigeons"));
                db.SaveResult(Result("other-patient", b, "memory"));
                Check(db.CountResults(a) == 3, "Three scenarios persist without duplicating lifecycle callbacks");
                Check(db.CountResults(b) == 1, "Results remain isolated between patients");
                Check(db.ListResults(a).All(r => r.profileId == a && r.metricsJson.Contains("60000")), "Metrics and profile identity round trip");
                Check(db.ListResults(a, 1).Count == 1, "History limit respected");
                Reject(() => db.SaveResult(Result("orphan", "missing-profile", "memory")), "Foreign key rejects orphan result");
                Reject(() => db.SaveResult(Result("bad-scenario", a, "unknown")), "Unknown scenario rejected");
                Reject(() => db.SaveResult(Result("bad-status", a, "memory", "unknown")), "Unknown status rejected");
                var invalid = Result("bad-difficulty", a, "memory"); invalid.difficulty = 4;
                Reject(() => db.SaveResult(invalid), "Invalid difficulty rejected");
                Check(db.CountResults(a) == 3, "Rejected writes leave existing results intact");
            }
            using (var reopened = new LocalProfileDatabase(path))
                Check(reopened.ListProfiles().Count == 2 && reopened.CountResults(a) == 3, "Profiles and results survive reopening");
            // Exercise actual session retry code against an unavailable database destination.
            using (var sessionDb = PatientSession.OpenDatabase()) PatientSession.Select(sessionDb.CreateProfile("Perfil de recuperación"));
            string sessionPath = PatientSession.DatabasePath;
            File.Move(sessionPath, sessionPath + ".backup"); Directory.CreateDirectory(sessionPath);
            Check(!PatientSession.Save(Result("retry-round", PatientSession.ActiveProfile.id, "memory")), "Storage failure is reported");
            string pending = Path.Combine(UnityEngine.Application.persistentDataPath, "NeuroFlex", "pending-results.json");
            Check(File.Exists(pending), "Failed result has a durable recovery file");
            Directory.Delete(sessionPath); File.Move(sessionPath + ".backup", sessionPath);
            typeof(PatientSession).GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            Check(PatientSession.ActiveProfile == null, "New session requires explicit patient selection");
            Check(PatientSession.RetryPending(), "Pending result recovers after a session restart");
            using (var sessionDb = PatientSession.OpenDatabase())
                Check(sessionDb.CountResults(sessionDb.ListProfiles()[0].id) == 1, "Recovery persists exactly one result");
            Check(!File.Exists(pending), "Recovery queue removed only after saving");
            Console.WriteLine(checks + " checks passed");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
