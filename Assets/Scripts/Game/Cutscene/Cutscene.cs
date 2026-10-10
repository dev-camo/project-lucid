using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000431. This abstract gameplay component is
    // distinct from its authored CutsceneDefinition data asset.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class Cutscene : MonoBehaviour
    {
        protected PlayableDirector m_timelineDirector; // 0x04000e37, not serialized.

        // Original 0x060018a1..ab and corresponding backing fields 0x04000e38..3c.
        public PlayableDirector TimelineDirector => m_timelineDirector;
        public CutsceneDefinition Definition { get; set; }
        public Action InvokeOnStopped { get; set; }
        public bool IsReadyToSkip { get; set; }
        public bool WasSkipped { get; set; }
        public float TimeSpentPlaying { get; private set; }
        private TimelineAsset m_timeline; // 0x04000e3d, original last field.

        // Original 0x060018ac. A non-Timeline playable asset faults at the cast;
        // missing director/timeline is not silently replaced or ignored.
        private void OnEnable()
        {
            m_timeline = (TimelineAsset)m_timelineDirector.playableAsset;
            InitialiseTimeline();
        }

        // Original 0x060018ad. Accumulate unscaled time without resetting it
        // when this component is re-enabled or consulting the director state.
        protected virtual void Update()
        {
            TimeSpentPlaying += Time.unscaledDeltaTime;
        }

        // Original 0x060018ae, complete dual-architecture body. Load skins and
        // move attachments before reading tracks. Each successful replacement
        // rebinds immediately; later matching characters may replace it again.
        public void InitialiseTimeline()
        {
            CharacterCutscene[] characters = GetComponentsInChildren<CharacterCutscene>();
            for (int i = 0; i < characters.Length; ++i)
            {
                characters[i].LoadCharacterSkin();
                characters[i].MoveAttachments();
            }

            foreach (TrackAsset track in m_timeline.GetOutputTracks())
            {
                UnityEngine.Object binding = m_timelineDirector.GetGenericBinding(track);
                if (binding == null) continue;
                // The original native type test is exact, followed by priority0.
                if (binding.GetType() == typeof(AudioSource))
                {
                    ((AudioSource)binding).priority = 0;
                    continue;
                }
                for (int i = 0; i < characters.Length; ++i)
                {
                    if (!characters[i].TryGetReplacement(binding, out UnityEngine.Object replacement)) continue;
                    if (replacement is Component replacementComponent)
                    {
                        // Cast the old binding before looking up the replacement
                        // transform; a wrong old type preserves its original fault.
                        Component oldComponent = (Component)binding;
                        replacementComponent.transform.SetLocalPositionAndRotation(
                            oldComponent.transform.localPosition, oldComponent.transform.localRotation);
                    }
                    // Null/non-component replacements still bind. No early break.
                    m_timelineDirector.SetGenericBinding(track, replacement);
                }
            }

            // Track enumeration must dispose successfully before this final pass.
            // A body/disposal fault preserves the partially completed replacements.
            for (int i = 0; i < characters.Length; ++i)
            {
                characters[i].SetPositions();
                characters[i].ReplacementComplete();
            }
            m_timelineDirector.RebindPlayableGraphOutputs();
        }

        // Original 0x060018af: only the genuine MonoBehaviour base constructor.
        protected Cutscene() { }
    }
}
