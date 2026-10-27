using System;
using System.Collections;
using System.Collections.Generic;
using RpsArena.Auth;
using RpsArena.Config;
using RpsArena.Networking;
using RpsArena.UI;
using UnityEngine;

namespace RpsArena.Game
{
    public sealed class RpsGameController : MonoBehaviour
    {
        public enum MatchPhase { Lobby, Queue, Choosing, Waiting, Revealing, Finished }
        public MatchPhase Phase { get; private set; }
        public RpsLocalCareer Career { get; private set; }
        public RpsRuntimeUi Ui { get; private set; }
        public RpsArenaStage Stage { get; private set; }
        public bool IsLocal { get; private set; } = true;
        private GoogleAuthController _auth;
        private RpsNetworkClient _network;
        private RpsApiClient _api;
        private string _matchId;
        private int _round, _yourScore, _otherScore, _session, _rankRequest;
        private float _deadline, _roundSeconds, _presentationUntil;
        private float _nextQueueCheck, _queueStarted;
        private bool _queueCheckPending;
        private bool _recorded, _globalRanking;
        private RpsChoice _botChoice;
        private readonly System.Random _random = new();
        private readonly Queue<(ServerMessage message, float received)> _events = new();

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            var config=gameObject.AddComponent<RpsGameConfig>();
            _auth=gameObject.AddComponent<GoogleAuthController>(); _auth.Initialize(config);
            _network=gameObject.AddComponent<RpsNetworkClient>(); _network.Initialize(config);
            _api=gameObject.AddComponent<RpsApiClient>(); _api.Initialize(config);
            Career=RpsLocalCareer.Load();
            Stage=gameObject.AddComponent<RpsArenaStage>(); Stage.Build();
            Ui=gameObject.AddComponent<RpsRuntimeUi>(); Ui.Build();
            Ui.nameInput.SetTextWithoutNotify(Career.Current.name);
            Ui.trainingButton.onClick.AddListener(StartTraining);
            Ui.onlineButton.onClick.AddListener(StartOnline);
            Ui.loginButton.onClick.AddListener(() => { Ui.statusText.text="Conectando ao Google..."; _auth.Login(); });
            Ui.logoutButton.onClick.AddListener(() => { Home(); _auth.Logout(); });
            Ui.nameInput.onEndEdit.AddListener(name => { Career.Select(name); Career.Save(); RefreshProfile(); Stage.SetAvatar(Career.Current.avatar); });
            Ui.maleButton.onClick.AddListener(() => Avatar(0)); Ui.femaleButton.onClick.AddListener(() => Avatar(1));
            Ui.rockButton.onClick.AddListener(() => Choose(RpsChoice.Rock));
            Ui.paperButton.onClick.AddListener(() => Choose(RpsChoice.Paper));
            Ui.scissorsButton.onClick.AddListener(() => Choose(RpsChoice.Scissors));
            Ui.rankingButton.onClick.AddListener(() => Ranking(false));
            Ui.localRankButton.onClick.AddListener(() => Ranking(false)); Ui.globalRankButton.onClick.AddListener(() => Ranking(true));
            Ui.backButton.onClick.AddListener(Home); Ui.homeButton.onClick.AddListener(Home); Ui.cancelButton.onClick.AddListener(Home);
            Ui.exitButton.onClick.AddListener(Forfeit); Ui.SetButtonLabel(Ui.exitButton,"DESISTIR");
            Ui.replayButton.onClick.AddListener(() => { if (IsLocal) StartTraining(); else StartOnline(); });
            Ui.soundButton.onClick.AddListener(() => { Stage.ToggleSound(); RefreshOptions(); });
            Ui.motionButton.onClick.AddListener(() => { Stage.ToggleMotion(); RefreshOptions(); });
            _auth.LoggedIn += session => { RefreshProfile(); Ui.statusText.text="Conta conectada. Entre em um duelo online."; };
            _auth.LoggedOut += () => { RefreshProfile(); Ui.statusText.text="Conta desconectada. Treino disponível."; };
            _auth.LoginFailed += error => { Ui.statusText.text=error; RefreshProfile(); };
            _network.ServerMessageReceived += message => _events.Enqueue((message,Time.realtimeSinceStartup));
            _network.Disconnected += reason =>
            {
                if (IsLocal || Phase==MatchPhase.Lobby || Phase==MatchPhase.Finished) return;
                Home(); Ui.statusText.text="Conexão perdida. A partida online não foi registrada localmente.";
            };
            Stage.SetAvatar(Career.Current.avatar); Home(); RefreshOptions();
        }
        private void Avatar(int index)
        {
            Career.Current.avatar=index; Career.Save(); Stage.SetAvatar(index); RefreshProfile();
        }
        private void RefreshOptions()
        {
            Ui.SetButtonLabel(Ui.soundButton,AudioListener.volume>0?"SOM ON":"SOM OFF");
            Ui.SetButtonLabel(Ui.motionButton,Stage.ReducedMotion?"CÂMERA FIXA":"CÂMERA ON");
        }
        private void RefreshProfile()
        {
            var p=Career.Current;
            Ui.profileText.text=$"{p.name}  /  {p.rating} ELO local";
            Ui.lobbyDetail.text=$"Primeiro a vencer 2 rodadas  •  {p.wins} vitórias  •  Melhor sequência: {p.bestStreak}";
            Ui.logoutButton.interactable=_auth.IsLoggedIn;
            Ui.SetButtonLabel(Ui.loginButton,_auth.IsLoggedIn?"GOOGLE CONECTADO":"ENTRAR COM GOOGLE");
            Ui.loginButton.interactable=!_auth.IsLoggedIn;
            Ui.maleButton.interactable=p.avatar!=0; Ui.femaleButton.interactable=p.avatar!=1;
            Ui.nameInput.SetTextWithoutNotify(p.name);
        }
        public void Home()
        {
            ++_session; ++_rankRequest; StopAllCoroutines(); _events.Clear(); _matchId=null;
            _nextQueueCheck=float.PositiveInfinity; _queueCheckPending=false;
            Phase=MatchPhase.Lobby; _presentationUntil=0; _ = _network.DisconnectAsync();
            Ui.Show("lobby"); Ui.statusText.text="Treino: ranking neste dispositivo. Online: conta Google e servidor.";
            Stage.Idle(); Stage.SetShot("lobby"); RefreshProfile();
        }
        public void StartTraining()
        {
            Home(); IsLocal=true; _recorded=false; _yourScore=_otherScore=_round=0;
            Ui.Show("game"); Ui.yourNameText.text=Career.Current.name+"  /  VOCÊ";
            Ui.opponentNameText.text="CPU  /  1000 ELO";
            Ui.statusText.text="Treino local • Teclas 1, 2 e 3 • Desistir conta como derrota";
            NextLocalRound();
        }
        private void NextLocalRound()
        {
            ++_round;
            // Commit the computer's move before enabling input; never inspect the player's choice.
            _botChoice=(RpsChoice)_random.Next(1,4);
            BeginChoice(10);
        }
        private void BeginChoice(float seconds)
        {
            Phase=MatchPhase.Choosing; _roundSeconds=Mathf.Max(.1f,seconds); _deadline=Time.realtimeSinceStartup+_roundSeconds;
            Stage.Idle(); Stage.SetShot("choose"); Ui.SetChoiceButtons(true); Ui.countdownText.text="";
            Ui.selectionText.text="Escolha sua jogada"; Ui.roundText.text=$"{(IsLocal?"TREINO":"ONLINE")}  /  RODADA {_round:00}";
            Ui.scoreText.text=$"{_yourScore}   :   {_otherScore}";
        }
        public async void Choose(RpsChoice choice)
        {
            if (Phase!=MatchPhase.Choosing || !RpsRules.Valid(choice)) return;
            Phase=MatchPhase.Waiting; Ui.SetChoiceButtons(false);
            Ui.selectionText.text=RpsRules.Name(choice)+" confirmada";
            if (IsLocal) { StartCoroutine(LocalReveal(choice,_botChoice)); return; }
            int session=_session;
            try
            {
                await _network.PlayAsync(_matchId,_round,choice);
                if(session==_session) Ui.selectionText.text="Jogada enviada. Aguardando adversário...";
            }
            catch(Exception)
            {
                if(session!=_session) return;
                Home(); Ui.statusText.text="Não foi possível confirmar a jogada. Reconecte para jogar online.";
            }
        }
        private IEnumerator CountIn()
        {
            Phase=MatchPhase.Revealing; Ui.SetChoiceButtons(false); Stage.Count();
            for(int i=3;i>=1;--i)
            { Ui.countdownText.text=i.ToString(); Stage.Sound("tick"); yield return new WaitForSeconds(.5f); }
            Ui.countdownText.text="";
        }
        private IEnumerator LocalReveal(RpsChoice yours,RpsChoice theirs)
        {
            yield return CountIn();
            int result=RpsRules.Compare(yours,theirs);
            if(result>0) _yourScore++; else if(result<0) _otherScore++;
            PresentRound(yours,theirs,result);
            yield return new WaitForSeconds(2.15f);
            if(_yourScore>=2 || _otherScore>=2 || _round>=20)
            {
                int outcome=_yourScore.CompareTo(_otherScore);
                FinishLocal(outcome);
            }
            else NextLocalRound();
        }
        private void PresentRound(RpsChoice yours,RpsChoice theirs,int result)
        {
            Stage.Reveal(yours,theirs,result); Ui.scoreText.text=$"{_yourScore}   :   {_otherScore}";
            Ui.selectionText.text=$"{RpsRules.Name(yours)}  ×  {RpsRules.Name(theirs)}";
            Ui.roundText.text=result>0?"PONTO PARA VOCÊ":result<0?"PONTO DO ADVERSÁRIO":"EMPATE / MAIS UMA RODADA";
            Ui.SetTimer(0);
        }
        private void FinishLocal(int outcome)
        {
            if(_recorded) return;
            _recorded=true; int delta=Career.Record(outcome,1000);
            Result(outcome,delta,Career.Current.rating,"Ranking neste dispositivo");
        }
        private void Result(int outcome,int delta,int rating,string origin)
        {
            Phase=MatchPhase.Finished; Ui.SetChoiceButtons(false); Ui.Show("result");
            Ui.resultTitle.text=outcome>0?"VITÓRIA!":outcome<0?"NA PRÓXIMA, VOCÊ VIRA":"EMPATE";
            Ui.resultTitle.color=outcome<0?RpsRuntimeUi.Coral:RpsRuntimeUi.Cyan;
            Ui.resultDetail.text=$"{_yourScore} : {_otherScore}  •  {delta:+0;-0;0} ELO  •  {rating} pontos\n{origin}";
            Ui.statusText.text=""; Stage.Celebrate(outcome); RefreshProfile();
        }
        private void Forfeit()
        {
            if(IsLocal && Phase!=MatchPhase.Finished && Phase!=MatchPhase.Lobby)
            { StopAllCoroutines(); _otherScore=Math.Max(2,_otherScore); FinishLocal(-1); }
            else { Home(); Ui.statusText.text="Você saiu da partida. O servidor define o resultado online."; }
        }
        private async void StartOnline()
        {
            Home();
            if(!_auth.IsLoggedIn) { Ui.statusText.text="Entre com Google para acessar duelos e ranking online."; _auth.Login(); return; }
            IsLocal=false; Phase=MatchPhase.Queue; Ui.Show("queue"); Ui.statusText.text="Conectando ao servidor...";
            _queueStarted=Time.realtimeSinceStartup;
            int session=_session;
            try
            {
                await _network.ConnectAsync(_auth.Token);
                if(session!=_session) return;
                await _network.JoinQueueAsync();
                if(session==_session && Phase==MatchPhase.Queue)
                {
                    _nextQueueCheck=Time.realtimeSinceStartup+20;
                    Ui.statusText.text="Fila online. Você pode cancelar a qualquer momento.";
                }
            }
            catch(Exception)
            {
                if(session!=_session) return;
                Home(); Ui.statusText.text="Servidor online indisponível. O treino continua disponível.";
            }
        }
        private async void RefreshOnlineQueue()
        {
            int session=_session;
            _queueCheckPending=true;
            try
            {
                // Recheck pairing and renew the server's two-minute queue lease.
                await _network.JoinQueueAsync();
                if(session==_session && Phase==MatchPhase.Queue)
                {
                    _nextQueueCheck=Time.realtimeSinceStartup+20;
                    int seconds=Mathf.FloorToInt(Time.realtimeSinceStartup-_queueStarted);
                    Ui.statusText.text=$"Busca ativa há {seconds}s. Aguardando outro jogador online.";
                }
            }
            catch(Exception)
            {
                if(session!=_session || Phase!=MatchPhase.Queue) return;
                Home(); Ui.statusText.text="A conexão com a fila foi interrompida. Entre no duelo novamente.";
            }
            finally { if(session==_session) _queueCheckPending=false; }
        }
        private void Update()
        {
            if(!IsLocal && Phase==MatchPhase.Queue && !_queueCheckPending && Time.realtimeSinceStartup>=_nextQueueCheck)
                RefreshOnlineQueue();
            if(Phase==MatchPhase.Choosing)
            {
                float remaining=_deadline-Time.realtimeSinceStartup;
                Ui.SetTimer(remaining/_roundSeconds);
                Ui.selectionText.text=$"Escolha sua jogada  /  {Mathf.Max(0,Mathf.CeilToInt(remaining))}s";
                if(Input.GetKeyDown(KeyCode.Alpha1)) Choose(RpsChoice.Rock);
                else if(Input.GetKeyDown(KeyCode.Alpha2)) Choose(RpsChoice.Paper);
                else if(Input.GetKeyDown(KeyCode.Alpha3)) Choose(RpsChoice.Scissors);
                if(remaining<=0 && Phase==MatchPhase.Choosing)
                {
                    if(IsLocal) { Choose((RpsChoice)_random.Next(1,4)); Ui.statusText.text="Tempo esgotado: jogada aleatória selecionada."; }
                    else { Phase=MatchPhase.Waiting; Ui.SetChoiceButtons(false); Ui.selectionText.text="Tempo esgotado. Aguardando o servidor."; }
                }
            }
            if(!IsLocal && _events.Count>0 && Time.realtimeSinceStartup>=_presentationUntil)
            {
                var item=_events.Dequeue(); HandleServer(item.message,Time.realtimeSinceStartup-item.received);
            }
        }
        private void HandleServer(ServerMessage message,float elapsed)
        {
            var p=message.payload??new ServerPayload();
            if(message.type!="match_found" && !string.IsNullOrEmpty(p.matchId) && p.matchId!=_matchId) return;
            switch(message.type)
            {
                case "queue_joined":
                    if(Phase==MatchPhase.Queue) Ui.statusText.text="Busca ativa. Aguardando outro jogador online...";
                    break;
                case "match_found":
                    if(Phase!=MatchPhase.Queue) return;
                    _matchId=p.matchId; _round=0; _yourScore=_otherScore=0;
                    Ui.Show("game"); Phase=MatchPhase.Waiting; Ui.SetChoiceButtons(false);
                    Ui.yourNameText.text=p.you?.displayName??"Você"; Ui.opponentNameText.text=p.opponent?.displayName??"Adversário";
                    Ui.selectionText.text="Adversário encontrado"; Ui.scoreText.text="0 : 0"; break;
                case "round_started":
                    if(string.IsNullOrEmpty(_matchId) || p.round<=_round) return;
                    _round=p.round; BeginChoice(Mathf.Max(.1f,(p.seconds>0?p.seconds:10)-elapsed)); break;
                case "choice_locked": Ui.selectionText.text="Jogada confirmada pelo servidor"; break;
                case "round_result":
                    if(p.round>0 && p.round!=_round) return;
                    _presentationUntil=Time.realtimeSinceStartup+3.7f;
                    StartCoroutine(OnlineReveal(p)); break;
                case "match_finished":
                    if(Phase==MatchPhase.Finished) return;
                    _yourScore=p.yourScore; _otherScore=p.opponentScore;
                    string detail=p.reason=="server_interrupted"?"Servidor reiniciado / sem alteração no ranking":
                        p.reason=="both_timed_out"?"Tempo esgotado para os dois / sem alteração no ranking":
                        p.reason=="opponent_disconnected" || p.reason=="opponent_left"?"Partida encerrada por saída / resultado do servidor":
                        "Ranking online / resultado do servidor";
                    Result(p.result=="win"?1:p.result=="lose"?-1:0,p.ratingDelta,p.yourRating,detail); break;
                case "opponent_disconnected": Home(); Ui.statusText.text="Adversário desconectado. Consulte o ranking online para o resultado."; break;
            }
        }
        private IEnumerator OnlineReveal(ServerPayload p)
        {
            yield return CountIn(); _yourScore=p.yourScore; _otherScore=p.opponentScore;
            PresentRound((RpsChoice)p.yourChoice,(RpsChoice)p.opponentChoice,p.result=="win"?1:p.result=="lose"?-1:0);
        }
        public void Ranking(bool global)
        {
            _globalRanking=global; int request=++_rankRequest; Ui.Show("ranking");
            if(!global)
            {
                var rows=Career.Ranking();
                Ui.ShowRanking(rows,rows.Length==0?"Conclua um treino para entrar no ranking deste dispositivo.":"Treinos contra CPU • Dados deste dispositivo • V: vitórias / D: derrotas / E: empates");
                return;
            }
            Ui.ShowRanking(Array.Empty<RankingRowDto>(),_auth.IsLoggedIn?"Carregando classificação online...":"Entre com Google na arena para consultar o ranking online.");
            if(!_auth.IsLoggedIn) return;
            StartCoroutine(_api.GetRanking(_auth.Token,rows=>
            {
                if(request==_rankRequest && _globalRanking) Ui.ShowRanking(rows,rows.Length==0?"Nenhum jogador classificado ainda.":"Classificação recebida do servidor Dingous");
            },error=>
            {
                if(request==_rankRequest && _globalRanking) Ui.ShowRanking(Array.Empty<RankingRowDto>(),"Ranking online indisponível. Toque em ONLINE para tentar novamente.");
            }));
        }
    }
}
