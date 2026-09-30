using System.Collections;
using RpsArena.Game;
using UnityEngine;
using UnityEngine.UI;

namespace RpsArena.UI
{
    public sealed class HandGestureView : MonoBehaviour
    {
        public Text handText;
        public Text nameText;
        public Text ratingText;
        public RectTransform handRect;

        public void SetPlayer(string playerName, int rating)
        {
            if (nameText) nameText.text = playerName;
            if (ratingText) ratingText.text = rating + " ELO";
        }

        public void SetHiddenHand()
        {
            if (handText) handText.text = "?";
        }

        public void ShowChoice(RpsChoice choice)
        {
            if (handText) handText.text = Symbol(choice);
            if (isActiveAndEnabled) StartCoroutine(Punch());
        }

        public static string Symbol(RpsChoice choice) => choice switch
        {
            RpsChoice.Rock => "✊",
            RpsChoice.Paper => "✋",
            RpsChoice.Scissors => "✌",
            _ => "?"
        };

        private IEnumerator Punch()
        {
            if (!handRect) yield break;
            var start = handRect.localScale;
            var t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime * 6f;
                handRect.localScale = start * Mathf.Lerp(1f, 1.3f, Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI));
                yield return null;
            }
            handRect.localScale = start;
        }
    }
}
