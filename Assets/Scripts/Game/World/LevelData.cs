using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000a86. All 22 own declarations are retained;
    // the two original iterators and their callback require genuine game providers.
    [ExecuteAlways]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class LevelData : MonoBehaviour
    {
        [Tooltip("Can create multiple start points to reference via teleportation, the first one in the list is defaulted to on level load")]
        [SerializeField] private StartingPosition[] m_startingPositions;
        [Tooltip("Default respawn point set on entering level.")]
        [SerializeField] private RespawnPoint m_defaultRespawnPoint;
        [SerializeField]
        [Tooltip("Add a reference to the TrackManager in the scene, there must be exactly one.")]
        private TrackManager m_trackManager;

        // Original 0x06003c84; ARM 0x6a75a0.
        public TrackManager TrackManager => m_trackManager;

        // Original constructor 0x06003c99 allocates these dictionaries in this order,
        // before the MonoBehaviour base constructor, with the default comparers.
        public Dictionary<LevelRendererIdentifier, RegisterLevelRenderer> LevelRenderers { get; }
            = new Dictionary<LevelRendererIdentifier, RegisterLevelRenderer>();
        private readonly Dictionary<IntroSequenceIdentifier, IntroSequence> m_introSequences
            = new Dictionary<IntroSequenceIdentifier, IntroSequence>();
        private OutroSequence m_outroSequence;
        private IntroSequence m_currentIntroSequence;
        private Action m_onIntroSequenceComplete;

        // Original auto getter 0x06003c85 is attached to LevelRenderers above.
        // Original 0x06003c86; ARM 0x6a75b0.
        public StartingPosition[] StartingPositions => m_startingPositions;

        // Original 0x06003c87 and <Start>d__14. MoveNext never yields a value.
        // Application.IsPlaying(this) retains the original object-specific check.
        private IEnumerator Start()
        {
            if (Application.IsPlaying(this))
                ProcessManager.GetSystemRef<LevelManager>().InvokeOnValid(OnLevelManagerIsValid);
            yield break;
        }

        // Original 0x06003c88; ARM 0x6a765c.
        private void OnLevelManagerIsValid(LevelManager levelManager) =>
            levelManager.RegisterLevelDataForMonoBehaviour(this);

        // Original 0x06003c89; ARM 0x6a7670. The equality operator belongs to
        // ScriptableObjectWithGuid. The first authored location is the fallback;
        // null/empty arrays and null entries have no additional guard.
        private Transform GetStartingPosition_Internal(LevelStartPositionDefinition definition)
        {
            if (definition == null)
                return m_startingPositions[0].Location;
            foreach (StartingPosition startingPosition in m_startingPositions)
                if (startingPosition.Identifier == definition)
                    return startingPosition.Location;
            return m_startingPositions[0].Location;
        }

        // Original 0x06003c8a; ARM 0x6a7700. An active mission position overrides
        // the argument using the genuine Guid inequality operator.
        public Transform GetStartingPosition(LevelStartPositionDefinition startPositionDefinition)
        {
            LevelStartPositionDefinition missionStart =
                ProcessManager.GetSystem<MissionManager>().GetActiveMissionStartPosition();
            if (missionStart != null)
                startPositionDefinition = missionStart;
            return GetStartingPosition_Internal(startPositionDefinition);
        }

        // Original 0x06003c8b; ARM 0x6a780c. A missing mission system reaches the
        // authored respawn transform; neither result receives a Unity-null guard.
        public Transform GetDefaultRespawnPoint()
        {
            if (ProcessManager.GetSystemRef<MissionManager>().TryGet(out MissionManager missionManager))
            {
                LevelStartPositionDefinition missionStart = missionManager.GetActiveMissionStartPosition();
                if (missionStart != null)
                    return GetStartingPosition_Internal(missionStart);
            }
            return m_defaultRespawnPoint.transform;
        }

        // Original 0x06003c8c; ARM 0x6a6abc. Registration replaces an existing row.
        public void RegisterIntroSequence(IntroSequence introSequence) =>
            m_introSequences[introSequence.Identifier] = introSequence;

        // Original 0x06003c8d; ARM 0x6a6940.
        public void UnregisterIntroSequence(IntroSequenceIdentifier identifier) =>
            m_introSequences.Remove(identifier);

        // Original 0x06003c8e; ARM 0x6a7968.
        public void RegisterOutroSequence(OutroSequence outroSequence) => m_outroSequence = outroSequence;

        // Original 0x06003c8f; ARM 0x6a7970. The returned coroutine handle is ignored.
        public void PrepareOutroSequence(OutroSequenceIdentifier outroSequenceIdentifier, IGraphUser user,
            Action<IGraphUser, GameObject, Character> onComplete)
        {
            CoroutineUtils.RunCoroutine(PrepareOutroSequenceCoroutine(outroSequenceIdentifier, user, onComplete));
        }

        // Original static 0x06003c90, <PrepareOutroSequenceCoroutine>d__23 and
        // <>c__DisplayClass23_0. Preserve pre-yield ordering, the pause-menu input,
        // and the direct callback invocation; the original supplies no finally.
        private static IEnumerator PrepareOutroSequenceCoroutine(OutroSequenceIdentifier outroSequenceIdentifier,
            IGraphUser user, Action<IGraphUser, GameObject, Character> onComplete)
        {
            var outroSequencePrefab = ProcessManager.GetSystem<DataManager>()
                .OutroSequenceDefinitions[outroSequenceIdentifier].OutroSequence;
            StackableDataHandle inputHandle = ProcessManager.GetSystem<InputSystem>()
                .ControlMapping.AddGameInputDisabled(GameInput.TogglePauseMenu);
            user.Storage.SetValue(AppFSMKeys.OutroGameInputDisabledHandle, inputHandle);
            Character currentCharacter = ProcessManager.GetSystem<CharacterManager>().GetCurrentCharacterUnsafe();
            currentCharacter.ToggleUI(false);
            yield return currentCharacter.ExitTriggerVolumes();
            currentCharacter.AllowCollisions(false);
            currentCharacter.TryExitInvulnerability();
            currentCharacter.Storage.SetValue(ActorFSMKeys.OutroSequenceType, outroSequenceIdentifier);
            AsyncOperationHandle<GameObject> handle = ProcessManager.GetSystem<AddressableManager>()
                .LoadAssetAsyncInstance(outroSequencePrefab, currentCharacter.WorldPosition,
                    currentCharacter.WorldRotation, null,
                    outroSequenceInstance => onComplete(user, outroSequenceInstance, currentCharacter));
            user.Storage.SetValue(AppFSMKeys.OutroSequenceHandle, handle);
        }

        // Original 0x06003c91; ARM 0x6a7ad8. GetValue requests storeDefault=true.
        // The addressable key remains stored. Input removal precedes object destruction,
        // and a missing input system still removes its storage key.
        public void UnregisterOutroSequence(IGraphStorage storage)
        {
            if (storage.HasValue(AppFSMKeys.OutroSequenceHandle))
                AddressableManager.ReleaseHandleInManager(
                    storage.GetValue(AppFSMKeys.OutroSequenceHandle, default(AsyncOperationHandle<GameObject>), true));
            if (storage.TryGetValue(AppFSMKeys.OutroGameInputDisabledHandle, out StackableDataHandle inputHandle))
            {
                if (ProcessManager.GetSystemRef<InputSystem>().TryGet(out InputSystem inputSystem))
                    inputSystem.ControlMapping.RemoveGameInputDisabled(inputHandle);
                storage.RemoveValue<StackableDataHandle>(AppFSMKeys.OutroGameInputDisabledHandle);
            }
            if (m_outroSequence == null)
                return;
            Destroy(m_outroSequence.gameObject);
            m_outroSequence = null;
        }

        // Original 0x06003c92; ARM 0x6a7e94. The pending delegate is cleared after
        // Play returns; callback reentrancy and failure retain that original ordering.
        public void PlayIntroSequence(IntroSequenceIdentifier identifier, Transform start,
            Action onComplete, bool destroySelf = false)
        {
            if (!m_introSequences.TryGetValue(identifier, out IntroSequence introSequence))
            {
                onComplete?.Invoke();
                return;
            }
            m_onIntroSequenceComplete += onComplete;
            m_onIntroSequenceComplete += OnIntroSequenceComplete;
            m_currentIntroSequence = introSequence;
            introSequence.Play(start, m_onIntroSequenceComplete, destroySelf);
            m_onIntroSequenceComplete = null;
        }

        // Original 0x06003c93; ARM 0x6a8058. Unity inequality selects the event path.
        public void RegisterForIntroSequenceComplete(Action onComplete)
        {
            if (m_currentIntroSequence != null)
                m_currentIntroSequence.OnCompleteCallback += onComplete;
            else
                m_onIntroSequenceComplete += onComplete;
        }

        // Original 0x06003c94; ARM 0x6a81f4. This only clears the current sequence.
        private void OnIntroSequenceComplete() => m_currentIntroSequence = null;

        // Original 0x06003c95; ARM 0x6a8200. Keep dictionary pair deconstruction
        // and enumerator disposal. Only an entry whose IsPlaying is true is skipped.
        public void SkipIntroSequence()
        {
            foreach (var (_, introSequence) in m_introSequences)
                if (introSequence.IsPlaying)
                    introSequence.Skip();
        }

        // Original 0x06003c96; ARM 0x6a8378. Registration replaces an existing row.
        public void RegisterRenderer(RegisterLevelRenderer levelRenderer) =>
            LevelRenderers[levelRenderer.Identifier] = levelRenderer;

        // Original 0x06003c97; ARM 0x6a83d8. Compare the registered game object
        // first. A missing row still re-reads Identifier and performs Remove.
        public void UnregisterRenderer(RegisterLevelRenderer levelRenderer)
        {
            if (LevelRenderers.TryGetValue(levelRenderer.Identifier, out RegisterLevelRenderer registered)
                && registered.gameObject != levelRenderer.gameObject)
                return;
            LevelRenderers.Remove(levelRenderer.Identifier);
        }

        // Original 0x06003c98; ARM 0x6a84fc and its complete x86 body return immediately.
        public void SetData(TrackManager trackManager, StartingPosition startingPosition = null) { }

        // Original 0x06003c99; ARM 0x6a8500. Initializers above retain allocation order.
        public LevelData() { }
    }
}
