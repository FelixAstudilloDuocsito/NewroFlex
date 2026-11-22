using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using CircuitVRion; // Para acceder a GuiaMunecaPuzzleLibreria

public class MemoryQuestionManager : MonoBehaviour
{
    [Header("Referencias de UI")]
    public GameObject panelCanvas;
    public TextMeshProUGUI textoPregunta;
    public TextMeshProUGUI textoFeedback;
    public Button[] botonesOpciones; // 4 botones

    [Header("Ocultar Puzzle y Guía de Muñeca")]
    [Tooltip("El objeto que agrupa las piezas o el tablero para ocultarlo al preguntar")]
    public GameObject puzzleVisuales;
    [Tooltip("Componente de guía en la muñeca izquierda para ocultar/bloquear al preguntar y actualizar al avanzar")]
    public GuiaMunecaPuzzleLibreria guiaMuneca;
    [Tooltip("Nivel que corresponde a 'siguienteNivel' cuando se avance tras responder bien")]
    public GuiaMunecaPuzzleLibreria.NivelPuzzle nivelSiguienteGuia = GuiaMunecaPuzzleLibreria.NivelPuzzle.Intermedio;

    [Header("Configuración de la Pregunta")]
    [TextArea(2, 4)]
    public string pregunta = "¿De que color es el cielo del puzzle anterior?";
    public string[] opciones = new string[4] { "Rojo", "Azul", "Verde", "Morado" };
    [Tooltip("Índice de la respuesta correcta: 0 para la A, 1 para la B, 2 para la C, 3 para la D")]
    public int indiceCorrecto = 0;

    [Header("Transición de Nivel")]
    public GameObject nivelActual;
    public GameObject siguienteNivel;

    private bool respondidoCorrectamente = false;

    private void Start()
    {
        if (panelCanvas != null)
            panelCanvas.SetActive(false);
    }

    // Llamado al terminar el puzzle
    public void MostrarPreguntaConDelay(float delay)
    {
        StartCoroutine(RutinaMostrarPregunta(delay));
    }

    private IEnumerator RutinaMostrarPregunta(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Ocultar las piezas y el fondo antes de lanzar la pregunta
        if (puzzleVisuales != null)
        {
            puzzleVisuales.SetActive(false);
        }

        // Bloquear y ocultar la guía de la muñeca para evitar trampas durante la pregunta
        if (guiaMuneca != null)
        {
            guiaMuneca.AlMostrarPregunta();
        }

        if (textoPregunta != null) textoPregunta.text = pregunta;
        if (textoFeedback != null) textoFeedback.text = "";

        // Configurar textos de los botones y sus clics
        for (int i = 0; i < botonesOpciones.Length; i++)
        {
            if (i < opciones.Length)
            {
                TextMeshProUGUI btnTexto = botonesOpciones[i].GetComponentInChildren<TextMeshProUGUI>();
                if (btnTexto != null) btnTexto.text = opciones[i];

                int indiceSeleccionado = i;
                botonesOpciones[i].onClick.RemoveAllListeners();
                botonesOpciones[i].onClick.AddListener(() => Responder(indiceSeleccionado));
            }
        }

        if (panelCanvas != null)
            panelCanvas.SetActive(true);
    }

    public void Responder(int indiceElegido)
    {
        if (respondidoCorrectamente) return;

        if (indiceElegido == indiceCorrecto)
        {
            respondidoCorrectamente = true;
            if (textoFeedback != null)
            {
                textoFeedback.color = Color.green;
                textoFeedback.text = "¡Excelente memoria! Avanzando...";
            }

            StartCoroutine(AvanzarSiguienteNivel());
        }
        else
        {
            if (textoFeedback != null)
            {
                textoFeedback.color = new Color(1f, 0.4f, 0.4f); // Rojo suave
                textoFeedback.text = "No exactamente. ¡Intenta con otra opción!";
            }
        }
    }

    private IEnumerator AvanzarSiguienteNivel()
    {
        yield return new WaitForSeconds(2.5f);

        if (panelCanvas != null) panelCanvas.SetActive(false);
        if (nivelActual != null) nivelActual.SetActive(false);

        if (siguienteNivel != null)
        {
            siguienteNivel.SetActive(true);

            // Actualiza el sprite de la muñeca y vuelve a habilitar su visualización para el nuevo nivel
            if (guiaMuneca != null)
            {
                guiaMuneca.IniciarNivel(nivelSiguienteGuia);
            }
        }
    }
}