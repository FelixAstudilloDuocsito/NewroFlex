using System;
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

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(RunTestSession);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(RunTestSession);
        }

        public void RunTestSession()
        {
            if (confirmBeforeRunning)
            {
                SetStatus("Confirma la ejecución de la sesión de prueba en el Inspector.");
                return;
            }

            try
            {
                string sessionId = NeuroFlexSession.SessionId;
                string[] scenarios = { "pigeons", "memory", "puzzle" };
                int uploaded = 0;
                foreach (string scenario in scenarios)
                    for (int difficulty = 1; difficulty <= 3; difficulty++)
                    {
                        PatientSession.Save(CreateResult(sessionId, scenario, difficulty));
                        uploaded++;
                    }

                SetStatus("Sesión de prueba creada: " + uploaded + " rondas.");
                Debug.Log("[NeuroFlex] Sesión de prueba anónima creada con " + uploaded + " rondas.");
            }
            catch (Exception exception)
            {
                SetStatus("No se pudo crear la sesión de prueba.");
                Debug.LogError("[NeuroFlex] Error en sesión de prueba: " + exception);
            }
        }

        private static PatientResult CreateResult(string sessionId, string scenario, int difficulty)
        {
            string now = LocalProfileDatabase.UtcNow();
            return new PatientResult
            {
                id = "test-" + scenario + "-d" + difficulty + "-" + Guid.NewGuid().ToString("N"),
                profileId = null,
                sessionId = sessionId,
                scenarioId = scenario,
                difficulty = difficulty,
                status = "completed",
                startedAt = now,
                endedAt = now,
                metricsJson = Metrics(scenario, difficulty)
            };
        }

        private static string Metrics(string scenario, int difficulty)
        {
            if (scenario == "pigeons")
            {
                return "{\"request\":{\"contractVersion\":\"scenes-v1\",\"rounds\":[{" +
                    "\"participantId\":null,\"sessionId\":\"" + NeuroFlexSession.SessionId +
                    "\",\"roundId\":\"test-pigeons-d" + difficulty +
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
