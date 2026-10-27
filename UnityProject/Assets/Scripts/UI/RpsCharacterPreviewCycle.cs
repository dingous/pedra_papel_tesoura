using System.Collections;
using RpsArena.Game;
using UnityEngine;

namespace RpsArena.UI
{
    // Used only by RpsCharactersPreview.unity; gameplay chooses gestures explicitly.
    public sealed class RpsCharacterPreviewCycle : MonoBehaviour
    {
        public RpsCharacterAnimator[] characters;

        private IEnumerator Start()
        {
            while (true)
            {
                foreach (var choice in new[] { RpsChoice.Rock, RpsChoice.Paper, RpsChoice.Scissors })
                {
                    foreach (var character in characters) character.PlayCountIn();
                    yield return new WaitForSeconds(1.5f);
                    foreach (var character in characters) character.ShowChoice(choice);
                    yield return new WaitForSeconds(2f);
                }
            }
        }
    }
}
