using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeuroFlexVR
{
    /// <summary>Descriptive calculations from entrenamiento_modelo_ia/scenes/contracts.py (scenes-v1).</summary>
    public sealed class SavedRoundMetrics
    {
        public JObject Round { get; private set; }
        public JObject Computed { get; private set; }
        public string ContractVersion { get; private set; }
        private PatientResult record;

        public static SavedRoundMetrics Read(PatientResult record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            JObject root;
            using (var reader = new JsonTextReader(new StringReader(record.metricsJson ?? ""))
                { DateParseHandling = DateParseHandling.None, MaxDepth = 32 })
            {
                root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new FormatException("Hay contenido después de las métricas JSON.");
            }
            var request = root["request"] as JObject ?? root;
            string version = (string)request["contractVersion"];
            if (version != null && version != "scenes-v1") throw new FormatException("Versión de métricas no compatible.");
            JObject round = request;
            if (request["rounds"] != null)
            {
                var rounds = request["rounds"] as JArray;
                if (rounds == null || rounds.Count != 1 || !(rounds[0] is JObject))
                    throw new FormatException("Un registro local debe contener una sola ronda.");
                round = (JObject)rounds[0];
            }
            foreach (var pair in new[] { new[] { "roundId", record.id }, new[] { "participantId", record.profileId }, new[] { "scenarioId", record.scenarioId } })
                if (round[pair[0]] != null && (string)round[pair[0]] != pair[1])
                    throw new FormatException("Las métricas no corresponden a esta ronda o perfil.");
            var result = new SavedRoundMetrics { record = record, Round = round, ContractVersion = version };
            result.Compute();
            return result;
        }

        private int? Number(string key)
        {
            JToken token = Round[key];
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token.Type != JTokenType.Integer) throw new FormatException("El campo " + key + " no es un entero.");
            long value = token.Value<long>();
            if (value < 0 || value > int.MaxValue) throw new FormatException("El campo " + key + " está fuera de rango.");
            return (int)value;
        }

        private double? Seconds(string key) => Number(key) / 1000.0;
        private void Add(string key, object value) { Computed[key] = value == null ? JValue.CreateNull() : JToken.FromObject(value); }
        private void Compute()
        {
            Computed = new JObject();
            bool completed = record.status == "completed";
            if (record.scenarioId == "puzzle")
            {
                int? total = Number("totalPieces"), correct = Number("correctPlacements");
                if (total == 0 || (correct.HasValue && total.HasValue && (correct > total || (completed && correct != total))))
                    throw new FormatException("El total de piezas no coincide con las piezas completadas.");
                Add("resolutionTimeSeconds", completed ? Seconds("activeTimeMs") : null);
                Add("activeTimeSeconds", Seconds("activeTimeMs"));
                Add("incorrectMoves", Number("incorrectPlacements"));
                Add("completedPieces", correct); Add("totalPieces", total); Add("aidsUsed", Number("aidsUsed"));
            }
            else if (record.scenarioId == "memory")
            {
                int? correct = Number("correctPairAttempts"), incorrect = Number("incorrectPairAttempts");
                int? found = Number("pairsFound"), total = Number("totalPairs"), cards = Number("totalCards");
                if (total == 0 || cards < 2 || (cards.HasValue && total.HasValue && cards.Value != 2L * total.Value) ||
                    (found.HasValue && total.HasValue && (found > total || (completed && found != total))) ||
                    (correct.HasValue && found.HasValue && correct != found))
                    throw new FormatException("Los contadores de parejas y cartas no coinciden.");
                double? attempts = correct.HasValue && incorrect.HasValue ? (double?)correct.Value + incorrect.Value : null;
                Add("matchingAccuracyPercent", attempts > 0 ? (double?)Math.Round(100.0 * correct.Value / attempts.Value, 2) : null);
                Add("incorrectAttempts", incorrect);
                Add("resolutionTimeSeconds", completed ? Seconds("activeTimeMs") : null);
                Add("activeTimeSeconds", Seconds("activeTimeMs")); Add("cardsShown", cards);
                Add("pairsFound", found); Add("totalPairs", total); Add("aidsUsed", Number("aidsUsed"));
            }
            else if (record.scenarioId == "pigeons")
            {
                int? actual = Number("actualPigeonCount"), answer = Number("userPigeonCount");
                bool answered = answer.HasValue;
                Add("responseCorrect", answered && actual.HasValue ? (bool?)(answer == actual) : null);
                int? error = answer - actual;
                Add("signedCountError", error); Add("absoluteCountError", error.HasValue ? (int?)Math.Abs(error.Value) : null);
                Add("actualPigeonCount", actual); Add("userPigeonCount", answer);
                Add("enabledResponseActiveSeconds", answered ? Seconds("responseTimeMs") : null);
                DateTimeOffset? hidden = Moment("pigeonsHiddenAt"), submitted = Moment("answerSubmittedAt");
                if (hidden.HasValue && submitted.HasValue && submitted < hidden)
                    throw new FormatException("La confirmación precede a la ocultación.");
                Add("responseTimeSeconds", answered && hidden.HasValue && submitted.HasValue
                    ? (double?)(submitted.Value - hidden.Value).TotalSeconds : null);
            }
            else throw new FormatException("Actividad no reconocida.");
        }

        private DateTimeOffset? Moment(string key)
        {
            string value = Text(key);
            if (value == null) return null;
            if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment))
                throw new FormatException("Fecha no válida en " + key + ".");
            return moment;
        }

        private string Text(string key)
        {
            JToken token = Round[key];
            return token == null || token.Type == JTokenType.Null ? null : token.ToString();
        }

        private static string Display(JToken value, string unit = "")
        {
            if (value == null || value.Type == JTokenType.Null) return "No registrado";
            if (value.Type == JTokenType.Boolean) return value.Value<bool>() ? "Sí" : "No";
            return value.Type == JTokenType.Float || value.Type == JTokenType.Integer
                ? value.Value<double>().ToString("0.###", CultureInfo.InvariantCulture) + unit : value.ToString();
        }

        public string MetricsText()
        {
            var text = new StringBuilder();
            void Line(string label, string key, string unit = "") => text.AppendLine(label + ": " + Display(Computed[key], unit));
            if (record.scenarioId == "puzzle")
            {
                Line("Piezas completadas", "completedPieces"); Line("Total de piezas", "totalPieces");
                Line("Movimientos incorrectos", "incorrectMoves");
            }
            else if (record.scenarioId == "memory")
            {
                Line("Precisión de emparejamiento", "matchingAccuracyPercent", " %");
                Line("Intentos incorrectos", "incorrectAttempts"); Line("Parejas encontradas", "pairsFound");
                Line("Total de parejas", "totalPairs"); Line("Cartas de la ronda", "cardsShown");
            }
            else
            {
                Line("Cantidad real", "actualPigeonCount"); Line("Respuesta confirmada", "userPigeonCount");
                Line("Respuesta correcta", "responseCorrect"); Line("Error con signo", "signedCountError");
                Line("Error absoluto", "absoluteCountError");
                Line("Respuesta activa (desde habilitar)", "enabledResponseActiveSeconds", " s");
                Line("Ocultación a confirmación (reloj)", "responseTimeSeconds", " s");
                if (Computed["userPigeonCount"].Type == JTokenType.Null) text.AppendLine("Sin respuesta confirmada.");
                return text.ToString().TrimEnd();
            }
            Line("Tiempo activo", "activeTimeSeconds", " s");
            text.AppendLine("Tiempo de resolución: " + (record.status == "completed"
                ? Display(Computed["resolutionTimeSeconds"], " s") : "No aplica: ronda sin completar"));
            Line("Ayudas utilizadas", "aidsUsed");
            if (record.scenarioId == "memory" && Computed["matchingAccuracyPercent"].Type == JTokenType.Null)
                text.AppendLine("Precisión no calculable sin intentos registrados.");
            return text.ToString().TrimEnd();
        }

        public string ContextText()
        {
            var text = new StringBuilder();
            void Line(string label, string key, string unit = "") => text.AppendLine(label + ": " + Display(Round[key], unit));
            Line("Versión del escenario", "scenarioVersion"); Line("Modo", "assessmentMode");
            Line("Pausas", "pausedDurationMs", " ms"); Line("Distracciones configuradas", "distractionsEnabled");
            Line("Tipo de distracción", "distractionType"); Line("Intensidad", "distractionIntensity");
            Line("Eventos de distracción", "distractionCount"); Line("Duración de distracciones", "distractionDurationMs", " ms");
            if (record.scenarioId == "pigeons")
            {
                Line("Exposición configurada", "displayDurationMs", " ms"); Line("Retención", "retentionDelayMs", " ms");
                Line("Movimiento", "movementLevel");
            }
            else if (record.scenarioId == "memory")
            {
                Line("Vista previa configurada", "previewDurationMs", " ms"); Line("Mazo", "deckId");
            }
            else { Line("Puzzle", "puzzleId"); Line("Rotación requerida", "rotationEnabled"); }
            Line("Grupo de comparación", "comparisonGroupId");
            text.AppendLine("Cálculos: scenes-v1");
            if (ContractVersion == null) text.AppendLine("El registro local no incluye todo el contexto del contrato.");
            return text.ToString().TrimEnd();
        }
    }
}
