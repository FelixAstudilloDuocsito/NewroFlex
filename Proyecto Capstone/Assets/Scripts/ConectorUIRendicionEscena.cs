using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ConectorUIRendicionEscena : MonoBehaviour
{
    public GameObject contenedorAviso;
    public TextMeshProUGUI textoEstado;
    public TextMeshProUGUI textoContador;
    public Image barraProgreso;

    private void Start()
    {
        RendirseJoysticksVR gestor = FindAnyObjectByType<RendirseJoysticksVR>();
        if (gestor != null)
        {
            gestor.RegistrarUIEnEscena(contenedorAviso, textoEstado, textoContador, barraProgreso);
        }
    }
}