using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class BloqueadorClicsAlMostrar : MonoBehaviour
{
    [Header("Tiempo de Espera")]
    [Tooltip("Segundos de bloqueo al mostrarse el panel antes de permitir clics")]
    [Min(0f)]
    public float segundosBloqueo = 0.7f;

    [Header("Apariencia Gradual (Opcional)")]
    [Tooltip("Si está activo, hace un fade suave de opacidad durante el tiempo de bloqueo")]
    public bool fadeSuaveAlMostrar = true;

    [Header("Botones a Bloquear (Opcional)")]
    [Tooltip("Si se deja vacío, busca automáticamente los Button hijos")]
    public Button[] botonesObjetivo;
    public bool incluirHijosInactivos = true;

    private CanvasGroup canvasGroup;
    private Coroutine rutinaBloqueo;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (botonesObjetivo == null || botonesObjetivo.Length == 0)
        {
            botonesObjetivo = GetComponentsInChildren<Button>(incluirHijosInactivos);
        }
    }

    private void OnEnable()
    {
        if (rutinaBloqueo != null)
        {
            StopCoroutine(rutinaBloqueo);
        }

        rutinaBloqueo = StartCoroutine(RutinaBloqueoInicial());
    }

    private void OnDisable()
    {
        if (rutinaBloqueo != null)
        {
            StopCoroutine(rutinaBloqueo);
            rutinaBloqueo = null;
        }
    }

    private IEnumerator RutinaBloqueoInicial()
    {
        EstablecerInteraccion(false);

        if (segundosBloqueo <= 0f)
        {
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            EstablecerInteraccion(true);
            yield break;
        }

        float transcurrido = 0f;
        if (canvasGroup != null && fadeSuaveAlMostrar)
        {
            canvasGroup.alpha = 0f;
        }

        while (transcurrido < segundosBloqueo)
        {
            transcurrido += Time.unscaledDeltaTime;
            float progreso = Mathf.Clamp01(transcurrido / segundosBloqueo);

            if (canvasGroup != null && fadeSuaveAlMostrar)
            {
                canvasGroup.alpha = progreso;
            }

            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        EstablecerInteraccion(true);
        rutinaBloqueo = null;
    }

    private void EstablecerInteraccion(bool habilitar)
    {
        if (canvasGroup != null)
        {
            canvasGroup.interactable = habilitar;
            canvasGroup.blocksRaycasts = habilitar;
        }

        if (botonesObjetivo != null)
        {
            for (int i = 0; i < botonesObjetivo.Length; i++)
            {
                if (botonesObjetivo[i] != null)
                {
                    botonesObjetivo[i].interactable = habilitar;
                }
            }
        }
    }
}