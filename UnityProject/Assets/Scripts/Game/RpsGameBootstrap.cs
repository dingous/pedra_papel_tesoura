using UnityEngine;

namespace RpsArena.Game
{
    public static class RpsGameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Main") return;
            if (Object.FindAnyObjectByType<RpsGameController>()) return;
            new GameObject("RpsArena").AddComponent<RpsGameController>();
        }
    }
}
