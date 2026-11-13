using UnityEngine.SceneManagement;

namespace NeuroFlexVR
{
    public static class NeuroFlexSceneNavigation
    {
        public const string ReceptionScene = "Recepción";
        public const string ThankYouScene = "Gracias";

        public static void LeaveActivity()
        {
            SceneManager.LoadScene(NeuroFlexSessionProgress.AreAllScenesComplete()
                ? ThankYouScene
                : ReceptionScene);
        }
    }
}
