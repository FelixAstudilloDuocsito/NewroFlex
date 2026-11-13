using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Extensions;
using Firebase.Firestore;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NeuroFlexVR
{
    /// <summary>
    /// Persists locally saved rounds in Firestore. The local database remains the source of truth.
    /// </summary>
    public sealed class FirebaseTelemetryUploader : MonoBehaviour
    {
        private const string CollectionName = "sessions";
        private static FirebaseTelemetryUploader instance;
        private static string initializationError;
        private FirebaseFirestore firestore;
        private bool ready;
        private readonly List<PatientResult> waitingResults = new List<PatientResult>();

        public static event Action<string, bool, string> UploadCompleted;
        public static event Action<string> InitializationFailed;
        public static bool IsAvailable => instance != null;
        public static string InitializationError => initializationError;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateInstance()
        {
            if (instance != null) return;
            var gameObject = new GameObject("NeuroFlex Firebase Telemetry");
            instance = gameObject.AddComponent<FirebaseTelemetryUploader>();
            DontDestroyOnLoad(gameObject);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            PatientSession.ResultStored += Upload;
            InitializeFirebase();
        }

        private void OnDestroy()
        {
            if (instance == this) PatientSession.ResultStored -= Upload;
        }

        private void InitializeFirebase()
        {
            try
            {
                FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
                {
                    if (task.IsCanceled || task.IsFaulted)
                    {
                        FailInitialization(task.Exception?.GetBaseException().Message ?? "La verificación de dependencias fue cancelada.");
                        return;
                    }

                    if (task.Result != DependencyStatus.Available)
                    {
                        FailInitialization("Dependencias de Firebase no disponibles: " + task.Result);
                        return;
                    }

                    try
                    {
                        firestore = FirebaseFirestore.DefaultInstance;
                        initializationError = null;
                        ready = true;
                        Debug.Log("[NeuroFlex] Firebase Firestore inicializado.");
                        foreach (PatientResult result in waitingResults)
                            Upload(result);
                        waitingResults.Clear();
                        UploadExistingResults();
                    }
                    catch (Exception exception)
                    {
                        FailInitialization(exception.Message);
                    }
                });
            }
            catch (Exception exception)
            {
                FailInitialization(exception.Message);
            }
        }

        private static void FailInitialization(string error)
        {
            initializationError = error;
            Debug.LogError("[NeuroFlex] No se pudo inicializar Firebase Firestore: " + error);
            InitializationFailed?.Invoke(error);
        }

        private void UploadExistingResults()
        {
            try
            {
                using (var database = PatientSession.OpenDatabase())
                    foreach (PatientProfile profile in database.ListProfiles())
                    {
                        const int pageSize = 100;
                        for (int offset = 0; ; offset += pageSize)
                        {
                            List<PatientResult> results = database.ListResultsPage(profile.id, offset, pageSize);
                            foreach (PatientResult result in results) Upload(result);
                            if (results.Count < pageSize) break;
                        }
                    }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NeuroFlex] No se pudo preparar el historial local para Firebase: " + exception.Message);
            }

            try
            {
                foreach (PatientResult result in PatientSession.PendingResults)
                    Upload(result);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NeuroFlex] No se pudieron preparar las métricas anónimas para Firebase: " + exception.Message);
            }
        }

        private void Upload(PatientResult result)
        {
            Upload(result, 0);
        }

        private void Upload(PatientResult result, int attempt)
        {
            if (result == null || string.IsNullOrEmpty(result.id)) return;
            if (!ready)
            {
                if (!waitingResults.Exists(item => item.id == result.id))
                    waitingResults.Add(result);
                return;
            }
            JObject metrics;
            string sceneName;
            try
            {
                metrics = ParseMetrics(result.metricsJson);
                sceneName = SceneName(result.scenarioId);
                if (result.difficulty < 1 || result.difficulty > 3)
                    throw new FormatException("La dificultad de la ronda debe estar entre 1 y 3.");
            }
            catch (FormatException exception)
            {
                Debug.LogError("[NeuroFlex] No se subió la métrica " + result.id + ": " + exception.Message);
                UploadCompleted?.Invoke(result.id, false, exception.Message);
                return;
            }

            string sessionId = string.IsNullOrWhiteSpace(result.sessionId)
                ? NeuroFlexSession.SessionId
                : result.sessionId;
            string difficultyId = result.difficulty.ToString(System.Globalization.CultureInfo.InvariantCulture);

            DocumentReference sessionDocument = firestore.Collection(CollectionName).Document(sessionId);
            DocumentReference sceneDocument = sessionDocument.Collection("scenes").Document(result.scenarioId);
            DocumentReference difficultyDocument = sceneDocument.Collection("difficulties").Document(difficultyId);
            DocumentReference roundDocument = difficultyDocument.Collection("rounds").Document(result.id);

            var sessionData = new Dictionary<string, object>
            {
                ["session_id"] = sessionId,
                ["anonymous"] = string.IsNullOrEmpty(result.profileId),
                ["updated_at"] = Timestamp.GetCurrentTimestamp()
            };
            var sceneData = new Dictionary<string, object>
            {
                ["scenario_id"] = result.scenarioId,
                ["scenario_name"] = sceneName,
                ["updated_at"] = Timestamp.GetCurrentTimestamp()
            };
            var difficultyData = new Dictionary<string, object>
            {
                ["difficulty"] = result.difficulty,
                ["difficulty_name"] = "Nivel " + result.difficulty,
                ["updated_at"] = Timestamp.GetCurrentTimestamp()
            };
            var roundData = new Dictionary<string, object>
            {
                ["session_id"] = sessionId,
                ["round_id"] = result.id,
                ["scenario_id"] = result.scenarioId,
                ["scenario_name"] = sceneName,
                ["difficulty"] = result.difficulty,
                ["difficulty_name"] = "Nivel " + result.difficulty,
                ["status"] = result.status ?? "",
                ["started_at"] = result.startedAt ?? "",
                ["ended_at"] = result.endedAt ?? "",
                ["metrics_json"] = result.metricsJson ?? "{}",
                ["metrics"] = ToFirestoreValue(metrics),
                ["uploaded_at"] = Timestamp.GetCurrentTimestamp()
            };

            try
            {
                ContinueWrite(sessionDocument.SetAsync(sessionData, SetOptions.MergeAll), result, attempt, () =>
                    ContinueWrite(sceneDocument.SetAsync(sceneData, SetOptions.MergeAll), result, attempt, () =>
                        ContinueWrite(difficultyDocument.SetAsync(difficultyData, SetOptions.MergeAll), result, attempt, () =>
                            ContinueWrite(roundDocument.SetAsync(roundData), result, attempt, () =>
                            {
                                Debug.Log("[NeuroFlex] Métrica guardada en Firestore: " + result.id);
                                UploadCompleted?.Invoke(result.id, true, "");
                            }))));
            }
            catch (Exception exception)
            {
                HandleUploadFailure(result, attempt, exception.Message);
            }
        }

        private void ContinueWrite(Task writeTask, PatientResult result, int attempt, Action onSuccess)
        {
            writeTask.ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    HandleUploadFailure(result, attempt, TaskError(task));
                    return;
                }

                try
                {
                    onSuccess();
                }
                catch (Exception exception)
                {
                    HandleUploadFailure(result, attempt, exception.Message);
                }
            });
        }

        private static string SceneName(string scenarioId)
        {
            switch (scenarioId)
            {
                case "pigeons": return "Palomas";
                case "memory": return "Memoria";
                case "puzzle": return "Biblioteca";
                default: throw new FormatException("Escenario no reconocido: " + scenarioId);
            }
        }

        private void HandleUploadFailure(PatientResult result, int attempt, string error)
        {
            Debug.LogError("[NeuroFlex] No se pudo guardar la métrica en Firestore (" + result.id + "): " + error);
            if (attempt < 3)
            {
                StartCoroutine(RetryUpload(result, attempt + 1));
                return;
            }

            UploadCompleted?.Invoke(result.id, false, error);
        }

        private static string TaskError(Task task)
        {
            return task.Exception?.GetBaseException().Message ?? "La escritura en Firestore fue cancelada.";
        }

        private IEnumerator RetryUpload(PatientResult result, int attempt)
        {
            yield return new WaitForSecondsRealtime(5f * attempt);
            Upload(result, attempt);
        }

        private static JObject ParseMetrics(string json)
        {
            try
            {
                return JObject.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            }
            catch (Exception exception)
            {
                throw new FormatException("Las métricas locales no son JSON válido.", exception);
            }
        }

        private static object ToFirestoreValue(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token is JObject obj)
            {
                var map = new Dictionary<string, object>();
                foreach (JProperty property in obj.Properties())
                    map[property.Name] = ToFirestoreValue(property.Value);
                return map;
            }
            if (token is JArray array)
            {
                var values = new List<object>();
                foreach (JToken item in array) values.Add(ToFirestoreValue(item));
                return values;
            }
            if (token.Type == JTokenType.Integer) return token.Value<long>();
            if (token.Type == JTokenType.Float) return token.Value<double>();
            if (token.Type == JTokenType.Boolean) return token.Value<bool>();
            return token.Value<string>();
        }
    }
}
