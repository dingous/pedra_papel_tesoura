using UnityEngine;

namespace RpsArena.Config
{
    public sealed class RpsGameConfig : MonoBehaviour
    {
        public const string ApplicationCode = "rps-arena";

        [Tooltip("URL base do Dingous/ChatTrade, sem barra final.")]
        public string apiBaseUrl = "https://www.dingous.com.br";

        [Tooltip("Google OAuth Web Client ID público aceito pelo backend Dingous.")]
        public string googleWebClientId = "922658167645-a7t0rp57r3uratqnrv25j6m9ouukiqti.apps.googleusercontent.com";

        public string ApiBaseUrl => (apiBaseUrl ?? string.Empty).TrimEnd('/');
        public string GoogleWebClientId => (googleWebClientId ?? string.Empty).Trim();
        public string RpsHubUrl => ApiBaseUrl + "/hubs/rps";
    }
}
