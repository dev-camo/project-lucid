using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000a85, complete 21-method declaration.
    // The original level, app, audio, effect, and island providers are required.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class IntroSequence : MonoBehaviour
    {
        [SerializeField] private IntroSequenceIdentifier m_identifier;
        [SerializeField] private PlayableDirector m_timelineDirector;
        [SerializeField] private EffectSequence m_effectSequence;
        [SerializeField] private UIContainerIdentifier m_skipContainerIdentifier;
        [Tooltip("If true, character will enter ground/air state immediately rather than going through the level enter state.")]
        [SerializeField] private bool m_characterSkipLevelEnterState;
        [SerializeField] private UnityEvent m_onPlayEvent;
        [SerializeField] private UnityEvent m_onCompleteEvent;

        // 0x06003c6f..0x06003c74; ARM 0x6a6474..0x6a65dc.
        public IntroSequenceIdentifier Identifier => m_identifier;
        public bool IsPlaying { get; private set; }
        public bool CharacterSkipLevelEnterState => m_characterSkipLevelEnterState;

        private App m_app;
        private UIManager m_uiManager;
        private LevelManager m_levelManager;
        private AudioManager m_audioManager;
        public event Action OnCompleteCallback;
        private Coroutine m_cameraTrackingCoroutine;
        private bool m_destroySelf;
        private bool m_skipped;

        // 0x06003c75 and natural 0x06003c83; ARM 0x6a65dc/0x6a7598.
        // The references for level and UI are locals; their callbacks assign
        // the stored managers. Audio is acquired after both subscriptions.
        private void Start()
        {
            m_app = ProcessManager.GetSystem<App>();
            ProcessManager.GetSystemRef<LevelManager>().InvokeOnValid(OnLevelManagerValid);
            ProcessManager.GetSystemRef<UIManager>().InvokeOnValid(manager => m_uiManager = manager);
            m_audioManager = ProcessManager.GetSystem<AudioManager>();
        }

        // 0x06003c76; ARM 0x6a67d4. Unity manager equality is the only guard;
        // neither timeline subscriptions nor camera tracking are cleared here.
        private void OnDestroy()
        {
            if (m_levelManager == null) return;
            m_levelManager.RemoveLevelLoadedAction(OnLevelLoaded);
            if (m_levelManager.TryGetCurrentLevel(out LevelManagerLevel level))
                level.Data.UnregisterIntroSequence(m_identifier);
        }

        // 0x06003c77; ARM 0x6a699c. Store first, then register immediately for
        // a valid current level or retain the loaded callback without a scene test.
        private void OnLevelManagerValid(LevelManager levelManager)
        {
            m_levelManager = levelManager;
            if (m_levelManager.TryGetCurrentLevel(out LevelManagerLevel level))
                level.Data.RegisterIntroSequence(this);
            else m_levelManager.InvokeOnLevelLoaded(OnLevelLoaded);
        }

        // 0x06003c78; ARM 0x6a6b1c. Scene names are compared as ordinary
        // strings. Registration precedes removal of the callback on a match.
        private void OnLevelLoaded(LevelManagerLevel level)
        {
            if (level.Data.gameObject.scene.name != gameObject.scene.name) return;
            level.Data.RegisterIntroSequence(this);
            m_levelManager.RemoveLevelLoadedAction(OnLevelLoaded);
        }

        // 0x06003c79; ARM 0x6a6c60. Replace the callback rather than append;
        // repeated play does not first stop an earlier timeline or coroutine.
        public void Play(Transform start, Action onCompleteCallback, bool destroySelf)
        {
            if (start != null)
            {
                Transform ownTransform = transform;
                Vector3 position = start.position;
                Quaternion rotation = start.rotation;
                ownTransform.SetPositionAndRotation(position, rotation);
            }
            m_audioManager.ListenerSetShouldFollowPlayer(false);
            OnCompleteCallback = onCompleteCallback;
            IsPlaying = true;
            m_skipped = false;
            m_destroySelf = destroySelf;
            m_onPlayEvent?.Invoke();
            OpenSkipContainer();
            if (m_timelineDirector != null) PlayTimeline();
            else PlaySequencedEffect();
        }

        // 0x06003c7a; ARM 0x6a7104, x86 0x6cc200. BOTH original backends
        // pass 0x4a64f0eb, which differs from metadata White (0x4a64d0eb).
        // Preserve that value instead of substituting a named enum member.
        public void Skip()
        {
            m_skipped = true;
            CloseSkipContainer();
            m_app.Storage.SetValue(AppFSMKeys.TransitionFadeParameters,
                new FadeTransitionParameters((FadeTransitionType)0x4a64f0eb, OnScreenTransitionMidpoint));
        }

        // 0x06003c7b; ARM 0x6a7300. A playing director is moved to asset
        // duration, evaluated, and stopped. Its stopped callback completes play.
        private void OnScreenTransitionMidpoint()
        {
            if (m_timelineDirector != null && m_timelineDirector.state == PlayState.Playing)
            {
                PlayableDirector director = m_timelineDirector;
                director.time = director.playableAsset.duration;
                m_timelineDirector.Evaluate();
                m_timelineDirector.Stop();
            }
            else OnComplete();
        }

        // 0x06003c7c; ARM 0x6a6ea4. Subscribe before acquiring the island
        // iterator; assign the returned coroutine before reading and playing director.
        private void PlayTimeline()
        {
            m_timelineDirector.stopped += OnTimelineDirectorStopped;
            m_cameraTrackingCoroutine = StartCoroutine(
                ProcessManager.GetSystem<GameplayIslandManager>().IslandCullingTracksCamera());
            m_timelineDirector.Play();
        }

        // 0x06003c7d; ARM 0x6a74e0. The field is unsubscribed, not the
        // supplied director. Skip completes; normal stop begins the effect sequence.
        private void OnTimelineDirectorStopped(PlayableDirector director)
        {
            m_timelineDirector.stopped -= OnTimelineDirectorStopped;
            if (m_skipped) OnComplete();
            else PlaySequencedEffect();
        }

        // 0x06003c7e; ARM 0x6a6fb4. Target is assigned before zero offset;
        // close the skip container before calling the real readonly-data API.
        private void PlaySequencedEffect()
        {
            IEffectData data = new TargetedEffect { Target = transform, Offset = Vector3.zero };
            CloseSkipContainer();
            m_effectSequence.BeginSequence(in data, OnComplete);
        }

        // 0x06003c7f; ARM 0x6a7400. Preserve callback and fault ordering.
        // There is no completion-once guard, callback clear, or finally cleanup.
        private void OnComplete()
        {
            IsPlaying = false;
            m_audioManager.ListenerSetShouldFollowPlayer(true);
            m_onCompleteEvent?.Invoke();
            OnCompleteCallback?.Invoke();
            this.SafeStopCoroutine(ref m_cameraTrackingCoroutine);
            if (m_destroySelf) Destroy(gameObject);
        }

        // 0x06003c80; ARM 0x6a6e5c. GUID equality supplies the null test;
        // GetOrCreate's result is deliberately discarded.
        private void OpenSkipContainer()
        {
            if (m_skipContainerIdentifier == null) return;
            m_uiManager.GetOrCreate(m_skipContainerIdentifier);
        }

        // 0x06003c81; ARM 0x6a72b8.
        private void CloseSkipContainer()
        {
            if (m_skipContainerIdentifier == null) return;
            m_uiManager.Close(m_skipContainerIdentifier);
        }

        // 0x06003c82; ARM 0x6a7590. Original MonoBehaviour base only;
        // no UnityEvent, system reference, or delegate initializer is added.
        public IntroSequence() { }
    }
}
