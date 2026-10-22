using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace NeuroFlexVR
{
    // One pseudonymous VR session throughout this application launch.
    // Replace with your existing global session manager if you have one.
    public static class NeuroFlexSession
    {
        public static readonly string SessionId = "s-" + Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// One flat 'pigeons' round, exactly as defined by scenes-v1.
    /// Null is written explicitly for missing optional values.
    /// </summary>
    public class PigeonRoundRecord
    {
        public string participantId;
        public string sessionId;
        public string roundId;
        public string scenarioId = "pigeons";
        public int difficultyLevel;
        public string startedAt;
        public string endedAt;
        public string status;
        public int pausedDurationMs;
        public string scenarioVersion;
        public string assessmentMode;
        public string comparisonGroupId;

        public bool distractionsEnabled;
        public string distractionType;
        public int distractionCount;
        public string distractionIntensity;
        public int distractionDurationMs;

        public int actualPigeonCount;
        public int? userPigeonCount;
        public int displayDurationMs;
        public int? responseTimeMs;
        public string pigeonsHiddenAt;
        public string answerEnabledAt;
        public string answerSubmittedAt;
        public string movementLevel;
        public int retentionDelayMs;
    }

    public static class PigeonTelemetry
    {
        public static string TimestampUtc(DateTime utc)
        {
            return utc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        }

        public static string ToRequestJson(PigeonRoundRecord r)
        {
            // JsonUtility does not reliably serialize Nullable<int> as JSON null;
            // write the explicit, fixed contract keys ourselves without dependencies.
            StringBuilder sb = new StringBuilder(1200);
            sb.Append("{\"contractVersion\":\"scenes-v1\",\"rounds\":[{");
            bool first = true;
            Add(sb, ref first, "participantId", r.participantId);
            Add(sb, ref first, "sessionId", r.sessionId);
            Add(sb, ref first, "roundId", r.roundId);
            Add(sb, ref first, "scenarioId", r.scenarioId);
            Add(sb, ref first, "difficultyLevel", r.difficultyLevel);
            Add(sb, ref first, "startedAt", r.startedAt);
            Add(sb, ref first, "endedAt", r.endedAt);
            Add(sb, ref first, "status", r.status);
            Add(sb, ref first, "pausedDurationMs", r.pausedDurationMs);
            Add(sb, ref first, "scenarioVersion", r.scenarioVersion);
            Add(sb, ref first, "assessmentMode", r.assessmentMode);
            Add(sb, ref first, "comparisonGroupId", r.comparisonGroupId);
            Add(sb, ref first, "distractionsEnabled", r.distractionsEnabled);
            Add(sb, ref first, "distractionType", r.distractionType);
            Add(sb, ref first, "distractionCount", r.distractionCount);
            Add(sb, ref first, "distractionIntensity", r.distractionIntensity);
            Add(sb, ref first, "distractionDurationMs", r.distractionDurationMs);
            Add(sb, ref first, "actualPigeonCount", r.actualPigeonCount);
            Add(sb, ref first, "userPigeonCount", r.userPigeonCount);
            Add(sb, ref first, "displayDurationMs", r.displayDurationMs);
            Add(sb, ref first, "responseTimeMs", r.responseTimeMs);
            Add(sb, ref first, "pigeonsHiddenAt", r.pigeonsHiddenAt);
            Add(sb, ref first, "answerEnabledAt", r.answerEnabledAt);
            Add(sb, ref first, "answerSubmittedAt", r.answerSubmittedAt);
            Add(sb, ref first, "movementLevel", r.movementLevel);
            Add(sb, ref first, "retentionDelayMs", r.retentionDelayMs);
            sb.Append("}]}");
            return sb.ToString();
        }

        public static string Save(PigeonRoundRecord r)
        {
            string directory = Path.Combine(Application.persistentDataPath, "NeuroFlexTelemetry");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, r.roundId + ".json");
            File.WriteAllText(path, ToRequestJson(r), new UTF8Encoding(false));
            return path;
        }

        private static void BeginProperty(StringBuilder sb, ref bool first, string name)
        {
            if (!first) sb.Append(',');
            first = false;
            AppendQuoted(sb, name);
            sb.Append(':');
        }

        private static void Add(StringBuilder sb, ref bool first, string key, string value)
        {
            BeginProperty(sb, ref first, key);
            if (value == null) sb.Append("null");
            else AppendQuoted(sb, value);
        }

        private static void Add(StringBuilder sb, ref bool first, string key, int value)
        {
            BeginProperty(sb, ref first, key);
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Add(StringBuilder sb, ref bool first, string key, int? value)
        {
            BeginProperty(sb, ref first, key);
            if (value.HasValue) sb.Append(value.Value.ToString(CultureInfo.InvariantCulture));
            else sb.Append("null");
        }

        private static void Add(StringBuilder sb, ref bool first, string key, bool value)
        {
            BeginProperty(sb, ref first, key);
            sb.Append(value ? "true" : "false");
        }

        private static void AppendQuoted(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
