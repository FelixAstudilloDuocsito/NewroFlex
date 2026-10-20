using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;

public class TutorialPanelController : MonoBehaviour
{
    [Header("Referencias")]
    public VideoPlayer videoPlayer;
    public GameObject panelCompleto;

    [Header("Eventos al cerrar")]
    [Tooltip("Evento que se dispara cuando el usuario confirma haber visto el tutorial")]
    public UnityEvent onTutorialCerrado;

    private void Start()
    {
        // Aseguramos que el video empiece a reproducirse
        if (videoPlayer != null)
        {
            videoPlayer.isLooping = true;
            videoPlayer.Play();
        }
    }

    // Este método se vincula al OnClick() del botón
    public void ConfirmarYCerrar()
    {
        // 1. Detener el video para liberar memoria/GPU
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }

        // 2. Ocultar o destruir el panel
        if (panelCompleto != null)
        {
            panelCompleto.SetActive(false);
        }

        // 3. Avisar al GameManager que inicie el juego
        onTutorialCerrado?.Invoke();
    }
}