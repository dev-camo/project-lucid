using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HardlightProject
{
    // Original Game.Runtime 0200026a. Configuration records, priorities, stackable
    // overrides and input callbacks retain their original identities and order.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CinemachineCameraManager : TimeScaledComponent_SDT, ISystem
    {
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Serializable]
        public class VirtualCameraByType : ISerializationCallbackReceiver
        {
            [HideInInspector] public string Name;
            public CameraType Type;
            public CinemachineVirtualCameraBase VirtualCamera;
            public bool FollowCameraProxy = true;
            public bool LookAtCameraProxy = true;
            [Tooltip("Optional override proxy target. Above bools will apply to this instead of the generic proxy target.")]
            public CameraProxyTarget CustomProxyTarget;
            [ShowIf("CustomProxyTarget", null)] public bool SetCustomProxyTargetToFollowCharacter;
            [HideInInspector] public int StartingPriority;
            public bool IgnoreWhenForcingToPosition;

            // 06000d95/96: both serialization callbacks refresh the editor name.
            public void OnBeforeSerialize() { Name = Type.ToString(); }
            public void OnAfterDeserialize() { Name = Type.ToString(); }
            // 06000d97: only the two follow/look flags have explicit initializers.
            public VirtualCameraByType() { }
        }

        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Serializable]
        public class VirtualCamerasBySetting
        {
            public List<VirtualCameraByType> VirtualCamerasByType;
            // 06000d98: leaves the list null rather than allocating a substitute.
            public VirtualCamerasBySetting() { }
        }

        [SerializeField] private SerializableDictionary<CameraConfigurationType, VirtualCamerasBySetting>
            m_virtualCamerasBySetting = new SerializableDictionary<CameraConfigurationType, VirtualCamerasBySetting>(
                HardlightEnumComparers.CameraConfigurationTypeComparer);
        [SerializeField] private CinemachineBrain m_cinemachineBrain;
        [SerializeField] private CameraProxyTarget m_proxyTarget;
        [SerializeField] private CharacterStickyControlsDefinition m_stickyControlsDefinition;
        [SerializeField] private ScriptableRendererFeature[] m_occlusionRenderFeatures;
        [SerializeField, Tooltip("Threshold distances to debounce movement input.")]
        private Vector2 m_movementSecondaryThresholds;
        [SerializeField, Tooltip("Timers to debounce movement input.")]
        private Vector2 m_movementSecondaryTimers;
        public CameraProxyTarget ProxyTarget => m_proxyTarget;
        public CinemachineBrain CinemachineBrain => m_cinemachineBrain;
        public event Action OnSystemShutdown;
        public event Action<bool> SecondaryMovementSubscribed = _ => { };
        public Dictionary<ICinemachineCamera, CinemachineRecenterHeadingExtension> CamerasWithSnapLookup { get; } =
            new Dictionary<ICinemachineCamera, CinemachineRecenterHeadingExtension>();
        public Vector2 ControllerMovement { get; private set; }
        public CameraInputBrain InputBrain => m_inputBrain;
        public Camera MainCamera => m_cinemachineBrain.OutputCamera;
        public Action<ICinemachineCamera, ICinemachineCamera> OnCameraLive;
        public Action<Camera> OnMainCameraReady = _ => { };
        public Action OnResetYAxisRecenter = () => { };
        public bool MainCameraIsReady { get; private set; }
        public const string DebugMenuPath = "Camera";
        private CameraConfigurationType m_cameraConfiguration;
        private List<VirtualCameraByType> m_virtualCamerasByType;
        private readonly Dictionary<CameraType, VirtualCameraByType> m_cameraTypeLookup =
            new Dictionary<CameraType, VirtualCameraByType>(HardlightEnumComparers.CameraTypeComparer);
        private readonly StackableData m_cameraSettings = new StackableData();
        private readonly CameraInputBrain m_inputBrain = new CameraInputBrain();
        private Character m_playerCharacter;
        private StackableDataHandle m_playerCharacterHandle;
        private List<CameraType> m_temporaryCameraStack = new List<CameraType>(1);
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>(null, true);
        private readonly SystemRef<CameraStackManager> m_cameraStackManagerRef =
            ProcessManager.GetSystemRef<CameraStackManager>(null, true);
        private bool m_movementSecondaryActive;
        private Action<CameraSensitivityType> m_onSensitivityChanged;
        private Vector2 m_impulse;

        // 06000d4e/4f/54..5c: original direct and inlined getters.
        private SaveManager SaveManager => m_saveManagerRef.Get();
        private SaveDataSettings SaveDataSettings => SaveManager.GetSaveDataSettings();

        // 06000d5d: register callbacks before disabling input and starting the wait.
        protected override void Awake()
        {
            base.Awake();
            this.SubscribeToAction(SystemAction.Initialise, Initialise);
            this.SubscribeToAction(SystemAction.Shutdown, Shutdown);
            StackableData.RegisterTypeOperations<CameraProxyTargetSettings>(
                logicalOr: CameraProxyTargetSettings.CameraProxyTargetSettingsPriority);
            SetControlsEnabled(false);
            StartCoroutine(WaitForMainCamera());
            ProcessManager.RegisterSystem(this, null, true, false);
        }

        // 06000d5e/94 + d__56: yield the actual WaitUntil, then publish readiness.
        // The original callback is unguarded even if callers replace its initial lambda.
        private IEnumerator WaitForMainCamera()
        {
            yield return new WaitUntil(() => MainCamera != null);
            MainCameraIsReady = true;
            OnMainCameraReady(MainCamera);
        }

        // 06000d5f: replace, rather than accumulate, a pending impulse.
        public void ApplyImpulse(Vector2 impulse) { m_impulse = impulse; }

        // 06000d60: consume movement before querying settings; invert only its Y.
        protected override void InternalUpdate(float deltaTime)
        {
            m_inputBrain.Update(deltaTime);
            Vector2 movement = m_inputBrain.GetAndClearMovement();
            if (SaveDataSettings.CameraInvertedControls) movement.y = -movement.y;
            ControllerMovement = movement + m_impulse;
            m_impulse = Vector2.zero;
        }

        // 06000d61..63: duplicate subscriptions and one-occurrence removal remain.
        private void OnSettingsInitialise()
        {
            OnSettingsChanged();
            SaveManager.OnLoadCompleted += OnSettingsChanged;
        }

        private void OnSettingsChanged()
        {
            SaveDataSettings.InvokeOnConfigurationChanged(OnConfigurationChanged);
            if (m_movementSecondaryActive)
                SaveDataSettings.InvokeOnCameraSecondarySensitivityChanged(OnSensitivityChanged);
            else SaveDataSettings.InvokeOnCameraSensitivityChanged(OnSensitivityChanged);
        }

        private void OnSettingsClose()
        {
            SaveManager.OnLoadCompleted -= OnSettingsChanged;
            SaveDataSettings.RemoveConfigurationChangedAction(OnConfigurationChanged);
            if (m_movementSecondaryActive)
                SaveDataSettings.RemoveCameraSecondarySensitivityChangedAction(OnSensitivityChanged);
            else SaveDataSettings.RemoveCameraSensitivityChangedAction(OnSensitivityChanged);
        }

        // 06000d64: the initial None configuration skips blend clearing.
        private void OnConfigurationChanged(CameraConfigurationType cameraConfiguration)
        {
            if (m_cameraConfiguration == cameraConfiguration) return;
            if (m_cameraConfiguration != CameraConfigurationType.None) ClearActiveBlend();
            m_cameraConfiguration = cameraConfiguration;
            ResetAndPopulateCameraTypeLookup();
            UpdateCameras();
            m_proxyTarget.SetCharacterAsTarget();
            SetVirtualCameraProxyTargets(m_proxyTarget.transform);
        }

        // 06000d65: append first, then invoke that argument with PRIMARY sensitivity.
        public void InvokeOnSensitivityChanged(Action<CameraSensitivityType> action)
        {
            m_onSensitivityChanged += action;
            action(SaveDataSettings.CameraSensitivity);
        }
        // 06000d66/67.
        private void OnSensitivityChanged(CameraSensitivityType cameraSensitivity)
        { m_onSensitivityChanged?.Invoke(cameraSensitivity); }
        public void RemoveSensitivityChangedAction(Action<CameraSensitivityType> action)
        { m_onSensitivityChanged -= action; }

        // 06000d68: base pause precedes clearing the last published movement.
        public override void OnPause() { base.OnPause(); ControllerMovement = Vector2.zero; }

        // 06000d69: snapshot current priorities by type, reset old cameras, then
        // install a configured list. A failed lookup retains the old list and lookup.
        public void ResetAndPopulateCameraTypeLookup()
        {
            var priorities = new Dictionary<CameraType, int>(HardlightEnumComparers.CameraTypeComparer);
            if (m_virtualCamerasByType != null)
            {
                foreach (VirtualCameraByType camera in m_virtualCamerasByType)
                {
                    priorities.Add(camera.Type, camera.VirtualCamera.Priority);
                    camera.VirtualCamera.Priority = camera.StartingPriority;
                    camera.VirtualCamera.gameObject.SetActive(false);
                }
            }
            if (m_virtualCamerasBySetting.TryGetValue(m_cameraConfiguration, out VirtualCamerasBySetting settings))
            {
                m_virtualCamerasByType = settings.VirtualCamerasByType;
                m_cameraTypeLookup.Clear();
                PopulateCameraTypeLookup(m_virtualCamerasByType);
                foreach (VirtualCameraByType camera in m_virtualCamerasByType)
                {
                    camera.VirtualCamera.gameObject.SetActive(true);
                    if (camera.SetCustomProxyTargetToFollowCharacter && camera.CustomProxyTarget != null)
                        camera.CustomProxyTarget.SetCharacterAsTarget();
                    if (priorities.TryGetValue(camera.Type, out int priority)) camera.VirtualCamera.Priority = priority;
                }
            }
        }

        // 06000d6a/6b: cache the priority before Dictionary.Add's duplicate check.
        private void PopulateCameraTypeLookup(IEnumerable<VirtualCameraByType> virtualCameraByTypes)
        { foreach (VirtualCameraByType camera in virtualCameraByTypes) PopulateCameraTypeLookup(camera); }
        private void PopulateCameraTypeLookup(VirtualCameraByType cameraByType)
        {
            cameraByType.StartingPriority = cameraByType.VirtualCamera.Priority;
            m_cameraTypeLookup.Add(cameraByType.Type, cameraByType);
        }

        // 06000d6c: establish base values before settings callbacks and input setup.
        private void Initialise(object context = null)
        {
            m_cameraSettings.SetRetrievalOperation<CameraType>((int)CameraSettingType.ActiveCamera,
                StackableData.RetrievalOperation.Latest, default(CameraType));
            m_cameraSettings.SetBaseValue((int)CameraSettingType.ActiveCamera, CameraType.Default);
            m_cameraSettings.SetRetrievalOperation<CameraProxyTargetSettings>((int)CameraSettingType.ProxySettingOverride,
                StackableData.RetrievalOperation.LogicalOr, default(CameraProxyTargetSettings));
            m_cameraSettings.SetBaseValue((int)CameraSettingType.ProxySettingOverride, default(CameraProxyTargetSettings));
            OnSettingsInitialise();
            m_inputBrain.Initialise();
            m_inputBrain.OnSnapButtonPressed += ClearSnapToCharacter;
            m_inputBrain.OnSnapButtonReleased += SnapToCharacter;
        }

        // 06000d6d: shutdown observers run first; remove the current character's
        // authored handle key before closing settings and disconnecting input.
        private void Shutdown(object context = null)
        {
            OnSystemShutdown?.Invoke();
            SystemRef<CharacterManager> managerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);
            if (managerRef.IsValid() && managerRef.Get().TryGetCurrentCharacter(out Character character))
                character.Storage.RemoveValue<StackableDataHandle>(ActorFSMKeys.CameraProxySettingHandle);
            OnSettingsClose();
            m_inputBrain.OnSnapButtonPressed -= ClearSnapToCharacter;
            m_inputBrain.OnSnapButtonReleased -= SnapToCharacter;
            m_inputBrain.Shutdown();
        }

        // 06000d6e..70: notify before closing old sensitivity, publish the mode only
        // after closing it, then reinitialize callbacks against the new mode.
        public void SetControlsEnabled(bool enable) { m_inputBrain.Enabled = enable; }
        public void SubscribeMovementSecondary()
        {
            m_inputBrain.SubscribeMovementSecondary(m_movementSecondaryThresholds, m_movementSecondaryTimers);
            SecondaryMovementSubscribed(true);
            OnSettingsClose();
            m_movementSecondaryActive = true;
            OnSettingsInitialise();
        }
        public void UnsubscribeMovementSecondary()
        {
            m_inputBrain.UnsubscribeMovementSecondary();
            SecondaryMovementSubscribed(false);
            OnSettingsClose();
            m_movementSecondaryActive = false;
            OnSettingsInitialise();
        }

        // 06000d71/72: update priorities before returning/publishing a new handle.
        public StackableDataHandle ApplyCameraTypeOverride(CameraType cameraType)
        {
            StackableDataHandle handle = m_cameraSettings.AddOverride((int)CameraSettingType.ActiveCamera, cameraType);
            UpdateCameras();
            return handle;
        }
        public void ApplyCameraTypeOverride(CameraType cameraType, StackableDataHandle stackableDataHandle)
        {
            m_cameraSettings.AddOverride(stackableDataHandle, (int)CameraSettingType.ActiveCamera, cameraType);
            UpdateCameras();
        }
        // 06000d73: null handles do not trigger a camera update.
        public void RemoveCameraSettingOverride(StackableDataHandle stackableDataHandle)
        {
            if (stackableDataHandle == null) return;
            m_cameraSettings.RemoveOverrides(stackableDataHandle);
            UpdateCameras();
        }
        // 06000d74..76: genuine unconstrained generic forwarding; these do not
        // perform the priority refresh supplied by the camera-type overloads.
        public StackableDataHandle ApplyCameraSettingOverride<T>(CameraSettingType cameraSettingType, T value)
        { return m_cameraSettings.AddOverride((int)cameraSettingType, value); }
        public void ModifyCameraSettingOverride<T>(CameraSettingType cameraSettingType, T value, StackableDataHandle settingHandle)
        { m_cameraSettings.AddOverride(settingHandle, (int)cameraSettingType, value); }
        public T GetCameraSettingOverride<T>(CameraSettingType cameraSettingType)
        { return m_cameraSettings.Get<T>((int)cameraSettingType, true, default(T)); }

        // 06000d77/78.
        public void ClearActiveBlend()
        { if (m_cinemachineBrain.ActiveBlend != null) m_cinemachineBrain.ActiveBlend = null; }
        public void ForceAllVirtualCamerasToCharacter(Character character)
        { ForceAllVirtualCamerasToPosition(character.WorldPosition, character.WorldRotation, true, true); }

        // 06000d79: teleport proxies before virtual cameras; mixing children are
        // forced before their parent, and every snap extension recenters afterward.
        public void ForceAllVirtualCamerasToPosition(Vector3 position, Quaternion rotation, bool applyFollowOffset,
            bool clearActiveBlend = true)
        {
            if (clearActiveBlend) ClearActiveBlend();
            m_proxyTarget.TeleportToTarget();
            foreach (VirtualCameraByType camera in m_virtualCamerasByType)
                if (camera.CustomProxyTarget != null) camera.CustomProxyTarget.TeleportToTarget();
            foreach (VirtualCameraByType camera in m_virtualCamerasByType)
            {
                if (camera.IgnoreWhenForcingToPosition) continue;
                CinemachineVirtualCameraBase virtualCamera = camera.VirtualCamera;
                if (virtualCamera is CinemachineMixingCamera mixingCamera)
                {
                    CinemachineVirtualCameraBase[] children = mixingCamera.ChildCameras;
                    for (int i = 0; i < children.Length; i++)
                        ForceVirtualCameraToPosition(children[i], position, rotation, applyFollowOffset);
                }
                ForceVirtualCameraToPosition(virtualCamera, position, rotation, applyFollowOffset);
            }
            foreach (KeyValuePair<ICinemachineCamera, CinemachineRecenterHeadingExtension> camera in CamerasWithSnapLookup)
                camera.Value.RecenterImmediately();
            m_cinemachineBrain.ManualUpdate();
            m_cinemachineBrain.enabled = false;
            m_cinemachineBrain.enabled = true;
        }
        // 06000d7a.
        private void ForceVirtualCameraToPosition(CinemachineVirtualCameraBase virtualCamera, Vector3 position,
            Quaternion rotation, bool applyFollowOffset)
        {
            Vector3 offset = applyFollowOffset ? CameraUtilities.TryGetFollowOffset(virtualCamera) : Vector3.zero;
            virtualCamera.ForceCameraPosition(position + offset, rotation);
        }

        // 06000d7b: every type represented in the stack gets its own starting
        // priority; absent types get -1. No sorting or new priority sequence.
        private void UpdateCameras()
        {
            m_temporaryCameraStack = m_cameraSettings.GetStack((int)CameraSettingType.ActiveCamera, m_temporaryCameraStack);
            foreach (VirtualCameraByType camera in m_virtualCamerasByType)
                camera.VirtualCamera.Priority = m_temporaryCameraStack.Contains(camera.Type) ? camera.StartingPriority : -1;
        }
        // 06000d7c.
        public void SetCameraTargetsForType(CameraType cameraType, Transform followTarget, Transform lookAtTarget)
        {
            if (m_cameraTypeLookup.TryGetValue(cameraType, out VirtualCameraByType camera))
                SetVirtualCameraTargets(camera.VirtualCamera, followTarget, lookAtTarget);
        }
        // 06000d7d: preserve two custom-transform reads with the Unity lifetime test
        // between them; user code can change that component while properties run.
        public void SetVirtualCameraProxyTargets(Transform proxyTransform)
        {
            foreach (VirtualCameraByType camera in m_virtualCamerasByType)
            {
                CinemachineVirtualCameraBase virtualCamera = camera.VirtualCamera;
                Transform target = proxyTransform;
                if (camera.CustomProxyTarget != null && camera.CustomProxyTarget.transform != null)
                    target = camera.CustomProxyTarget.transform;
                SetVirtualCameraTargets(virtualCamera, camera.FollowCameraProxy ? target : null,
                    camera.LookAtCameraProxy ? target : null);
            }
        }
        // 06000d7e: null returns that custom proxy to its character target.
        public void SetProxyTargetTransform(CameraType cameraType, Transform proxyTarget)
        {
            if (!m_cameraTypeLookup.TryGetValue(cameraType, out VirtualCameraByType camera) || camera.CustomProxyTarget == null)
                return;
            if (proxyTarget == null) camera.CustomProxyTarget.SetCharacterAsTarget();
            else camera.CustomProxyTarget.SetTarget(proxyTarget);
        }
        // 06000d7f..81: the live comparison is managed interface reference equality.
        public VirtualCameraByType GetCameraOfType(CameraType type)
        { return m_cameraTypeLookup.TryGetValue(type, out VirtualCameraByType camera) ? camera : null; }
        private VirtualCameraByType GetLiveCamera()
        {
            ICinemachineCamera active = m_cinemachineBrain.ActiveVirtualCamera;
            foreach (KeyValuePair<CameraType, VirtualCameraByType> camera in m_cameraTypeLookup)
                if ((ICinemachineCamera)camera.Value.VirtualCamera == active) return camera.Value;
            return null;
        }
        public VirtualCameraByType GetLatestCamera()
        { return GetCameraOfType(m_cameraSettings.Get<CameraType>((int)CameraSettingType.ActiveCamera, true, default(CameraType))); }

        // 06000d82/83.
        private void ClearSnapToCharacter()
        {
            foreach (KeyValuePair<ICinemachineCamera, CinemachineRecenterHeadingExtension> camera in CamerasWithSnapLookup)
                camera.Value.ClearSnapToCharacter();
        }
        public void Action_SnapToCharacter() { SnapToCharacter(true, false); }
        // 06000d84: snap all extensions first, then conditionally force sticky input.
        public void SnapToCharacter(bool triggerTurnCamera = true, bool forceStickyControls = false)
        {
            foreach (KeyValuePair<ICinemachineCamera, CinemachineRecenterHeadingExtension> camera in CamerasWithSnapLookup)
                camera.Value.SnapToCharacter();
            if (!(forceStickyControls || (triggerTurnCamera && LiveCameraIsUsingSnapExtension()))) return;
            // Both original CPU ranges call Unity equality and return on true.
            if (m_playerCharacter == null) return;
            m_stickyControlsDefinition.StickyControls.SetTurnCameraActive(m_playerCharacter, m_playerCharacter.CameraRotation);
        }
        // 06000d85: test actual mixing children before testing the active parent.
        public bool LiveCameraIsUsingSnapExtension()
        {
            ICinemachineCamera active = m_cinemachineBrain.ActiveVirtualCamera;
            if (active == null) return false;
            if (active is CinemachineMixingCamera mixingCamera)
            {
                CinemachineVirtualCameraBase[] children = mixingCamera.ChildCameras;
                for (int i = 0; i < children.Length; i++)
                {
                    CinemachineVirtualCameraBase child = children[i];
                    // Both original CPU ranges call Unity equality and skip on true.
                    if (child == null) continue;
                    if (CamerasWithSnapLookup.ContainsKey(child)) return true;
                }
            }
            return CamerasWithSnapLookup.ContainsKey(active);
        }

        // 06000d86: clear the previous handle's values, allocate a NEW override,
        // replace the out handle, then publish settings onto all proxy targets.
        public void SetCameraProxySettings(CameraProxyTargetSettings settings, ref StackableDataHandle handle)
        {
            if (handle != null) m_cameraSettings.ClearOverrides(handle);
            handle = m_cameraSettings.AddOverride((int)CameraSettingType.ProxySettingOverride, settings);
            UpdateCameraProxySettingsOnTargets();
        }
        // 06000d87: deliberately clears, rather than removes, the supplied handle.
        public void RemoveCameraProxySettingOverride(StackableDataHandle handle)
        { m_cameraSettings.ClearOverrides(handle); UpdateCameraProxySettingsOnTargets(); }
        // 06000d88.
        private void UpdateCameraProxySettingsOnTargets()
        {
            CameraProxyTargetSettings settings = GetCameraProxySettings();
            m_proxyTarget.SetSettings(settings);
            foreach (VirtualCameraByType camera in m_virtualCamerasByType)
                if (camera.CustomProxyTarget != null) camera.CustomProxyTarget.SetSettings(settings);
        }
        // 06000d89: a missing live record or custom proxy falls back to the default.
        public bool GetCameraProxyTargetIsCharacter()
        {
            VirtualCameraByType camera = GetLiveCamera();
            CameraProxyTarget custom = camera == null ? null : camera.CustomProxyTarget;
            return custom == null ? m_proxyTarget.TargetIsCharacter() : camera.CustomProxyTarget.TargetIsCharacter();
        }
        // 06000d8a/8b.
        public CameraProxyTargetSettings GetCameraProxySettings()
        { return m_cameraSettings.Get<CameraProxyTargetSettings>((int)CameraSettingType.ProxySettingOverride, true,
            default(CameraProxyTargetSettings)); }
        public void Action_OnCameraLive(ICinemachineCamera activeCamera, ICinemachineCamera previousCamera)
        { OnCameraLive?.Invoke(activeCamera, previousCamera); }
        // 06000d8c: authored feature array is not guarded; destroyed entries are skipped.
        public void SetOcclusionRenderFeaturesActive(bool on)
        {
            ScriptableRendererFeature[] features = m_occlusionRenderFeatures;
            for (int i = 0; i < features.Length; i++)
            {
                ScriptableRendererFeature feature = features[i];
                if (feature == null) continue;
                if (feature.isActive != on) feature.SetActive(on);
            }
        }

        // 06000d8d: assigning the first character handle happens AFTER updating
        // cameras. A failing update therefore leaves the new override unpublished.
        public void RequestCharacterCameraChange(CameraType cameraType)
        {
            if (cameraType == CameraType.None)
            {
                RemoveCameraSettingOverride(m_playerCharacterHandle);
                m_playerCharacterHandle = null;
                return;
            }
            if (m_playerCharacterHandle != null) ApplyCameraTypeOverride(cameraType, m_playerCharacterHandle);
            else m_playerCharacterHandle = ApplyCameraTypeOverride(cameraType);
        }
        // 06000d8e: recurse into mixing children first; null target arguments leave
        // the respective property unchanged rather than clearing it.
        private void SetVirtualCameraTargets(CinemachineVirtualCameraBase virtualCamera, Transform followTarget, Transform lookAtTarget)
        {
            if (virtualCamera is CinemachineMixingCamera mixingCamera)
            {
                CinemachineVirtualCameraBase[] children = mixingCamera.ChildCameras;
                for (int i = 0; i < children.Length; i++) SetVirtualCameraTargets(children[i], followTarget, lookAtTarget);
            }
            if (followTarget != null) virtualCamera.Follow = followTarget;
            if (lookAtTarget != null) virtualCamera.LookAt = lookAtTarget;
        }
        // 06000d8f: finding any already registered type returns from the WHOLE call.
        // Earlier list/lookup changes survive; the final priority update is skipped.
        public void RegisterCameraByType(IEnumerable<VirtualCameraByType> virtualCamerasByType)
        {
            foreach (VirtualCameraByType camera in virtualCamerasByType)
            {
                if (m_cameraTypeLookup.ContainsKey(camera.Type)) return;
                m_virtualCamerasByType.Add(camera);
                PopulateCameraTypeLookup(camera);
            }
            UpdateCameras();
        }
        // 06000d90: break immediately after mutating the enumerated list.
        public void UnregisterCameraByType(CameraType type)
        {
            if (!m_cameraTypeLookup.ContainsKey(type)) return;
            foreach (VirtualCameraByType camera in m_virtualCamerasByType)
            {
                if (camera.Type != type) continue;
                camera.VirtualCamera.Priority = camera.StartingPriority;
                m_virtualCamerasByType.Remove(camera);
                m_cameraTypeLookup.Remove(camera.Type);
                UpdateCameras();
                break;
            }
        }
        // 06000d91.
        public void ResetYAxisRecenteringOnAllCameras() { OnResetYAxisRecenter?.Invoke(); }
        // 06000d92: camera enabled state changes before the optional stack service.
        public void ToggleGameplayCameraActive(bool activateCamera)
        {
            MainCamera.enabled = activateCamera;
            if (!m_cameraStackManagerRef.IsValid()) return;
            CameraStackManager manager = m_cameraStackManagerRef.Get();
            if (activateCamera)
            {
                if (!manager.TryGetAudioListener(MainCamera, out AudioListener listener))
                    listener = MainCamera.GetComponentInChildren<AudioListener>();
                manager.OnCameraLoaded(MainCamera, listener);
            }
            else manager.OnCameraUnloaded(MainCamera);
        }

        // 06000d93 and <>c 06000d99..9d: ordered field initializers and three genuine
        // empty default delegates precede TimeScaledComponent_SDT construction.
        // Natural delegate and iterator emitted identities require separate binding.
        public CinemachineCameraManager() { }
    }
}
