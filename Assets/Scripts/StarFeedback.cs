using System.Collections;
using UnityEngine;

public class StarFeedback : MonoBehaviour
{
    public float duracion = 1.0f;
    public float distancia = 0.45f;
    
    [Header("Corrección del Modelo")]
    [Tooltip("Compensa si el modelo original fue diseñado acostado")]
    public Vector3 compensacionRotacion = new Vector3(90f, 0f, 0f);

    private Vector3 direccionVuelo = Vector3.up;

    private void Start()
    {
        // Se destruye de forma obligatoria tras cumplirse el tiempo
        Destroy(gameObject, duracion);
    }

    public void IniciarEfecto(Vector3 direccion)
    {
        direccionVuelo = direccion;
        StartCoroutine(AnimarEstrella());
    }

    private IEnumerator AnimarEstrella()
    {
        Vector3 posInicial = transform.position;
        Vector3 posFinal = posInicial + (direccionVuelo.normalized * distancia);
        Vector3 escalaInicial = transform.localScale;

        Camera cam = Camera.main;
        float tiempo = 0f;

        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;
            float t = Mathf.Clamp01(tiempo / duracion);

            // 1. Movimiento en abanico
            transform.position = Vector3.Lerp(posInicial, posFinal, Mathf.SmoothStep(0f, 1f, t));

            // 2. Encoger progresivamente
            transform.localScale = Vector3.Lerp(escalaInicial, Vector3.zero, t);

            // 3. Orientar de frente a la cámara con la compensación de 90°
            if (cam != null)
            {
                Quaternion rotHaciaCamara = Quaternion.LookRotation(transform.position - cam.transform.position);
                transform.rotation = rotHaciaCamara * Quaternion.Euler(compensacionRotacion);
            }

            yield return null;
        }

        transform.localScale = Vector3.zero;
    }
}