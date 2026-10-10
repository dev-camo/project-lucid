using Cinemachine;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000288. Full source candidate; runtime acceptance pending.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class CinemachineRecenterHeadingExtension : CinemachineExtension
    {
        protected bool m_snapToCharacter;
        protected float m_timeoutTimerSeconds;
        protected readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef =
            ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);
        protected readonly SystemRef<CharacterManager> m_characterManagerRef =
            ProcessManager.GetSystemRef<CharacterManager>(null, true);
        protected bool m_hasTimedOut;
        protected CinemachineCameraManager m_cameraManager;

        // 0x06000dff: register only after the original Cinemachine extension has awakened.
        protected override void Awake()
        {
            base.Awake();
            m_cameraManagerRef.InvokeOnValid(OnCameraManagerReady);
        }

        // 0x06000e00: Add preserves duplicate faults after publishing the manager reference.
        protected virtual void OnCameraManagerReady(CinemachineCameraManager cameraManager)
        {
            m_cameraManager = cameraManager;
            m_cameraManager.CamerasWithSnapLookup.Add(VirtualCamera, this);
        }

        // 0x06000e01: the live-camera query runs even when the definition disables cleanup.
        public void ClearSnapToCharacter()
        {
            m_snapToCharacter = false;
            CharacterManager manager = m_characterManagerRef.Get();
            if (!manager.CharacterValid) return;
            bool clearControls = GetSettingsDefinition().ClearStickyControlsOnSnapEnd;
            bool isLive = m_cameraManager.CinemachineBrain.IsLive(VirtualCamera, false);
            if (clearControls && isLive)
                manager.GetCurrentCharacterUnsafe().Storage.SetValue(ActorFSMKeys.TurnCameraActive, false);
        }

        // 0x06000e02: an absent character leaves the previous timer and flags untouched.
        public virtual void SnapToCharacter()
        {
            if (m_characterManagerRef.IsNull()) return;
            if (!m_characterManagerRef.Get().CharacterValid) return;
            m_snapToCharacter = true;
            m_timeoutTimerSeconds = 0f;
            m_hasTimedOut = false;
        }

        // 0x06000e03: obtain the offset before reading the follow transform's current pose.
        public virtual void RecenterImmediately()
        {
            Transform follow = VirtualCamera.Follow;
            if (follow != null)
            {
                Vector3 offset = CameraUtilities.TryGetFollowOffset(VirtualCamera);
                CinemachineVirtualCameraBase camera = VirtualCamera;
                camera.ForceCameraPosition(follow.position + offset, follow.rotation);
            }
        }

        // 0x06000e04: the shipped base callback is genuinely empty on both architectures.
        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime) { }

        // 0x06000e05: timeout is published before cleanup. A cleanup fault retains it.
        // Both native comparisons reject unordered values; NaN does not finish the snap.
        protected virtual void FixedUpdate()
        {
            if (!m_snapToCharacter) return;
            m_timeoutTimerSeconds += Time.fixedDeltaTime;
            m_hasTimedOut = m_timeoutTimerSeconds >= GetSettingsDefinition().TimeoutSeconds;
            if (m_hasTimedOut)
            {
                ClearSnapToCharacter();
                m_timeoutTimerSeconds = 0f;
                m_hasTimedOut = false;
            }
        }

        // 0x06000e06: original abstract contract; implemented by the authored extensions.
        protected abstract CameraRecenterHeadingDefinition GetSettingsDefinition();

        // 0x06000e07: the two readonly system references initialize before the base ctor.
        protected CinemachineRecenterHeadingExtension() { }
    }
}
