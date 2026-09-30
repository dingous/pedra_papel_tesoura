using System;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using RpsArena.Config;
using RpsArena.Game;
using UnityEngine;
using UnityEngine.Networking;

namespace RpsArena.Networking
{
    public sealed class RpsNetworkClient : MonoBehaviour
    {
        private const char RecordSeparator = '\u001e';
        private RpsGameConfig _config;
        private IRealtimeTransport _transport;
        private TaskCompletionSource<bool> _ready;
        private bool _handshakeComplete;
        private readonly StringBuilder _receiveBuffer = new();

        public event Action Connected;
        public event Action<ServerMessage> ServerMessageReceived;
        public event Action<string> Disconnected;
        public bool IsConnected => _transport?.IsConnected == true && _handshakeComplete;

        public void Initialize(RpsGameConfig config)
        {
            _config = config;
#if UNITY_WEBGL && !UNITY_EDITOR
            _transport = new WebGlWebSocketTransport();
#else
            _transport = new NativeWebSocketTransport();
#endif
            _transport.Connected += OnTransportConnected;
            _transport.MessageReceived += OnRawMessage;
            _transport.Closed += reason => { _handshakeComplete = false; Disconnected?.Invoke(reason); };
        }

        public async Task ConnectAsync(string token)
        {
            if (IsConnected) return;
            _handshakeComplete = false;
            _ready = new TaskCompletionSource<bool>();
            var connectionToken = await NegotiateAsync(token);
            var ws = ToWebSocketUrl(_config.RpsHubUrl) + "?id=" + Uri.EscapeDataString(connectionToken) + "&access_token=" + Uri.EscapeDataString(token);
            await _transport.ConnectAsync(ws);
            await _ready.Task;
        }

        public Task JoinQueueAsync() => InvokeAsync("JoinQueue", "");
        public Task LeaveQueueAsync() => InvokeAsync("LeaveQueue", "");
        public Task PlayAsync(string matchId, int round, RpsChoice choice)
            => InvokeAsync("Play", "\"" + Escape(matchId) + "\"," + round + "," + (int)choice);

        public async Task DisconnectAsync()
        {
            _handshakeComplete = false;
            if (_transport is not null) await _transport.DisconnectAsync();
        }

        private async void OnTransportConnected()
        {
            try { await _transport.SendAsync("{\"protocol\":\"json\",\"version\":1}" + RecordSeparator); }
            catch (Exception ex) { _ready?.TrySetException(ex); }
        }

        private Task InvokeAsync(string target, string rawArguments)
        {
            if (!IsConnected) return Task.CompletedTask;
            var json = "{\"type\":1,\"target\":\"" + target + "\",\"arguments\":[" + rawArguments + "]}" + RecordSeparator;
            return _transport.SendAsync(json);
        }

        private Task<string> NegotiateAsync(string token)
        {
            var tcs = new TaskCompletionSource<string>();
            StartCoroutine(NegotiateCoroutine(token, tcs));
            return tcs.Task;
        }

        private IEnumerator NegotiateCoroutine(string token, TaskCompletionSource<string> tcs)
        {
            var url = _config.RpsHubUrl + "/negotiate?negotiateVersion=1&access_token=" + Uri.EscapeDataString(token);
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Accept", "application/json");
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                tcs.TrySetException(new InvalidOperationException("SignalR negotiate falhou (HTTP " + request.responseCode + ")."));
                yield break;
            }
            try
            {
                var response = JsonUtility.FromJson<SignalRNegotiateResponse>(request.downloadHandler.text);
                if (string.IsNullOrWhiteSpace(response?.connectionToken)) throw new InvalidOperationException("connectionToken ausente.");
                tcs.TrySetResult(response.connectionToken);
            }
            catch (Exception ex) { tcs.TrySetException(ex); }
        }

        private void OnRawMessage(string chunk)
        {
            if (string.IsNullOrEmpty(chunk)) return;
            _receiveBuffer.Append(chunk);
            while (true)
            {
                var all = _receiveBuffer.ToString();
                var i = all.IndexOf(RecordSeparator);
                if (i < 0) break;
                var frame = all.Substring(0, i);
                _receiveBuffer.Remove(0, i + 1);
                ProcessFrame(frame);
            }
        }

        private void ProcessFrame(string frame)
        {
            if (string.IsNullOrWhiteSpace(frame)) return;
            if (!_handshakeComplete)
            {
                if (frame.Contains("\"error\"")) { _ready?.TrySetException(new InvalidOperationException("Handshake SignalR recusado.")); return; }
                _handshakeComplete = true;
                _ready?.TrySetResult(true);
                Connected?.Invoke();
                return;
            }

            if (frame.Contains("\"type\":6")) return;
            var target = ReadJsonString(frame, "target");
            if (string.IsNullOrWhiteSpace(target)) return;
            var payloadJson = ExtractFirstArgumentObject(frame);
            var payload = string.IsNullOrWhiteSpace(payloadJson) ? new ServerPayload() : JsonUtility.FromJson<ServerPayload>(payloadJson);
            var type = target switch
            {
                "QueueJoined" => "queue_joined",
                "MatchFound" => "match_found",
                "RoundStarted" => "round_started",
                "ChoiceLocked" => "choice_locked",
                "RoundResult" => "round_result",
                "MatchFinished" => "match_finished",
                "OpponentDisconnected" => "opponent_disconnected",
                _ => target
            };
            ServerMessageReceived?.Invoke(new ServerMessage { type = type, payload = payload ?? new ServerPayload() });
        }

        private static string ExtractFirstArgumentObject(string json)
        {
            const string marker = "\"arguments\":[";
            var start = json.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return null;
            start += marker.Length;
            while (start < json.Length && char.IsWhiteSpace(json[start])) start++;
            if (start >= json.Length || json[start] != '{') return null;
            var depth = 0;
            var inString = false;
            var escaped = false;
            for (var i = start; i < json.Length; i++)
            {
                var c = json[i];
                if (inString)
                {
                    if (escaped) { escaped = false; continue; }
                    if (c == '\\') { escaped = true; continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') { inString = true; continue; }
                if (c == '{') depth++;
                else if (c == '}' && --depth == 0) return json.Substring(start, i - start + 1);
            }
            return null;
        }

        private static string ReadJsonString(string json, string property)
        {
            var marker = "\"" + property + "\":\"";
            var start = json.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return null;
            start += marker.Length;
            var end = start;
            var escaped = false;
            while (end < json.Length)
            {
                var c = json[end];
                if (!escaped && c == '"') break;
                escaped = !escaped && c == '\\';
                if (c != '\\') escaped = false;
                end++;
            }
            return end <= json.Length ? json.Substring(start, end - start) : null;
        }

        private static string ToWebSocketUrl(string url)
        {
            if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return "wss://" + url.Substring(8);
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return "ws://" + url.Substring(7);
            return url;
        }

        private static string Escape(string value) => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        private void Update() => _transport?.Tick();

#if UNITY_WEBGL && !UNITY_EDITOR
        public void OnWsOpen(string _) => (_transport as WebGlWebSocketTransport)?.OnOpen();
        public void OnWsMessage(string message) => (_transport as WebGlWebSocketTransport)?.OnMessage(message);
        public void OnWsClose(string reason) => (_transport as WebGlWebSocketTransport)?.OnClose(reason);
#endif

        private async void OnDestroy() => await DisconnectAsync();
    }
}
