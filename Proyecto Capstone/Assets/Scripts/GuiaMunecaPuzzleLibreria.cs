using UnityEngine;
using UnityEngine.UI;

namespace CircuitVRion
{
    public class GuiaMunecaPuzzleLibreria : MonoBehaviour
    {
        public enum NivelPuzzle
        {
            Facil,
            Intermedio,
            Dificil
        }

        public enum EjeMuneca
        {
            MenosY_AbajoAntebrazo,
            MasY_Arriba,
            MasZ_Adelante,
            MenosZ_Atras,
            MasX_Derecha,
            MenosX_Izquierda
        }

        public enum ReferenciaComparacion
        {
            ArribaCamara_Up,
            DerechaCamara_Right,
            Mundo_Up,
            HaciaCara_Dir
        }

        [Header("Referencias")]
        [SerializeField] private Transform camaraVR;
        [SerializeField] private Transform canvasGuia;
        [SerializeField] private Image imagenReferencia;

        [Header("Imágenes por Nivel")]
        [SerializeField] private NivelPuzzle nivelActual = NivelPuzzle.Facil;
        [SerializeField] private Sprite spriteFacil;
        [SerializeField] private Sprite spriteIntermedio;
        [SerializeField] private Sprite spriteDificil;

        [Header("Control de Estado (Anti-Trampa en Preguntas)")]
        [Tooltip("Se desactiva al pasar a la pregunta para evitar que el usuario vuelva a ver la imagen")]
        [SerializeField] private bool permitirVisualizacion = true;

        [Header("Detección del Giro de Muñeca")]
        [SerializeField] private EjeMuneca ejeDeMuneca = EjeMuneca.MenosY_AbajoAntebrazo;
        [SerializeField] private bool invertirEje = true;

        [Tooltip("Comparar contra ArribaCamara o Mundo_Up evita que mover la mano a los lados altere la alineación")]
        [SerializeField] private ReferenciaComparacion compararContra = ReferenciaComparacion.ArribaCamara_Up;

        [Range(-1f, 1f)]
        [SerializeField] private float umbralGiro = 0.55f;

        [Range(0.05f, 0.6f)]
        [SerializeField] private float margenApagado = 0.15f;

        [Range(15f, 90f)]
        [SerializeField] private float anguloMaximoFrenteCamara = 45f;

        [SerializeField] private float distanciaMaxima = 0.85f;
        [SerializeField] private float velocidadAnimacion = 12f;

        [Header("Lectura del Canvas")]
        [SerializeField] private bool forzarMirarCamara = false;

        [Header("Depuración")]
        [SerializeField] private bool mostrarDebugLog = false;
        [Range(1, 120)]
        [SerializeField] private int intervaloFrames = 20;

        private Vector3 escalaOriginalCanvas;
        private Quaternion rotacionOriginalCanvas;
        private CanvasGroup canvasGroup;
        private bool estaVisible = false;

        private void Awake()
        {
            if (camaraVR == null && Camera.main != null)
                camaraVR = Camera.main.transform;

            if (canvasGuia != null)
            {
                escalaOriginalCanvas = canvasGuia.localScale;
                if (escalaOriginalCanvas == Vector3.zero)
                    escalaOriginalCanvas = new Vector3(0.001f, 0.001f, 0.001f);

                rotacionOriginalCanvas = canvasGuia.localRotation;

                canvasGroup = canvasGuia.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = canvasGuia.gameObject.AddComponent<CanvasGroup>();

                canvasGuia.localScale = Vector3.zero;
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            AplicarSpriteNivelActual();
        }

        private void LateUpdate()
        {
            if (camaraVR == null || canvasGuia == null) return;

            if (!permitirVisualizacion)
            {
                estaVisible = false;
            }
            else
            {
                Vector3 origenMedicion = transform.position;
                Vector3 dirDesdeCamaraAMuneca = (origenMedicion - camaraVR.position).normalized;

                float distancia = Vector3.Distance(camaraVR.position, origenMedicion);
                float anguloFrenteCamara = Vector3.Angle(camaraVR.forward, dirDesdeCamaraAMuneca);

                Vector3 eje = ObtenerVectorEje();
                if (invertirEje) eje = -eje;

                Vector3 referencia = ObtenerVectorReferencia(origenMedicion);
                float alineacion = Vector3.Dot(eje.normalized, referencia.normalized);
                float umbralApagado = umbralGiro - margenApagado;

                bool dentroDeZona =
                    alineacion >= umbralGiro &&
                    distancia <= distanciaMaxima &&
                    anguloFrenteCamara <= anguloMaximoFrenteCamara;

                bool fueraDeZona =
                    alineacion < umbralApagado ||
                    distancia > distanciaMaxima + 0.15f ||
                    anguloFrenteCamara > anguloMaximoFrenteCamara + 10f;

                if (!estaVisible && dentroDeZona)
                    estaVisible = true;
                else if (estaVisible && fueraDeZona)
                    estaVisible = false;

                if (mostrarDebugLog && Time.frameCount % intervaloFrames == 0)
                {
                    Debug.Log($"[GuiaMuneca] Nivel={nivelActual} | Visible={estaVisible} | Alineacion={alineacion:F2} | Dist={distancia:F2}");
                }
            }

            Vector3 escalaObjetivo = estaVisible ? escalaOriginalCanvas : Vector3.zero;
            float alphaObjetivo = estaVisible ? 1f : 0f;

            canvasGuia.localScale = Vector3.Lerp(canvasGuia.localScale, escalaObjetivo, Time.deltaTime * velocidadAnimacion);
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, alphaObjetivo, Time.deltaTime * velocidadAnimacion);

            if (estaVisible)
            {
                if (forzarMirarCamara)
                {
                    canvasGuia.LookAt(
                        canvasGuia.position + (canvasGuia.position - camaraVR.position).normalized,
                        camaraVR.up
                    );
                }
                else
                {
                    canvasGuia.localRotation = Quaternion.Slerp(
                        canvasGuia.localRotation,
                        rotacionOriginalCanvas,
                        Time.deltaTime * velocidadAnimacion
                    );
                }
            }
        }

        // --- Métodos públicos para conectar con tu puzle ---

        public void IniciarNivel(NivelPuzzle nuevoNivel)
        {
            nivelActual = nuevoNivel;
            AplicarSpriteNivelActual();
            permitirVisualizacion = true;
        }

        public void IniciarNivelFacil() => IniciarNivel(NivelPuzzle.Facil);
        public void IniciarNivelIntermedio() => IniciarNivel(NivelPuzzle.Intermedio);
        public void IniciarNivelDificil() => IniciarNivel(NivelPuzzle.Dificil);

        /// <summary>
        /// Llama a este método al terminar el nivel y mostrar la pregunta.
        /// Oculta el panel y bloquea que vuelva a aparecer durante la pregunta.
        /// </summary>
        public void AlMostrarPregunta()
        {
            permitirVisualizacion = false;
            estaVisible = false;
        }

        /// <summary>
        /// Oculta el panel pero mantiene habilitado el giro para el nivel actual.
        /// </summary>
        public void OcultarTemporalmente()
        {
            estaVisible = false;
        }

        private void AplicarSpriteNivelActual()
        {
            if (imagenReferencia == null) return;

            imagenReferencia.sprite = nivelActual switch
            {
                NivelPuzzle.Facil => spriteFacil,
                NivelPuzzle.Intermedio => spriteIntermedio,
                NivelPuzzle.Dificil => spriteDificil,
                _ => spriteFacil
            };
        }

        private Vector3 ObtenerVectorEje()
        {
            switch (ejeDeMuneca)
            {
                case EjeMuneca.MasY_Arriba: return transform.up;
                case EjeMuneca.MasZ_Adelante: return transform.forward;
                case EjeMuneca.MenosZ_Atras: return -transform.forward;
                case EjeMuneca.MasX_Derecha: return transform.right;
                case EjeMuneca.MenosX_Izquierda: return -transform.right;
                default: return -transform.up;
            }
        }

        private Vector3 ObtenerVectorReferencia(Vector3 origenMedicion)
        {
            switch (compararContra)
            {
                case ReferenciaComparacion.DerechaCamara_Right: return camaraVR.right;
                case ReferenciaComparacion.Mundo_Up: return Vector3.up;
                case ReferenciaComparacion.HaciaCara_Dir: return (camaraVR.position - origenMedicion).normalized;
                default: return camaraVR.up;
            }
        }
    }
}