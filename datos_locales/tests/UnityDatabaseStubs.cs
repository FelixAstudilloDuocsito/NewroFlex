// Only compiled by test_profiles.py, outside Assets. SQLite is the production implementation.
using System;
namespace UnityEngine
{
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { }
    }
    public static class Application
    {
        public static string dataPath;
        public static string persistentDataPath;
        public static bool CanStreamedLevelBeLoaded(string value) => true;
    }
    public static class Debug
    {
        public static void LogError(object message, object context = null) { Console.Error.WriteLine(message); }
        public static void LogWarning(object message) { Console.Error.WriteLine(message); }
    }
    public static class JsonUtility
    {
        public static string ToJson(object value) => new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(value);
        public static T FromJson<T>(string value) => new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<T>(value);
    }
}
