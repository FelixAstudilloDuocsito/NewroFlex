using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using TMPro;

public class RendirseJoysticksVR : MonoBehaviour
{
    public enum FormatoContador
    {
        SegundosRestantes,
        Porcentaje,
        SoloTexto
    }

    [Header("Configuración de Salida")]
    [Tooltip("Nombre exacto de la escena de Recepción a la que volverá")]
    public string nombreEscenaRecepcion = "Recepcion";

    [Tooltip("Segundos que se deben mantener presionados ambos joysticks a la vez")]
    [Min(0.2f)]
    public float tiempoMantener = 1.2f;

    [Tooltip("Evita que se active en la escena de Recepción si ya estás ahí")]
    public bool ignorarEnRecepcion = true;

    [Header("Feedback Visual (Opcional)")]
    [Tooltip("Contenedor o panel del aviso de rendición (se oculta cuando no se está rindiendo)")]
    public GameObject contenedorAviso;

    [Tooltip("Texto TMP para mostrar el mensaje (ej. 'Rindiéndose...')")]
    public TextMeshProUGUI textoEstado;

    [Tooltip("Texto TMP para mostrar el contador (ej. '1.2s' o '85%')")]
    public TextMeshProUGUI textoContador;

    [Tooltip("Barra opcional de tipo Image (Image Type = Filled) para progreso visual")]
    public Image barraProgreso;

    public string mensajeRindiendose = "Rindiéndose...";
    public FormatoContador formatoContador = FormatoContador.SegundosRestantes;

    [Header("Persistencia")]
    public bool persistirEntreEscenas = true;

    private static RendirseJoysticksVR instancia;
    private float tiempoAcumulado = 0f;
    private bool yaRindiendose = false;

    private readonly List<InputDevice> dispositivosIzquierdos = new List<InputDevice>();
    private readonly List<InputDevice> dispositivosDerechos = new List<InputDevice>();

    private void Awake()
    {
        if (persistirEntreEscenas)
        {
            if (instancia != null && instancia != this)
            {
                Destroy(gameObject);
                return;
            }
            instancia = this;
            DontDestroyOnLoad(gameObject);
        }

        OcultarFeedbackVisual();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnEscenaCargada;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnEscenaCargada;
    }

    private void OnEscenaCargada(Scene escena, LoadSceneMode modo)
    {
        yaRindiendose = false;
        tiempoAcumulado = 0f;
        OcultarFeedbackVisual();
    }

    private void Update()
    {
        if (yaRindiendose) return;

        if (ignorarEnRecepcion && SceneManager.GetActiveScene().name == nombreEscenaRecepcion)
        {
            ReiniciarEstado();
            return;
        }

        if (AmbosJoysticksPresionados())
        {
            tiempoAcumulado += Time.unscaledDeltaTime;
            float progreso = Mathf.Clamp01(tiempoAcumulado / tiempoMantener);

            ActualizarFeedbackVisual(progreso, tiempoAcumulado);

            if (tiempoAcumulado >= tiempoMantener)
            {
                yaRindiendose = true;
                ActualizarFeedbackVisual(1f, tiempoMantener);
                EjecutarRendicion();
            }
        }
        else
        {
            ReiniciarEstado();
        }
    }

    private void ReiniciarEstado()
    {
        if (tiempoAcumulado > 0f)
        {
            tiempoAcumulado = 0f;
            OcultarFeedbackVisual();
        }
    }

    private void ActualizarFeedbackVisual(float progreso, float acumulado)
    {
        if (contenedorAviso != null && !contenedorAviso.activeSelf)
        {
            contenedorAviso.SetActive(true);
        }

        if (textoEstado != null)
        {
            if (!textoEstado.gameObject.activeSelf) textoEstado.gameObject.SetActive(true);
            textoEstado.text = mensajeRindiendose;
        }

        if (textoContador != null)
        {
            if (!textoContador.gameObject.activeSelf) textoContador.gameObject.SetActive(true);

            switch (formatoContador)
            {
                case FormatoContador.SegundosRestantes:
                    float restante = Mathf.Max(0f, tiempoMantener - acumulado);
                    textoContador.text = $"{restante:0.0}s";
                    break;

                case FormatoContador.Porcentaje:
                    int pct = Mathf.RoundToInt(progreso * 100f);
                    textoContador.text = $"{pct}%";
                    break;

                case FormatoContador.SoloTexto:
                    textoContador.text = string.Empty;
                    break;
            }
        }

        if (barraProgreso != null)
        {
            barraProgreso.fillAmount = progreso;
        }
    }

    private void OcultarFeedbackVisual()
    {
        if (barraProgreso != null)
        {
            barraProgreso.fillAmount = 0f;
        }

        if (textoContador != null)
        {
            textoContador.text = string.Empty;
            textoContador.gameObject.SetActive(false);
        }

        if (textoEstado != null)
        {
            textoEstado.gameObject.SetActive(false);
        }

        if (contenedorAviso != null)
        {
            contenedorAviso.SetActive(false);
        }
    }

    private bool AmbosJoysticksPresionados()
    {
        InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, dispositivosIzquierdos);
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, dispositivosDerechos);

        bool izqPresionado = EstaStickPresionado(dispositivosIzquierdos);
        bool derPresionado = EstaStickPresionado(dispositivosDerechos);

        return izqPresionado && derPresionado;
    }

    private bool EstaStickPresionado(List<InputDevice> dispositivos)
    {
        for (int i = 0; i < dispositivos.Count; i++)
        {
            if (!dispositivos[i].isValid) continue;

            if (dispositivos[i].TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool presionado) && presionado)
            {
                return true;
            }
        }
        return false;
    }

    // Permite que un helper local en cada escena registre su UI al cargarse la escena
    public void RegistrarUIEnEscena(GameObject aviso, TextMeshProUGUI estado, TextMeshProUGUI contador, Image barra = null)
    {
        contenedorAviso = aviso;
        textoEstado = estado;
        textoContador = contador;
        barraProgreso = barra;
        OcultarFeedbackVisual();
    }

    private void EjecutarRendicion()
    {
        Debug.Log("[RendirseJoysticksVR] Ambos joysticks mantenidos. Volviendo a Recepción...");

        if (!string.IsNullOrEmpty(nombreEscenaRecepcion))
        {
            SceneManager.LoadScene(nombreEscenaRecepcion);
        }
        else
        {
            Debug.LogError("[RendirseJoysticksVR] Falta asignar 'nombreEscenaRecepcion'.");
        }
    }
}