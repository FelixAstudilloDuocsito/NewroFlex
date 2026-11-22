using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class ImageSceneLoaderVR : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Escena a Cargar")]
    public string nombreEscena;
    [Tooltip("Requiere elegir un perfil en el panel de Recepción antes de entrar.")]
    public bool requierePerfil;

    [Header("Feedback de Texto")]
    [Tooltip("Arrastra aquí el componente TextMeshPro que mostrará el mensaje")]
    public TextMeshProUGUI textoMensaje;
    [Tooltip("Texto que se mostrará al presionar la foto/botón")]
    public string mensajeSeleccion = "Cargando escena seleccionada...";

    [Header("Configuración de Transición (Fade)")]
    [Tooltip("El GameObject del Canvas o Panel de la pantalla en negro.")]
    public GameObject objetoCanvasFade;
    [Tooltip("CanvasGroup del panel negro que subirá de alpha 0 a 1.")]
    public CanvasGroup panelFade;
    [Tooltip("Tiempo en segundos que tardará el fundido de 0 a 1.")]
    [Min(0.05f)]
    public float tiempoFade = 3.0f;
    [Tooltip("Usa tiempo real (no afectado por Time.timeScale).")]
    public bool usarTiempoReal = true;

    [Header("Feedback Visual del Botón")]
    public bool usarEfectoHover = true;
    public Color colorNormal = Color.white;
    public Color colorHover = new Color(0.85f, 0.85f, 0.85f, 1f);

    private Image imagen;
    private bool yaCargando = false;

    private void Awake()
    {
        imagen = GetComponent<Image>();
    }

    private void OnEnable()
    {
        if (imagen != null) imagen.color = colorNormal;
        yaCargando = false;

        if (textoMensaje != null)
        {
            textoMensaje.gameObject.SetActive(false);
        }

        if (panelFade != null)
        {
            panelFade.alpha = 0f;
            panelFade.blocksRaycasts = false;
            panelFade.interactable = false;
        }

        if (objetoCanvasFade != null)
        {
            objetoCanvasFade.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (usarEfectoHover && imagen != null && !yaCargando)
            imagen.color = colorHover;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (usarEfectoHover && imagen != null && !yaCargando)
            imagen.color = colorNormal;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        EjecutarCarga();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            EjecutarCarga();
        }
    }

    public void EjecutarCarga()
    {
        if (yaCargando) return;

        if (requierePerfil && NeuroFlexVR.PatientSession.ActiveProfile == null)
        {
            if (textoMensaje != null)
            {
                textoMensaje.text = "Crea o elige tu perfil en el panel detrás de ti.";
                textoMensaje.gameObject.SetActive(true);
            }
            return;
        }

        if (string.IsNullOrEmpty(nombreEscena))
        {
            Debug.LogError($"El campo 'Nombre Escena' está vacío en {gameObject.name}.");
            return;
        }

        yaCargando = true;
        StartCoroutine(TransicionYCargar());
    }

    private IEnumerator TransicionYCargar()
    {
        if (textoMensaje != null)
        {
            textoMensaje.text = mensajeSeleccion;
            textoMensaje.gameObject.SetActive(true);
        }

        if (objetoCanvasFade != null)
        {
            objetoCanvasFade.SetActive(true);
        }

        if (panelFade != null)
        {
            panelFade.gameObject.SetActive(true);
            panelFade.alpha = 0f;
            panelFade.blocksRaycasts = true;
            panelFade.interactable = false;

            float transcurrido = 0f;
            float duracion = Mathf.Max(0.05f, tiempoFade);

            while (transcurrido < duracion)
            {
                transcurrido += usarTiempoReal ? Time.unscaledDeltaTime : Time.deltaTime;
                panelFade.alpha = Mathf.Clamp01(transcurrido / duracion);
                yield return null;
            }

            panelFade.alpha = 1f;
        }
        else
        {
            Debug.LogWarning($"[ImageSceneLoaderVR] 'panelFade' (CanvasGroup) no está asignado en {gameObject.name}.");
            yield return usarTiempoReal ? new WaitForSecondsRealtime(tiempoFade) : new WaitForSeconds(tiempoFade);
        }

        AsyncOperation operacionCarga = SceneManager.LoadSceneAsync(nombreEscena);
        operacionCarga.allowSceneActivation = false;

        while (operacionCarga.progress < 0.9f)
        {
            yield return null;
        }

        yield return usarTiempoReal ? new WaitForSecondsRealtime(0.2f) : new WaitForSeconds(0.2f);
        operacionCarga.allowSceneActivation = true;
    }
}