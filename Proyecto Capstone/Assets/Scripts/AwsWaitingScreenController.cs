using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeuroFlexVR
{
    /// <summary>
    /// Controlador para la pantalla de espera (World Space Canvas en VR).
    /// Muestra el código de pre-sesión aleatorio (4 letras y 4 números intercalados),
    /// el temporizador de expiración y actualiza el estado de la conexión en tiempo real.
    /// </summary>
    public sealed class AwsWaitingScreenController : MonoBehaviour
    {
        [Header("Referencias de UI (TextMeshPro / UI)")]
        [SerializeField] private TMP_Text sessionCodeText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text feedbackText;
        [SerializeField] private TMP_Text sessionIdText;

        [Header("Botones de Control")]
        [SerializeField] private Button regenerateButton;
        [SerializeField] private Button manualConfirmButton; // Botón de test para desarrollo

        [Header("Configuración")]
        [SerializeField] private string testParticipantId = "P001";

        private string currentCode = "Generando...";
        private string currentStatus = "Conectando con AWS Cognito...";
        private string currentSessionId = "";
        private float lastTimerUpdate;

        private void Start()
        {
            if (regenerateButton != null)
            {
                regenerateButton.onClick.AddListener(OnRegenerateClicked);
            }

            if (manualConfirmButton != null)
            {
                manualConfirmButton.onClick.AddListener(OnManualConfirmClicked);
            }

            RenderUI();

            // Suscribirse a eventos del bridge
            if (AwsSessionBridge.Instance != null)
            {
                AwsSessionBridge.Instance.PreSessionCodeGenerated += OnCodeGenerated;
                AwsSessionBridge.Instance.StateChanged += OnStateChanged;
                AwsSessionBridge.Instance.SessionConfirmed += OnSessionConfirmed;

                // Si ya había un código activo, mostrarlo; si no, iniciar el flujo
                if (!string.IsNullOrEmpty(AwsSessionBridge.Instance.CurrentPreSessionCode))
                {
                    OnCodeGenerated(AwsSessionBridge.Instance.CurrentSessionId, AwsSessionBridge.Instance.CurrentPreSessionCode);
                }
                else
                {
                    AwsSessionBridge.Instance.StartPreSessionFlow();
                }
            }
            else
            {
                // Crear bridge si no existe en la escena
                var bridgeGo = new GameObject("AwsSessionBridge");
                var bridge = bridgeGo.AddComponent<AwsSessionBridge>();
                bridge.PreSessionCodeGenerated += OnCodeGenerated;
                bridge.StateChanged += OnStateChanged;
                bridge.SessionConfirmed += OnSessionConfirmed;
                bridge.StartPreSessionFlow();
            }
        }

        private void Update()
        {
            // Actualizar cuenta regresiva cada 1 segundo si estamos esperando
            if (AwsSessionBridge.Instance != null &&
                AwsSessionBridge.Instance.CurrentState == SessionHandshakeState.WaitingForDashboard)
            {
                if (Time.unscaledTime - lastTimerUpdate >= 1.0f)
                {
                    lastTimerUpdate = Time.unscaledTime;
                    RenderUI();
                }
            }
        }

        private void OnDestroy()
        {
            if (AwsSessionBridge.Instance != null)
            {
                AwsSessionBridge.Instance.PreSessionCodeGenerated -= OnCodeGenerated;
                AwsSessionBridge.Instance.StateChanged -= OnStateChanged;
                AwsSessionBridge.Instance.SessionConfirmed -= OnSessionConfirmed;
            }
        }

        private void OnCodeGenerated(string sessionId, string code)
        {
            currentCode = code;
            currentSessionId = sessionId;
            currentStatus = "Ingresa este código en el dashboard del celular para iniciar.";
            if (regenerateButton != null) regenerateButton.interactable = true;
            RenderUI();
        }

        private void OnStateChanged(SessionHandshakeState state, string message)
        {
            switch (state)
            {
                case SessionHandshakeState.Authenticating:
                    currentStatus = "Conectando con AWS Cognito...";
                    break;
                case SessionHandshakeState.WaitingForDashboard:
                    currentStatus = "Ingresa este código en el dashboard del celular para iniciar.";
                    break;
                case SessionHandshakeState.Confirmed:
                    currentStatus = "¡Sesión Confirmada! Iniciando...";
                    break;
                case SessionHandshakeState.Expired:
                    currentStatus = "El código ha expirado.";
                    break;
                case SessionHandshakeState.Error:
                    currentStatus = "Error: " + message;
                    break;
            }

            if (feedbackText != null)
            {
                feedbackText.text = message;
            }

            RenderUI();
        }

        private void OnSessionConfirmed(string sessionId, string participantId)
        {
            currentStatus = $"¡Sesión confirmada para participante {participantId}! Cargando recepción...";
            RenderUI();

            if (regenerateButton != null) regenerateButton.interactable = false;
            if (manualConfirmButton != null) manualConfirmButton.interactable = false;
        }

        private void RenderUI()
        {
            bool isWaiting = AwsSessionBridge.Instance != null &&
                             AwsSessionBridge.Instance.CurrentState == SessionHandshakeState.WaitingForDashboard;
            bool isExpired = AwsSessionBridge.Instance != null &&
                             AwsSessionBridge.Instance.CurrentState == SessionHandshakeState.Expired;

            // Formatear temporizador si está activo
            string timerText = "";
            if (isWaiting)
            {
                float rem = AwsSessionBridge.Instance.RemainingExpirationSeconds;
                int mins = Mathf.FloorToInt(rem / 60f);
                int secs = Mathf.FloorToInt(rem % 60f);
                timerText = $"\n<size=18><color=#E6B800>Expira en: <b>{mins:D2}:{secs:D2}</b></color></size>";
            }

            // Si sessionCodeText y statusText son el mismo componente (o statusText es null)
            if (sessionCodeText != null && (statusText == null || statusText == sessionCodeText))
            {
                if (isExpired)
                {
                    sessionCodeText.text =
                        "<b><size=34>NEUROFLEX VR</size></b>\n" +
                        "<size=20><color=#CCCCCC>Sincronización Clínica con Celular</color></size>\n\n" +
                        "<b><size=50><color=#FF5555>CÓDIGO EXPIRADO</color></size></b>\n\n" +
                        "<size=18><i>El tiempo de espera expiró por seguridad.\nGenera un nuevo código para conectar.</i></size>";
                    return;
                }

                string idShort = string.IsNullOrEmpty(currentSessionId) ? "" :
                    (currentSessionId.Length > 16 ? currentSessionId.Substring(0, 16) + "..." : currentSessionId);

                sessionCodeText.text =
                    "<b><size=34>NEUROFLEX VR</size></b>\n" +
                    "<size=20><color=#CCCCCC>Sincronización Clínica con Celular</color></size>\n\n" +
                    "Código de Pre-Sesión:\n" +
                    $"<b><size=56><color=#4E9FFF>{currentCode}</color></size></b>" +
                    timerText + "\n\n" +
                    $"<size=18><i>{currentStatus}</i></size>" +
                    (string.IsNullOrEmpty(idShort) ? "" : $"\n<size=12><color=#888888>ID: {idShort}</color></size>");
                return;
            }

            // Si son componentes de texto separados
            if (sessionCodeText != null)
            {
                sessionCodeText.text = isExpired ? "<color=#FF5555>EXPIRADO</color>" : currentCode;
            }

            if (statusText != null)
            {
                statusText.text = isExpired ? "Código expirado. Presiona regenerar." : (currentStatus + timerText);
            }

            if (sessionIdText != null && !string.IsNullOrEmpty(currentSessionId))
            {
                sessionIdText.text = "ID: " + (currentSessionId.Length > 16 ? currentSessionId.Substring(0, 16) + "..." : currentSessionId);
            }
        }

        private void OnRegenerateClicked()
        {
            if (feedbackText != null) feedbackText.text = "Generando nuevo código...";
            if (AwsSessionBridge.Instance != null)
            {
                AwsSessionBridge.Instance.StartPreSessionFlow();
            }
        }

        private void OnManualConfirmClicked()
        {
            if (AwsSessionBridge.Instance != null)
            {
                Debug.Log("[NeuroFlex AWS] Confirmación manual activada para pruebas de desarrollo.");
                AwsSessionBridge.Instance.ConfirmSession(
                    AwsSessionBridge.Instance.CurrentSessionId ?? ("s-" + Guid.NewGuid().ToString("N")),
                    testParticipantId
                );
            }
        }
    }
}
