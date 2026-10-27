using RpsArena.Game;
using UnityEngine;

namespace RpsArena.UI
{
    public sealed class RpsArenaStage : MonoBehaviour
    {
        public RpsCharacterAnimator You { get; private set; }
        public RpsCharacterAnimator Opponent { get; private set; }
        public Camera Camera { get; private set; }
        public bool ReducedMotion { get; private set; }
        public string Shot { get; private set; } = "lobby";
        private RpsCharacterCatalog _catalog;
        private Transform _actors;
        private float _shotTime;
        private int _winner;
        private AudioSource _audio;
        private AudioClip _tick, _reveal, _win, _lose;
        private ParticleSystem _confetti;

        public void Build()
        {
            _catalog = Resources.Load<RpsCharacterCatalog>("RpsCharacterCatalog");
            if (!_catalog) throw new System.InvalidOperationException("Execute RPS > Build Playable Game Assets antes de jogar.");
            foreach (var c in FindObjectsByType<Camera>()) c.enabled = false;
            foreach (var a in FindObjectsByType<AudioListener>()) a.enabled = false;
            // Clear the entire framebuffer; the stage camera only renders its central viewport.
            var backdrop = new GameObject("Background camera").AddComponent<Camera>();
            backdrop.transform.SetParent(transform); backdrop.depth = -10; backdrop.cullingMask = 0;
            backdrop.clearFlags = CameraClearFlags.SolidColor; backdrop.backgroundColor = new Color(.026f,.043f,.075f);
            Camera = new GameObject("Arena camera").AddComponent<Camera>();
            Camera.transform.SetParent(transform);
            Camera.tag = "MainCamera";
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(.026f, .043f, .075f);
            Camera.fieldOfView = 35;
            Camera.nearClipPlane = .05f;
            Camera.farClipPlane = 50;
            Camera.gameObject.AddComponent<AudioListener>();
            _audio = gameObject.AddComponent<AudioSource>(); _audio.playOnAwake = false; _audio.volume = .2f;
            _tick = Tone("count", 420, .13f); _reveal = Tone("reveal", 660, .24f);
            _win = Tone("win", 880, .7f); _lose = Tone("lose", 180, .45f);
            ReducedMotion = PlayerPrefs.GetInt("rps.motion", 1) == 0;
            AudioListener.volume = PlayerPrefs.GetInt("rps.sound", 1);
            RenderSettings.ambientLight = new Color(.5f, .55f, .66f);
            RenderSettings.fog = false;
            Light("Key light", new Vector3(35, 155, 0), Color.white, .95f);
            Light("Rim light", new Vector3(25, -20, 0), new Color(.5f,.8f,1), .55f);
            Primitive("Arena floor", PrimitiveType.Cylinder, new Vector3(0,-.16f,0), new Vector3(4,.1f,3), _catalog.stageMaterial);
            for (int i = 0; i < 2; i++)
            {
                var p = new Vector3(i == 0 ? .61f : -.61f, -.035f, 0);
                Primitive("Podium rim", PrimitiveType.Cylinder, p, new Vector3(1,.03f,.88f), i == 0 ? _catalog.cyanMaterial : _catalog.coralMaterial);
                Primitive("Podium", PrimitiveType.Cylinder, p + Vector3.up * .025f, new Vector3(.96f,.025f,.84f), _catalog.darkMaterial);
            }
            for (int i = -6; i <= 6; i++)
            {
                Primitive("Studio light", PrimitiveType.Cube, new Vector3(i*.48f,1.25f,-1.8f), new Vector3(.025f,1.4f+(i%3)*.18f,.025f), i%2==0?_catalog.cyanMaterial:_catalog.coralMaterial);
            }
            _actors = new GameObject("Contestants").transform; _actors.SetParent(transform);
            var fx = new GameObject("Victory confetti"); fx.transform.SetParent(transform); fx.transform.position = new Vector3(0,2.5f,.1f);
            _confetti = fx.AddComponent<ParticleSystem>();
            _confetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _confetti.main; main.playOnAwake = false; main.loop = false; main.duration = 1;
            main.startLifetime = 2.5f; main.startSpeed = .6f; main.startSize = .025f; main.gravityModifier = .15f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.2f,1,.85f), new Color(1,.45f,.3f));
            var shape = _confetti.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(2.5f,.05f,.4f);
            var emission = _confetti.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 70) });
            fx.GetComponent<ParticleSystemRenderer>().sharedMaterial = _catalog.cyanMaterial;
            SetAvatar(0); SetShot("lobby", true);
        }

        public void SetAvatar(int avatar)
        {
            if (You) { You.gameObject.SetActive(false); Destroy(You.gameObject); }
            if (Opponent) { Opponent.gameObject.SetActive(false); Destroy(Opponent.gameObject); }
            You = Spawn(avatar == 0 ? _catalog.male : _catalog.female, .61f, true);
            Opponent = Spawn(avatar == 0 ? _catalog.female : _catalog.male, -.61f, false);
        }
        private RpsCharacterAnimator Spawn(GameObject prefab, float x, bool left)
        {
            var actor = Instantiate(prefab, _actors);
            actor.transform.localPosition = new Vector3(x,.015f,0);
            var animator = actor.GetComponent<RpsCharacterAnimator>(); animator.useLeftHand = left;
            return animator;
        }
        public void SetShot(string shot, bool snap = false, int winner = 0)
        {
            Shot = shot; _shotTime = Time.time; _winner = winner;
            if (snap) MoveCamera(true);
        }
        public void Idle()
        {
            You.SetHiddenHand(); Opponent.SetHiddenHand();
            You.GetComponent<RpsExpressions>().Neutral(); Opponent.GetComponent<RpsExpressions>().Neutral();
        }
        public void Count() { You.PlayCountIn(); Opponent.PlayCountIn(); SetShot("count"); }
        public void Reveal(RpsChoice yours, RpsChoice theirs, int result)
        {
            You.ShowChoice(yours); Opponent.ShowChoice(theirs); SetShot("reveal");
            You.GetComponent<RpsExpressions>().React(result > 0 ? "Smile" : result < 0 ? "Frown" : "Surprise");
            Opponent.GetComponent<RpsExpressions>().React(result < 0 ? "Smile" : result > 0 ? "Frown" : "Surprise");
            Sound("reveal");
        }
        public void Celebrate(int result)
        {
            SetShot("winner", false, result >= 0 ? 0 : 1);
            You.GetComponent<RpsExpressions>().React(result > 0 ? "Smile" : result < 0 ? "Frown" : "Surprise", 4);
            Opponent.GetComponent<RpsExpressions>().React(result < 0 ? "Smile" : result > 0 ? "Frown" : "Surprise", 4);
            if (result > 0 && !ReducedMotion) _confetti.Play();
            Sound(result > 0 ? "win" : "lose");
        }
        public void ToggleMotion() { ReducedMotion = !ReducedMotion; PlayerPrefs.SetInt("rps.motion", ReducedMotion ? 0 : 1); PlayerPrefs.Save(); }
        public void ToggleSound() { AudioListener.volume = AudioListener.volume > 0 ? 0 : 1; PlayerPrefs.SetInt("rps.sound", (int)AudioListener.volume); PlayerPrefs.Save(); }
        public void Sound(string cue) => _audio.PlayOneShot(cue == "tick" ? _tick : cue == "win" ? _win : cue == "lose" ? _lose : _reveal);
        private void LateUpdate() { if (Camera) MoveCamera(false); }
        private void MoveCamera(bool snap)
        {
            // Keep all gestures inside the unobstructed stage area on phones and desktops.
            Camera.rect = new Rect(0,.27f,1,.55f);
            float aspect = Screen.width / (Screen.height * .55f);
            float distance = Mathf.Max(3.65f, 3.9f / aspect);
            float t = Time.time - _shotTime;
            var target = new Vector3(0, .98f, 0);
            var pos = new Vector3(0,1.18f,distance);
            float roll = 0;
            if (!ReducedMotion)
            {
                if (Shot == "lobby") pos.x = Mathf.Sin(t*.3f) * .22f;
                if (Shot == "count") { pos.z -= Mathf.Min(t*.13f,.23f); pos.x = Mathf.Sin(t*2)*.045f; }
                if (Shot == "reveal") { pos.z -= .12f; roll = Mathf.Sin(Mathf.Min(t,1.5f)*2)*.5f; }
                if (Shot == "winner")
                {
                    target = new Vector3(_winner == 0 ? .61f : -.61f,1.36f,0);
                    pos = new Vector3(target.x + Mathf.Sin(t*.4f)*.13f,1.43f,Mathf.Max(1.65f,1.7f/aspect));
                }
            }
            var rot = Quaternion.LookRotation(target-pos) * Quaternion.Euler(0,0,roll);
            float blend = snap ? 1 : 1-Mathf.Exp(-Time.unscaledDeltaTime*3);
            Camera.transform.position = Vector3.Lerp(Camera.transform.position,pos,blend);
            Camera.transform.rotation = Quaternion.Slerp(Camera.transform.rotation,rot,blend);
        }
        private void Light(string name, Vector3 angles, Color color, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>(); light.transform.SetParent(transform);
            light.type=LightType.Directional; light.color=color; light.intensity=intensity; light.transform.rotation=Quaternion.Euler(angles);
        }
        private void Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material mat)
        {
            var obj=GameObject.CreatePrimitive(type); obj.name=name; obj.transform.SetParent(transform);
            obj.transform.position=position; obj.transform.localScale=scale; obj.GetComponent<Renderer>().sharedMaterial=mat;
            Destroy(obj.GetComponent<Collider>());
        }
        private static AudioClip Tone(string name,float hz,float length)
        {
            int count=(int)(22050*length); var samples=new float[count];
            for(int i=0;i<count;i++) { float t=i/22050f; samples[i]=Mathf.Sin(2*Mathf.PI*hz*t)*(1-i/(float)count)*Mathf.Min(1,t*80)*.45f; }
            var clip=AudioClip.Create(name,count,1,22050,false); clip.SetData(samples,0); return clip;
        }
    }
}
