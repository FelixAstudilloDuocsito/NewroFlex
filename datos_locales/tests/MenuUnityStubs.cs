// A small UI event/tree host for testing production menu callbacks without a running Editor.
// It does not simulate raycasts or XR hardware.
using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine
{
    public class Object { public virtual string name { get; set; } }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public override string name { get => gameObject.name; set => gameObject.name = value; }
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component => gameObject.GetComponentsInChildren<T>(includeInactive);
        public T GetComponentInChildren<T>(bool includeInactive = false) where T : Component => GetComponentsInChildren<T>(includeInactive).FirstOrDefault();
    }
    public class MonoBehaviour : Component { public bool enabled = true; }
    public class Transform : Component
    {
        public Transform parent;
        public readonly List<Transform> children = new List<Transform>();
        public Transform Find(string value) => children.FirstOrDefault(t => t.name == value);
        public void SetParent(Transform target)
        {
            parent?.children.Remove(this); parent = target; target?.children.Add(this);
        }
    }
    public class RectTransform : Transform { }
    public class GameObject : Object
    {
        public readonly List<Component> components = new List<Component>();
        public Transform transform;
        public bool activeSelf = true;
        public SceneManagement.Scene scene;
        public GameObject(string value) { name = value; }
        public void SetActive(bool value) { activeSelf = value; }
        public T GetComponentInChildren<T>(bool includeInactive = false) where T : Component => GetComponentsInChildren<T>(includeInactive).FirstOrDefault();
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component
        {
            var result = new List<T>();
            if (!includeInactive && !activeSelf) return result.ToArray();
            result.AddRange(components.OfType<T>());
            foreach (var child in transform.children) result.AddRange(child.gameObject.GetComponentsInChildren<T>(includeInactive));
            return result.ToArray();
        }
    }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string value) { } }
    public class DefaultExecutionOrderAttribute : Attribute { public DefaultExecutionOrderAttribute(int value) { } }
    public static class Mathf { public static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(value, max)); }
}
namespace UnityEngine.Events
{
    public delegate void UnityAction();
    public class UnityEvent
    {
        private readonly List<UnityAction> actions = new List<UnityAction>();
        public void AddListener(UnityAction action) { actions.Add(action); }
        public void Invoke() { foreach (var action in actions.ToArray()) action(); }
    }
}
namespace UnityEngine.UI
{
    public class Graphic : UnityEngine.Component { public bool enabled = true; }
    public class Button : UnityEngine.Component
    {
        public bool interactable = true;
        public UnityEngine.Events.UnityEvent onClick = new UnityEngine.Events.UnityEvent();
    }
}
namespace TMPro
{
    public class TMP_Text : UnityEngine.UI.Graphic { public string text = ""; }
    public class TMP_InputField : UnityEngine.Component { public string text = ""; public int characterLimit = 40; }
}
namespace UnityEngine.SceneManagement
{
    public class Scene
    {
        public readonly List<UnityEngine.GameObject> objects = new List<UnityEngine.GameObject>();
        public UnityEngine.GameObject[] GetRootGameObjects() => objects.Where(go => go.transform.parent == null).ToArray();
    }
    public static class SceneManager
    {
        public static string lastLoaded;
        public static bool CanStreamedLevelBeLoaded(string value) => true;
        public static void LoadScene(string value) { lastLoaded = value; }
        public static void LoadSceneAsync(string value) { lastLoaded = value; }
    }
}
namespace NeuroFlexVR { public class PigeonDifficultyHighlight : UnityEngine.UI.Graphic { } }
public class MemoryGameManager : UnityEngine.Component
{
    public bool esperarTutorial;
    public int difficultyLevel;
    public int PairsFound = 6, TotalPairs = 6, IncorrectAttempts = 2;
    public float ActiveSeconds = 25;
    public int starts;
    public UnityEngine.Events.UnityEvent onGameWon = new UnityEngine.Events.UnityEvent();
    public void IniciarJuego() { starts++; }
}
public class TutorialPanelController : UnityEngine.Component
{
    public class Player { public void Play() { } }
    public Player videoPlayer;
    public UnityEngine.Events.UnityEvent onTutorialCerrado = new UnityEngine.Events.UnityEvent();
}
