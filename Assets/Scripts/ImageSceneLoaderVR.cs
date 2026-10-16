using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro; // Necesario para TextMeshPro

public class ImageSceneLoaderVR : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Escena a Cargar")]
    public string nombreEscena;

    [Header("Feedback de Texto")]
    [Tooltip("Arrastra aquí el componente TextMeshPro que mostrará el mensaje")]
    public TextMeshProUGUI textoMensaje;
    [Tooltip("Texto que se mostrará al presionar la foto/botón")]
    public string mensajeSeleccion = "Cargando escena seleccionada...";

    [Header("Configuración de Transición")]
    [Tooltip("Panel o CanvasGroup que se oscurecerá al presionar")]
    public CanvasGroup panelFade;
    [Tooltip("Tiempo en segundos que tardará el fundido")]
    public float tiempoFade = 1.0f;

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

        // Asegurar que el mensaje comience oculto si está asignado
        if (textoMensaje != null)
        {
            textoMensaje.gameObject.SetActive(false);
        }

        // Asegurar que el panel de fundido comience invisible
        if (panelFade != null)
        {
            panelFade.alpha = 0f;
            panelFade.blocksRaycasts = false;
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
        // 1. Mostrar el texto de confirmación arriba
        if (textoMensaje != null)
        {
            textoMensaje.text = mensajeSeleccion;
            textoMensaje.gameObject.SetActive(true);
        }

        // 2. Fundido a Negro (Fade Out)
        if (panelFade != null)
        {
            panelFade.blocksRaycasts = true; // Bloquea nuevos clics
            float tiempoInicial = 0f;

            while (tiempoInicial < tiempoFade)
            {
                tiempoInicial += Time.deltaTime;
                panelFade.alpha = Mathf.Clamp01(tiempoInicial / tiempoFade);
                yield return null;
            }
            panelFade.alpha = 1f;
        }
        else
        {
            // Pausa breve si no usas panelFade para que el usuario alcance a ver el texto
            yield return new WaitForSeconds(0.8f);
        }

        // 3. Carga asíncrona de la escena en segundo plano
        AsyncOperation operacionCarga = SceneManager.LoadSceneAsync(nombreEscena);
        operacionCarga.allowSceneActivation = false;

        while (operacionCarga.progress < 0.9f)
        {
            yield return null;
        }

        yield return new WaitForSeconds(0.2f);
        operacionCarga.allowSceneActivation = true;
    }
}