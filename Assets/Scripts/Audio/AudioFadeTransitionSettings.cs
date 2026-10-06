using System;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001a0. The supplied struct has no constructor
    // and leaves both durations at their serialized/default values.
    [Serializable]
    public struct AudioFadeTransitionSettings
    {
        [Header("Fade Settings")]
        [Tooltip("The duration to fade out the clip that is currently playing.")]
        [SerializeField]
        private float m_fadeOutDuration;

        [Tooltip("The duration to fade in the next clip.")]
        [SerializeField]
        private float m_fadeInDuration;

        // Original 0x060008b5 / arm64 0x642320.
        public float FadeOutDuration => m_fadeOutDuration;
        // Original 0x060008b6 / arm64 0x642328.
        public float FadeInDuration => m_fadeInDuration;
    }
}
