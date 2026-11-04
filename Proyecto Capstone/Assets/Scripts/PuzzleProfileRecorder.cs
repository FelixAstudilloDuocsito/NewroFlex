using System;
using UnityEngine;

namespace NeuroFlexVR
{
    /// <summary>Records results without changing the puzzle's movement or progression.</summary>
    public sealed class PuzzleProfileRecorder : MonoBehaviour
    {
        public int difficulty = 1;
        private SwapPuzzleManager manager;
        private string profileId, roundId, startedAt;
        private bool roundActive, applicationPaused;
        private float activeSeconds;
        private int incorrectPlacements;

        [Serializable]
        private sealed class Metrics
        {
            public int totalPieces, correctPlacements, incorrectPlacements, activeTimeMs, aidsUsed;
            public string puzzleId;
            public bool rotationEnabled;
        }

        public void BeginRound(SwapPuzzleManager owner)
        {
            FinishRecord("abandoned");
            manager = owner;
            profileId = PatientSession.ActiveProfile?.id;
            roundId = "r-" + Guid.NewGuid().ToString("N");
            startedAt = LocalProfileDatabase.UtcNow();
            activeSeconds = 0;
            incorrectPlacements = 0;
            roundActive = true;
        }

        public void RecordPlacement(bool correct)
        {
            if (roundActive && !correct) incorrectPlacements++;
        }

        public void CompleteRound() { FinishRecord("completed"); }

        private void Update()
        {
            if (roundActive && !applicationPaused && Time.timeScale > 0) activeSeconds += Time.unscaledDeltaTime;
        }

        private void OnApplicationPause(bool paused) { applicationPaused = paused; }
        private void OnDisable() { FinishRecord("abandoned"); }
        private void OnApplicationQuit() { FinishRecord("interrupted"); }

        private void FinishRecord(string status)
        {
            if (!roundActive) return;
            roundActive = false;
            int correct = 0;
            foreach (var piece in manager.pieces)
                if (piece != null && piece.currentSlotIndex == piece.pieceID) correct++;
            PatientSession.Save(new PatientResult
            {
                id = roundId, profileId = profileId, sessionId = NeuroFlexSession.SessionId,
                scenarioId = "puzzle", difficulty = Mathf.Clamp(difficulty, 1, 3), status = status,
                startedAt = startedAt, endedAt = LocalProfileDatabase.UtcNow(),
                metricsJson = JsonUtility.ToJson(new Metrics
                {
                    totalPieces = manager.pieces.Count, correctPlacements = correct,
                    incorrectPlacements = incorrectPlacements, activeTimeMs = Mathf.RoundToInt(activeSeconds * 1000),
                    aidsUsed = 0, puzzleId = "library-level-" + difficulty, rotationEnabled = false
                })
            });
        }
    }
}
