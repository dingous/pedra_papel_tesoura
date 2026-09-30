using System;
using RpsArena.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RpsArena.UI
{
    public sealed class RpsRuntimeUi : MonoBehaviour
    {
        public Button loginButton;
        public Button logoutButton;
        public Button playButton;
        public Button rankingButton;
        public Button rockButton;
        public Button paperButton;
        public Button scissorsButton;
        public Button backButton;
        public Text statusText;
        public Text profileText;
        public Text scoreText;
        public Text rankingText;
        public GameObject loginPanel;
        public GameObject lobbyPanel;
        public GameObject gamePanel;
        public GameObject rankingPanel;
        public HandGestureView youView;
        public HandGestureView opponentView;

        private Font _font;

        public void Build()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            var canvas = CreateCanvas();
            var root = Panel("Root", canvas.transform, new Color(0.045f, 0.05f, 0.09f, 1f), Vector2.zero, Vector2.one);

            var header = Panel("Header", root.transform, new Color(0.08f, 0.09f, 0.16f, 0.96f), new Vector2(0, .88f), Vector2.one);
            Label("RPS ARENA", header.transform, 34, TextAnchor.MiddleLeft, new Vector2(.04f, 0), new Vector2(.5f, 1));
            profileText = Label("", header.transform, 20, TextAnchor.MiddleRight, new Vector2(.5f, 0), new Vector2(.96f, 1));

            loginPanel = Panel("Login", root.transform, new Color(0,0,0,0), Vector2.zero, new Vector2(1, .88f));
            Label("PEDRA • PAPEL • TESOURA\nONLINE", loginPanel.transform, 46, TextAnchor.MiddleCenter, new Vector2(.08f, .55f), new Vector2(.92f, .9f));
            Label("Partidas rápidas, ranking ELO e personagens animados.", loginPanel.transform, 21, TextAnchor.MiddleCenter, new Vector2(.1f, .42f), new Vector2(.9f, .58f));
            loginButton = Button("Continuar com Google", loginPanel.transform, new Vector2(.22f, .22f), new Vector2(.78f, .36f));

            lobbyPanel = Panel("Lobby", root.transform, new Color(0,0,0,0), Vector2.zero, new Vector2(1, .88f));
            Label("ENCONTRE UM ADVERSÁRIO", lobbyPanel.transform, 36, TextAnchor.MiddleCenter, new Vector2(.05f, .63f), new Vector2(.95f, .83f));
            playButton = Button("JOGAR AGORA", lobbyPanel.transform, new Vector2(.2f, .4f), new Vector2(.8f, .55f));
            rankingButton = Button("RANKING", lobbyPanel.transform, new Vector2(.2f, .23f), new Vector2(.8f, .36f));
            logoutButton = Button("Sair", lobbyPanel.transform, new Vector2(.37f, .08f), new Vector2(.63f, .16f));

            gamePanel = Panel("Game", root.transform, new Color(0,0,0,0), Vector2.zero, new Vector2(1, .88f));
            scoreText = Label("0  ×  0", gamePanel.transform, 34, TextAnchor.MiddleCenter, new Vector2(.35f, .8f), new Vector2(.65f, .9f));
            youView = Character("Você", gamePanel.transform, new Vector2(.04f, .34f), new Vector2(.47f, .79f));
            opponentView = Character("Adversário", gamePanel.transform, new Vector2(.53f, .34f), new Vector2(.96f, .79f));
            rockButton = Button("✊  PEDRA", gamePanel.transform, new Vector2(.04f, .08f), new Vector2(.32f, .25f));
            paperButton = Button("✋  PAPEL", gamePanel.transform, new Vector2(.36f, .08f), new Vector2(.64f, .25f));
            scissorsButton = Button("✌  TESOURA", gamePanel.transform, new Vector2(.68f, .08f), new Vector2(.96f, .25f));

            rankingPanel = Panel("Ranking", root.transform, new Color(0,0,0,0), Vector2.zero, new Vector2(1, .88f));
            Label("RANKING GLOBAL", rankingPanel.transform, 36, TextAnchor.MiddleCenter, new Vector2(.08f, .77f), new Vector2(.92f, .92f));
            rankingText = Label("Carregando...", rankingPanel.transform, 21, TextAnchor.UpperLeft, new Vector2(.12f, .16f), new Vector2(.88f, .76f));
            backButton = Button("VOLTAR", rankingPanel.transform, new Vector2(.3f, .04f), new Vector2(.7f, .13f));

            statusText = Label("", root.transform, 18, TextAnchor.MiddleCenter, new Vector2(.02f, .005f), new Vector2(.98f, .07f));
            ShowLogin();
        }

        public void ShowLogin() => Show(loginPanel);
        public void ShowLobby() => Show(lobbyPanel);
        public void ShowGame() => Show(gamePanel);
        public void ShowRanking() => Show(rankingPanel);

        public void SetChoiceButtons(bool enabled)
        {
            rockButton.interactable = enabled;
            paperButton.interactable = enabled;
            scissorsButton.interactable = enabled;
        }

        private void Show(GameObject panel)
        {
            foreach (var p in new[] { loginPanel, lobbyPanel, gamePanel, rankingPanel })
                if (p) p.SetActive(p == panel);
        }

        private Canvas CreateCanvas()
        {
            var go = new GameObject("RpsArenaCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            return canvas;
        }

        private GameObject Panel(string name, Transform parent, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>(), min, max);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private Text Label(string text, Transform parent, int size, TextAnchor anchor, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>(), min, max);
            var label = go.GetComponent<Text>();
            label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.alignment = anchor;
            label.color = Color.white;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Math.Max(14, size / 2);
            label.resizeTextMaxSize = size;
            return label;
        }

        private Button Button(string text, Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>(), min, max);
            go.GetComponent<Image>().color = new Color(.18f, .42f, .92f, 1f);
            var label = Label(text, go.transform, 28, TextAnchor.MiddleCenter, new Vector2(.04f, .05f), new Vector2(.96f, .95f));
            label.fontStyle = FontStyle.Bold;
            return go.GetComponent<Button>();
        }

        private HandGestureView Character(string fallbackName, Transform parent, Vector2 min, Vector2 max)
        {
            var card = Panel(fallbackName, parent, new Color(.10f, .12f, .20f, 1f), min, max);
            var view = card.AddComponent<HandGestureView>();
            view.nameText = Label(fallbackName, card.transform, 26, TextAnchor.MiddleCenter, new Vector2(.06f, .78f), new Vector2(.94f, .94f));
            view.ratingText = Label("1000 ELO", card.transform, 18, TextAnchor.MiddleCenter, new Vector2(.06f, .68f), new Vector2(.94f, .79f));

            var head = Panel("Head", card.transform, new Color(.92f, .68f, .48f, 1f), new Vector2(.34f, .37f), new Vector2(.66f, .65f));
            Label("•  •\n ᴗ", head.transform, 40, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            view.handText = Label("?", card.transform, 80, TextAnchor.MiddleCenter, new Vector2(.22f, .05f), new Vector2(.78f, .38f));
            view.handRect = view.handText.rectTransform;
            return view;
        }

        private static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>()) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }
}
