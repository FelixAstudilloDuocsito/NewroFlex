using UnityEngine;
using UnityEngine.SceneManagement; // Necesario para cambiar de escena

namespace NeuroFlexVR
{
    public class ProgresionNivelesPalomas : MonoBehaviour
    {
        public PigeonPoolSpawner poolSpawner;
        public PigeonRoundController roundController;

        [Header("Configuración de Niveles")]
        [Range(1, 3)] public int nivelActual = 1;

        [Header("Escena de Recepción")]
        [Tooltip("Nombre exacto de tu escena de la recepción o menú principal")]
        public string nombreEscenaRecepcion = "Recepción"; 

        // Método que se ejecuta al confirmar la respuesta de una ronda
        public void AvanzarSiguienteNivel()
        {
            nivelActual++;

            if (nivelActual <= 3)
            {
                // 1. Si aún estamos en el nivel 2 o 3, aumentamos la dificultad y reiniciamos la ronda
                if (poolSpawner != null)
                {
                    poolSpawner.SelectDifficulty(nivelActual);
                    Debug.Log("[NeuroFlex] Avanzando automáticamente al Nivel: " + nivelActual);
                }

                if (roundController != null)
                {
                    roundController.StartRound();
                }
            }
            else
            {
                // 2. Si ya pasó del nivel 3, el minijuego terminó por completo
                Debug.Log("[NeuroFlex] ¡Completados los 3 niveles! Volviendo a la recepción...");
                VolverARecepcion();
            }
        }

        public void VolverARecepcion()
        {
            // Carga la escena de la recepción (asegúrate de que esté agregada en Build Settings)
            SceneManager.LoadScene(nombreEscenaRecepcion);
        }
    }
}