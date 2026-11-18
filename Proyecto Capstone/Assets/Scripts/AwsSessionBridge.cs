using System;
using System.Collections;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace NeuroFlexVR
{
    public enum SessionHandshakeState
    {
        Idle,
        Authenticating,
        WaitingForDashboard,
        Confirmed,
        Expired,
        Error
    }

    /// <summary>
    /// Gestiona el flujo de Handshake del código de pre-sesión entre el visor VR y el dashboard.
    /// Genera el código pre-sesión aleatorio (4 letras y 4 números intercalados),
    /// controla el tiempo de expiración de la sesión y consulta el estado en AWS.
    /// </summary>
    public sealed class AwsSessionBridge : MonoBehaviour
    {
        public const string DefaultApiBaseUrl = "https://4gesdz7435.execute-api.us-east-1.amazonaws.com/v1";

        private static AwsSessionBridge instance;
        public static AwsSessionBridge Instance => instance;

        [Header("Configuración Endpoint AWS")]
        [SerializeField] private string apiBaseUrl = DefaultApiBaseUrl;
        [SerializeField] private float pollIntervalSeconds = 3.0f;
        [SerializeField] private bool autoLoadReceptionOnConfirm = true;
        [SerializeField] private string receptionSceneName = "Recepción";

        [Header("Expiración de Sesión")]
        [Tooltip("Tiempo límite en segundos antes de que el código caduque.")]
        [SerializeField] private float sessionExpirationSeconds = 300f; // 5 minutos por defecto
        [Tooltip("Si es true, genera automáticamente un código nuevo al expirar.")]
        [SerializeField] private bool autoRegenerateOnExpire = false;

        [Header("Estado")]
        [SerializeField] private SessionHandshakeState currentState = SessionHandshakeState.Idle;
        [SerializeField] private string currentSessionId;
        [SerializeField] private string currentPreSessionCode;
        [SerializeField] private string currentParticipantId = "P001";

        private DateTime sessionExpirationTime = DateTime.MinValue;

        public SessionHandshakeState CurrentState => currentState;
        public string CurrentSessionId => currentSessionId;
        public string CurrentPreSessionCode => currentPreSessionCode;
        public string CurrentParticipantId => currentParticipantId;

        public float RemainingExpirationSeconds
        {
            get
            {
                if (currentState != SessionHandshakeState.WaitingForDashboard) return 0f;
                var remaining = (float)(sessionExpirationTime - DateTime.UtcNow).TotalSeconds;
                return Mathf.Max(0f, remaining);
            }
        }

        public event Action<SessionHandshakeState, string> StateChanged;
        public event Action<string, string> PreSessionCodeGenerated; // sessionId, code
        public event Action<string, string> SessionConfirmed;        // sessionId, participantId

        private Coroutine pollCoroutine;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (pollCoroutine != null) StopCoroutine(pollCoroutine);
        }

        /// <summary>
        /// Inicia el flujo: Autentica con Cognito y genera un código de pre-sesión con expiración.
        /// </summary>
        public void StartPreSessionFlow()
        {
            if (pollCoroutine != null) StopCoroutine(pollCoroutine);
            SetState(SessionHandshakeState.Authenticating, "Conectando con AWS Cognito...");

            AwsAuthService.Instance.EnsureAuthenticated((success, message) =>
            {
                if (!success)
                {
                    SetState(SessionHandshakeState.Error, "Error de autenticación AWS: " + message);
                    return;
                }

                // Generar ID de sesión único y código de 4 letras y 4 números intercalados aleatoriamente
                currentSessionId = "s-" + Guid.NewGuid().ToString("N");
                currentPreSessionCode = GenerateFriendlyCode();
                sessionExpirationTime = DateTime.UtcNow.AddSeconds(sessionExpirationSeconds);
                NeuroFlexSession.SetSessionId(currentSessionId);

                SetState(SessionHandshakeState.WaitingForDashboard, "Esperando confirmación en dashboard celular...");
                PreSessionCodeGenerated?.Invoke(currentSessionId, currentPreSessionCode);

                Debug.Log($"[NeuroFlex AWS] Código generado: {currentPreSessionCode} (Expira en {sessionExpirationSeconds}s)");

                // Iniciar sondeo (polling) para verificar confirmación y vigilar la expiración
                pollCoroutine = StartCoroutine(PollSessionConfirmationRoutine());
            });
        }

        /// <summary>
        /// Genera un código de 8 caracteres con exactamente 4 letras y 4 números colocados
        /// de forma completamente aleatoria (ej: 5d34g1ab).
        /// </summary>
        public static string GenerateFriendlyCode()
        {
            const string letters = "abcdefghijklmnopqrstuvwxyz";
            const string digits = "0123456789";
            var random = new System.Random();

            char[] chars = new char[8];

            // 4 letras aleatorias
            for (int i = 0; i < 4; i++)
            {
                chars[i] = letters[random.Next(letters.Length)];
            }

            // 4 dígitos aleatorios
            for (int i = 4; i < 8; i++)
            {
                chars[i] = digits[random.Next(digits.Length)];
            }

            // Mezcla aleatoria Fisher-Yates para intercalar posiciones al azar
            for (int i = chars.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                char temp = chars[i];
                chars[i] = chars[j];
                chars[j] = temp;
            }

            return new string(chars);
        }

        private IEnumerator PollSessionConfirmationRoutine()
        {
            while (currentState == SessionHandshakeState.WaitingForDashboard)
            {
                // Verificar si el tiempo expiró
                if (DateTime.UtcNow >= sessionExpirationTime)
                {
                    Debug.LogWarning("[NeuroFlex AWS] La sesión pre-sesión ha expirado sin confirmación.");
                    SetState(SessionHandshakeState.Expired, "El código ha expirado por tiempo límite.");

                    if (autoRegenerateOnExpire)
                    {
                        yield return new WaitForSecondsRealtime(2.0f);
                        StartPreSessionFlow();
                    }
                    yield break;
                }

                yield return new WaitForSecondsRealtime(pollIntervalSeconds);

                // Consulta a la API de sesiones en AWS
                string url = $"{apiBaseUrl}/sessions?session_id={UnityWebRequest.EscapeURL(currentSessionId)}&participante_id={UnityWebRequest.EscapeURL(currentParticipantId)}";

                using (var req = UnityWebRequest.Get(url))
                {
                    req.SetRequestHeader("Authorization", "Bearer " + AwsAuthService.Instance.AccessToken);
                    yield return req.SendWebRequest();

                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        try
                        {
                            var json = JObject.Parse(req.downloadHandler.text);
                            var items = json["items"] as JArray;
                            if (items != null && items.Count > 0)
                            {
                                // Sesión confirmada en AWS!
                                var sessionItem = items[0];
                                string participant = (string)sessionItem["participante_id"] ?? currentParticipantId;
                                ConfirmSession(currentSessionId, participant);
                                yield break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("[NeuroFlex AWS] Respuesta no esperada durante polling: " + ex.Message);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Confirma la sesión y transiciona a la experiencia principal.
        /// </summary>
        public void ConfirmSession(string sessionId, string participantId)
        {
            if (pollCoroutine != null) StopCoroutine(pollCoroutine);

            currentSessionId = sessionId;
            currentParticipantId = string.IsNullOrWhiteSpace(participantId) ? "P001" : participantId;

            NeuroFlexSession.SetSessionId(currentSessionId);
            PatientSession.Select(new PatientProfile
            {
                id = currentParticipantId,
                alias = "Paciente " + currentParticipantId,
                age = 65
            });

            SetState(SessionHandshakeState.Confirmed, $"¡Sesión confirmada para participante {currentParticipantId}!");
            SessionConfirmed?.Invoke(currentSessionId, currentParticipantId);

            if (autoLoadReceptionOnConfirm)
            {
                StartCoroutine(LoadReceptionRoutine());
            }
        }

        private IEnumerator LoadReceptionRoutine()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            Debug.Log("[NeuroFlex AWS] Transicionando a la escena: " + receptionSceneName);
            SceneManager.LoadScene(receptionSceneName);
        }

        private void SetState(SessionHandshakeState state, string message)
        {
            currentState = state;
            StateChanged?.Invoke(state, message);
        }
    }
}
