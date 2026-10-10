// Complete original Game.Runtime CameraYAxisProxyManager family. Private candidate;
// genuine Character/CharacterManager/LevelManager/camera graph remains source-open.
using System;
using System.Collections.Generic;
using Cinemachine;
using Cinemachine.Utility;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CameraYAxisProxyManager : TimeScaledComponent_SDT, ISystem
    {
        [SerializeField, Tooltip("Used as smoothing rate for default target.")]
        private float m_defaultSmoothingRate = 1f;
        [Tooltip("X axis = Angle from target up\nY axis = multiplied by smoothing rate."), SerializeField]
        private AnimationCurve m_defaultSmoothingRateByAngleCurve = AnimationCurve.Constant(0f, 1f, 1f);
        [Tooltip("Whether to apply smoothing multiplier from current speed lookup."), SerializeField]
        private bool m_defaultUseSpeedForSmoothing;
        [Tooltip("X axis = Current speed\nY axis = multiplied by smoothing rate."), SerializeField]
        private AnimationCurve m_defaultSmoothingRateBySpeedCurve = AnimationCurve.Constant(0f, 1f, 1f);
        [SerializeField] private bool m_defaultUseCharacterForwardAsRotationGuide;
        [Tooltip("Order in array defines layer priority."), SerializeField]
        private TransformStackLayerDefinition[] m_transformStackLayerDefinition;
        [SerializeField, Tooltip("Dot product is of camera follow forward to Y override target up.\nThis up will be rotated slightly towards follow up when dot product is over this threshold to attempt to prevent gimbal lock on the camera."), Range(0f, 1f), Header("Gimbal Lock")]
        private float m_gimbalLockDotThreshold = 0.9f;
        [SerializeField] private CinemachineBrain m_cinemachineBrain;
        [SerializeField] private MetadataKeyType m_cameraYOverrideMetadataKeyType;
        [SerializeField] private MetadataKeyType m_cameraYSmoothingRateMetadataKeyType;
        [SerializeField, Range(0f, 360f)] private float m_angleDifferenceAngleThreshold = 10f;

        public float DefaultSmoothingRate => m_defaultSmoothingRate; // 06000d26.
        public float SignedAngleDifferenceFromLastForward { get; private set; } // d27/d28.
        public Vector3 TargetForward { get; private set; } // d29/d2a.
        public Vector3 TargetUp { get; private set; } // d2b/d2c.

        private bool m_canUseGimbalLockResolver;
        private readonly StackableData m_layerStacks = new StackableData();
        private TargetData m_currentTargetData;
        private readonly HashSet<CinemachineVirtualCameraBase> m_gimbalLockCameras = new HashSet<CinemachineVirtualCameraBase>();
        private readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>();
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();
        private Character m_playerCharacter;
        private readonly Dictionary<CharacterCollisionData, StackableDataHandle> m_characterYOverrideHandles = new Dictionary<CharacterCollisionData, StackableDataHandle>();
        private bool m_resolvingGimbalLock;
        private TargetData m_defaultTargetData;
        private Vector3 m_previousForward = Vector3.forward;
        private Transform m_transform;
        private float m_angleDifferenceCosineThreshold;
        private bool m_rotatingBackFromGimbalLock;

        // 06000d2d: registration precedes base initialisation and character subscription.
        protected override void Awake()
        {
            m_transform = transform;
            m_angleDifferenceCosineThreshold = Mathf.Cos(m_angleDifferenceAngleThreshold * Mathf.Deg2Rad);
            ProcessManager.RegisterSystem(this, null, true, false);
            base.Awake();
            m_characterManagerRef.Get().RegisterOnCharacterChange(OnCharacterChange, true);
            foreach (TransformStackLayerDefinition layer in m_transformStackLayerDefinition)
                m_layerStacks.SetRetrievalOperation<TargetData>((int)layer.LayerType, StackableData.RetrievalOperation.Latest);
            ProcessManager.GetSystemRef<LevelManager>().InvokeOnValid(OnLevelManagerReady);
        }

        // 06000d2e: unavailable registry retains registration; event removals precede base.
        public override void OnDestroy()
        {
            if (m_characterManagerRef.IsValid())
                m_characterManagerRef.Get().ReleaseOnCharacterChange(OnCharacterChange);
            if (m_playerCharacter != null)
            {
                m_playerCharacter.OnCollisionDataEnterEvent -= OnCollisionDataEnterEvent;
                m_playerCharacter.OnCollisionDataExitEvent -= OnCollisionDataExitEvent;
            }
            base.OnDestroy();
        }

        private void OnLevelManagerReady(LevelManager levelManager) // 06000d2f.
        {
            levelManager.ManagedSystems.AddManagedSystem(this, false);
        }

        // 06000d30: old callbacks are removed before publishing the new character.
        // The new value is deliberately dereferenced without a null guard.
        private void OnCharacterChange(Character character)
        {
            if (m_playerCharacter != null)
            {
                m_playerCharacter.OnCollisionDataEnterEvent -= OnCollisionDataEnterEvent;
                m_playerCharacter.OnCollisionDataExitEvent -= OnCollisionDataExitEvent;
            }
            m_playerCharacter = character;
            m_playerCharacter.OnCollisionDataEnterEvent += OnCollisionDataEnterEvent;
            m_playerCharacter.OnCollisionDataExitEvent += OnCollisionDataExitEvent;
            m_defaultTargetData = new TargetData
            {
                Target = m_playerCharacter.GravityProxy,
                SmoothingRate = m_defaultSmoothingRate,
                Instant = false,
                Layer = CameraYOverrideLayer.Default,
                SmoothingRateByAngleCurve = m_defaultSmoothingRateByAngleCurve,
                UseSpeedForSmoothing = m_defaultUseSpeedForSmoothing,
                SmoothingRateBySpeedCurve = m_defaultSmoothingRateBySpeedCurve
            };
            m_currentTargetData = m_defaultTargetData;
            m_layerStacks.SetBaseValue((int)CameraYOverrideLayer.Default, m_defaultTargetData);
            UpdateHighestPriorityTarget();
        }

        // 06000d31: existing override is removed before obtaining the new rate/target.
        private void OnCollisionDataEnterEvent(CharacterCollisionData collisionData, Collision collision)
        {
            if (!MetaDataHasYOverride(collisionData)) return;
            TryRemoveCharacterYOverrideHandle(collisionData);
            collisionData.CameraOverrideMetadata.TryGetMetadata(m_cameraYSmoothingRateMetadataKeyType, out Metadata smoothingMetadata);
            float smoothingRate = smoothingMetadata != null ? smoothingMetadata.AsFloat() : m_defaultSmoothingRate;
            TargetData data = new TargetData
            {
                Instant = smoothingRate == 0f,
                Layer = CameraYOverrideLayer.Override,
                SmoothingRate = smoothingRate,
                Target = m_playerCharacter.transform
            };
            StackableDataHandle handle = OnAddCameraYUpOverride(data);
            m_characterYOverrideHandles[collisionData] = handle;
        }

        private void OnCollisionDataExitEvent(CharacterCollisionData collisionData, Collision collision) // 06000d32.
        {
            if (MetaDataHasYOverride(collisionData)) TryRemoveCharacterYOverrideHandle(collisionData);
        }

        private bool MetaDataHasYOverride(CharacterCollisionData collisionData) // 06000d33.
        {
            return collisionData.HasCameraOverride &&
                collisionData.CameraOverrideMetadata.TryGetMetadata(m_cameraYOverrideMetadataKeyType, out Metadata metadata) && metadata.AsBool();
        }

        private void TryRemoveCharacterYOverrideHandle(CharacterCollisionData collisionData) // 06000d34.
        {
            if (!m_characterYOverrideHandles.TryGetValue(collisionData, out StackableDataHandle handle)) return;
            OnRemoveCameraYUpOverride(handle);
            m_characterYOverrideHandles.Remove(collisionData);
        }

        protected override void InternalUpdate(float deltaTime) // 06000d35.
        {
            if (m_characterManagerRef.IsNull() || !m_characterManagerRef.Get().CharacterValid) return;
            Transform target = m_currentTargetData.Target;
            float angleFromTarget = Vector3.Angle(m_transform.up, target.up);
            bool forceInstantTransition;
            if (angleFromTarget <= 0.0001f)
            {
                m_rotatingBackFromGimbalLock = false;
                forceInstantTransition = true;
            }
            else forceInstantTransition = false;
            if (m_canUseGimbalLockResolver &&
                (forceInstantTransition || m_resolvingGimbalLock || m_rotatingBackFromGimbalLock))
            {
                ICinemachineCamera activeCamera = m_cameraManagerRef.Get().CinemachineBrain.ActiveVirtualCamera;
                if (activeCamera != null && activeCamera.Follow)
                {
                    bool resolvingBefore = m_resolvingGimbalLock;
                    TryResolveGimbalLock(activeCamera.Follow);
                    if (resolvingBefore && !m_resolvingGimbalLock) m_rotatingBackFromGimbalLock = true;
                }
            }
            if (!m_resolvingGimbalLock) RotateToTarget(deltaTime, angleFromTarget, forceInstantTransition);
            TargetForward = (m_resolvingGimbalLock ? m_transform : target).forward;
            TargetUp = (m_resolvingGimbalLock ? m_transform : target).up;
            UpdateAngleDifference();
            m_previousForward = TargetForward;
        }

        // 06000d36: ordered <= preserves the native unordered clear branch.
        private void TryResolveGimbalLock(Transform followTransform)
        {
            if (!m_canUseGimbalLockResolver) return;
            if (m_cameraManagerRef.IsNull())
            {
                m_transform.up = m_currentTargetData.Target.up;
                return;
            }
            float dot = Mathf.Abs(Vector3.Dot(followTransform.up, m_currentTargetData.Target.up));
            if (dot <= m_gimbalLockDotThreshold)
            {
                m_transform.up = Vector3.Slerp(m_currentTargetData.Target.up, followTransform.up,
                    m_gimbalLockDotThreshold - dot);
                m_resolvingGimbalLock = true;
            }
            else m_resolvingGimbalLock = false;
        }

        public StackableDataHandle OnAddCameraYUpOverride(TargetData data) // 06000d37.
        {
            if (data.UseCharacterGravityAsTarget) data.Target = m_playerCharacter.GravityProxy;
            if (data.UseDefaultSmoothingRate)
            {
                data.SmoothingRate = m_defaultSmoothingRate;
                data.SmoothingRateByAngleCurve = m_defaultSmoothingRateByAngleCurve;
                data.UseSpeedForSmoothing = m_defaultUseSpeedForSmoothing;
                data.SmoothingRateBySpeedCurve = m_defaultSmoothingRateBySpeedCurve;
            }
            StackableDataHandle handle = m_layerStacks.AddOverride((int)data.Layer, data);
            UpdateHighestPriorityTarget();
            return handle;
        }

        public void OnRemoveCameraYUpOverride(StackableDataHandle handle) // 06000d38.
        {
            m_layerStacks.RemoveOverrides(handle);
            UpdateHighestPriorityTarget();
        }

        public void SnapToRotation() { RotateToTarget(0f, 0f, true); } // 06000d39.

        private void UpdateHighestPriorityTarget() // 06000d3a.
        {
            for (int i = m_transformStackLayerDefinition.Length - 1; i >= 0; i--)
            {
                int layer = (int)m_transformStackLayerDefinition[i].LayerType;
                if (!m_layerStacks.HasData(layer)) continue;
                TargetData target = m_layerStacks.Get<TargetData>(layer);
                if (target != m_currentTargetData) m_currentTargetData = target;
                return;
            }
        }

        private void RotateToTarget(float deltaTime, float angleFromTarget, bool forceInstantTransition = false) // 06000d3b.
        {
            Transform target = m_currentTargetData.Target;
            Transform forwardTarget = m_currentTargetData.OptionalForwardLockTarget;
            Vector3 up = target.up;
            Quaternion rotation;
            if (forwardTarget != null) rotation = Quaternion.LookRotation(forwardTarget.forward, up);
            else if (m_currentTargetData.UseCharacterForwardAsRotationGuide)
            {
                Vector3 forward = m_playerCharacter.ForwardDirection.ProjectOntoPlane(up);
                if (forward.AlmostZero()) forward = target.forward;
                rotation = Quaternion.LookRotation(forward, up);
            }
            else rotation = target.rotation;
            if (m_currentTargetData.Instant || forceInstantTransition) m_transform.rotation = rotation;
            else
            {
                float smoothingRate = m_currentTargetData.SmoothingRate *
                    m_currentTargetData.SmoothingRateByAngleCurve.Evaluate(angleFromTarget);
                if (m_currentTargetData.UseSpeedForSmoothing && m_playerCharacter != null)
                    smoothingRate *= m_currentTargetData.SmoothingRateBySpeedCurve.Evaluate(m_playerCharacter.WorldVelocityMagnitude);
                float amount = smoothingRate * deltaTime;
                Transform proxy = m_transform;
                proxy.rotation = Quaternion.Slerp(proxy.rotation, rotation, amount);
            }
        }

        // 06000d3c: native ordered >= suppresses the projected angle; unordered falls through.
        private void UpdateAngleDifference()
        {
            if (Mathf.Abs(Vector3.Dot(m_previousForward, TargetUp)) >= m_angleDifferenceCosineThreshold)
                SignedAngleDifferenceFromLastForward = 0f;
            else
            {
                Vector3 projected = m_previousForward.ProjectOntoPlane(TargetUp).normalized;
                SignedAngleDifferenceFromLastForward = Vector3.SignedAngle(projected, TargetForward, TargetUp);
            }
        }

        public void Action_OnLiveCameraChanged(ICinemachineCamera activeCamera, ICinemachineCamera previousCamera) // 06000d3d.
        {
            m_canUseGimbalLockResolver = m_gimbalLockCameras.Contains(activeCamera as CinemachineVirtualCameraBase);
            if (!m_canUseGimbalLockResolver) m_resolvingGimbalLock = false;
        }

        public void RegisterGimbalLockCamera(CinemachineVirtualCameraBase virtualCamera) // 06000d3e.
        {
            m_gimbalLockCameras.Add(virtualCamera);
            if (m_cinemachineBrain.IsLive(virtualCamera, false)) m_canUseGimbalLockResolver = true;
        }

        public CameraYAxisProxyManager() { } // 06000d3f; all original initializers precede base.

        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public class TargetData // 02000266; ctor06000d40.
        {
            public bool UseCharacterGravityAsTarget;
            [HideIf("UseCharacterGravityAsTarget", null)] public Transform Target;
            public Transform OptionalForwardLockTarget;
            public bool UseDefaultSmoothingRate;
            [HideIf("UseDefaultSmoothingRate", null)] public float SmoothingRate;
            [Tooltip("X axis = Angle from target up\nY axis = multiplied by smoothing rate"), HideIf("UseDefaultSmoothingRate", null)]
            public AnimationCurve SmoothingRateByAngleCurve = AnimationCurve.Constant(0f, 1f, 1f);
            [HideIf("UseDefaultSmoothingRate", null)] public bool UseSpeedForSmoothing;
            [HideIf("UseDefaultSmoothingRate", null), Tooltip("X axis = Current speed\nY axis = multiplied by smoothing rate")]
            public AnimationCurve SmoothingRateBySpeedCurve = AnimationCurve.Constant(0f, 1f, 1f);
            [Tooltip("Will snap to rotation instantly.")] public bool Instant;
            public CameraYOverrideLayer Layer;
            public bool UseCharacterForwardAsRotationGuide = true;
            public TargetData() { }
        }

        [Serializable]
        private struct TransformStackLayerDefinition : ISerializationCallbackReceiver // 02000267.
        {
            [HideInInspector] public string Name;
            public CameraYOverrideLayer LayerType;
            public void OnBeforeSerialize() { Name = LayerType.ToString(); } // 06000d41.
            public void OnAfterDeserialize() { Name = LayerType.ToString(); } // 06000d42.
        }
    }
}
