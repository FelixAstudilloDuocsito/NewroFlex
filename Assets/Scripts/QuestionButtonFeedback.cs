using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public class QuestionButtonFeedback : MonoBehaviour
{
    [Header("Configuración de la Opción")]
    [Tooltip("Marca esto si este botón representa la respuesta correcta")]
    public bool esCorrecta = false;

    [Header("Colores de Feedback")]
    public Color colorOscuro = new Color(0.2f, 0.2f, 0.2f, 1f);   // Al presionar
    public Color colorCorrecto = new Color(0.15f, 0.8f, 0.25f, 1f); // Verde
    public Color colorIncorrecto = new Color(0.85f, 0.15f, 0.15f, 1f); // Rojo

    [Header("Tiempos")]
    [Tooltip("Tiempo breve en que se ve oscurecido")]
    public float tiempoOscuro = 0.15f;
    [Tooltip("Tiempo que permanece en verde/rojo para que el usuario asimile")]
    public float tiempoResultado = 1.2f;

    [Header("Eventos")]
    public UnityEvent onRespuestaCorrecta;
    public UnityEvent onRespuestaIncorrecta;

    private Button boton;
    private Image imagenBoton;
    private Color colorOriginal;
    private static bool bloqueadoGlobal = false; // Bloquea todos los botones mientras se evalúa uno

    private void Awake()
    {
        boton = GetComponent<Button>();
        imagenBoton = GetComponent<Image>();
        colorOriginal = imagenBoton.color;

        boton.onClick.AddListener(AlHacerClic);
    }

    private void OnEnable()
    {
        ResetearVisual();
        bloqueadoGlobal = false;
    }

    public void AlHacerClic()
    {
        if (bloqueadoGlobal) return;
        StartCoroutine(SecuenciaFeedback());
    }

    private IEnumerator SecuenciaFeedback()
    {
        bloqueadoGlobal = true;

        // 1. Oscurecer inmediatamente para dar sensación táctil
        imagenBoton.color = colorOscuro;
        yield return new WaitForSeconds(tiempoOscuro);

        // 2. Mostrar Verde o Rojo
        if (esCorrecta)
        {
            imagenBoton.color = colorCorrecto;
            yield return new WaitForSeconds(tiempoResultado);
            onRespuestaCorrecta?.Invoke();
        }
        else
        {
            imagenBoton.color = colorIncorrecto;
            yield return new WaitForSeconds(tiempoResultado);
            onRespuestaIncorrecta?.Invoke();
        }

        // 3. Restaurar estado base
        ResetearVisual();
        bloqueadoGlobal = false;
    }

    public void ResetearVisual()
    {
        if (imagenBoton != null)
        {
            imagenBoton.color = colorOriginal;
        }
    }
}