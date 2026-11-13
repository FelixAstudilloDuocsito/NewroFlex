using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NeuroFlexVR
{
    /// <summary>
    /// Creates a complete anonymous test session through the normal persistence/upload flow.
    /// Attach this component to a Reception button and bind RunTestSession to its OnClick.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class NeuroFlexTestSessionButton : MonoBehaviour
    {
        [SerializeField] private bool confirmBeforeRunning;
        [SerializeField] private TMPro.TMP_Text statusText;

        private Button button;
        private readonly HashSet<string> expectedUploads = new HashSet<string>();
        private readonly List<string> uploadErrors = new List<string>();
        private int successfulUploads;
        private bool awaitingUploads;
        private string testSessionId;
        private string testSessionCode;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(RunTestSession);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(RunTestSession);
            StopWatchingUploads();
        }

        public void RunTestSession()
        {
            if (confirmBeforeRunning)
            {
                SetStatus("Confirma la ejecución de la sesión de prueba en el Inspector.");
                return;
            }

            button.interactable = false;
            try
            {
                testSessionId = "s-test-" + Guid.NewGuid().ToString("N");
                string[] scenarios = { "pigeons", "memory", "puzzle" };
                var results = new List<PatientResult>();
                foreach (string scenario in scenarios)
                    for (int difficulty = 1; difficulty <= 3; difficulty++)
                        results.Add(CreateResult(testSessionId, scenario, difficulty));

                expectedUploads.Clear();
                uploadErrors.Clear();
                successfulUploads = 0;
                testSessionCode = null;
                foreach (PatientResult result in results) expectedUploads.Add(result.id);
                awaitingUploads = true;
                FirebaseTelemetryUploader.UploadCompleted += OnUploadCompleted;
                FirebaseTelemetryUploader.SessionCodeAssigned += OnSessionCodeAssigned;
                FirebaseTelemetryUploader.InitializationFailed += OnInitializationFailed;

                foreach (PatientResult result in results)
                    if (!PatientSession.Save(result))
                        throw new InvalidOperationException("No se pudo guardar localmente la ronda " + result.id + ".");

                Debug.Log("[NeuroFlex] Sesión de prueba creada con " + results.Count + " rondas.");
                if (!FirebaseTelemetryUploader.IsAvailable)
                {
                    FinishWithError("El cargador de Firebase no está activo. Las rondas quedaron guardadas localmente.");
                    return;
                }

                if (!string.IsNullOrEmpty(FirebaseTelemetryUploader.InitializationError))
                {
                    FinishWithError("Firebase no está disponible. Las rondas quedaron guardadas localmente.");
                    return;
                }

                SetStatus("Enviando " + results.Count + " rondas a Firebase...");
            }
            catch (Exception exception)
            {
                StopWatchingUploads();
                button.interactable = true;
                SetStatus("No se pudo crear la sesión de prueba.");
                Debug.LogError("[NeuroFlex] Error en sesión de prueba: " + exception);
            }
        }

        private void OnUploadCompleted(string resultId, bool succeeded, string error)
        {
            if (!awaitingUploads || !expectedUploads.Remove(resultId)) return;
            if (succeeded) successfulUploads++;
            else uploadErrors.Add(error);

            if (expectedUploads.Count > 0)
            {
                SetStatus("Firebase confirmó " + successfulUploads + "/" +
                    (successfulUploads + expectedUploads.Count + uploadErrors.Count) + " rondas...");
                return;
            }

            int total = successfulUploads + uploadErrors.Count;
            if (uploadErrors.Count == 0)
            {
                string sessionLabel = string.IsNullOrEmpty(testSessionCode)
                    ? "sesión de prueba"
                    : testSessionCode;
                SetStatus("Firebase confirmó las " + total + " métricas de " + sessionLabel + ".");
                Debug.Log("[NeuroFlex] Firebase confirmó las " + total +
                    " métricas de " + sessionLabel + ".");
            }
            else
            {
                SetStatus("Firebase guardó " + successfulUploads + "/" + total +
                    " rondas. Revisa la consola para ver el error.");
                Debug.LogError("[NeuroFlex] Firebase no guardó " + uploadErrors.Count +
                    " ronda(s) de prueba. Último error: " + uploadErrors[uploadErrors.Count - 1]);
            }

            StopWatchingUploads();
            button.interactable = true;
        }

        private void OnInitializationFailed(string error)
        {
            if (!awaitingUploads) return;
            FinishWithError("Firebase no está disponible. Las rondas quedaron guardadas localmente.");
            Debug.LogError("[NeuroFlex] No se pudieron subir las métricas de prueba: " + error);
        }

        private void OnSessionCodeAssigned(string sessionId, string sessionCode)
        {
            if (sessionId == testSessionId) testSessionCode = sessionCode;
        }

        private void FinishWithError(string message)
        {
            StopWatchingUploads();
            SetStatus(message);
            if (button != null) button.interactable = true;
        }

        private void StopWatchingUploads()
        {
            FirebaseTelemetryUploader.UploadCompleted -= OnUploadCompleted;
            FirebaseTelemetryUploader.SessionCodeAssigned -= OnSessionCodeAssigned;
            FirebaseTelemetryUploader.InitializationFailed -= OnInitializationFailed;
            awaitingUploads = false;
            expectedUploads.Clear();
        }

        private static PatientResult CreateResult(string sessionId, string scenario, int difficulty)
        {
            string now = LocalProfileDatabase.UtcNow();
            string id = "test-" + scenario + "-d" + difficulty + "-" + Guid.NewGuid().ToString("N");
            return new PatientResult
            {
                id = id,
                profileId = null,
                sessionId = sessionId,
                scenarioId = scenario,
                difficulty = difficulty,
                status = "completed",
                startedAt = now,
                endedAt = now,
                metricsJson = Metrics(sessionId, id, scenario, difficulty)
            };
        }

        private static string Metrics(string sessionId, string roundId, string scenario, int difficulty)
        {
            if (scenario == "pigeons")
            {
                return "{\"request\":{\"contractVersion\":\"scenes-v1\",\"rounds\":[{" +
                    "\"participantId\":null,\"sessionId\":\"" + sessionId +
                    "\",\"roundId\":\"" + roundId +
                    "\",\"scenarioId\":\"pigeons\",\"difficultyLevel\":" + difficulty +
                    ",\"startedAt\":\"" + LocalProfileDatabase.UtcNow() +
                    "\",\"endedAt\":\"" + LocalProfileDatabase.UtcNow() +
                    "\",\"status\":\"completed\",\"pausedDurationMs\":0," +
                    "\"scenarioVersion\":\"test\",\"assessmentMode\":\"training\"," +
                    "\"comparisonGroupId\":null,\"distractionsEnabled\":false," +
                    "\"distractionType\":\"none\",\"distractionCount\":0," +
                    "\"distractionIntensity\":\"none\",\"distractionDurationMs\":0," +
                    "\"actualPigeonCount\":5,\"userPigeonCount\":5,\"displayDurationMs\":5000," +
                    "\"responseTimeMs\":1200,\"pigeonsHiddenAt\":\"" + LocalProfileDatabase.UtcNow() +
                    "\",\"answerEnabledAt\":\"" + LocalProfileDatabase.UtcNow() +
                    "\",\"answerSubmittedAt\":\"" + LocalProfileDatabase.UtcNow() +
                    "\",\"movementLevel\":\"normal\",\"retentionDelayMs\":1000}]}}";
            }

            if (scenario == "memory")
                return "{\"correctPairAttempts\":3,\"incorrectPairAttempts\":1,\"pairsFound\":3," +
                    "\"totalPairs\":3,\"totalCards\":6,\"activeTimeMs\":12000,\"aidsUsed\":0," +
                    "\"previewDurationMs\":2000,\"deckId\":\"test-memory-level-" + difficulty + "\"}";

            return "{\"totalPieces\":6,\"correctPlacements\":6,\"incorrectPlacements\":1," +
                "\"activeTimeMs\":15000,\"aidsUsed\":0,\"puzzleId\":\"test-library-level-" +
                difficulty + "\",\"rotationEnabled\":false}";
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }
    }
}
