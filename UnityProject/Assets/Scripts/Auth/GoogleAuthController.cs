using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using RpsArena.Config;
using UnityEngine;
using UnityEngine.Networking;

namespace RpsArena.Auth
{
    [Serializable]
    public sealed class DingousSession
    {
        public string accessToken;
        public string expiresAtUtc;
        public long userId;
        public string displayName;
        public string email;
        public string pictureUrl;
        public string applicationCode;
        public string previousLoginAtUtc;
        public string lastLoginAtUtc;

        public bool IsValid
        {
            get
            {
                if (string.IsNullOrWhiteSpace(accessToken)) return false;
                return !DateTimeOffset.TryParse(expiresAtUtc, out var expires) ||
                       expires > DateTimeOffset.UtcNow.AddMinutes(1);
            }
        }
    }

    [Serializable]
    internal sealed class GoogleLoginRequest
    {
        public string idToken;
        public string applicationCode;
        public string language;
    }

    [Serializable]
    internal sealed class WebGoogleResult
    {
        public string requestId;
        public string idToken;
        public string error;
    }

    public sealed class GoogleAuthController : MonoBehaviour
    {
        private RpsGameConfig _config;
        private TaskCompletionSource<DingousSession> _pending;
        private string _attemptId;

        public event Action<DingousSession> LoggedIn;
        public event Action LoggedOut;

        public DingousSession Session { get; private set; }
        public string Token => Session?.IsValid == true ? Session.accessToken : string.Empty;
        public bool IsLoggedIn => Session?.IsValid == true;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void RpsGoogleWebSignIn(string clientId, string target, string requestId, string locale);
        [DllImport("__Internal")] private static extern void RpsGoogleWebSignInSilent(string clientId, string target, string requestId, string locale);
        [DllImport("__Internal")] private static extern void RpsGoogleWebCancel();
        [DllImport("__Internal")] private static extern void RpsGoogleWebSignOut();
#endif

        public void Initialize(RpsGameConfig config)
        {
            _config = config;
            gameObject.name = "RpsArena";
        }

        public async void Login()
        {
            try
            {
                var session = await StartSignInAsync(false);
                if (session?.IsValid == true) LoggedIn?.Invoke(session);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Login Google falhou: " + ex.Message);
            }
        }

        public async void TryRestore()
        {
            try
            {
                var session = await StartSignInAsync(true);
                if (session?.IsValid == true) LoggedIn?.Invoke(session);
            }
            catch (Exception ex)
            {
                Debug.Log("Login silencioso indisponível: " + ex.Message);
            }
        }

        public void Logout()
        {
            Session = null;
            _attemptId = null;
            var pending = _pending;
            _pending = null;
            pending?.TrySetResult(null);
#if UNITY_WEBGL && !UNITY_EDITOR
            try { RpsGoogleWebSignOut(); } catch { }
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var bridge = new AndroidJavaClass("com.dingous.rpsarena.auth.CredManBridge");
                bridge.CallStatic("clearCredentialState", activity);
            }
            catch { }
#endif
            LoggedOut?.Invoke();
        }

        private Task<DingousSession> StartSignInAsync(bool silent)
        {
            if (IsLoggedIn) return Task.FromResult(Session);
            if (_pending != null && !_pending.Task.IsCompleted) return _pending.Task;

            _pending = new TaskCompletionSource<DingousSession>();
            _attemptId = Guid.NewGuid().ToString("N");

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var bridge = new AndroidJavaClass("com.dingous.rpsarena.auth.CredManBridge");
                bridge.CallStatic(
                    silent ? "signInSilent" : "signInInteractive",
                    activity,
                    _config.GoogleWebClientId,
                    _attemptId);
            }
            catch (Exception ex)
            {
                CompleteError(ex.Message);
            }
#elif UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                if (silent)
                    RpsGoogleWebSignInSilent(
                        _config.GoogleWebClientId,
                        gameObject.name,
                        _attemptId,
                        Application.systemLanguage.ToString());
                else
                    RpsGoogleWebSignIn(
                        _config.GoogleWebClientId,
                        gameObject.name,
                        _attemptId,
                        Application.systemLanguage.ToString());
            }
            catch (Exception ex)
            {
                CompleteError(ex.Message);
            }
#elif UNITY_EDITOR
            StartCoroutine(EditorDevelopmentLogin(_pending));
#else
            _pending.TrySetResult(null);
#endif
            return _pending.Task;
        }

#if UNITY_EDITOR
        private IEnumerator EditorDevelopmentLogin(TaskCompletionSource<DingousSession> attempt)
        {
            var url = _config.ApiBaseUrl +
                      "/api/auth/development?applicationCode=" +
                      Uri.EscapeDataString(RpsGameConfig.ApplicationCode);

            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 15;
            yield return request.SendWebRequest();

            if (!ReferenceEquals(_pending, attempt)) yield break;
            if (request.result != UnityWebRequest.Result.Success)
            {
                CompleteError("Login de desenvolvimento falhou (HTTP " + request.responseCode + ").");
                yield break;
            }

            CompleteSession(request.downloadHandler.text);
        }
#endif

        public void OnGoogleIdToken(string callback)
        {
            if (_pending == null) return;

            if (!TryParseCallback(callback, out var attempt, out var token) ||
                !string.Equals(attempt, _attemptId, StringComparison.Ordinal))
                return;

            if (string.IsNullOrWhiteSpace(token))
            {
                CompleteError("Google retornou token vazio.");
                return;
            }

            StartCoroutine(ExchangeIdToken(token, _pending));
        }

        public void OnSignInError(string callback)
        {
            if (_pending == null) return;

            if (!TryParseCallback(callback, out var attempt, out var reason) ||
                !string.Equals(attempt, _attemptId, StringComparison.Ordinal))
                return;

            if (reason is "SilentFailed" or "NoCredential" or "UserCanceled")
            {
                var pending = _pending;
                _pending = null;
                _attemptId = null;
                pending.TrySetResult(null);
                return;
            }

            CompleteError(
                string.IsNullOrWhiteSpace(reason)
                    ? "Não foi possível entrar com Google."
                    : reason);
        }

        public void OnWebGoogleResult(string json)
        {
            if (_pending == null) return;

            WebGoogleResult result;
            try
            {
                result = JsonUtility.FromJson<WebGoogleResult>(json);
            }
            catch
            {
                CompleteError("Resposta inválida do Google no navegador.");
                return;
            }

            if (result == null ||
                !string.Equals(result.requestId, _attemptId, StringComparison.Ordinal))
                return;

            if (!string.IsNullOrWhiteSpace(result.error))
            {
                OnSignInError(result.requestId + "|" + result.error);
                return;
            }

            OnGoogleIdToken(result.requestId + "|" + result.idToken);
        }

        private IEnumerator ExchangeIdToken(
            string idToken,
            TaskCompletionSource<DingousSession> attempt)
        {
            var payload = JsonUtility.ToJson(new GoogleLoginRequest
            {
                idToken = idToken,
                applicationCode = RpsGameConfig.ApplicationCode,
                language = Application.systemLanguage.ToString()
            });

            using var request = new UnityWebRequest(
                _config.ApiBaseUrl + "/api/auth/google",
                UnityWebRequest.kHttpVerbPOST);

            request.uploadHandler =
                new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.timeout = 30;

            yield return request.SendWebRequest();

            if (!ReferenceEquals(_pending, attempt)) yield break;

            if (request.result != UnityWebRequest.Result.Success)
            {
                CompleteError(
                    "Dingous recusou o login Google (HTTP " +
                    request.responseCode +
                    ").");
                yield break;
            }

            CompleteSession(request.downloadHandler.text);
        }

        private void CompleteSession(string json)
        {
            try
            {
                var session = JsonUtility.FromJson<DingousSession>(json);
                if (session == null || !session.IsValid || session.userId <= 0)
                    throw new InvalidOperationException("Sessão Dingous inválida.");

                Session = session;
                var pending = _pending;
                _pending = null;
                _attemptId = null;
                pending?.TrySetResult(session);
            }
            catch (Exception ex)
            {
                CompleteError(ex.Message);
            }
        }

        private void CompleteError(string message)
        {
            var pending = _pending;
            _pending = null;
            _attemptId = null;

#if UNITY_WEBGL && !UNITY_EDITOR
            try { RpsGoogleWebCancel(); } catch { }
#endif

            pending?.TrySetException(
                new InvalidOperationException(message));
        }

        private static bool TryParseCallback(
            string value,
            out string attemptId,
            out string payload)
        {
            attemptId = null;
            payload = null;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            var separator = value.IndexOf('|');
            if (separator <= 0)
                return false;

            attemptId = value.Substring(0, separator);
            payload = separator + 1 < value.Length
                ? value.Substring(separator + 1)
                : string.Empty;

            return true;
        }
    }
}
