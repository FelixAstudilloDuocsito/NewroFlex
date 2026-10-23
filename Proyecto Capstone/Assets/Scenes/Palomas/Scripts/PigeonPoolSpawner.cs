using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeuroFlexVR
{
    [Serializable]
    public class PigeonDifficultyPreset
    {
        [Min(1)] public int level = 1;
        [Min(1)] public int pigeonCount = 4;
        public PigeonMovementLevel movementLevel = PigeonMovementLevel.Low;
        [Min(0.1f)] public float displaySeconds = 10f;
        [Min(0)] public int latePigeons = 0;
        [Min(0f)] public float lateArrivalSecond = 2f;
    }

    /// <summary>
    /// Creates and reuses pigeons OUTSIDE active trials. During a trial the
    /// PigeonRoundController activates/deactivates the pre-created objects.
    /// No per-frame allocations, Instantiate or Destroy during observation.
    /// </summary>
    public enum PigeonEntrySide { Left, Right, BothSides, Front }

    public class PigeonPoolSpawner : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Prefab root with PigeonAgent + animated child model.")]
        public PigeonAgent pigeonPrefab;
        public PigeonRoundController roundController;
        [Tooltip("Center of the flat rectangular spawn area, near the visible ground.")]
        public Transform spawnCenter;
        [Tooltip("Optional parent for generated pigeons. It MUST remain active.")]
        public Transform poolRoot;

        [Header("Spawn area (meters)")]
        [Min(1f)] public float width = 7f;
        [Min(1f)] public float depth = 5f;
        [Min(0f)] public float edgeMargin = 0.8f;
        [Min(0.1f)] public float minimumSeparation = 0.8f;
        [Range(5, 250)] public int attemptsPerPigeon = 100;

        [Header("Entrada desde fuera del area")]
        public PigeonEntrySide entrySide = PigeonEntrySide.BothSides;
        [Min(0.5f)] public float outsideDistance = 2.5f;
        [Min(0.1f)] public float approachSpeed = 4.0f;
        [Min(0.1f)] public float approachAnimationMultiplier = 1.8f;
        [Min(0f)] public float arrivalStaggerSeconds = 0.15f;
        [Min(0.1f)] public float minimumLateVisibleSeconds = 1.5f;

        [Header("Optional collision / uneven terrain")]
        public bool snapToGround = false;
        [Tooltip("Assign only terrain/ground layers if Snap To Ground is enabled.")]
        public LayerMask groundLayers;
        public bool avoidObstacles = false;
        [Tooltip("Assign only props/obstacles layers, NOT the terrain.")]
        public LayerMask obstacleLayers;
        [Min(0.05f)] public float obstacleCheckRadius = 0.35f;

        [Header("Difficulty presets")]
        [Min(1)] public int initialDifficulty = 1;
        public PigeonDifficultyPreset[] presets =
        {
            new PigeonDifficultyPreset { level = 1, pigeonCount = 4, movementLevel = PigeonMovementLevel.Low, displaySeconds = 10f, latePigeons = 0, lateArrivalSecond = 2f },
            new PigeonDifficultyPreset { level = 2, pigeonCount = 6, movementLevel = PigeonMovementLevel.Medium, displaySeconds = 11f, latePigeons = 1, lateArrivalSecond = 2f },
            new PigeonDifficultyPreset { level = 3, pigeonCount = 8, movementLevel = PigeonMovementLevel.High, displaySeconds = 12f, latePigeons = 2, lateArrivalSecond = 2f }
        };

        private readonly List<PigeonAgent> pool = new List<PigeonAgent>();
        private readonly List<Vector3> positions = new List<Vector3>();
        private readonly List<Vector3> entryPositions = new List<Vector3>();
        private int selectedDifficulty;
        private int selectedCount;
        private bool initialized;
        private int selectedLatePigeons;
        private float selectedLateArrivalSecond;
        public int EarlyCount => selectedCount - selectedLatePigeons;
        public int LateCount => selectedLatePigeons;
        public float LateArrivalSecond => selectedLateArrivalSecond;
        public float StaggerSeconds => arrivalStaggerSeconds;
        public float ApproachSpeed => approachSpeed;
        public float ApproachAnimationMultiplier => approachAnimationMultiplier;
        public int SelectedCount => selectedCount;

        private void Start()
        {
            // The controller's Awake can run before ours; Start happens after all Awake calls.
            initialized = true;
            SelectDifficulty(initialDifficulty);
        }

        /// <summary>Connect to a Button's OnClick and set a fixed integer 1, 2 or 3.</summary>
        public void SelectDifficulty(int level)
        {
            if (!initialized)
            {
                Debug.LogWarning("[NeuroFlex] La dificultad se configura al iniciar la escena.");
                return;
            }
            if (roundController == null || pigeonPrefab == null || spawnCenter == null)
            {
                Debug.LogError("[NeuroFlex] Asigna Round Controller, Pigeon Prefab y Spawn Center.");
                return;
            }
            if (roundController.IsRoundActive)
            {
                Debug.LogWarning("[NeuroFlex] No se puede cambiar la dificultad durante una ronda.");
                return;
            }

            PigeonDifficultyPreset preset = null;
            foreach (PigeonDifficultyPreset p in presets)
            {
                if (p != null && p.level == level) { preset = p; break; }
            }
            if (preset == null || preset.pigeonCount < 1 ||
                preset.latePigeons < 0 || preset.latePigeons >= preset.pigeonCount)
            {
                Debug.LogError("[NeuroFlex] Nivel no encontrado o cantidad invalida: " + level);
                return;
            }

            // Expand once, on difficulty selection, never during the timed exercise.
            while (pool.Count < preset.pigeonCount)
            {
                PigeonAgent instance = Instantiate(
                    pigeonPrefab, spawnCenter.position, spawnCenter.rotation,
                    poolRoot != null ? poolRoot : transform);
                instance.name = "Pigeon_" + (pool.Count + 1).ToString("00");
                instance.gameObject.SetActive(false);
                pool.Add(instance);
            }
            foreach (PigeonAgent agent in pool)
                if (agent != null) agent.gameObject.SetActive(false);

            roundController.pigeons.Clear();
            roundController.pigeons.AddRange(pool);
            roundController.pigeonCount = preset.pigeonCount;
            roundController.difficultyLevel = preset.level;
            roundController.movementLevel = preset.movementLevel;
            roundController.displaySeconds = preset.displaySeconds;
            roundController.poolSpawner = this;
            selectedDifficulty = preset.level;
            selectedCount = preset.pigeonCount;
            selectedLatePigeons = preset.latePigeons;
            selectedLateArrivalSecond = preset.lateArrivalSecond;

            Debug.Log($"[NeuroFlex] Dificultad {selectedDifficulty}: " +
                      $"{selectedCount} palomas en memoria, inactivas. Presiona Iniciar.");
        }

        /// <summary>Called by the Round Controller before showing pigeons.</summary>
        public bool PrepareRound()
        {
            if (roundController == null || selectedCount <= 0 || pool.Count < selectedCount)
            {
                Debug.LogError("[NeuroFlex] Primero elige una dificultad valida.");
                return false;
            }
            if (roundController.pigeonCount != selectedCount)
            {
                Debug.LogError("[NeuroFlex] La cantidad del controlador difiere del pool. " +
                               "Cambia la dificultad desde PigeonPoolSpawner.");
                return false;
            }

            // Compute all locations first. No partial relocation on failure.
            positions.Clear();
            for (int i = 0; i < selectedCount; i++)
            {
                bool found = false;
                for (int attempt = 0; attempt < attemptsPerPigeon; attempt++)
                {
                    float halfW = width * 0.5f - edgeMargin;
                    float halfD = depth * 0.5f - edgeMargin;
                    if (halfW <= 0 || halfD <= 0)
                    {
                        Debug.LogError("[NeuroFlex] El margen supera el tamaño del area.");
                        return false;
                    }
                    float x = UnityEngine.Random.Range(-halfW, halfW);
                    float z = UnityEngine.Random.Range(-halfD, halfD);
                    Vector3 candidate = spawnCenter.position + spawnCenter.right * x + spawnCenter.forward * z;
                    if (snapToGround)
                    {
                        RaycastHit hit;
                        if (!Physics.Raycast(candidate + Vector3.up * 12f,
                                             Vector3.down, out hit, 30f,
                                             groundLayers, QueryTriggerInteraction.Ignore))
                            continue;
                        candidate.y = hit.point.y;
                    }
                    if (avoidObstacles && Physics.CheckSphere(candidate + Vector3.up * 0.2f,
                        obstacleCheckRadius, obstacleLayers, QueryTriggerInteraction.Ignore))
                        continue;

                    bool tooClose = false;
                    foreach (Vector3 chosen in positions)
                    {
                        Vector2 a = new Vector2(candidate.x, candidate.z);
                        Vector2 b = new Vector2(chosen.x, chosen.z);
                        if ((a - b).sqrMagnitude < minimumSeparation * minimumSeparation)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose) continue;
                    positions.Add(candidate);
                    found = true;
                    break;
                }
                if (!found)
                {
                    Debug.LogError("[NeuroFlex] Area insuficiente para " + selectedCount +
                                   " palomas: aumenta Width/Depth o baja Minimum Separation.");
                    return false;
                }
            }

            // Plan de entrada desde los bordes, antes de activar ninguna paloma.
            entryPositions.Clear();
            for (int i = 0; i < selectedCount; i++)
            {
                Vector3 from = BuildEntryPoint(i);
                if (snapToGround)
                {
                    RaycastHit hit;
                    if (!Physics.Raycast(from + Vector3.up * 12f, Vector3.down, out hit, 30f,
                        groundLayers, QueryTriggerInteraction.Ignore))
                    {
                        Debug.LogError("[NeuroFlex] No hay suelo bajo una entrada de paloma. Ajusta Outside Distance.");
                        return false;
                    }
                    from.y = hit.point.y;
                }
                entryPositions.Add(from);
            }

            if (selectedLatePigeons > 0)
            {
                // Exigir que cada llegada tardia se complete antes de ocultar la escena.
                // Se reserva un minimo de observacion tras la ultima llegada.
                for (int i = EarlyCount; i < selectedCount; i++)
                {
                    float departAt = selectedLateArrivalSecond + (i - EarlyCount) * arrivalStaggerSeconds;
                    float travel = Vector3.Distance(entryPositions[i], positions[i]) /
                                   Mathf.Max(0.1f, approachSpeed);
                    if (departAt + travel + minimumLateVisibleSeconds >= roundController.displaySeconds)
                    {
                        Debug.LogError($"[NeuroFlex] Paloma tardia {i + 1} no tiene tiempo suficiente. " +
                                       "Aumenta Display Seconds o Approach Speed, reduce Outside Distance " +
                                       "o adelanta Late Arrival Second.");
                        return false;
                    }
                }
            }

            for (int i = 0; i < pool.Count; i++)
            {
                PigeonAgent pigeon = pool[i];
                pigeon.gameObject.SetActive(false);
                if (i >= selectedCount) continue;
                pigeon.transform.SetPositionAndRotation(entryPositions[i],
                    Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
            }
            return true;
        }


        public bool ReleasePigeon(int index, PigeonMovementLevel movement)
        {
            if (index < 0 || index >= selectedCount || index >= positions.Count ||
                index >= entryPositions.Count || pool[index] == null) return false;
            PigeonAgent pigeon = pool[index];
            pigeon.transform.position = entryPositions[index];
            pigeon.gameObject.SetActive(true);
            pigeon.BeginApproach(positions[index], approachSpeed, approachAnimationMultiplier, movement);
            return true;
        }

        public bool IsPigeonArrived(int index)
        {
            return index >= 0 && index < pool.Count && pool[index] != null &&
                   pool[index].gameObject.activeSelf && pool[index].HasArrived;
        }

        public bool AllArrived(int startInclusive, int endExclusive)
        {
            for (int i = startInclusive; i < endExclusive; i++)
                if (!IsPigeonArrived(i)) return false;
            return true;
        }

        private Vector3 BuildEntryPoint(int index)
        {
            Vector3 target = positions[index];
            float sideSign = (index % 2 == 0) ? -1f : 1f;
            float halfW = width * 0.5f;
            float halfD = depth * 0.5f;
            Vector3 local = Quaternion.Inverse(spawnCenter.rotation) * (target - spawnCenter.position);
            // Lados exteriores pero manteniendo el otro eje cerca del destino.
            switch (entrySide)
            {
                case PigeonEntrySide.Left:
                    local.x = -halfW - outsideDistance;
                    break;
                case PigeonEntrySide.Right:
                    local.x = halfW + outsideDistance;
                    break;
                case PigeonEntrySide.Front:
                    local.z = halfD + outsideDistance;
                    break;
                default:
                    local.x = sideSign * (halfW + outsideDistance);
                    break;
            }
            return spawnCenter.position + spawnCenter.rotation * local;
        }

        private void OnDrawGizmosSelected()
        {
            if (spawnCenter == null) return;
            Gizmos.color = new Color(0.1f, 0.8f, 0.4f, 0.9f);
            Matrix4x4 matrix = Matrix4x4.TRS(spawnCenter.position,
                Quaternion.Euler(0f, spawnCenter.eulerAngles.y, 0f), Vector3.one);
            Gizmos.matrix = matrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(width, 0.03f, depth));
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(Vector3.zero,
                new Vector3(Mathf.Max(0.02f, width - edgeMargin * 2f),
                            0.03f, Mathf.Max(0.02f, depth - edgeMargin * 2f)));
            Gizmos.color = new Color(0.95f, 0.3f, 0.2f, 0.9f);
            float edge = width * 0.5f + outsideDistance;
            Gizmos.DrawSphere(new Vector3(-edge, 0f, 0f), 0.18f);
            Gizmos.DrawSphere(new Vector3(edge, 0f, 0f), 0.18f);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
