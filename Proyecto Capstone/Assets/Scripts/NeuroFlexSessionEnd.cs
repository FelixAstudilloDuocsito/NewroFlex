using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeuroFlexVR
{
    public sealed class NeuroFlexSessionEnd : MonoBehaviour
    {
        [SerializeField] private string thankYouScene = "Gracias";

        public void ShowThankYouIfComplete()
        {
            if (NeuroFlexSessionProgress.AreAllScenesComplete())
                SceneManager.LoadScene(thankYouScene);
        }
    }
}
