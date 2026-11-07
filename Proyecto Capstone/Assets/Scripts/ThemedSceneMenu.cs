using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NeuroFlexVR
{
    /// <summary>References to editable scene UI. Appearance is authored in the scene.</summary>
    public sealed class ThemedSceneMenu : MonoBehaviour
    {
        public RectTransform panel;
        public TMP_Text title;
        public TMP_Text description;
        public Button primaryButton;
        public Button[] optionButtons;

        // Recover references after an Editor script reload without creating or moving UI.
        public void RecoverReferences()
        {
            if (panel == null) panel = transform.Find(name.Replace("Canvas", "Panel")) as RectTransform;
            if (title == null) title = SceneMenuReferences.Find<TMP_Text>(this, "Titulo", this);
            if (description == null)
                description = SceneMenuReferences.Find<TMP_Text>(this, "Descripcion", this) ??
                    SceneMenuReferences.Find<TMP_Text>(this, "ResumenDeResultados", this);
            if (primaryButton == null)
            {
                string buttonName = name == "RecepcionMenuCanvas" ? "GestionarPerfiles" :
                    name == "MemoriaMenuCanvas" ? "Iniciar" :
                    name == "ResultadosMemoriaCanvas" ? "VolverAlInicio" : "VolverARecepcion";
                primaryButton = SceneMenuReferences.Find<Button>(this, buttonName, this);
            }
            if (optionButtons == null || optionButtons.Length == 0)
            {
                string[] names = name == "RecepcionMenuCanvas"
                    ? new[] { "Actividad1", "Actividad2", "Actividad3" }
                    : name == "MemoriaMenuCanvas" ? new[] { "Fácil", "Medio", "Difícil" } : new string[0];
                optionButtons = new Button[names.Length];
                for (int i = 0; i < names.Length; i++)
                    optionButtons[i] = SceneMenuReferences.Find<Button>(this, names[i], this);
            }
        }

        public static void ReplaceAction(Button button, UnityAction action)
        {
            if (button == null) return;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
        }

        public static void Bind(Button button, UnityAction action)
        {
            if (button != null && action != null) button.onClick.AddListener(action);
        }
    }

    internal static class SceneMenuReferences
    {
        public static T Find<T>(Component owner, string name, Component scope = null) where T : Component
        {
            if (scope != null)
            {
                foreach (T component in scope.GetComponentsInChildren<T>(true))
                    if (component.name == name) return component;
                return null;
            }
            foreach (GameObject root in owner.gameObject.scene.GetRootGameObjects())
                foreach (T component in root.GetComponentsInChildren<T>(true))
                    if (component.name == name) return component;
            return null;
        }

        public static bool Complete<T>(T[] references, int count) where T : Object
        {
            return references != null && references.Length == count &&
                !System.Array.Exists(references, reference => reference == null);
        }
    }
}
