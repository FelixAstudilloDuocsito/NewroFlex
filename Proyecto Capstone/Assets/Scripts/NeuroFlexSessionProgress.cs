using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeuroFlexVR
{
    public static class NeuroFlexSessionProgress
    {
        private static readonly HashSet<string> CompletedRounds = new HashSet<string>();
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            CompletedRounds.Clear();
            Changed = null;
        }

        public static bool IsDifficultyUnlocked(string scenarioId, int difficulty)
        {
            if (difficulty <= 1) return true;
            if (difficulty > 3) return false;
            return CompletedRounds.Contains(Key(scenarioId, difficulty - 1));
        }

        public static bool IsSceneComplete(string scenarioId)
        {
            return CompletedRounds.Contains(Key(scenarioId, 3));
        }

        public static bool IsSceneAvailable(string scenarioId) => !IsSceneComplete(scenarioId);

        public static bool AreAllScenesComplete()
        {
            return IsSceneComplete("pigeons") && IsSceneComplete("memory") && IsSceneComplete("puzzle");
        }

        public static void Register(PatientResult result)
        {
            if (result == null || result.status != "completed") return;
            CompletedRounds.Add(Key(result.scenarioId, result.difficulty));
            Changed?.Invoke();
        }

        private static string Key(string scenarioId, int difficulty) => scenarioId + ":" + difficulty;
    }
}
