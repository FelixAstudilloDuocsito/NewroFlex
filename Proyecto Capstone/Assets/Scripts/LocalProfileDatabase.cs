using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace NeuroFlexVR
{
    [Serializable]
    public sealed class PatientProfile
    {
        public string id;
        public string alias;
        public int age;
        public string createdAt;
    }

    [Serializable]
    public sealed class PatientResult
    {
        public string id;
        public string profileId;
        public string sessionId;
        public string scenarioId;
        public int difficulty;
        public string status;
        public string startedAt;
        public string endedAt;
        public string metricsJson;
    }

    /// <summary>SQLite transactions shared by the Editor and desktop/Android players. No server required.</summary>
    public sealed class LocalProfileDatabase : IDisposable
    {
        public const int SchemaVersion = 2;
        // Kept here so the same schema is available in an Android APK without reading StreamingAssets.
        private static readonly string[] Schema =
        {
            "CREATE TABLE IF NOT EXISTS patient_profiles (profile_id TEXT PRIMARY KEY, alias TEXT NOT NULL, alias_key TEXT NOT NULL UNIQUE, created_at TEXT NOT NULL)",
            "CREATE TABLE IF NOT EXISTS activity_results (result_id TEXT PRIMARY KEY, profile_id TEXT NOT NULL REFERENCES patient_profiles(profile_id), session_id TEXT NOT NULL, scenario_id TEXT NOT NULL CHECK(scenario_id IN ('pigeons','memory','puzzle')), difficulty INTEGER NOT NULL CHECK(difficulty BETWEEN 1 AND 3), status TEXT NOT NULL CHECK(status IN ('completed','abandoned','interrupted')), started_at TEXT NOT NULL, ended_at TEXT NOT NULL, metrics_json TEXT NOT NULL)",
            "CREATE INDEX IF NOT EXISTS idx_activity_profile ON activity_results(profile_id, ended_at)"
        };

        private readonly ISqlite database;

        public LocalProfileDatabase(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
#if UNITY_ANDROID && !UNITY_EDITOR
            database = new AndroidSqlite(path);
#else
            database = new NativeSqlite(path);
#endif
            try
            {
                database.Execute("PRAGMA foreign_keys = ON");
                database.Execute("PRAGMA busy_timeout = 5000");
                int version = int.Parse(database.Query("PRAGMA user_version")[0][0], CultureInfo.InvariantCulture);
                if (version < 0 || version > SchemaVersion)
                    throw new InvalidOperationException("Versión de base de datos no compatible: " + version);
                database.Execute("BEGIN IMMEDIATE");
                try
                {
                    foreach (string sql in Schema) database.Execute(sql);
                    if (version < 2)
                        database.Execute("ALTER TABLE patient_profiles ADD COLUMN age INTEGER NOT NULL DEFAULT 0 CHECK(age BETWEEN 0 AND 120)");
                    database.Execute("PRAGMA user_version = 2");
                    database.Execute("COMMIT");
                }
                catch { database.Execute("ROLLBACK"); throw; }
            }
            catch { database.Dispose(); throw; }
        }

        public static string ValidateAlias(string value)
        {
            value = (value ?? "").Trim().Normalize(NormalizationForm.FormC);
            if (value.Length < 2 || value.Length > 40)
                throw new ArgumentException("Escribe un nombre o alias de 2 a 40 caracteres.");
            foreach (char c in value)
                if (!char.IsLetterOrDigit(c) && c != ' ' && c != '-' && c != '_' && c != '\'')
                    throw new ArgumentException("Usa letras, números, espacios, guiones o apóstrofes.");
            return value;
        }

        public PatientProfile CreateProfile(string alias, int age = 0)
        {
            alias = ValidateAlias(alias);
            if (age < 0 || age > 120) throw new ArgumentException("La edad debe estar entre 0 y 120 años.");
            var profile = new PatientProfile { id = "p-" + Guid.NewGuid().ToString("N"), alias = alias, createdAt = UtcNow() };
            string key = alias.ToUpperInvariant();
            profile.age = age;
            database.Execute("BEGIN IMMEDIATE");
            try
            {
                if (database.Query("SELECT profile_id FROM patient_profiles WHERE alias_key = ?", key).Count > 0)
                    throw new ArgumentException("Ya existe un perfil con ese nombre o alias.");
                database.Execute("INSERT INTO patient_profiles (profile_id,alias,alias_key,created_at,age) VALUES (?,?,?,?,?)", profile.id, alias, key, profile.createdAt, age.ToString(CultureInfo.InvariantCulture));
                database.Execute("COMMIT");
            }
            catch { database.Execute("ROLLBACK"); throw; }
            return profile;
        }

        public List<PatientProfile> ListProfiles()
        {
            var profiles = new List<PatientProfile>();
            foreach (string[] row in database.Query("SELECT profile_id, alias, created_at, age FROM patient_profiles ORDER BY alias_key, profile_id"))
                profiles.Add(new PatientProfile { id = row[0], alias = row[1], createdAt = row[2], age = int.Parse(row[3], CultureInfo.InvariantCulture) });
            return profiles;
        }

        public void SaveResult(PatientResult result)
        {
            if (result == null || string.IsNullOrEmpty(result.id) || string.IsNullOrEmpty(result.profileId) ||
                string.IsNullOrEmpty(result.sessionId) || string.IsNullOrEmpty(result.startedAt) ||
                string.IsNullOrEmpty(result.endedAt) || string.IsNullOrEmpty(result.metricsJson))
                throw new ArgumentException("El resultado debe incluir perfil, sesión, fechas y métricas.");
            // A repeated lifecycle callback must neither duplicate nor overwrite a saved round.
            database.Execute("INSERT INTO activity_results VALUES (?,?,?,?,?,?,?,?,?) ON CONFLICT(result_id) DO NOTHING",
                result.id, result.profileId, result.sessionId, result.scenarioId,
                result.difficulty.ToString(CultureInfo.InvariantCulture), result.status,
                result.startedAt, result.endedAt, result.metricsJson);
        }

        public List<PatientResult> ListResults(string profileId, int limit = 100)
        {
            var results = new List<PatientResult>();
            foreach (string[] r in database.Query("SELECT result_id, profile_id, session_id, scenario_id, difficulty, status, started_at, ended_at, metrics_json FROM activity_results WHERE profile_id = ? ORDER BY ended_at DESC, result_id DESC LIMIT ?",
                profileId, Math.Max(1, Math.Min(limit, 1000)).ToString(CultureInfo.InvariantCulture)))
                results.Add(new PatientResult { id = r[0], profileId = r[1], sessionId = r[2], scenarioId = r[3],
                    difficulty = int.Parse(r[4], CultureInfo.InvariantCulture), status = r[5], startedAt = r[6], endedAt = r[7], metricsJson = r[8] });
            return results;
        }

        public List<PatientResult> ListResultsPage(string profileId, int offset, int limit = 4,
            string scenarioId = "", int difficulty = 0, string status = "")
        {
            var arguments = new List<string>();
            string filter = ResultFilter(profileId, scenarioId, difficulty, status, arguments);
            arguments.Add(Math.Max(1, Math.Min(limit, 100)).ToString(CultureInfo.InvariantCulture));
            arguments.Add(Math.Max(0, offset).ToString(CultureInfo.InvariantCulture));
            var results = new List<PatientResult>();
            foreach (string[] r in database.Query("SELECT result_id, profile_id, session_id, scenario_id, difficulty, status, started_at, ended_at, metrics_json " +
                "FROM activity_results WHERE " + filter + " ORDER BY ended_at DESC, result_id DESC LIMIT ? OFFSET ?", arguments.ToArray()))
                results.Add(new PatientResult { id = r[0], profileId = r[1], sessionId = r[2], scenarioId = r[3],
                    difficulty = int.Parse(r[4], CultureInfo.InvariantCulture), status = r[5], startedAt = r[6], endedAt = r[7], metricsJson = r[8] });
            return results;
        }

        public int CountResults(string profileId, string scenarioId = "", int difficulty = 0, string status = "")
        {
            var arguments = new List<string>();
            string filter = ResultFilter(profileId, scenarioId, difficulty, status, arguments);
            return int.Parse(database.Query("SELECT COUNT(*) FROM activity_results WHERE " + filter, arguments.ToArray())[0][0], CultureInfo.InvariantCulture);
        }

        private static string ResultFilter(string profileId, string scenarioId, int difficulty, string status, List<string> arguments)
        {
            string filter = "profile_id = ?";
            arguments.Add(profileId);
            if (!string.IsNullOrEmpty(scenarioId)) { filter += " AND scenario_id = ?"; arguments.Add(scenarioId); }
            if (difficulty > 0) { filter += " AND difficulty = ?"; arguments.Add(difficulty.ToString(CultureInfo.InvariantCulture)); }
            if (!string.IsNullOrEmpty(status)) { filter += " AND status = ?"; arguments.Add(status); }
            return filter;
        }

        public static string UtcNow() => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        public void Dispose() => database.Dispose();

        private interface ISqlite : IDisposable
        {
            void Execute(string sql, params string[] arguments);
            List<string[]> Query(string sql, params string[] arguments);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // Android's public SQLite API avoids relying on a private system libsqlite3.so.
        private sealed class AndroidSqlite : ISqlite
        {
            private readonly AndroidJavaObject connection;
            public AndroidSqlite(string path)
            {
                using (var type = new AndroidJavaClass("android.database.sqlite.SQLiteDatabase"))
                    connection = type.CallStatic<AndroidJavaObject>("openOrCreateDatabase", path, null);
            }
            public void Execute(string sql, params string[] arguments)
            {
                if (sql.StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase)) { Query(sql, arguments); return; }
                // Java's Object[] bindings are obtained from a String[] using array covariance.
                using (var args = new AndroidJavaObject("java.util.ArrayList"))
                {
                    foreach (string argument in arguments) args.Call<bool>("add", argument);
                    using (var bindings = args.Call<AndroidJavaObject>("toArray"))
                        connection.Call("execSQL", sql, bindings);
                }
            }
            public List<string[]> Query(string sql, params string[] arguments)
            {
                var rows = new List<string[]>();
                using (var cursor = connection.Call<AndroidJavaObject>("rawQuery", sql, arguments))
                {
                    try
                    {
                        int columns = cursor.Call<int>("getColumnCount");
                        while (cursor.Call<bool>("moveToNext"))
                        {
                            var row = new string[columns];
                            for (int i = 0; i < columns; i++) row[i] = cursor.Call<string>("getString", i);
                            rows.Add(row);
                        }
                    }
                    finally { cursor.Call("close"); }
                }
                return rows;
            }
            public void Dispose() { connection.Call("close"); connection.Dispose(); }
        }
#else
        private sealed class NativeSqlite : ISqlite
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            private const string Library = "winsqlite3";
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            private const string Library = "libsqlite3.dylib";
#else
            private const string Library = "libsqlite3.so.0";
#endif
            private IntPtr handle;
            private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value + "\0");
            public NativeSqlite(string path)
            {
                int code = sqlite3_open_v2(Utf8(path), out handle, 2 | 4, IntPtr.Zero);
                if (code != 0)
                {
                    string error = Error();
                    Dispose();
                    throw new IOException(error);
                }
            }
            private string Error() => ReadUtf8(sqlite3_errmsg(handle));
            private static string ReadUtf8(IntPtr pointer)
            {
                if (pointer == IntPtr.Zero) return null;
                int size = 0;
                while (Marshal.ReadByte(pointer, size) != 0) size++;
                var bytes = new byte[size];
                Marshal.Copy(pointer, bytes, 0, size);
                return Encoding.UTF8.GetString(bytes);
            }
            public void Execute(string sql, params string[] arguments) { Query(sql, arguments); }
            public List<string[]> Query(string sql, params string[] arguments)
            {
                IntPtr statement = IntPtr.Zero;
                try
                {
                    if (sqlite3_prepare_v2(handle, Utf8(sql), -1, out statement, IntPtr.Zero) != 0)
                        throw new IOException(Error());
                    for (int i = 0; i < arguments.Length; i++)
                    {
                        byte[] bytes = Utf8(arguments[i]);
                        if (sqlite3_bind_text(statement, i + 1, bytes, bytes.Length - 1, new IntPtr(-1)) != 0)
                            throw new IOException(Error());
                    }
                    var rows = new List<string[]>();
                    int code;
                    while ((code = sqlite3_step(statement)) == 100)
                    {
                        var row = new string[sqlite3_column_count(statement)];
                        for (int i = 0; i < row.Length; i++) row[i] = ReadUtf8(sqlite3_column_text(statement, i));
                        rows.Add(row);
                    }
                    if (code != 101) throw new IOException(Error());
                    return rows;
                }
                finally { if (statement != IntPtr.Zero) sqlite3_finalize(statement); }
            }
            public void Dispose() { if (handle != IntPtr.Zero) { sqlite3_close(handle); handle = IntPtr.Zero; } }
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_open_v2(byte[] path, out IntPtr db, int flags, IntPtr vfs);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_close(IntPtr db);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_errmsg(IntPtr db);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int size, out IntPtr statement, IntPtr tail);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_bind_text(IntPtr statement, int index, byte[] text, int size, IntPtr destructor);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_step(IntPtr statement);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_column_count(IntPtr statement);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_column_text(IntPtr statement, int index);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_finalize(IntPtr statement);
        }
#endif
    }

    public static class PatientSession
    {
        public static PatientProfile ActiveProfile { get; private set; }
        public static string LastSaveMessage { get; private set; } = "";
        public static event Action SaveStateChanged;
        private static readonly List<PatientResult> Pending = new List<PatientResult>();
        public static bool HasPendingResults => Pending.Count > 0;
        private static bool pendingLoaded;
        [Serializable] private sealed class PendingFile { public List<PatientResult> results = new List<PatientResult>(); }
        private static string PendingPath => Path.Combine(Application.persistentDataPath, "NeuroFlex", "pending-results.json");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            ActiveProfile = null;
            LastSaveMessage = "";
            Pending.Clear();
            pendingLoaded = false;
            SaveStateChanged = null;
        }

        public static string DatabasePath
        {
            get
            {
#if UNITY_EDITOR || UNITY_STANDALONE
                for (var folder = new DirectoryInfo(Application.dataPath); folder != null; folder = folder.Parent)
                    if (Directory.Exists(Path.Combine(folder.FullName, "datos_locales")))
                        return Path.Combine(folder.FullName, "datos_locales", "neuroflex.sqlite3");
#endif
                return Path.Combine(Application.persistentDataPath, "NeuroFlex", "neuroflex.sqlite3");
            }
        }

        public static LocalProfileDatabase OpenDatabase() => new LocalProfileDatabase(DatabasePath);
        public static void Select(PatientProfile profile) { ActiveProfile = profile; }

        public static bool IsDifficultyUnlocked(string scenario, int difficulty)
        {
            if (difficulty == 1) return true;
            if (difficulty < 1 || difficulty > 3 || ActiveProfile == null) return false;
            try
            {
                using (var db = OpenDatabase())
                    for (int level = 1; level < difficulty; level++)
                        if (db.CountResults(ActiveProfile.id, scenario, level, "completed") == 0 &&
                            !Pending.Exists(r => r.profileId == ActiveProfile.id && r.scenarioId == scenario && r.difficulty == level && r.status == "completed"))
                            return false;
                return true;
            }
            catch (Exception ex) { Debug.LogWarning("[NeuroFlex] No se pudo leer el progreso: " + ex.Message); return false; }
        }

        public static bool Save(PatientResult result)
        {
            try { LoadPending(); }
            catch (Exception ex) { Debug.LogError("[NeuroFlex] No se pudo leer el respaldo pendiente: " + ex.Message); }
            if (string.IsNullOrEmpty(result.profileId))
            {
                LastSaveMessage = "Sin perfil: el resultado no se ha guardado.";
                SaveStateChanged?.Invoke();
                return false;
            }
            Pending.Add(result);
            try { if (pendingLoaded) PersistPending(); }
            catch (Exception ex) { Debug.LogWarning("[NeuroFlex] No se pudo escribir el respaldo pendiente: " + ex.Message); }
            return RetryPending();
        }

        private static void LoadPending()
        {
            if (pendingLoaded) return;
            // Do not replace an unreadable recovery file with a fresh empty list.
            if (File.Exists(PendingPath))
            {
                var file = JsonUtility.FromJson<PendingFile>(File.ReadAllText(PendingPath));
                if (file == null || file.results == null) throw new IOException("Archivo de recuperación inválido.");
                Pending.AddRange(file.results);
            }
            pendingLoaded = true;
        }

        private static void PersistPending()
        {
            if (Pending.Count == 0)
            {
                if (File.Exists(PendingPath)) File.Delete(PendingPath);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(PendingPath));
            string temporary = PendingPath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(new PendingFile { results = Pending }));
            if (File.Exists(PendingPath)) File.Replace(temporary, PendingPath, null);
            else File.Move(temporary, PendingPath);
        }

        public static bool RetryPending()
        {
            try
            {
                LoadPending();
                using (var db = OpenDatabase())
                    while (Pending.Count > 0) { db.SaveResult(Pending[0]); Pending.RemoveAt(0); }
                PersistPending();
                LastSaveMessage = "Resultados guardados en el perfil.";
                SaveStateChanged?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                LastSaveMessage = "No se pudo guardar en la base. Reintenta el guardado.";
                Debug.LogError("[NeuroFlex] " + LastSaveMessage + " " + exception.Message);
                SaveStateChanged?.Invoke();
                return false;
            }
        }
    }
}
