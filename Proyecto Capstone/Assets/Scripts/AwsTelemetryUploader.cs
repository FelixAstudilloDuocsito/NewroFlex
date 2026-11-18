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
    /// Sube las métricas de rondas guardadas en la base local directamente a AWS API Gateway / SQS.
    /// Funciona en paralelo con FirebaseTelemetryUploader mientras ambos convivan.
    /// Mantiene cola en disco para resiliencia offline.
    /// </summary>
    public sealed class AwsTelemetryUploader : MonoBehaviour
    {
        private const string EndpointKey = "NeuroFlex.TelemetryEndpoint";
        private const string TenantKey = "NeuroFlex.TelemetryTenant";
        public const string DefaultEndpoint = "https://4gesdz7435.execute-api.us-east-1.amazonaws.com/v1/sessions";
        public const string DefaultTenant = "habaA";
        private const string QueueDirectoryName = "NeuroFlexTelemetry/cloud-pending";

        private static AwsTelemetryUploader instance;
        private readonly Queue<string> queue = new Queue<string>();
        private bool sending;

        public static event Action<string, bool, string> UploadCompleted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateInstance()
        {
            // Por defecto activo (1) para convivir con Firebase
            if (PlayerPrefs.GetInt("NeuroFlex.UseAwsTelemetry", 1) != 1) return;
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
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            PatientSession.ResultStored += Enqueue;
            LoadQueue();
            // QueueLocalHistory(); // Desactivado para no re-encolar todo el historial previo
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
                Debug.LogError("[NeuroFlex AWS] No se pudo preparar la métrica para AWS: " + exception.Message);
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
                Debug.LogError("[NeuroFlex AWS] No se pudo leer la cola local de AWS: " + exception.Message);
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
                Debug.LogWarning("[NeuroFlex AWS] No se pudo preparar el historial local para AWS: " + exception.Message);
            }
        }

        private void ProcessQueue()
        {
            if (!isActiveAndEnabled || sending || queue.Count == 0) return;
            if (string.IsNullOrWhiteSpace(Endpoint))
            {
                Debug.LogWarning("[NeuroFlex AWS] Configura el endpoint de telemetría antes de enviar métricas.");
                return;
            }

            // Asegurar autenticación Cognito antes de transmitir a API Gateway
            AwsAuthService.Instance.EnsureAuthenticated((authSuccess, authMsg) =>
            {
                if (!authSuccess)
                {
                    Debug.LogWarning("[NeuroFlex AWS] Esperando autenticación para enviar métricas: " + authMsg);
                    return;
                }
                StartCoroutine(SendNext());
            });
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
                Debug.LogError("[NeuroFlex AWS] No se pudo leer una métrica pendiente: " + exception.Message);
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
                request.SetRequestHeader("Authorization", "Bearer " + AwsAuthService.Instance.AccessToken);

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success &&
                    request.responseCode >= 200 && request.responseCode < 300)
                {
                    try
                    {
                        File.Delete(path);
                        queue.Dequeue();
                        string roundId = Path.GetFileNameWithoutExtension(path);
                        Debug.Log("[NeuroFlex AWS] Métrica enviada exitosamente a AWS: " + roundId);
                        UploadCompleted?.Invoke(roundId, true, "");
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError("[NeuroFlex AWS] AWS respondió correctamente, pero error en cola local: " + exception.Message);
                    }
                }
                else
                {
                    string err = $"HTTP {request.responseCode}: {request.error} - {request.downloadHandler.text}";
                    Debug.LogWarning("[NeuroFlex AWS] Reintentando envío a AWS: " + err);
                    UploadCompleted?.Invoke(Path.GetFileNameWithoutExtension(path), false, err);
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
                metrics = JToken.Parse(string.IsNullOrWhiteSpace(result.metricsJson) ? "{}" : result.metricsJson);
            }
            catch (JsonException exception)
            {
                throw new FormatException("Las métricas locales no son JSON válido.", exception);
            }

            string sessionId = string.IsNullOrWhiteSpace(result.sessionId) ? NeuroFlexSession.SessionId : result.sessionId;
            string participantId = string.IsNullOrWhiteSpace(result.profileId) ? "P001" : result.profileId;

            // Formato scenes-v1 compatible con el worker Lambda y DynamoDB multi-tenant
            var roundItem = new JObject
            {
                ["participantId"] = participantId,
                ["sessionId"] = sessionId,
                ["roundId"] = result.id,
                ["scenarioId"] = result.scenarioId,
                ["difficultyLevel"] = result.difficulty,
                ["status"] = result.status,
                ["startedAt"] = result.startedAt,
                ["endedAt"] = result.endedAt
            };

            // Incorporar campos específicos de métricas si existen
            if (metrics is JObject metricsObj)
            {
                foreach (var prop in metricsObj.Properties())
                {
                    roundItem[prop.Name] = prop.Value;
                }
            }

            var payload = new JObject
            {
                ["sesion_id"] = sessionId,
                ["participante_id"] = participantId,
                ["experiencia"] = "neuroflex",
                ["scene_config_id"] = "REC-PARK-LVL-FACIL-V1",
                ["duracion_seg"] = 60,
                ["contractVersion"] = "scenes-v1",
                ["tenantId"] = Tenant,
                ["rounds"] = new JArray { roundItem },
                ["metricas"] = metrics
            };

            return payload.ToString(Formatting.None);
        }
    }
}
