using UnityEngine;

public class TerminosSesionController : MonoBehaviour
{
    [Header("Referencias de UI")]
    [Tooltip("Panel de Términos y Condiciones")]
    public GameObject panelTerminos;

    [Tooltip("Contenedor de los botones de escenarios (o panel principal de Recepción)")]
    public GameObject contenedorEscenarios;

    // Vive durante toda la ejecución de la app; se reinicia al cerrar y reabrir la app
    private static bool terminosAceptadosEnEstaEjecucion = false;

    private void Start()
    {
        if (terminosAceptadosEnEstaEjecucion)
        {
            MostrarEscenariosDirecto();
        }
        else
        {
            MostrarTerminos();
        }
    }

    public void MostrarTerminos()
    {
        if (panelTerminos != null) panelTerminos.SetActive(true);
        if (contenedorEscenarios != null) contenedorEscenarios.SetActive(false);
    }

    // Conecta este método al botón "Confirmar" de Términos y Condiciones
    public void ConfirmarTerminos()
    {
        terminosAceptadosEnEstaEjecucion = true;
        MostrarEscenariosDirecto();
    }

    private void MostrarEscenariosDirecto()
    {
        if (panelTerminos != null) panelTerminos.SetActive(false);
        if (contenedorEscenarios != null) contenedorEscenarios.SetActive(true);
    }

    // Opcional: útil si quieres probar el flujo en el Editor sin reiniciar todo
    public static void ResetearEstadoSesion()
    {
        terminosAceptadosEnEstaEjecucion = false;
    }

    #if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void RegistrarReinicioEditor()
    {
        UnityEditor.EditorApplication.playModeStateChanged += estado =>
        {
            if (estado == UnityEditor.PlayModeStateChange.EnteredEditMode)
            {
                terminosAceptadosEnEstaEjecucion = false;
            }
        };
    }
    #endif
}