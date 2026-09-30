using System;
using System.Text;
using RpsArena.Auth;
using RpsArena.Config;
using RpsArena.Networking;
using RpsArena.UI;
using UnityEngine;

namespace RpsArena.Game
{
    public sealed class RpsGameController : MonoBehaviour
    {
        private RpsGameConfig _config;
        private GoogleAuthController _auth;
        private RpsNetworkClient _network;
        private RpsApiClient _api;
        private RpsRuntimeUi _ui;
        private string _matchId;
        private int _round;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _config = gameObject.AddComponent<RpsGameConfig>();
            _auth = gameObject.AddComponent<GoogleAuthController>();
            _network = gameObject.AddComponent<RpsNetworkClient>();
            _api = gameObject.AddComponent<RpsApiClient>();
            _ui = gameObject.AddComponent<RpsRuntimeUi>();

            _auth.Initialize(_config);
            _network.Initialize(_config);
            _api.Initialize(_config);
            _ui.Build();
            WireEvents();
            _ui.ShowLogin();
            _ui.statusText.text = "Verificando sua conta Google...";
            _auth.TryRestore();
        }

        private void WireEvents()
        {
            _ui.loginButton.onClick.AddListener(_auth.Login);
            _ui.logoutButton.onClick.AddListener(() => { _ = _network.DisconnectAsync(); _auth.Logout(); });
            _ui.playButton.onClick.AddListener(async () =>
            {
                try
                {
                    _ui.statusText.text = "Procurando adversário...";
                    if (!_network.IsConnected) await _network.ConnectAsync(_auth.Token);
                    await _network.JoinQueueAsync();
                }
                catch (Exception ex) { _ui.statusText.text = "Falha ao entrar na fila: " + ex.Message; }
            });
            _ui.rankingButton.onClick.AddListener(LoadRanking);
            _ui.backButton.onClick.AddListener(_ui.ShowLobby);
            _ui.rockButton.onClick.AddListener(() => Play(RpsChoice.Rock));
            _ui.paperButton.onClick.AddListener(() => Play(RpsChoice.Paper));
            _ui.scissorsButton.onClick.AddListener(() => Play(RpsChoice.Scissors));

            _auth.LoggedIn += session => OnAuthenticated(session.accessToken);
            _auth.LoggedOut += () =>
            {
                _ui.profileText.text = string.Empty;
                _ui.ShowLogin();
                _ui.statusText.text = "Entre com Google para jogar";
            };
            _network.Connected += () => _ui.statusText.text = "Online";
            _network.Disconnected += reason => _ui.statusText.text = "Conexão encerrada: " + reason;
            _network.ServerMessageReceived += OnServerMessage;
        }

        private void OnAuthenticated(string token)
        {
            StartCoroutine(_api.GetMe(token, me =>
            {
                _ui.profileText.text = $"{me.displayName}  •  {me.rating} ELO";
                _ui.ShowLobby();
                _ui.statusText.text = "Pronto para jogar";
            }, error =>
            {
                _ui.statusText.text = "Não foi possível carregar o perfil: " + error;
            }));
        }

        private async void Play(RpsChoice choice)
        {
            if (string.IsNullOrWhiteSpace(_matchId) || !_network.IsConnected) return;
            _ui.youView.ShowChoice(choice);
            _ui.SetChoiceButtons(false);
            _ui.statusText.text = "Jogada enviada. Aguardando adversário...";
            try { await _network.PlayAsync(_matchId, _round, choice); }
            catch (Exception ex) { _ui.statusText.text = "Falha ao enviar jogada: " + ex.Message; }
        }

        private void OnServerMessage(ServerMessage message)
        {
            var p = message.payload ?? new ServerPayload();
            switch (message.type)
            {
                case "queue_joined":
                    _ui.statusText.text = "Na fila...";
                    break;
                case "match_found":
                    _matchId = p.matchId;
                    _round = Math.Max(1, p.round);
                    _ui.youView.SetPlayer(p.you?.displayName ?? "Você", p.you?.rating ?? 1000);
                    _ui.opponentView.SetPlayer(p.opponent?.displayName ?? "Adversário", p.opponent?.rating ?? 1000);
                    _ui.youView.SetHiddenHand();
                    _ui.opponentView.SetHiddenHand();
                    _ui.scoreText.text = "0  ×  0";
                    _ui.SetChoiceButtons(true);
                    _ui.ShowGame();
                    _ui.statusText.text = "Adversário encontrado!";
                    break;
                case "round_started":
                    _round = Math.Max(1, p.round);
                    _ui.youView.SetHiddenHand();
                    _ui.opponentView.SetHiddenHand();
                    _ui.SetChoiceButtons(true);
                    _ui.statusText.text = $"Rodada {p.round} — escolha sua jogada";
                    break;
                case "choice_locked":
                    _ui.statusText.text = "Jogada confirmada no servidor";
                    break;
                case "round_result":
                    _ui.youView.ShowChoice((RpsChoice)p.yourChoice);
                    _ui.opponentView.ShowChoice((RpsChoice)p.opponentChoice);
                    _ui.scoreText.text = $"{p.yourScore}  ×  {p.opponentScore}";
                    _ui.statusText.text = p.result == "win"
                        ? "Você venceu a rodada!"
                        : p.result == "lose"
                            ? "Adversário venceu a rodada"
                            : "Empate";
                    break;
                case "match_finished":
                    _ui.SetChoiceButtons(false);
                    _ui.statusText.text = p.result == "win"
                        ? $"VITÓRIA!  {Signed(p.ratingDelta)} ELO • novo rating {p.yourRating}"
                        : $"DERROTA  {Signed(p.ratingDelta)} ELO • novo rating {p.yourRating}";
                    Invoke(nameof(ReturnToLobby), 3f);
                    break;
                case "opponent_disconnected":
                    _ui.statusText.text = "O adversário desconectou.";
                    Invoke(nameof(ReturnToLobby), 2f);
                    break;
            }
        }

        private void LoadRanking()
        {
            if (!_auth.IsLoggedIn) return;
            _ui.ShowRanking();
            _ui.rankingText.text = "Carregando ranking...";
            StartCoroutine(_api.GetRanking(_auth.Token, rows =>
            {
                var sb = new StringBuilder();
                var count = Math.Min(rows.Length, 20);
                for (var i = 0; i < count; i++)
                {
                    var row = rows[i];
                    sb.Append(row.position.ToString().PadLeft(2)).Append(".  ")
                      .Append(row.displayName).Append("    ")
                      .Append(row.rating).Append(" ELO")
                      .Append("   ").Append(row.wins).Append("V/").Append(row.losses).Append("D")
                      .AppendLine();
                }
                _ui.rankingText.text = sb.Length == 0 ? "Ainda não há jogadores no ranking." : sb.ToString();
            }, error => _ui.rankingText.text = "Falha ao carregar ranking: " + error));
        }

        private void ReturnToLobby()
        {
            _matchId = null;
            _round = 0;
            _ui.ShowLobby();
            _ui.statusText.text = "Pronto para a próxima partida";
            if (_auth.IsLoggedIn) OnAuthenticated(_auth.Token);
        }

        private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();
    }
}
