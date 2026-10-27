using CrazyMinnow.SALSA;
using UnityEngine;

namespace RpsArena.UI
{
    public sealed class RpsExpressions : MonoBehaviour
    {
        private Emoter _emoter;
        private float _nextBlink;
        public string LastExpression { get; private set; }
        private void Awake() { _emoter = GetComponent<Emoter>(); _nextBlink = Time.time + Random.Range(2f, 5f); }
        public void React(string expression, float seconds = 2f)
        {
            if (!_emoter) _emoter = GetComponent<Emoter>();
            LastExpression = expression;
            _emoter.ManualEmote(expression, ExpressionComponent.ExpressionHandler.RoundTrip, seconds);
        }
        public void Neutral() { if (_emoter) _emoter.TurnOffAll(); LastExpression = "Neutral"; }
        private void Update()
        {
            if (Time.time < _nextBlink) return;
            React("Blink", .06f);
            _nextBlink = Time.time + Random.Range(3f, 6f);
        }
    }
}
