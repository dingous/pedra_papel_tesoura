using System;
using RpsArena.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RpsArena.UI
{
    public sealed class RpsRuntimeUi : MonoBehaviour
    {
        public Button trainingButton, onlineButton, loginButton, logoutButton, rankingButton, backButton,
            rockButton, paperButton, scissorsButton, maleButton, femaleButton, localRankButton, globalRankButton,
            replayButton, homeButton, cancelButton, soundButton, motionButton, exitButton;
        public InputField nameInput;
        public Text statusText, profileText, scoreText, roundText, countdownText, selectionText, yourNameText,
            opponentNameText, resultTitle, resultDetail, rankNotice, lobbyDetail;
        public Image timerFill;
        public string ScreenName { get; private set; }
        private GameObject _lobby, _game, _ranking, _result, _queue;
        private RectTransform _safe, _rankContent;
        private CanvasScaler _scaler;
        private Font _font;
        private Rect _lastSafe;
        private Vector2Int _lastSize;
        private static readonly Color Ink = new(.035f,.055f,.092f,1);
        private static readonly Color Panel = new(.075f,.105f,.16f,.98f);
        public static readonly Color Cyan = new(.23f,.94f,.81f,1);
        public static readonly Color Coral = new(1,.42f,.36f,1);
        private static readonly Color Muted = new(.62f,.7f,.79f,1);

        public void Build()
        {
            _font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo=new GameObject("Arena UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform,false);
            canvasGo.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            _scaler=canvasGo.GetComponent<CanvasScaler>(); _scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.matchWidthOrHeight=.5f;
            _safe=Rect("Safe area",canvasGo.transform,0,0,1,1);
            var header=Box("Header",_safe,0,.932f,1,1,Ink);
            Label(header,"D / A",.035f,.12f,.18f,.88f,30,Cyan,TextAnchor.MiddleLeft,FontStyle.Bold);
            profileText=Label(header,"",.2f,.13f,.71f,.87f,20,Color.white,TextAnchor.MiddleLeft);
            soundButton=Button(header,"SOM ON",.72f,.2f,.84f,.8f,Panel,16);
            motionButton=Button(header,"CÂMERA ON",.855f,.2f,.98f,.8f,Panel,14);
            statusText=Label(_safe,"",.03f,.002f,.97f,.044f,17,Muted);
            _lobby=Rect("Lobby",_safe,0,.045f,1,.93f).gameObject;
            Label(_lobby.transform,"DUEL ARENA",.03f,.89f,.97f,.995f,52,Color.white,TextAnchor.MiddleCenter,FontStyle.Bold);
            Label(_lobby.transform,"PEDRA  /  PAPEL  /  TESOURA",.05f,.835f,.95f,.9f,19,Cyan);
            var controls=Box("Lobby controls",_lobby.transform,.03f,.015f,.97f,.24f,Panel);
            nameInput=Input(controls,"Jogador",.025f,.68f,.45f,.92f);
            maleButton=Button(controls,"MASCULINO",.47f,.68f,.72f,.92f,Ink,18);
            femaleButton=Button(controls,"FEMININO",.735f,.68f,.975f,.92f,Ink,18);
            trainingButton=Button(controls,"JOGAR TREINO",.025f,.31f,.49f,.61f,Cyan,27,true);
            onlineButton=Button(controls,"DUELO ONLINE",.51f,.31f,.975f,.61f,Coral,27,true);
            rankingButton=Button(controls,"RANKING",.025f,.04f,.30f,.255f,Ink,18);
            loginButton=Button(controls,"ENTRAR COM GOOGLE",.32f,.04f,.70f,.255f,Ink,17);
            logoutButton=Button(controls,"SAIR DA CONTA",.72f,.04f,.975f,.255f,Ink,16);
            lobbyDetail=Label(_lobby.transform,"Primeiro a vencer 2 rodadas • Treino contra o computador",.03f,.26f,.97f,.315f,18,Muted);
            _game=Rect("Match",_safe,0,.045f,1,.93f).gameObject;
            roundText=Label(_game.transform,"TREINO  •  RODADA 01",.16f,.92f,.84f,.985f,19,Cyan);
            scoreText=Label(_game.transform,"0   :   0",.3f,.805f,.7f,.93f,56,Color.white,TextAnchor.MiddleCenter,FontStyle.Bold);
            exitButton=Button(_game.transform,"SAIR",.025f,.92f,.14f,.975f,Panel,17);
            var timer=Box("Timer track",_game.transform,.35f,.797f,.65f,.804f,Panel);
            timerFill=Box("Time remaining",timer,0,0,1,1,Cyan).GetComponent<Image>();
            yourNameText=Label(_game.transform,"VOCÊ",.035f,.272f,.46f,.323f,20,Cyan,TextAnchor.MiddleCenter,FontStyle.Bold);
            opponentNameText=Label(_game.transform,"CPU",.54f,.272f,.965f,.323f,20,Coral,TextAnchor.MiddleCenter,FontStyle.Bold);
            countdownText=Label(_game.transform,"",.39f,.41f,.61f,.68f,90,Cyan,TextAnchor.MiddleCenter,FontStyle.Bold);
            selectionText=Label(_game.transform,"Escolha sua jogada",.03f,.207f,.97f,.262f,23,Color.white);
            rockButton=Button(_game.transform,"01   PEDRA",.03f,.075f,.33f,.195f,Cyan,27,true);
            paperButton=Button(_game.transform,"02   PAPEL",.35f,.075f,.65f,.195f,Color.white,27,true);
            scissorsButton=Button(_game.transform,"03   TESOURA",.67f,.075f,.97f,.195f,Coral,27,true);
            Label(_game.transform,"Pedra vence tesoura   •   Tesoura vence papel   •   Papel vence pedra",.03f,.018f,.97f,.065f,16,Muted);
            _queue=Box("Queue",_safe,.08f,.3f,.92f,.7f,Panel).gameObject;
            Label(_queue.transform,"PROCURANDO DUELO",.05f,.63f,.95f,.88f,34,Color.white,TextAnchor.MiddleCenter,FontStyle.Bold);
            Label(_queue.transform,"Aguardando outro jogador conectado",.06f,.38f,.94f,.61f,22,Muted);
            cancelButton=Button(_queue.transform,"CANCELAR",.16f,.08f,.84f,.3f,Coral,24,true);
            _result=Box("Result",_safe,.07f,.055f,.93f,.32f,Panel).gameObject;
            resultTitle=Label(_result.transform,"VITÓRIA",.04f,.65f,.96f,.97f,40,Cyan,TextAnchor.MiddleCenter,FontStyle.Bold);
            resultDetail=Label(_result.transform,"",.04f,.37f,.96f,.65f,20,Color.white);
            replayButton=Button(_result.transform,"JOGAR NOVAMENTE",.04f,.065f,.63f,.3f,Cyan,22,true);
            homeButton=Button(_result.transform,"INÍCIO",.66f,.065f,.96f,.3f,Ink,21);
            _ranking=Box("Ranking",_safe,0,.045f,1,.932f,Ink).gameObject;
            Label(_ranking.transform,"RANKING",.04f,.855f,.96f,.975f,48,Color.white,TextAnchor.MiddleLeft,FontStyle.Bold);
            localRankButton=Button(_ranking.transform,"NESTE DISPOSITIVO",.04f,.77f,.485f,.845f,Cyan,22,true);
            globalRankButton=Button(_ranking.transform,"ONLINE",.515f,.77f,.96f,.845f,Panel,22);
            rankNotice=Label(_ranking.transform,"",.04f,.675f,.96f,.76f,20,Muted,TextAnchor.MiddleLeft);
            Label(_ranking.transform,"POS.     JOGADOR",.045f,.61f,.6f,.672f,17,Muted,TextAnchor.MiddleLeft);
            Label(_ranking.transform,"ELO          V / D / E",.62f,.61f,.95f,.672f,17,Muted,TextAnchor.MiddleRight);
            var viewport=Box("Ranking scroll",_ranking.transform,.035f,.145f,.965f,.61f,Ink);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped;
            _rankContent=Rect("Rows",viewport,0,1,1,1); _rankContent.pivot=new Vector2(.5f,1);
            scroll.content=_rankContent; scroll.viewport=viewport;
            backButton=Button(_ranking.transform,"VOLTAR À ARENA",.04f,.025f,.96f,.115f,Panel,24);
            if (!FindAnyObjectByType<EventSystem>()) new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            UpdateLayout(); Show("lobby");
        }
        private void Update() => UpdateLayout();
        private void UpdateLayout()
        {
            if (!_safe) return;
            var size=new Vector2Int(Screen.width,Screen.height); var safe=Screen.safeArea;
            if(size==_lastSize && safe==_lastSafe) return;
            _lastSize=size; _lastSafe=safe;
            _scaler.referenceResolution=Screen.width>=Screen.height ? new Vector2(1280,850) : new Vector2(800,1300);
            _safe.anchorMin=new Vector2(safe.x/Screen.width,safe.y/Screen.height);
            _safe.anchorMax=new Vector2(safe.xMax/Screen.width,safe.yMax/Screen.height);
        }
        public void Show(string screen)
        {
            ScreenName=screen; _lobby.SetActive(screen=="lobby"); _game.SetActive(screen=="game");
            _ranking.SetActive(screen=="ranking"); _result.SetActive(screen=="result"); _queue.SetActive(screen=="queue");
            EventSystem.current?.SetSelectedGameObject(null);
        }
        public void SetChoiceButtons(bool enabled)
        { rockButton.interactable=enabled; paperButton.interactable=enabled; scissorsButton.interactable=enabled; }
        public void SetTimer(float fraction)
        { timerFill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(fraction),1); }
        public void ShowRanking(RankingRowDto[] rows, string notice)
        {
            rankNotice.text=notice;
            foreach(Transform child in _rankContent) Destroy(child.gameObject);
            _rankContent.anchoredPosition=Vector2.zero;
            _rankContent.sizeDelta=new Vector2(0,Math.Max(rows.Length,1)*68);
            for(int i=0;i<rows.Length;i++)
            {
                var row=rows[i]; var rt=Box("Rank "+(i+1),_rankContent,0,1,1,1,i%2==0?Panel:Ink);
                rt.pivot=new Vector2(.5f,1); rt.sizeDelta=new Vector2(0,62); rt.anchoredPosition=new Vector2(0,-i*68);
                Label(rt,row.position.ToString("00"),.025f,0,.11f,1,23,i<3?Cyan:Muted,TextAnchor.MiddleLeft,FontStyle.Bold);
                Label(rt,row.displayName,.13f,0,.62f,1,22,Color.white,TextAnchor.MiddleLeft);
                Label(rt,row.rating.ToString(),.64f,0,.78f,1,23,Cyan,TextAnchor.MiddleRight,FontStyle.Bold);
                Label(rt,$"{row.wins} / {row.losses} / {row.draws}",.80f,0,.975f,1,18,Muted,TextAnchor.MiddleRight);
            }
        }
        public void SetButtonLabel(Button button,string label) => button.GetComponentInChildren<Text>().text=label;
        private RectTransform Rect(string name,Transform parent,float x0,float y0,float x1,float y1)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
            rect.anchorMin=new Vector2(x0,y0); rect.anchorMax=new Vector2(x1,y1); rect.offsetMin=rect.offsetMax=Vector2.zero; return rect;
        }
        private RectTransform Box(string name,Transform parent,float x0,float y0,float x1,float y1,Color color)
        { var r=Rect(name,parent,x0,y0,x1,y1); var image=r.gameObject.AddComponent<Image>(); image.color=color; return r; }
        private Text Label(Transform parent,string value,float x0,float y0,float x1,float y1,int size,Color color,TextAnchor align=TextAnchor.MiddleCenter,FontStyle style=FontStyle.Normal)
        {
            var r=Rect("Label",parent,x0,y0,x1,y1); var text=r.gameObject.AddComponent<Text>();
            text.font=_font; text.text=value; text.fontSize=size; text.color=color; text.alignment=align; text.fontStyle=style;
            text.supportRichText=false; text.raycastTarget=false; text.resizeTextForBestFit=true; text.resizeTextMinSize=Math.Max(11,size-9); text.resizeTextMaxSize=size;
            return text;
        }
        private Button Button(Transform parent,string label,float x0,float y0,float x1,float y1,Color color,int size,bool dark=false)
        {
            var r=Box(label,parent,x0,y0,x1,y1,color); var b=r.gameObject.AddComponent<Button>(); b.targetGraphic=r.GetComponent<Image>();
            var colors=b.colors; colors.highlightedColor=new Color(.87f,.95f,1); colors.pressedColor=new Color(.66f,.8f,.88f); colors.disabledColor=new Color(.4f,.46f,.5f,.8f); b.colors=colors;
            Label(r,label,.03f,.05f,.97f,.95f,size,dark?Ink:Color.white,TextAnchor.MiddleCenter,FontStyle.Bold); return b;
        }
        private InputField Input(Transform parent,string value,float x0,float y0,float x1,float y1)
        {
            var rect=Box("Nome local",parent,x0,y0,x1,y1,Ink); var input=rect.gameObject.AddComponent<InputField>();
            input.targetGraphic=rect.GetComponent<Image>(); input.textComponent=Label(rect,value,.04f,0,.96f,1,21,Color.white,TextAnchor.MiddleLeft);
            input.characterLimit=18; input.contentType=InputField.ContentType.Standard; input.lineType=InputField.LineType.SingleLine; input.text=value; return input;
        }
    }
}
