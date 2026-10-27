using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using RpsArena.Config;
using RpsArena.Game;
using UnityEngine;
using UnityEngine.Networking;

namespace RpsArena.Networking
{
    [Serializable] public sealed class HubFrame
    { public int type; public string target, invocationId, error; public ServerPayload[] arguments; }
    public sealed class RpsNetworkClient : MonoBehaviour
    {
        private const char Separator='\u001e';
        private RpsGameConfig _config;
        private IRealtimeTransport _transport;
        private TaskCompletionSource<bool> _ready;
        private readonly Dictionary<string,TaskCompletionSource<bool>> _invocations=new();
        private readonly StringBuilder _buffer=new();
        private int _generation, _sequence;
        private long _lastEventId;
        private bool _handshake;
        private float _lastReceived, _lastPing;
        public event Action Connected;
        public event Action<ServerMessage> ServerMessageReceived;
        public event Action<string> Disconnected;
        public bool IsConnected=>_transport?.IsConnected==true && _handshake;
        public void Initialize(RpsGameConfig config)
        {
            _config=config;
#if UNITY_WEBGL && !UNITY_EDITOR
            _transport=new WebGlWebSocketTransport();
#else
            _transport=new NativeWebSocketTransport();
#endif
            _transport.Connected+=Handshake;
            _transport.MessageReceived+=Receive;
            _transport.Closed+=Closed;
        }
        public async Task ConnectAsync(string token)
        {
            if(IsConnected) return;
            if(string.IsNullOrEmpty(token)) throw new InvalidOperationException("Sessão expirada.");
            int generation=++_generation; _handshake=false; _buffer.Clear(); _lastEventId=0;
            var ready=_ready=new TaskCompletionSource<bool>();
            StartCoroutine(Timeout(ready,20));
            var negotiate=new TaskCompletionSource<string>(); StartCoroutine(Negotiate(token,negotiate));
            var connection=await negotiate.Task;
            if(generation!=_generation) throw new OperationCanceledException();
            string url=_config.RpsHubUrl.Replace("https://","wss://").Replace("http://","ws://");
            await _transport.ConnectAsync(url+"?id="+Uri.EscapeDataString(connection)+"&access_token="+Uri.EscapeDataString(token));
            await ready.Task;
            if(generation!=_generation) throw new OperationCanceledException();
        }
        private IEnumerator Negotiate(string token,TaskCompletionSource<string> result)
        {
            using var request=new UnityWebRequest(_config.RpsHubUrl+"/negotiate?negotiateVersion=1",UnityWebRequest.kHttpVerbPOST);
            request.downloadHandler=new DownloadHandlerBuffer(); request.SetRequestHeader("Authorization","Bearer "+token); request.timeout=15;
            yield return request.SendWebRequest();
            if(request.result!=UnityWebRequest.Result.Success) { result.TrySetException(new InvalidOperationException("Servidor indisponível.")); yield break; }
            try
            {
                var response=JsonUtility.FromJson<SignalRNegotiateResponse>(request.downloadHandler.text);
                if(string.IsNullOrEmpty(response?.connectionToken)) throw new InvalidOperationException("Negociação inválida.");
                result.TrySetResult(response.connectionToken);
            }
            catch(Exception e) { result.TrySetException(e); }
        }
        private async void Handshake()
        {
            try { await _transport.SendAsync("{\"protocol\":\"json\",\"version\":1}"+Separator); }
            catch(Exception e) { _ready?.TrySetException(e); }
        }
        public Task JoinQueueAsync()=>Invoke("JoinQueue","");
        public Task LeaveQueueAsync()=>Invoke("LeaveQueue","");
        public Task PlayAsync(string id,int round,RpsChoice choice)=>Invoke("Play","\""+Escape(id)+"\","+round+","+(int)choice);
        private async Task Invoke(string target,string arguments)
        {
            if(!IsConnected) throw new InvalidOperationException("Sem conexão.");
            string id=(++_sequence).ToString(); var pending=new TaskCompletionSource<bool>(); _invocations.Add(id,pending);
            StartCoroutine(Timeout(pending,12));
            try
            {
                await _transport.SendAsync("{\"type\":1,\"invocationId\":\""+id+"\",\"target\":\""+target+"\",\"arguments\":["+arguments+"]}"+Separator);
                await pending.Task;
            }
            finally { _invocations.Remove(id); }
        }
        private IEnumerator Timeout(TaskCompletionSource<bool> pending,float seconds)
        {
            float deadline=Time.realtimeSinceStartup+seconds;
            while(!pending.Task.IsCompleted && Time.realtimeSinceStartup<deadline) yield return null;
            if(!pending.Task.IsCompleted) pending.TrySetException(new TimeoutException("Tempo de conexão esgotado."));
        }
        public async Task DisconnectAsync()
        {
            ++_generation; _handshake=false; _buffer.Clear();
            _ready?.TrySetCanceled();
            var pending=new List<TaskCompletionSource<bool>>(_invocations.Values); _invocations.Clear();
            foreach(var task in pending) task.TrySetCanceled();
            if(_transport!=null) await _transport.DisconnectAsync();
        }
        private void Closed(string reason)
        {
            _ = DisconnectAsync(); Disconnected?.Invoke(reason);
        }
        private void Receive(string chunk)
        {
            _lastReceived=Time.realtimeSinceStartup; _buffer.Append(chunk);
            if(_buffer.Length>1024*1024) { Closed("Resposta excessiva."); return; }
            while(true)
            {
                string all=_buffer.ToString(); int split=all.IndexOf(Separator); if(split<0) return;
                string raw=all.Substring(0,split); _buffer.Remove(0,split+1);
                try { Process(raw); }
                catch(Exception) { Closed("Resposta inválida."); return; }
            }
        }
        private void Process(string raw)
        {
            if(string.IsNullOrWhiteSpace(raw)) return;
            var frame=JsonUtility.FromJson<HubFrame>(raw);
            if(!_handshake)
            {
                if(!string.IsNullOrEmpty(frame.error)) { _ready?.TrySetException(new InvalidOperationException("Handshake recusado.")); return; }
                if(frame.type!=0) throw new InvalidOperationException("Handshake inválido.");
                _handshake=true; _lastPing=Time.realtimeSinceStartup; _ready?.TrySetResult(true); Connected?.Invoke(); return;
            }
            if(frame.type==6) return;
            if(frame.type==7) { Closed("Servidor encerrou a sessão."); return; }
            if(frame.type==3)
            {
                if(frame.invocationId!=null && _invocations.TryGetValue(frame.invocationId,out var pending))
                { if(string.IsNullOrEmpty(frame.error)) pending.TrySetResult(true); else pending.TrySetException(new InvalidOperationException("Jogada recusada pelo servidor.")); }
                return;
            }
            if(frame.type!=1 || string.IsNullOrEmpty(frame.target)) return;
            string type=frame.target switch
            { "QueueJoined"=>"queue_joined", "MatchFound"=>"match_found", "RoundStarted"=>"round_started", "ChoiceLocked"=>"choice_locked", "RoundResult"=>"round_result", "MatchFinished"=>"match_finished", "OpponentDisconnected"=>"opponent_disconnected", _=>frame.target };
            var payload=frame.arguments?.Length>0?frame.arguments[0]:new ServerPayload();
            // The server can redeliver a committed outbox event after a transient DB failure.
            if(payload.eventId>0)
            {
                if(payload.eventId<=_lastEventId) return;
                _lastEventId=payload.eventId;
            }
            ServerMessageReceived?.Invoke(new ServerMessage{type=type,payload=payload});
        }
        private void Update()
        {
            _transport?.Tick(); if(!IsConnected) return;
            if(Time.realtimeSinceStartup-_lastReceived>40) { Closed("Servidor sem resposta."); return; }
            if(Time.realtimeSinceStartup-_lastPing>10) { _lastPing=Time.realtimeSinceStartup; Ping(); }
        }
        private async void Ping() { try { await _transport.SendAsync("{\"type\":6}"+Separator); } catch(Exception) { Closed("Conexão perdida."); } }
        private static string Escape(string value)=>(value??"").Replace("\\","\\\\").Replace("\"","\\\"");
#if UNITY_WEBGL && !UNITY_EDITOR
        public void OnWsOpen(string _) => (_transport as WebGlWebSocketTransport)?.OnOpen();
        public void OnWsMessage(string message) => (_transport as WebGlWebSocketTransport)?.OnMessage(message);
        public void OnWsClose(string reason) => (_transport as WebGlWebSocketTransport)?.OnClose(reason);
#endif
        private void OnDestroy() { _ = DisconnectAsync(); }
    }
}
