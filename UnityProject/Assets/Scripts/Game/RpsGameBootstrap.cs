using UnityEngine;

namespace RpsArena.Game
{
    public static class RpsGameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            if (Object.FindAnyObjectByType<RpsGameController>()) return;
            new GameObject("RpsArena").AddComponent<RpsGameController>();
        }
    }
}
