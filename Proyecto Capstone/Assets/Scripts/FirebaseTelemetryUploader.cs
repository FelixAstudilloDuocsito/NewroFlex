using System;
using System.Collections;
using System.Collections.Generic;
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
        private FirebaseFirestore firestore;
        private bool ready;
        private readonly List<PatientResult> waitingResults = new List<PatientResult>();

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
            if (instance != this)
            {
                Destroy(gameObject);
                return;
            }

            PatientSession.ResultStored += Upload;
            InitializeFirebase();
        }

        private void OnDestroy()
        {
            if (instance == this) PatientSession.ResultStored -= Upload;
        }

        private void InitializeFirebase()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogError("[NeuroFlex] No se pudieron verificar las dependencias de Firebase.");
                    return;
                }

                if (task.Result != DependencyStatus.Available)
                {
                    Debug.LogError("[NeuroFlex] Dependencias de Firebase no disponibles: " + task.Result);
                    return;
                }

                firestore = FirebaseFirestore.DefaultInstance;
                ready = true;
                Debug.Log("[NeuroFlex] Firebase Firestore inicializado.");
                foreach (PatientResult result in waitingResults)
                    Upload(result);
                waitingResults.Clear();
                UploadExistingResults();
            });
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
            try
            {
                metrics = ParseMetrics(result.metricsJson);
            }
            catch (FormatException exception)
            {
                Debug.LogError("[NeuroFlex] No se subió la métrica " + result.id + ": " + exception.Message);
                return;
            }

            var data = new Dictionary<string, object>
            {
                ["session_id"] = result.sessionId ?? NeuroFlexSession.SessionId,
                ["round_id"] = result.id,
                ["scenario_id"] = result.scenarioId ?? "",
                ["difficulty"] = result.difficulty,
                ["status"] = result.status ?? "",
                ["started_at"] = result.startedAt ?? "",
                ["ended_at"] = result.endedAt ?? "",
                ["metrics_json"] = result.metricsJson ?? "{}",
                ["metrics"] = ToFirestoreValue(metrics),
                ["uploaded_at"] = Timestamp.GetCurrentTimestamp()
            };

            DocumentReference sessionDocument = firestore.Collection(CollectionName)
                .Document(result.sessionId ?? NeuroFlexSession.SessionId);
            sessionDocument.SetAsync(new Dictionary<string, object>
            {
                ["session_id"] = result.sessionId ?? NeuroFlexSession.SessionId,
                ["anonymous"] = true,
                ["updated_at"] = Timestamp.GetCurrentTimestamp()
            }, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                    Debug.LogError("[NeuroFlex] No se pudo crear la sesión en Firestore: " +
                        task.Exception?.GetBaseException().Message);
            });
            CollectionReference session = sessionDocument.Collection("rounds");
            session.Document(result.id).SetAsync(data)
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsCanceled || task.IsFaulted)
                    {
                        Debug.LogError("[NeuroFlex] No se pudo guardar la métrica en Firestore: " +
                            task.Exception?.GetBaseException().Message);
                        if (attempt < 3) StartCoroutine(RetryUpload(result, attempt + 1));
                        return;
                    }

                    Debug.Log("[NeuroFlex] Métrica guardada en Firestore: " + result.id);
                });
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
