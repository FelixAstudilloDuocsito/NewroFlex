using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class EscenarioButtonVR : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Configuración del Escenario")]
    [Tooltip("Nombre exacto de la escena a la que debe viajar")]
    public string nombreEscenaCargar;

    [Header("Referencias de UI")]
    [Tooltip("La imagen del borde/contenedor que cambiará de color")]
    public Image containerImage;
    public Color normalColor = Color.white;
    public Color highlightColor = Color.yellow;

    private void OnEnable()
    {
        if (containerImage != null)
            containerImage.color = normalColor;
    }

    // Resaltar borde cuando el rayo XR pasa por encima
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (containerImage != null)
            containerImage.color = highlightColor;
    }

    // Restaurar borde cuando el rayo sale del marco
    public void OnPointerExit(PointerEventData eventData)
    {
        if (containerImage != null)
            containerImage.color = normalColor;
    }

    // Al presionar con el gatillo del control VR sobre la foto
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!string.IsNullOrEmpty(nombreEscenaCargar))
        {
            SceneManager.LoadScene(nombreEscenaCargar);
        }
        else
        {
            Debug.LogWarning("¡No se ha asignado el nombre de la escena en el Inspector!");
        }
    }
}