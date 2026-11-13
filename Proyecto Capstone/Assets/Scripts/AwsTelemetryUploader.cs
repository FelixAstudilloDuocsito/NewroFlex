using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace NeuroFlexVR
{
    /// <summary>
    /// Sends locally persisted rounds to the API Gateway telemetry endpoint.
    /// Local records remain the source of truth and are never deleted here.
    /// </summary>
    public sealed class AwsTelemetryUploader : MonoBehaviour
    {
        private const string EndpointKey = "NeuroFlex.TelemetryEndpoint";
        private const string TenantKey = "NeuroFlex.TelemetryTenant";
        private const string DefaultEndpoint = "https://4gesdz7435.execute-api.us-east-1.amazonaws.com/telemetry";
        private const string DefaultTenant = "lab";
        private const string QueueDirectoryName = "NeuroFlexTelemetry/cloud-pending";

        private static AwsTelemetryUploader instance;
        private readonly Queue<string> queue = new Queue<string>();
        private bool sending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateInstance()
        {
            if (PlayerPrefs.GetInt("NeuroFlex.UseAwsTelemetry", 0) != 1) return;
            if (instance != null) return;
            var gameObject = new GameObject("NeuroFlex AWS Telemetry");
            instance = gameObject.AddComponent<AwsTelemetryUploader>();
            DontDestroyOnLoad(gameObject);
        }

        private string Endpoint
        {
            get
            {
                string configured = PlayerPrefs.GetString(EndpointKey, "");
                return string.IsNullOrWhiteSpace(configured) ? DefaultEndpoint : configured.Trim();
            }
        }

        private string Tenant =>
            string.IsNullOrWhiteSpace(PlayerPrefs.GetString(TenantKey, ""))
                ? DefaultTenant
                : PlayerPrefs.GetString(TenantKey, "").Trim();

        private string QueueDirectory => Path.Combine(Application.persistentDataPath, QueueDirectoryName);

        public static void Configure(string endpoint, string tenantId = DefaultTenant)
        {
            if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("El endpoint de telemetría es obligatorio.");
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("El tenant de telemetría es obligatorio.");
            PlayerPrefs.SetString(EndpointKey, endpoint.Trim());
            PlayerPrefs.SetString(TenantKey, tenantId.Trim());
            PlayerPrefs.Save();
            if (instance != null) instance.ProcessQueue();
        }

        private void Awake()
        {
            if (instance != this)
            {
                Destroy(gameObject);
                return;
            }

            PatientSession.ResultStored += Enqueue;
            LoadQueue();
            QueueLocalHistory();
            ProcessQueue();
        }

        private void OnDestroy()
        {
            if (instance == this) PatientSession.ResultStored -= Enqueue;
        }

        private void Enqueue(PatientResult result)
        {
            if (result == null) return;
            try
            {
                Directory.CreateDirectory(QueueDirectory);
                string path = Path.Combine(QueueDirectory, result.id + ".json");
                if (!File.Exists(path))
                    File.WriteAllText(path, BuildPayload(result), new UTF8Encoding(false));
                if (!queue.Contains(path)) queue.Enqueue(path);
                ProcessQueue();
            }
            catch (Exception exception)
            {
                Debug.LogError("[NeuroFlex] No se pudo preparar la métrica para AWS: " + exception.Message);
            }
        }

        private void LoadQueue()
        {
            try
            {
                Directory.CreateDirectory(QueueDirectory);
                foreach (string path in Directory.GetFiles(QueueDirectory, "*.json"))
                    queue.Enqueue(path);
            }
            catch (Exception exception)
            {
                Debug.LogError("[NeuroFlex] No se pudo leer la cola local de AWS: " + exception.Message);
            }
        }

        private void QueueLocalHistory()
        {
            try
            {
                using (var database = PatientSession.OpenDatabase())
                    foreach (PatientProfile profile in database.ListProfiles())
                        foreach (PatientResult result in database.ListResults(profile.id, 1000))
                            Enqueue(result);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NeuroFlex] No se pudo preparar el historial local para AWS: " + exception.Message);
            }
        }

        private void ProcessQueue()
        {
            if (!isActiveAndEnabled || sending || queue.Count == 0) return;
            if (string.IsNullOrWhiteSpace(Endpoint))
            {
                Debug.LogWarning("[NeuroFlex] Configura el endpoint de telemetría antes de enviar métricas.");
                return;
            }
            StartCoroutine(SendNext());
        }

        private IEnumerator SendNext()
        {
            sending = true;
            string path = queue.Peek();
            string body;
            try
            {
                body = File.ReadAllText(path);
            }
            catch (Exception exception)
            {
                Debug.LogError("[NeuroFlex] No se pudo leer una métrica pendiente: " + exception.Message);
                queue.Dequeue();
                sending = false;
                ProcessQueue();
                yield break;
            }

            using (var request = new UnityWebRequest(Endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(body);
                request.uploadHandler = new UploadHandlerRaw(bytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success &&
                    request.responseCode >= 200 && request.responseCode < 300)
                {
                    try
                    {
                        File.Delete(path);
                        queue.Dequeue();
                        Debug.Log("[NeuroFlex] Métrica enviada a AWS: " + Path.GetFileNameWithoutExtension(path));
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError("[NeuroFlex] AWS respondió correctamente, pero no se pudo actualizar la cola local: " + exception.Message);
                    }
                }
                else
                {
                    Debug.LogWarning("[NeuroFlex] No se pudo enviar la métrica a AWS. Se reintentará: " +
                        request.responseCode + " " + request.error);
                }
            }

            sending = false;
            if (queue.Count > 0) Invoke(nameof(ProcessQueue), 5f);
        }

        private string BuildPayload(PatientResult result)
        {
            JToken metrics;
            try
            {
                metrics = JToken.Parse(result.metricsJson ?? "{}");
            }
            catch (JsonException exception)
            {
                throw new FormatException("Las métricas locales no son JSON válido.", exception);
            }

            var payload = new JObject
            {
                ["tenant_id"] = Tenant,
                // The round ID is unique; app_session_id keeps the launch/session relation.
                ["session_id"] = result.id,
                ["app_session_id"] = result.sessionId,
                ["participant_id"] = result.profileId,
                ["round_id"] = result.id,
                ["scenario_id"] = result.scenarioId,
                ["difficulty"] = result.difficulty,
                ["status"] = result.status,
                ["started_at"] = result.startedAt,
                ["ended_at"] = result.endedAt,
                ["metrics"] = metrics
            };
            return payload.ToString(Formatting.None);
        }
    }
}
