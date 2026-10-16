using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ImageSceneLoaderVR : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Escena a Cargar")]
    public string nombreEscena;

    [Header("Feedback Visual")]
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
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (usarEfectoHover && imagen != null)
            imagen.color = colorHover;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (usarEfectoHover && imagen != null)
            imagen.color = colorNormal;
    }

    // Dispara la carga inmediatamente al presionar con el rayo de VR o teclado
    public void OnPointerDown(PointerEventData eventData)
    {
        EjecutarCarga();
    }

    // Respaldo para el clic del mouse en el Editor de Unity
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

        try
        {
            SceneManager.LoadScene(nombreEscena);
        }
        catch (System.Exception e)
        {
            yaCargando = false;
            Debug.LogError($"No se pudo cargar '{nombreEscena}'. Revisa si está agregada en File > Build Settings.\nDetalle: {e.Message}");
        }
    }
}