using System;
using System.Collections.Generic;
using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace NeuroFlexVR
{
    public enum PigeonDistractorType { None, Visual, Audio, Mixed }
    public enum PigeonDistractorIntensity { None, Low, Medium, High }
    public enum PigeonAssessmentMode { Training, Comparison }

    /// <summary>
    /// MVP of pigeon-counting assessment. Attach to a permanently active scene object.
    /// Public methods can be wired directly to World Space Canvas Buttons.
    /// Saves each terminal round against the patient selected in reception.
    /// </summary>
    public class PigeonRoundController : MonoBehaviour
    {
        [Header("Identificación y protocolo")]
        [Tooltip("Identificador seudónimo del participante. No escribas nombres reales.")]
        [InspectorName("ID del participante")] public string participantId = "p-demo";
        [InspectorName("Versión del escenario")] public string scenarioVersion = "1.2.0";
        [InspectorName("Modo de evaluación")] public PigeonAssessmentMode assessmentMode = PigeonAssessmentMode.Training;
        [Tooltip("Solo para rondas comparables bajo condiciones equivalentes. Vacío = null en JSON.")]
        [InspectorName("Grupo de comparación")] public string comparisonGroupId = "";
        [InspectorName("Nivel de dificultad")] [Min(0)] public int difficultyLevel = 1;

        [Header("Palomas y dificultad")]
        [Tooltip("Opcional. Genera y reutiliza palomas antes de cada ronda.")]
        [InspectorName("Generador de palomas")] public PigeonPoolSpawner poolSpawner;
        [InspectorName("Palomas disponibles")] public List<PigeonAgent> pigeons = new List<PigeonAgent>();
        [InspectorName("Cantidad de palomas")] [Min(0)] public int pigeonCount = 5;
        [InspectorName("Nivel de movimiento")] public PigeonMovementLevel movementLevel = PigeonMovementLevel.Low;
        [InspectorName("Tiempo de observación (s)")] [Min(0.1f)] public float displaySeconds = 8f;
        [InspectorName("Demora para responder (s)")] [Min(0f)] public float retentionDelaySeconds = 0f;
        [InspectorName("Respuesta máxima")] [Min(0)] public int maximumAnswer = 30;

        [Header("Comida y lanzamiento")]
        [Tooltip("Opcional. Visible al terminar StartRound para lanzar la comida.")]
        [InspectorName("Panel de comida")] public GameObject foodPanel;
        [Tooltip("Opcional: mesh de comida que se mueve desde Food Throw Origin a Food Target.")]
        [InspectorName("Comida en vuelo")] public GameObject flyingFood;
        [InspectorName("Origen del lanzamiento")] public Transform foodThrowOrigin;
        [InspectorName("Destino de la comida")] public Transform foodTarget;
        [Tooltip("Opcional: comida visible en el suelo tras el lanzamiento.")]
        [InspectorName("Comida en el suelo")] public GameObject foodOnGround;
        [InspectorName("Duración del lanzamiento (s)")] [Min(0.1f)] public float foodFlightSeconds = 0.65f;
        [InspectorName("Altura del arco")] [Min(0f)] public float foodArcHeight = 0.6f;

        [Header("Distracciones opcionales")]
        [InspectorName("Activar distracciones")] public bool distractionsEnabled;
        [InspectorName("Tipo de distracción")] public PigeonDistractorType distractorType = PigeonDistractorType.Visual;
        [InspectorName("Intensidad de distracción")] public PigeonDistractorIntensity distractorIntensity = PigeonDistractorIntensity.Low;
        [InspectorName("Inicio de distracción (s)")] [Min(0f)] public float distractorStartsAtSecond = 3f;
        [InspectorName("Duración de distracción (s)")] [Min(0f)] public float distractorDurationSeconds = 2f;
        [Tooltip("Por ejemplo, un personaje caminando o un perro animado.")]
        [InspectorName("Objeto distractor visual")] public GameObject visualDistractor;
        [InspectorName("Fuente de audio")] public AudioSource audioSource;
        [InspectorName("Sonido distractor")] public AudioClip distractorAudio;

        [Header("Interfaz VR: paneles y textos")]
        [Tooltip("Panel de inicio. Su Canvas padre debe ser exclusivo de este menu: tambien se desactiva al iniciar la ronda.")]
        [InspectorName("Panel de inicio")] public GameObject startPanel;
        [InspectorName("Panel de observación")] public GameObject observingPanel;
        [InspectorName("Panel de respuesta")] public GameObject answerPanel;
        [InspectorName("Panel de resultados")] public GameObject resultPanel;
        [Tooltip("Texto opcional situado fuera de los paneles. Debe permanecer visible.")]
        [InspectorName("Texto de estado")] public TMP_Text statusText;
        [InspectorName("Texto del contador")] public TMP_Text answerNumberText;
        [InspectorName("Texto de resultado (resumen)")] public TMP_Text resultText;

        [Header("Resultados: textos independientes")]
        [Tooltip("Frase central con cantidad real. Configura la alineación centrada en TextMeshPro.")]
        [InspectorName("Texto de cantidad real")] public TMP_Text actualCountText;
        [Tooltip("Respuesta confirmada por el participante.")]
        [InspectorName("Texto de respuesta")] public TMP_Text userCountText;
        [Tooltip("Cantidad absoluta de palomas de diferencia.")]
        [InspectorName("Texto de diferencia")] public TMP_Text errorCountText;
        [Tooltip("Puntaje porcentual del minijuego.")]
        [InspectorName("Texto de puntaje")] public TMP_Text scoreText;
        [Tooltip("Tiempo de respuesta desde que desaparecen las palomas.")]
        [InspectorName("Texto de tiempo")] public TMP_Text responseTimeText;
        [Tooltip("Palabra inicial de la frase central. Por defecto: Aparecieron.")]
        [InspectorName("Prefijo de cantidad real")] public string actualCountPrefix = "Aparecieron";
        [Tooltip("Color del puntaje normal, editable en el Inspector.")]
        [InspectorName("Color del puntaje")] public Color scoreColor = new Color(0.45f, 0.82f, 0.95f, 1f);
        [Tooltip("Color del puntaje perfecto, editable en el Inspector.")]
        [InspectorName("Color del puntaje perfecto")] public Color perfectScoreColor = new Color(1f, 0.84f, 0.34f, 1f);


        [Header("Resultados: imágenes, títulos y sonidos")]
        [Tooltip("Imagen UI dentro del panel de resultados. Su sprite cambia según el puntaje.")]
        [InspectorName("Imagen de resultado (UI)")] public Image resultImage;
        [Tooltip("Sprite cuando el puntaje está entre 0% y 24.99%.")]
        [InspectorName("Trofeo: nivel bajo")] public Sprite resultImageLow;
        [Tooltip("Sprite cuando el puntaje está entre 25% y 49.99%.")]
        [InspectorName("Trofeo: nivel medio")] public Sprite resultImageMedium;
        [Tooltip("Sprite cuando el puntaje está entre 50% y 74.99%.")]
        [InspectorName("Trofeo: nivel alto")] public Sprite resultImageHigh;
        [Tooltip("Sprite cuando el puntaje está entre 75% y 100%.")]
        [InspectorName("Trofeo: nivel excelente")] public Sprite resultImageExcellent;

        [Tooltip("Texto TMP del título del menu de resultados.")]
        [InspectorName("Texto del título")] public TMP_Text resultTitleText;
        [Tooltip("Título editable para 0% - 24.99%.")]
        [InspectorName("Título: nivel bajo")] public string titleLow = "Sigue intentándolo";
        [Tooltip("Título editable para 25% - 49.99%.")]
        [InspectorName("Título: nivel medio")] public string titleMedium = "Buen intento";
        [Tooltip("Título editable para 50% - 74.99%.")]
        [InspectorName("Título: nivel alto")] public string titleHigh = "Bien hecho";
        [Tooltip("Título editable para 75% - 100%.")]
        [InspectorName("Título: nivel excelente")] public string titleExcellent = "¡Muy bien!";
        [Tooltip("Título cuando no se completa la ronda.")]
        [InspectorName("Título: ronda incompleta")] public string titleIncomplete = "Prueba no completada";

        [Tooltip("AudioSource exclusivo de resultados, en el manager o un objeto siempre activo.")]
        [InspectorName("Fuente de sonido de resultados")] public AudioSource resultAudioSource;
        [Tooltip("Sonido para resultado positivo, excepto puntaje perfecto.")]
        [InspectorName("Sonido de victoria")] public AudioClip victorySound;
        [Tooltip("Sonido especial para resultado exacto: 100%.")]
        [InspectorName("Sonido de puntaje perfecto")] public AudioClip perfectSound;

        private enum Phase { Ready, AwaitFood, FoodFlight, Arrival, Observe, Retain, Answer, Finished }
        private Phase phase = Phase.Ready;
        private PigeonRoundMenu roundMenu;
        public PigeonRoundRecord LastRoundRecord { get; private set; }
        public string LastArrivalMetadata { get; private set; }
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
        private string roundProfileId;
        private string roundScenarioVersion;
        private bool roundDistractionsEnabled;
        private PigeonDistractorType roundDistractorType;
        private PigeonDistractorIntensity roundDistractorIntensity;

        private void Awake()
        {
            ValidateMenuCanvases();

            // Existing pigeons are managed by this controller, not by scene activation.
            foreach (PigeonAgent pigeon in pigeons)
                if (pigeon != null) pigeon.gameObject.SetActive(false);
            if (visualDistractor != null) visualDistractor.SetActive(false);
            if (flyingFood != null) flyingFood.SetActive(false);
            if (foodOnGround != null) foodOnGround.SetActive(false);
            UpdatePanels(Phase.Ready);
            roundMenu = GetComponent<PigeonRoundMenu>();
            if (roundMenu == null) roundMenu = gameObject.AddComponent<PigeonRoundMenu>();
            roundMenu.Initialize(this);
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
                    if (phase != Phase.Observe) break;
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
            if (!PatientSession.IsDifficultyUnlocked("pigeons", difficultyLevel)) return;
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
            roundProfileId = PatientSession.ActiveProfile?.id;
            roundParticipant = roundProfileId ?? (participantId.Trim() == "p-demo"
                ? "p-" + Guid.NewGuid().ToString("N") : participantId.Trim());
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
            LastRoundRecord = null;
            LastArrivalMetadata = null;
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
            if (resultAudioSource != null) resultAudioSource.Stop();

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
                SetStatus("Cuenta todas las palomas que veas.");
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
            LastRoundRecord = record;
            LastArrivalMetadata = BuildArrivalMetadata(terminalStatus);
            PatientSession.Save(new PatientResult
            {
                id = record.roundId, profileId = roundProfileId, sessionId = record.sessionId,
                scenarioId = "pigeons", difficulty = record.difficultyLevel, status = record.status,
                startedAt = record.startedAt, endedAt = record.endedAt,
                metricsJson = "{\"request\":" + PigeonTelemetry.ToRequestJson(record) +
                    ",\"arrivalMetadata\":" + LastArrivalMetadata + "}"
            });

            // Solo las rondas completadas reciben puntaje e imagen de resultado.
            if (terminalStatus == "completed" && submittedUtc.HasValue && hiddenUtc.HasValue)
            {
                int signedError = answer - roundPigeonCount;
                int absoluteError = Math.Abs(signedError);
                // Puntaje ludico, no una evaluacion cognitiva clinica.
                float scorePercent = roundPigeonCount == 0
                    ? (answer == 0 ? 100f : 0f)
                    : Mathf.Clamp01(1f - (float)absoluteError / roundPigeonCount) * 100f;
                bool perfect = signedError == 0;
                UpdateResultPresentation(scorePercent, perfect);

                double timeSinceHidden = (submittedUtc.Value - hiddenUtc.Value).TotalSeconds;
                string countPrefix = string.IsNullOrWhiteSpace(actualCountPrefix)
                    ? "Aparecieron" : actualCountPrefix.Trim();
                string countLine = countPrefix + " " + roundPigeonCount +
                    (roundPigeonCount == 1 ? " paloma" : " palomas");
                string answerLine = "Tu respuesta: " + answer;
                string errorLine = "Diferencia: " + absoluteError;
                string scoreLine = "Puntaje: " + scorePercent.ToString("F0") + "%";
                string timeLine = "Tiempo de respuesta: " + timeSinceHidden.ToString("F1") + " s";

                bool hasSeparateTexts = actualCountText != null || userCountText != null ||
                    errorCountText != null || scoreText != null || responseTimeText != null;
                // El TMP antiguo sirve de respaldo si aun no se asignaron textos separados.
                if (resultText != null)
                    resultText.text = hasSeparateTexts ? "" :
                        countLine + "\n" + answerLine + "\n" + errorLine +
                        "\n" + scoreLine + "\n" + timeLine;
                SetResultLines(countLine, answerLine, errorLine, scoreLine, timeLine);
                if (scoreText != null)
                    scoreText.color = perfect ? perfectScoreColor : scoreColor;
            }
            else
            {
                if (resultText != null) resultText.text = "Ronda no completada. No se registro respuesta.";
                SetResultLines("", "", "", "", "");
                if (resultTitleText != null) resultTitleText.text = titleIncomplete;
                if (resultImage != null)
                {
                    resultImage.sprite = null;
                    resultImage.enabled = false;
                }
            }
            SetStatus("Ronda finalizada. Elige Siguiente o Salir.");
            if (resultPanel == null) ReturnToStart();
        }

        public void ReturnToStart()
        {
            if (IsRunning()) return;
            phase = Phase.Ready;
            UpdatePanels(phase);
            SetStatus("Elige una dificultad para comenzar.");
        }


        private void SetResultLines(string count, string user, string error, string score, string time)
        {
            if (actualCountText != null) actualCountText.text = count;
            if (userCountText != null) userCountText.text = user;
            if (errorCountText != null) errorCountText.text = error;
            if (scoreText != null) scoreText.text = score;
            if (responseTimeText != null) responseTimeText.text = time;
        }

        private void UpdateResultPresentation(float scorePercent, bool perfect)
        {
            // 4 rangos visuales, elegidos por puntaje normalizado, no por numero fijo de palomas.
            Sprite sprite;
            string title;
            if (scorePercent < 25f) { sprite = resultImageLow; title = titleLow; }
            else if (scorePercent < 50f) { sprite = resultImageMedium; title = titleMedium; }
            else if (scorePercent < 75f) { sprite = resultImageHigh; title = titleHigh; }
            else { sprite = resultImageExcellent; title = titleExcellent; }

            if (resultImage != null)
            {
                resultImage.sprite = sprite;
                resultImage.enabled = sprite != null;
            }
            if (resultTitleText != null) resultTitleText.text = title;

            // El sonido especial tiene prioridad: no reproducimos ambos simultaneamente.
            if (resultAudioSource != null)
            {
                AudioClip clip = perfect ? perfectSound : (scorePercent > 0f ? victorySound : null);
                if (clip != null)
                {
                    resultAudioSource.Stop();
                    resultAudioSource.PlayOneShot(clip);
                }
            }
        }

        [Serializable]
        private class ArrivalMetadata
        {
            public string roundId;
            public string scenarioId = "pigeons";
            public string metadataVersion = "pigeon-arrival-v1";
            public int plannedPigeons;
            public int minimumPigeons;
            public int maximumPigeons;
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

        private string BuildArrivalMetadata(string terminalStatus)
        {
            if (poolSpawner == null) return "null";
            ArrivalMetadata extra = new ArrivalMetadata
            {
                roundId = currentRoundId,
                plannedPigeons = roundPigeonCount,
                minimumPigeons = poolSpawner.MinimumPigeons,
                maximumPigeons = poolSpawner.MaximumPigeons,
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
            return JsonUtility.ToJson(extra);
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

        // Cada menu debe tener su propio Canvas World Space.
        // Se apaga el Canvas completo: esto desactiva incluso el blur
        // si la imagen se encuentra fuera del panel pero dentro del Canvas.
        private void UpdatePanels(Phase p)
        {
            bool showStart = (p == Phase.Ready && (roundMenu == null || !roundMenu.IsInitialized)) ||
                (p == Phase.AwaitFood && foodPanel == null);
            bool showFood = p == Phase.AwaitFood && foodPanel != null;
            bool showObserve = p == Phase.FoodFlight || p == Phase.Arrival ||
                p == Phase.Observe || p == Phase.Retain;
            bool showAnswer = p == Phase.Answer;
            bool showResult = p == Phase.Finished;

            GameObject[] panels = { startPanel, foodPanel, observingPanel, answerPanel, resultPanel };
            bool[] visible = { showStart, showFood, showObserve, showAnswer, showResult };
            HashSet<GameObject> canvasRoots = new HashSet<GameObject>();

            // Primero apagar TODOS los Canvas de menu, incluidos sus efectos.
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] == null) continue;
                Canvas canvas = panels[i].GetComponentInParent<Canvas>(true);
                if (canvas != null && !transform.IsChildOf(canvas.transform))
                    canvasRoots.Add(canvas.gameObject);
            }
            foreach (GameObject root in canvasRoots)
                root.SetActive(false);

            // Desactivar todos los paneles. En Canvas compartidos, esto evita
            // superposicion de paneles, pero NO de efectos ajenos al panel.
            for (int i = 0; i < panels.Length; i++)
                if (panels[i] != null) panels[i].SetActive(false);

            // Activar solamente el menu de la fase actual.
            for (int i = 0; i < panels.Length; i++)
            {
                if (!visible[i] || panels[i] == null) continue;
                Canvas canvas = panels[i].GetComponentInParent<Canvas>(true);
                if (canvas != null && !transform.IsChildOf(canvas.transform))
                    canvas.gameObject.SetActive(true);
                panels[i].SetActive(true);
            }
            if (roundMenu != null)
                roundMenu.ShowPhase(p == Phase.Ready);
        }

        private void ValidateMenuCanvases()
        {
            GameObject[] panels = { startPanel, foodPanel, observingPanel, answerPanel, resultPanel };
            string[] names = { "Start", "Food", "Observing", "Answer", "Result" };
            HashSet<Canvas> used = new HashSet<Canvas>();

            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] == null) continue;
                Canvas canvas = panels[i].GetComponentInParent<Canvas>(true);
                if (canvas == null)
                {
                    Debug.LogWarning("[NeuroFlex] El panel " + names[i] +
                        " no tiene un Canvas padre; se desactivara solo el panel.", this);
                    continue;
                }
                if (transform.IsChildOf(canvas.transform))
                {
                    Debug.LogError("[NeuroFlex] El manager esta DENTRO del Canvas " +
                        canvas.name + ". Mueve PalomaTrialManager fuera de los menus " +
                        "para poder desactivarlos sin detener el controlador.", this);
                    continue;
                }
                if (!used.Add(canvas))
                    Debug.LogWarning("[NeuroFlex] Varios paneles comparten el Canvas " +
                        canvas.name + ". Para apagar completamente cada blur, " +
                        "usa un Canvas independiente por menu.", this);
            }
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

        private void OnDisable()
        {
            if (IsRunning()) FinishRound("interrupted");
        }

        private void OnApplicationQuit()
        {
            if (IsRunning()) FinishRound("interrupted");
        }
    }
}
