using System;
using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace NeuroFlexVR
{
    public enum PigeonDistractorType { None, Visual, Audio, Mixed }
    public enum PigeonDistractorIntensity { None, Low, Medium, High }
    public enum PigeonAssessmentMode { Training, Comparison }

    /// <summary>
    /// MVP of pigeon-counting assessment. Attach to a permanently active scene object.
    /// Public methods can be wired directly to World Space Canvas Buttons.
    /// Stores a scenes-v1-compatible, single-round request in persistentDataPath.
    /// </summary>
    public class PigeonRoundController : MonoBehaviour
    {
        [Header("Identity and protocol")]
        [Tooltip("Pseudonymous participant ID. Do not put real names here.")]
        public string participantId = "p-demo";
        public string scenarioVersion = "1.1.0";
        public PigeonAssessmentMode assessmentMode = PigeonAssessmentMode.Training;
        [Tooltip("Only for rounds truly intended to be compared under identical conditions. Blank = JSON null.")]
        public string comparisonGroupId = "";
        [Min(0)] public int difficultyLevel = 1;

        [Header("Pigeons in the scene")]
        [Tooltip("Optional. Automatically generates/reuses pigeons before each round.")]
        public PigeonPoolSpawner poolSpawner;
        public List<PigeonAgent> pigeons = new List<PigeonAgent>();
        [Min(0)] public int pigeonCount = 5;
        public PigeonMovementLevel movementLevel = PigeonMovementLevel.Low;
        [Min(0.1f)] public float displaySeconds = 8f;
        [Min(0f)] public float retentionDelaySeconds = 0f;
        [Min(0)] public int maximumAnswer = 30;

        [Header("Comida: lanzar antes de que entren las palomas")]
        [Tooltip("Opcional. Visible al terminar StartRound para lanzar la comida.")]
        public GameObject foodPanel;
        [Tooltip("Opcional: mesh de comida que se mueve desde Food Throw Origin a Food Target.")]
        public GameObject flyingFood;
        public Transform foodThrowOrigin;
        public Transform foodTarget;
        [Tooltip("Opcional: comida visible en el suelo tras el lanzamiento.")]
        public GameObject foodOnGround;
        [Min(0.1f)] public float foodFlightSeconds = 0.65f;
        [Min(0f)] public float foodArcHeight = 0.6f;

        [Header("Optional distractor (one episode during observation)")]
        public bool distractionsEnabled;
        public PigeonDistractorType distractorType = PigeonDistractorType.Visual;
        public PigeonDistractorIntensity distractorIntensity = PigeonDistractorIntensity.Low;
        [Min(0f)] public float distractorStartsAtSecond = 3f;
        [Min(0f)] public float distractorDurationSeconds = 2f;
        [Tooltip("For example, a walking person or animated dog GameObject.")]
        public GameObject visualDistractor;
        public AudioSource audioSource;
        public AudioClip distractorAudio;

        [Header("World Space UI: inicio, comida, observacion, respuesta, resultado")]
        [Tooltip("Panel de inicio. Su Canvas padre debe ser exclusivo de este menu: tambien se desactiva al iniciar la ronda.")]
        public GameObject startPanel;
        public GameObject observingPanel;
        public GameObject answerPanel;
        public GameObject resultPanel;
        [Tooltip("Optional text outside of the panels, always visible.")]
        public TMP_Text statusText;
        public TMP_Text answerNumberText;
        public TMP_Text resultText;

        private enum Phase { Ready, AwaitFood, FoodFlight, Arrival, Observe, Retain, Answer, Finished }
        private Phase phase = Phase.Ready;
        private Canvas startCanvas;
        private bool isPaused;
        private float observationElapsed;
        private float arrivalElapsed;
        private float foodFlightElapsed;
        private int nextEarlyPigeon;
        private int nextLatePigeon;
        private int earlyCount;
        private int lateCount;
        private float latePigeonSecond;
        private int launchedPigeonCount;
        private readonly List<string> releaseTimeline = new List<string>();
        private DateTime? observationStartedUtc;
        private DateTime? foodLandedUtc;
        private float retentionElapsed;
        private float activeDistractionSeconds;
        private bool distractionStarted;
        private bool distractorCurrentlyActive;
        private int actualDistractionEvents;
        private int answer;
        private DateTime? hiddenUtc;
        private DateTime? enabledUtc;
        private DateTime? submittedUtc;
        private DateTime startedUtc;
        private Stopwatch responseActiveWatch;
        private readonly Stopwatch pausedWatch = new Stopwatch();
        private string currentRoundId;
        private int roundPigeonCount;
        private int roundDifficultyLevel;
        private int configuredDisplayMs;
        private int configuredRetentionMs;
        private PigeonMovementLevel roundMovement;
        private PigeonAssessmentMode roundAssessment;
        private string roundGroup;
        private string roundParticipant;
        private string roundScenarioVersion;
        private bool roundDistractionsEnabled;
        private PigeonDistractorType roundDistractorType;
        private PigeonDistractorIntensity roundDistractorIntensity;

        private void Awake()
        {
            if (startPanel != null)
                startCanvas = startPanel.GetComponentInParent<Canvas>(true);

            // Existing pigeons are managed by this controller, not by scene activation.
            foreach (PigeonAgent pigeon in pigeons)
                if (pigeon != null) pigeon.gameObject.SetActive(false);
            if (visualDistractor != null) visualDistractor.SetActive(false);
            if (flyingFood != null) flyingFood.SetActive(false);
            if (foodOnGround != null) foodOnGround.SetActive(false);
            UpdatePanels(Phase.Ready);
        }

        private void Update()
        {
            if (isPaused) return;
            float dt = Time.deltaTime;

            switch (phase)
            {
                case Phase.FoodFlight:
                    UpdateFoodFlight(dt);
                    break;
                case Phase.Arrival:
                    UpdateArrival(dt);
                    break;
                case Phase.Observe:
                    observationElapsed += dt;
                    UpdateLateArrivals();
                    UpdateDistractor(dt);
                    if (observationElapsed >= displaySeconds)
                    {
                        if (launchedPigeonCount == roundPigeonCount &&
                            (poolSpawner == null || poolSpawner.AllArrived(0, roundPigeonCount)))
                            HideAndRetain();
                        else
                        {
                            // Nunca contar una paloma que no alcanzo a mostrarse.
                            Debug.LogError("[NeuroFlex] Una paloma no llego antes de ocultar. Ronda interrumpida.");
                            FinishRound("interrupted");
                        }
                    }
                    break;
                case Phase.Retain:
                    retentionElapsed += dt;
                    if (retentionElapsed >= retentionDelaySeconds) EnableAnswer();
                    break;
            }
        }

        /// <summary>Prevent changing difficulty / allocating objects mid-round.</summary>
        public bool IsRoundActive => IsRunning();

        // Call from the "Iniciar" button.
        public void StartRound()
        {
            if (IsRunning())
            {
                Debug.LogWarning("[NeuroFlex] La ronda ya está activa.");
                return;
            }

            if (string.IsNullOrWhiteSpace(participantId) || string.IsNullOrWhiteSpace(scenarioVersion))
            {
                Debug.LogError("[NeuroFlex] Configura participantId y scenarioVersion.");
                return;
            }

            // The pool already exists: only reposition inactive pigeons here.
            if (poolSpawner != null && !poolSpawner.PrepareRound())
            {
                Debug.LogError("[NeuroFlex] No fue posible preparar las posiciones de las palomas.");
                return;
            }

            if (poolSpawner == null)
            {
                Debug.LogError("[NeuroFlex] Esta version de lanzamiento necesita PigeonPoolSpawner.");
                return;
            }
            if (assessmentMode == PigeonAssessmentMode.Comparison)
            {
                Debug.LogError("[NeuroFlex] La llegada aleatoria no es una prueba estandarizada. " +
                               "Usa Assessment Mode = Training por ahora.");
                return;
            }
            if (pigeonCount > maximumAnswer)
            {
                Debug.LogError("[NeuroFlex] Maximum Answer debe ser >= cantidad de palomas.");
                return;
            }
            if (pigeonCount > pigeons.Count || pigeons.Exists(p => p == null))
            {
                Debug.LogError("[NeuroFlex] Faltan referencias de palomas en la lista Pigeons.");
                return;
            }
            if (distractionsEnabled && (distractorType == PigeonDistractorType.None ||
                                        distractorIntensity == PigeonDistractorIntensity.None))
            {
                Debug.LogError("[NeuroFlex] Distracciones activadas requieren tipo e intensidad distintos de None.");
                return;
            }
            if (distractionsEnabled && !HasConfiguredDistractor())
            {
                Debug.LogError("[NeuroFlex] Asigna el objeto visual o AudioSource + AudioClip del distractor.");
                return;
            }

            roundPigeonCount = pigeonCount;
            roundDifficultyLevel = difficultyLevel;
            roundMovement = movementLevel;
            roundAssessment = assessmentMode;
            roundGroup = string.IsNullOrWhiteSpace(comparisonGroupId) ? null : comparisonGroupId.Trim();
            roundParticipant = participantId.Trim();
            roundScenarioVersion = scenarioVersion.Trim();
            roundDistractionsEnabled = distractionsEnabled;
            roundDistractorType = distractorType;
            roundDistractorIntensity = distractorIntensity;
            configuredDisplayMs = Mathf.RoundToInt(displaySeconds * 1000f);
            configuredRetentionMs = Mathf.RoundToInt(retentionDelaySeconds * 1000f);
            earlyCount = poolSpawner.EarlyCount;
            lateCount = poolSpawner.LateCount;
            latePigeonSecond = poolSpawner.LateArrivalSecond;
            nextEarlyPigeon = 0;
            nextLatePigeon = earlyCount;
            launchedPigeonCount = 0;
            releaseTimeline.Clear();
            foodFlightElapsed = 0f;
            arrivalElapsed = 0f;
            observationStartedUtc = null;
            foodLandedUtc = null;

            currentRoundId = "r-" + Guid.NewGuid().ToString("N");
            startedUtc = DateTime.UtcNow;
            hiddenUtc = null;
            enabledUtc = null;
            submittedUtc = null;
            responseActiveWatch = null;
            pausedWatch.Reset();
            isPaused = false;
            observationElapsed = 0f;
            retentionElapsed = 0f;
            activeDistractionSeconds = 0f;
            distractionStarted = false;
            distractorCurrentlyActive = false;
            actualDistractionEvents = 0;
            answer = 0;

            StopDistractor();
            if (flyingFood != null) flyingFood.SetActive(false);
            if (foodOnGround != null) foodOnGround.SetActive(false);
            foreach (PigeonAgent pigeon in pigeons)
                if (pigeon != null) pigeon.gameObject.SetActive(false);

            phase = Phase.AwaitFood;
            UpdatePanels(phase);
            UpdateAnswerText();
            SetStatus("Lanza un poco de comida para atraer a las palomas.");
            Debug.Log("[NeuroFlex] Prueba lista. Usa ThrowFood() o lanza comida al trigger XR.");
        }

        public bool IsAwaitingFood => phase == Phase.AwaitFood && !isPaused;

        // Boton XR "Lanzar comida". La simulacion visual es opcional.
        public void ThrowFood()
        {
            if (!IsAwaitingFood) return;
            if (flyingFood != null && foodThrowOrigin != null && foodTarget != null)
            {
                phase = Phase.FoodFlight;
                foodFlightElapsed = 0f;
                flyingFood.transform.position = foodThrowOrigin.position;
                flyingFood.SetActive(true);
                UpdatePanels(phase);
                SetStatus("Lanzando comida...");
            }
            else FoodLanded();
        }

        // Usar con el Trigger de la zona de comida si se lanza un objeto fisico XR.
        public void FoodLanded()
        {
            if (phase != Phase.AwaitFood && phase != Phase.FoodFlight) return;
            if (isPaused) return;
            if (flyingFood != null) flyingFood.SetActive(false);
            if (foodOnGround != null) foodOnGround.SetActive(true);
            foodLandedUtc = DateTime.UtcNow;
            arrivalElapsed = 0f;
            nextEarlyPigeon = 0;
            phase = Phase.Arrival;
            UpdatePanels(phase);
            SetStatus("¡Mira! Las palomas se acercan a comer.");
            Debug.Log($"[NeuroFlex] Comida lanzada. Primer grupo: {earlyCount}. Tardias: {lateCount}.");
            UpdateArrival(0f); // Primera paloma inmediatamente.
        }

        private void UpdateFoodFlight(float dt)
        {
            foodFlightElapsed += dt;
            float t = Mathf.Clamp01(foodFlightElapsed / foodFlightSeconds);
            Vector3 from = foodThrowOrigin.position;
            Vector3 to = foodTarget.position;
            flyingFood.transform.position = Vector3.Lerp(from, to, t) +
                                            Vector3.up * (4f * foodArcHeight * t * (1f - t));
            if (t >= 1f) FoodLanded();
        }

        private void UpdateArrival(float dt)
        {
            arrivalElapsed += dt;
            while (nextEarlyPigeon < earlyCount &&
                arrivalElapsed >= nextEarlyPigeon * poolSpawner.StaggerSeconds)
            {
                if (!ReleasePigeon(nextEarlyPigeon)) { FinishRound("interrupted"); return; }
                nextEarlyPigeon++;
            }
            if (nextEarlyPigeon >= earlyCount && poolSpawner.AllArrived(0, earlyCount))
            {
                phase = Phase.Observe;
                observationStartedUtc = DateTime.UtcNow;
                observationElapsed = 0f;
                UpdatePanels(phase);
                SetStatus("Cuenta todas las palomas que aparezcan, incluso las que lleguen después.");
                Debug.Log("[NeuroFlex] Observacion iniciada, segundos: " + displaySeconds);
            }
        }

        private void UpdateLateArrivals()
        {
            while (nextLatePigeon < roundPigeonCount &&
                observationElapsed >= latePigeonSecond +
                                     (nextLatePigeon - earlyCount) * poolSpawner.StaggerSeconds)
            {
                if (!ReleasePigeon(nextLatePigeon)) { FinishRound("interrupted"); return; }
                nextLatePigeon++;
            }
        }

        private bool ReleasePigeon(int index)
        {
            if (!poolSpawner.ReleasePigeon(index, roundMovement))
            {
                Debug.LogError("[NeuroFlex] No se pudo activar la paloma " + index);
                return false;
            }
            launchedPigeonCount++;
            releaseTimeline.Add(PigeonTelemetry.TimestampUtc(DateTime.UtcNow));
            return true;
        }

        private void HideAndRetain()
        {
            StopDistractor();
            foreach (PigeonAgent pigeon in pigeons)
                if (pigeon != null) pigeon.gameObject.SetActive(false);
            hiddenUtc = DateTime.UtcNow;
            phase = Phase.Retain;
            retentionElapsed = 0f;
            UpdatePanels(phase);
            SetStatus("Recuerda cuántas palomas viste.");
            if (retentionDelaySeconds <= 0f) EnableAnswer();
        }

        private void EnableAnswer()
        {
            enabledUtc = DateTime.UtcNow;
            responseActiveWatch = Stopwatch.StartNew();
            phase = Phase.Answer;
            UpdatePanels(phase);
            UpdateAnswerText();
            SetStatus("¿Cuántas palomas viste? Confirma tu respuesta.");
        }

        // Wire these to the + / - buttons.
        public void IncreaseAnswer()
        {
            if (phase != Phase.Answer || isPaused) return;
            answer = Mathf.Min(Mathf.Max(0, maximumAnswer), answer + 1);
            UpdateAnswerText();
        }

        public void DecreaseAnswer()
        {
            if (phase != Phase.Answer || isPaused) return;
            answer = Mathf.Max(0, answer - 1);
            UpdateAnswerText();
        }

        // Wire this to the "Confirmar" button.
        public void ConfirmAnswer()
        {
            if (phase != Phase.Answer || isPaused) return;
            submittedUtc = DateTime.UtcNow;
            if (responseActiveWatch != null) responseActiveWatch.Stop();
            FinishRound("completed");
        }

        public void AbandonRound()
        {
            if (!IsRunning()) return;
            FinishRound("abandoned");
        }

        public void InterruptRound()
        {
            if (!IsRunning()) return;
            FinishRound("interrupted");
        }

        public void PauseRound()
        {
            if (!IsRunning() || isPaused) return;
            isPaused = true;
            pausedWatch.Start();
            if (responseActiveWatch != null && responseActiveWatch.IsRunning) responseActiveWatch.Stop();
            foreach (PigeonAgent p in pigeons) if (p != null && p.gameObject.activeSelf) p.SetPaused(true);
            if (visualDistractor != null && distractorCurrentlyActive) visualDistractor.SetActive(false);
            if (audioSource != null && distractorCurrentlyActive) audioSource.Pause();
            SetStatus("Pausa. Usa Reanudar para continuar.");
        }

        public void ResumeRound()
        {
            if (!IsRunning() || !isPaused) return;
            pausedWatch.Stop();
            isPaused = false;
            if (phase == Phase.Answer && responseActiveWatch != null) responseActiveWatch.Start();
            foreach (PigeonAgent p in pigeons) if (p != null && p.gameObject.activeSelf) p.SetPaused(false);
            if (visualDistractor != null && distractorCurrentlyActive && IncludesVisual()) visualDistractor.SetActive(true);
            if (audioSource != null && distractorCurrentlyActive && IncludesAudio()) audioSource.UnPause();
            SetStatus(phase == Phase.Answer ? "Confirma tu respuesta." : "Ronda reanudada.");
        }

        private void FinishRound(string terminalStatus)
        {
            if (!IsRunning()) return;
            if (pausedWatch.IsRunning) pausedWatch.Stop();
            if (responseActiveWatch != null && responseActiveWatch.IsRunning) responseActiveWatch.Stop();
            StopDistractor();
            if (flyingFood != null) flyingFood.SetActive(false);
            if (foodOnGround != null) foodOnGround.SetActive(false);
            foreach (PigeonAgent p in pigeons) if (p != null) p.gameObject.SetActive(false);
            DateTime endedUtc = DateTime.UtcNow;

            int wallMs = Math.Max(0, (int)(endedUtc - startedUtc).TotalMilliseconds);
            PigeonRoundRecord record = new PigeonRoundRecord
            {
                participantId = roundParticipant,
                sessionId = NeuroFlexSession.SessionId,
                roundId = currentRoundId,
                difficultyLevel = roundDifficultyLevel,
                startedAt = PigeonTelemetry.TimestampUtc(startedUtc),
                endedAt = PigeonTelemetry.TimestampUtc(endedUtc),
                status = terminalStatus,
                pausedDurationMs = Math.Min(wallMs, (int)pausedWatch.ElapsedMilliseconds),
                scenarioVersion = roundScenarioVersion,
                assessmentMode = roundAssessment == PigeonAssessmentMode.Comparison ? "comparison" : "training",
                comparisonGroupId = roundGroup,
                distractionsEnabled = roundDistractionsEnabled,
                distractionType = !roundDistractionsEnabled ? "none" : roundDistractorType.ToString().ToLowerInvariant(),
                distractionIntensity = !roundDistractionsEnabled ? "none" : roundDistractorIntensity.ToString().ToLowerInvariant(),
                distractionCount = actualDistractionEvents,
                distractionDurationMs = Math.Min(wallMs, Mathf.RoundToInt(activeDistractionSeconds * 1000f)),
                // Con respuesta confirmada, todas las palomas debieron llegar.
                // En una interrupcion temprana es el numero efectivamente liberado.
                actualPigeonCount = terminalStatus == "completed" ? roundPigeonCount : launchedPigeonCount,
                userPigeonCount = submittedUtc.HasValue ? (int?)answer : null,
                displayDurationMs = configuredDisplayMs,
                responseTimeMs = submittedUtc.HasValue && responseActiveWatch != null ?
                    (int?)(int)Math.Min((long)int.MaxValue, responseActiveWatch.ElapsedMilliseconds) : null,
                pigeonsHiddenAt = hiddenUtc.HasValue ? PigeonTelemetry.TimestampUtc(hiddenUtc.Value) : null,
                answerEnabledAt = enabledUtc.HasValue ? PigeonTelemetry.TimestampUtc(enabledUtc.Value) : null,
                answerSubmittedAt = submittedUtc.HasValue ? PigeonTelemetry.TimestampUtc(submittedUtc.Value) : null,
                movementLevel = roundMovement.ToString().ToLowerInvariant(),
                retentionDelayMs = hiddenUtc.HasValue && enabledUtc.HasValue ?
                    Math.Max(0, (int)(enabledUtc.Value - hiddenUtc.Value).TotalMilliseconds) : configuredRetentionMs
            };

            phase = Phase.Finished;
            isPaused = false;
            UpdatePanels(phase);
            string path;
            try
            {
                path = PigeonTelemetry.Save(record);
                Debug.Log("[NeuroFlex] Ronda guardada en: " + path);
            }
            catch (Exception e)
            {
                path = "NO GUARDADO: " + e.Message;
                Debug.LogError("[NeuroFlex] No se pudo guardar la ronda: " + e);
            }

            if (resultText != null)
            {
                if (terminalStatus == "completed")
                {
                    int signedError = answer - roundPigeonCount;
                    double timeSinceHidden = (submittedUtc.Value - hiddenUtc.Value).TotalSeconds;
                    resultText.text = "Había " + roundPigeonCount + " palomas.\n" +
                                      "Tu respuesta: " + answer + "\n" +
                                      "Error absoluto: " + Math.Abs(signedError) + "\n" +
                                      "Tiempo desde ocultación: " + timeSinceHidden.ToString("F1") + " s";
                }
                else resultText.text = "Ronda " + terminalStatus + ". No se registró respuesta.";
            }
            SaveArrivalSidecar(terminalStatus);
            SetStatus("Ronda finalizada. JSON local: " + path);
        }


        [Serializable]
        private class ArrivalMetadata
        {
            public string roundId;
            public string scenarioId = "pigeons";
            public string metadataVersion = "pigeon-arrival-v1";
            public int plannedPigeons;
            public int initiallyArriving;
            public int lateArriving;
            public int pigeonsReleased;
            public float lateArrivalSecond;
            public float arrivalSpeed;
            public string foodLandedAt;
            public string observationStartedAt;
            public string[] pigeonsReleasedAt;
            public string finalStatus;
        }

        private void SaveArrivalSidecar(string terminalStatus)
        {
            if (poolSpawner == null) return;
            try
            {
                ArrivalMetadata extra = new ArrivalMetadata
                {
                    roundId = currentRoundId,
                    plannedPigeons = roundPigeonCount,
                    initiallyArriving = earlyCount,
                    lateArriving = lateCount,
                    pigeonsReleased = launchedPigeonCount,
                    lateArrivalSecond = latePigeonSecond,
                    arrivalSpeed = poolSpawner.ApproachSpeed,
                    foodLandedAt = foodLandedUtc.HasValue ? PigeonTelemetry.TimestampUtc(foodLandedUtc.Value) : null,
                    observationStartedAt = observationStartedUtc.HasValue ? PigeonTelemetry.TimestampUtc(observationStartedUtc.Value) : null,
                    pigeonsReleasedAt = releaseTimeline.ToArray(),
                    finalStatus = terminalStatus
                };
                string folder = System.IO.Path.Combine(Application.persistentDataPath, "NeuroFlexTelemetry", "arrival-metadata");
                System.IO.Directory.CreateDirectory(folder);
                string metadataPath = System.IO.Path.Combine(folder, currentRoundId + ".json");
                System.IO.File.WriteAllText(metadataPath, JsonUtility.ToJson(extra, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NeuroFlex] No se pudo guardar metadata de llegada: " + ex.Message);
            }
        }

        private bool IsRunning()
        {
            return phase == Phase.AwaitFood || phase == Phase.FoodFlight || phase == Phase.Arrival ||
                   phase == Phase.Observe || phase == Phase.Retain || phase == Phase.Answer;
        }

        private bool IncludesVisual()
        {
            return roundDistractorType == PigeonDistractorType.Visual ||
                   roundDistractorType == PigeonDistractorType.Mixed;
        }

        private bool IncludesAudio()
        {
            return roundDistractorType == PigeonDistractorType.Audio ||
                   roundDistractorType == PigeonDistractorType.Mixed;
        }

        private bool HasConfiguredDistractor()
        {
            bool visual = distractorType == PigeonDistractorType.Visual || distractorType == PigeonDistractorType.Mixed;
            bool audio = distractorType == PigeonDistractorType.Audio || distractorType == PigeonDistractorType.Mixed;
            bool visualReady = visualDistractor != null;
            bool audioReady = audioSource != null && distractorAudio != null;
            return (!visual || visualReady) && (!audio || audioReady);
        }

        private void UpdateDistractor(float dt)
        {
            if (!roundDistractionsEnabled || distractorDurationSeconds <= 0f) return;
            float start = Mathf.Min(distractorStartsAtSecond, displaySeconds);
            float end = Mathf.Min(start + distractorDurationSeconds, displaySeconds);
            if (end <= start) return;

            if (!distractionStarted && observationElapsed >= start && observationElapsed < end)
            {
                distractionStarted = true;
                StartDistractor();
            }

            if (!distractorCurrentlyActive) return;
            // Audio-only: the event ends if the clip finishes before the configured limit.
            if (!IncludesVisual() && IncludesAudio() && audioSource != null && !audioSource.isPlaying)
            {
                StopDistractor();
                return;
            }
            float remaining = Mathf.Max(0f, end - (observationElapsed - dt));
            activeDistractionSeconds += Mathf.Min(dt, remaining);
            if (observationElapsed >= end) StopDistractor();
        }

        private void StartDistractor()
        {
            bool fired = false;
            if (IncludesVisual() && visualDistractor != null)
            {
                visualDistractor.SetActive(true);
                fired = true;
            }
            if (IncludesAudio() && audioSource != null && distractorAudio != null)
            {
                audioSource.clip = distractorAudio;
                audioSource.Play();
                fired = true;
            }
            distractorCurrentlyActive = fired;
            if (fired) actualDistractionEvents++;
        }

        private void StopDistractor()
        {
            if (visualDistractor != null) visualDistractor.SetActive(false);
            if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
            distractorCurrentlyActive = false;
        }

        private void UpdatePanels(Phase p)
        {
            bool showStart = p == Phase.Ready || p == Phase.Finished ||
                (p == Phase.AwaitFood && foodPanel == null);
            if (startPanel != null) startPanel.SetActive(showStart);
            // Disable the entire start menu, including blur images outside startPanel.
            if (startCanvas != null) startCanvas.gameObject.SetActive(showStart);
            if (foodPanel != null) foodPanel.SetActive(p == Phase.AwaitFood);
            if (observingPanel != null) observingPanel.SetActive(p == Phase.FoodFlight ||
                p == Phase.Arrival || p == Phase.Observe || p == Phase.Retain);
            if (answerPanel != null) answerPanel.SetActive(p == Phase.Answer);
            if (resultPanel != null) resultPanel.SetActive(p == Phase.Finished);
        }

        private void UpdateAnswerText()
        {
            if (answerNumberText != null) answerNumberText.text = answer.ToString();
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) PauseRound();
        }

        private void OnDestroy()
        {
            if (IsRunning()) FinishRound("interrupted");
        }
    }
}
