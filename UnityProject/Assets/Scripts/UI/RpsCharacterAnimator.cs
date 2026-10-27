using RpsArena.Game;
using UnityEngine;

namespace RpsArena.UI
{
    [RequireComponent(typeof(Animator))]
    public sealed class RpsCharacterAnimator : MonoBehaviour
    {
        [Tooltip("Use the authored left-hand versions of the gestures.")]
        public bool useLeftHand;
        private Animator _animator;

        private void Awake() => _animator = GetComponent<Animator>();

        public void SetHiddenHand() => Play("Idle");
        public void PlayCountIn() => Play(useLeftHand ? "LeftCountIn" : "CountIn");

        public void ShowChoice(RpsChoice choice)
        {
            string state = choice switch
            {
                RpsChoice.Rock => "Rock",
                RpsChoice.Paper => "Paper",
                RpsChoice.Scissors => "Scissors",
                _ => "Idle"
            };
            Play(useLeftHand && state != "Idle" ? "Left" + state : state);
        }

        private void Play(string state)
        {
            if (!_animator) _animator = GetComponent<Animator>();
            _animator.CrossFadeInFixedTime(state, 0.12f, 0, 0);
        }
    }
}
