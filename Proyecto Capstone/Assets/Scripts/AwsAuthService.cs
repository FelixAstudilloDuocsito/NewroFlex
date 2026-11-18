using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace NeuroFlexVR
{
    /// <summary>
    /// Servicio de autenticación con AWS Cognito Identity Provider mediante llamadas HTTPS nativas.
    /// No depende de SDKs pesados de AWS y funciona en Windows, Mac y Android (Meta Quest).
    /// </summary>
    public sealed class AwsAuthService : MonoBehaviour
    {
        private const string PrefClientId = "NeuroFlex.Aws.ClientId";
        private const string PrefRegion = "NeuroFlex.Aws.Region";
        private const string PrefUsername = "NeuroFlex.Aws.Username";
        private const string PrefPassword = "NeuroFlex.Aws.Password";

        public const string DefaultClientId = "332crj9p27t5823jbcda9aqcho";
        public const string DefaultRegion = "us-east-1";
        public const string DefaultUsername = "equipo.unity@neuroflex.test";
        public const string DefaultPassword = "Neuroflex2024!";

        private static AwsAuthService instance;
        public static AwsAuthService Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("NeuroFlex_AwsAuthService");
                    instance = go.AddComponent<AwsAuthService>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        [Header("Configuración Cognito")]
        [SerializeField] private string clientId = DefaultClientId;
        [SerializeField] private string region = DefaultRegion;
        [SerializeField] private string username = DefaultUsername;
        [SerializeField] private string password = DefaultPassword;

        public string AccessToken { get; private set; }
        public string IdToken { get; private set; }
        public DateTime TokenExpiration { get; private set; } = DateTime.MinValue;
        public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken) && DateTime.UtcNow < TokenExpiration;
        public bool IsAuthenticating { get; private set; }

        public static event Action<string> TokenRefreshed;
        public static event Action<string> AuthenticationFailed;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadConfiguration();
        }

        public void LoadConfiguration()
        {
            clientId = PlayerPrefs.GetString(PrefClientId, DefaultClientId);
            region = PlayerPrefs.GetString(PrefRegion, DefaultRegion);
            username = PlayerPrefs.GetString(PrefUsername, DefaultUsername);
            password = PlayerPrefs.GetString(PrefPassword, DefaultPassword);
        }

        public void SaveConfiguration(string newClientId, string newRegion, string newUsername, string newPassword)
        {
            clientId = string.IsNullOrWhiteSpace(newClientId) ? DefaultClientId : newClientId.Trim();
            region = string.IsNullOrWhiteSpace(newRegion) ? DefaultRegion : newRegion.Trim();
            username = string.IsNullOrWhiteSpace(newUsername) ? DefaultUsername : newUsername.Trim();
            password = string.IsNullOrWhiteSpace(newPassword) ? DefaultPassword : newPassword.Trim();

            PlayerPrefs.SetString(PrefClientId, clientId);
            PlayerPrefs.SetString(PrefRegion, region);
            PlayerPrefs.SetString(PrefUsername, username);
            PlayerPrefs.SetString(PrefPassword, password);
            PlayerPrefs.Save();
        }

        public void EnsureAuthenticated(Action<bool, string> callback)
        {
            if (IsAuthenticated)
            {
                callback?.Invoke(true, AccessToken);
                return;
            }

            StartCoroutine(AuthenticateRoutine(callback));
        }

        public IEnumerator AuthenticateRoutine(Action<bool, string> callback = null)
        {
            if (IsAuthenticating)
            {
                // Espera si ya hay una petición en curso
                while (IsAuthenticating)
                    yield return null;

                callback?.Invoke(IsAuthenticated, IsAuthenticated ? AccessToken : "Autenticación fallida.");
                yield break;
            }

            IsAuthenticating = true;
            string authUrl = $"https://cognito-idp.{region}.amazonaws.com/";

            string jsonBody = "{\"AuthFlow\":\"USER_PASSWORD_AUTH\",\"ClientId\":\"" + clientId +
                              "\",\"AuthParameters\":{\"USERNAME\":\"" + username +
                              "\",\"PASSWORD\":\"" + password + "\"}}";

            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            using (var request = new UnityWebRequest(authUrl, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/x-amz-json-1.1");
                request.SetRequestHeader("X-Amz-Target", "AWSCognitoIdentityProviderService.InitiateAuth");

                yield return request.SendWebRequest();

                IsAuthenticating = false;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string errorMsg = $"Error en Cognito ({request.responseCode}): {request.error} - {request.downloadHandler.text}";
                    Debug.LogError("[NeuroFlex AWS] " + errorMsg);
                    AuthenticationFailed?.Invoke(errorMsg);
                    callback?.Invoke(false, errorMsg);
                    yield break;
                }

                try
                {
                    var responseJson = JObject.Parse(request.downloadHandler.text);
                    var authResult = responseJson["AuthenticationResult"];
                    if (authResult != null)
                    {
                        AccessToken = (string)authResult["AccessToken"];
                        IdToken = (string)authResult["IdToken"];
                        int expiresIn = (int)(authResult["ExpiresIn"] ?? 3600);
                        TokenExpiration = DateTime.UtcNow.AddSeconds(expiresIn - 120);

                        Debug.Log("[NeuroFlex AWS] Autenticación exitosa en Cognito.");
                        TokenRefreshed?.Invoke(AccessToken);
                        callback?.Invoke(true, AccessToken);
                    }
                    else
                    {
                        string msg = "Respuesta de Cognito sin AuthenticationResult: " + request.downloadHandler.text;
                        Debug.LogError("[NeuroFlex AWS] " + msg);
                        AuthenticationFailed?.Invoke(msg);
                        callback?.Invoke(false, msg);
                    }
                }
                catch (Exception ex)
                {
                    string msg = "Error parseando respuesta de Cognito: " + ex.Message;
                    Debug.LogError("[NeuroFlex AWS] " + msg);
                    AuthenticationFailed?.Invoke(msg);
                    callback?.Invoke(false, msg);
                }
            }
        }
    }
}
