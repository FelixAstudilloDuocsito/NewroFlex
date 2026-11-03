// Colocar este archivo en una carpeta llamada Editor. No se incluye en builds.
using UnityEditor;
using UnityEngine;
using NeuroFlexVR;

[CustomEditor(typeof(PigeonRoundController))]
[CanEditMultipleObjects]
public class PigeonRoundControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawScriptField();
        EditorGUILayout.Space(9);
        EditorGUILayout.LabelField("Identificación y protocolo", EditorStyles.boldLabel);
        DrawField("participantId", "ID del participante");
        DrawField("scenarioVersion", "Versión del escenario");
        DrawField("assessmentMode", "Modo de evaluación");
        DrawField("comparisonGroupId", "Grupo de comparación");
        DrawField("difficultyLevel", "Nivel de dificultad");
        EditorGUILayout.Space(9);
        EditorGUILayout.LabelField("Palomas y dificultad", EditorStyles.boldLabel);
        DrawField("poolSpawner", "Generador de palomas");
        DrawField("pigeons", "Palomas disponibles");
        DrawField("pigeonCount", "Cantidad de palomas");
        DrawField("movementLevel", "Nivel de movimiento");
        DrawField("displaySeconds", "Tiempo de observación (s)");
        DrawField("retentionDelaySeconds", "Demora para responder (s)");
        DrawField("maximumAnswer", "Respuesta máxima");
        EditorGUILayout.Space(9);
        EditorGUILayout.LabelField("Comida y lanzamiento", EditorStyles.boldLabel);
        DrawField("foodPanel", "Panel de comida");
        DrawField("flyingFood", "Comida en vuelo");
        DrawField("foodThrowOrigin", "Origen del lanzamiento");
        DrawField("foodTarget", "Destino de la comida");
        DrawField("foodOnGround", "Comida en el suelo");
        DrawField("foodFlightSeconds", "Duración del lanzamiento (s)");
        DrawField("foodArcHeight", "Altura del arco");
        EditorGUILayout.Space(9);
        EditorGUILayout.LabelField("Distracciones opcionales", EditorStyles.boldLabel);
        DrawField("distractionsEnabled", "Activar distracciones");
        DrawField("distractorType", "Tipo de distracción");
        DrawField("distractorIntensity", "Intensidad de distracción");
        DrawField("distractorStartsAtSecond", "Inicio de distracción (s)");
        DrawField("distractorDurationSeconds", "Duración de distracción (s)");
        DrawField("visualDistractor", "Objeto distractor visual");
        DrawField("audioSource", "Fuente de audio");
        DrawField("distractorAudio", "Sonido distractor");
        EditorGUILayout.Space(9);
        EditorGUILayout.LabelField("Interfaz VR: paneles y textos", EditorStyles.boldLabel);
        DrawField("startPanel", "Panel de inicio");
        DrawField("observingPanel", "Panel de observación");
        DrawField("answerPanel", "Panel de respuesta");
        DrawField("resultPanel", "Panel de resultados");
        DrawField("statusText", "Texto de estado");
        DrawField("answerNumberText", "Texto del contador");
        DrawField("resultText", "Texto de resultado (resumen)");
        EditorGUILayout.Space(9);
        EditorGUILayout.LabelField("Resultados: textos independientes", EditorStyles.boldLabel);
        DrawField("actualCountText", "Texto de cantidad real");
        DrawField("userCountText", "Texto de respuesta");
        DrawField("errorCountText", "Texto de diferencia");
        DrawField("scoreText", "Texto de puntaje");
        DrawField("responseTimeText", "Texto de tiempo");
        DrawField("actualCountPrefix", "Prefijo de cantidad real");
        DrawField("scoreColor", "Color del puntaje");
        DrawField("perfectScoreColor", "Color del puntaje perfecto");
        EditorGUILayout.Space(9);
        EditorGUILayout.LabelField("Resultados: imágenes, títulos y sonidos", EditorStyles.boldLabel);
        DrawField("resultImage", "Imagen de resultado (UI)");
        DrawField("resultImageLow", "Trofeo: nivel bajo");
        DrawField("resultImageMedium", "Trofeo: nivel medio");
        DrawField("resultImageHigh", "Trofeo: nivel alto");
        DrawField("resultImageExcellent", "Trofeo: nivel excelente");
        DrawField("resultTitleText", "Texto del título");
        DrawField("titleLow", "Título: nivel bajo");
        DrawField("titleMedium", "Título: nivel medio");
        DrawField("titleHigh", "Título: nivel alto");
        DrawField("titleExcellent", "Título: nivel excelente");
        DrawField("titleIncomplete", "Título: ronda incompleta");
        DrawField("resultAudioSource", "Fuente de sonido de resultados");
        DrawField("victorySound", "Sonido de victoria");
        DrawField("perfectSound", "Sonido de puntaje perfecto");
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawScriptField()
    {
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((PigeonRoundController)target), typeof(MonoScript), false);
    }

    private void DrawField(string propertyName, string label)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null) return;

        GUIContent guiLabel = new GUIContent(label, property.tooltip);
        string[] translatedOptions = null;
        switch (propertyName)
        {
            case "assessmentMode": translatedOptions = new[] { "Entrenamiento", "Comparación" }; break;
            case "movementLevel": translatedOptions = new[] { "Estático", "Bajo", "Medio", "Alto" }; break;
            case "distractorType": translatedOptions = new[] { "Ninguna", "Visual", "Auditiva", "Mixta" }; break;
            case "distractorIntensity": translatedOptions = new[] { "Ninguna", "Baja", "Media", "Alta" }; break;
        }

        if (property.propertyType == SerializedPropertyType.Enum && translatedOptions != null &&
            property.enumDisplayNames.Length == translatedOptions.Length)
        {
            // Solo cambia la etiqueta visible. Conserva el valor serializado del enum.
            property.enumValueIndex = EditorGUILayout.Popup(guiLabel, property.enumValueIndex, translatedOptions);
        }
        else
        {
            EditorGUILayout.PropertyField(property, guiLabel, true);
        }
    }
}
