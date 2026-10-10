using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [RequireComponent(typeof(ParticleSystem))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TimeScaledParticleSystem : TimeScaledComponent_SDT
    {
        [SerializeField] protected ParticleSystem m_mainParticleSystem;
        [Header("Simulation speed will be set to main particle system's speed"), SerializeField]
        protected ParticleSystem[] m_subParticleSystems;
        [SerializeField] protected TrailRenderer[] m_trailRenderers;
        [SerializeField] private bool m_useCustomRenderBounds;
        [ShowIf("m_useCustomRenderBounds", (object)null), SerializeField] private float m_cullingBoundsSize = 2f;
        [ShowIf("m_useCustomRenderBounds", (object)null), SerializeField,
         Tooltip("Auto-populated when subsystem list regenerated, if using custom render bounds.")]
        private Renderer[] m_particleRenderers;
        private CullingGroup m_cullingGroup;
        private float m_simulationSpeed;
        private float m_lastSimulationSpeed;
        private readonly SystemRef<CinemachineCameraManager> m_cinemachineCameraManagerRef =
            ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);
        private string m_cachedSceneHierarchy;
        private string m_cachedNullMainParticleSystemErrorMessage;

        protected override void Awake() // Game.Runtime 06002e4b.
        {
            base.Awake();
            m_simulationSpeed = m_lastSimulationSpeed = m_mainParticleSystem.main.simulationSpeed;
        }
        protected override void OnEnable() // 06002e4c: ready callback precedes startup subscription.
        {
            base.OnEnable();
            if (m_useCustomRenderBounds)
            {
                if (m_cinemachineCameraManagerRef.TryGet(out CinemachineCameraManager manager))
                    CameraRegistered(manager);
                m_cinemachineCameraManagerRef.OnSystemStartup += CameraRegistered;
            }
        }
        private void CameraRegistered(CinemachineCameraManager manager) // 06002e4d.
        {
            if (manager.MainCameraIsReady) SetupCustomCulling(manager.MainCamera);
            else manager.OnMainCameraReady += SetupCustomCulling;
        }
        protected override void OnDisable() // 06002e4e: preserve original teardown/fault order.
        {
            base.OnDisable();
            if (m_cinemachineCameraManagerRef.IsValid())
                m_cinemachineCameraManagerRef.Get().OnMainCameraReady -= SetupCustomCulling;
            if (m_cullingGroup != null)
            {
                m_cullingGroup.onStateChanged -= OnStateChanged;
                m_cullingGroup.Dispose();
                m_cullingGroup = null;
            }
            m_cinemachineCameraManagerRef.OnSystemStartup -= CameraRegistered;
        }
        private void SetupCustomCulling(Camera mainCamera) // 06002e4f.
        {
            if (this == null) return;
            if (m_cullingGroup == null)
            {
                m_cullingGroup = new CullingGroup();
                m_cullingGroup.SetBoundingSpheres(new[] { new BoundingSphere(transform.position, m_cullingBoundsSize) });
                m_cullingGroup.SetBoundingSphereCount(1);
                m_cullingGroup.onStateChanged += OnStateChanged;
                Cull(m_cullingGroup.IsVisible(0));
            }
            m_cullingGroup.targetCamera = mainCamera;
            m_cullingGroup.enabled = true;
        }
        private void OnStateChanged(CullingGroupEvent sphere) { Cull(sphere.isVisible); } // 06002e50.
        private void Cull(bool visible) // 06002e51: main-system Unity null check only.
        {
            if (m_mainParticleSystem == null) return;
            if (visible) m_mainParticleSystem.Play(true);
            else m_mainParticleSystem.Pause(true);
            SetRenderers(visible);
        }
        protected void SetRenderers(bool enable) // 06002e52: retain direct array/element faults.
        {
            foreach (Renderer renderer in m_particleRenderers) renderer.enabled = enable;
        }
        protected override void OnValidate() // 06002e53.
        {
            base.OnValidate();
            if (m_mainParticleSystem == null) m_mainParticleSystem = GetComponent<ParticleSystem>();
            if (!m_subParticleSystems.IsArrayValid(true)) RegenerateSubsystemsList();
            if (!m_trailRenderers.IsArrayValid(true) || m_trailRenderers.Length == 0)
                m_trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
        }
        public bool RegenerateSubsystemsList() // 06002e54: membership comparison ignores ordering.
        {
            List<ParticleSystem> systems = new List<ParticleSystem>(GetComponentsInChildren<ParticleSystem>(true));
            systems.Remove(m_mainParticleSystem);
            bool changed = m_subParticleSystems == null || systems.Count != m_subParticleSystems.Length;
            if (!changed)
            {
                foreach (ParticleSystem system in systems)
                {
                    if (!m_subParticleSystems.Contains(system)) { changed = true; break; }
                }
                if (!changed)
                {
                    foreach (ParticleSystem system in m_subParticleSystems)
                    {
                        if (!systems.Contains(system)) { changed = true; break; }
                    }
                }
            }
            if (!changed) return false;
            m_subParticleSystems = systems.ToArray();
            m_particleRenderers = m_mainParticleSystem.GetComponentsInChildren<Renderer>();
            return true;
        }
        protected override void InternalUpdate(float deltaTime) { UpdateTimescale(); } // 06002e55.
        public override void OnPause() { base.OnPause(); UpdateTimescale(); } // 06002e56.
        private void UpdateTimescale() // 06002e57: repeated live reads and final cached speed matter.
        {
            if (!Mathf.Approximately(m_lastSimulationSpeed, m_mainParticleSystem.main.simulationSpeed))
                m_simulationSpeed = m_mainParticleSystem.main.simulationSpeed;
            float simulationSpeed = m_simulationSpeed * GetTimescale();
            if (!Mathf.Approximately(simulationSpeed, m_mainParticleSystem.main.simulationSpeed))
            {
                ParticleSystem.MainModule main = m_mainParticleSystem.main;
                main.simulationSpeed = simulationSpeed;
                foreach (ParticleSystem system in m_subParticleSystems)
                {
                    if (system.gameObject.activeInHierarchy)
                    {
                        ParticleSystem.MainModule sub = system.main;
                        sub.simulationSpeed = simulationSpeed;
                    }
                }
            }
            m_lastSimulationSpeed = simulationSpeed;
        }
        private string GetNullMainParticleSystemErrorMessage() // 06002e58: original lazy message.
        {
            if (m_cachedNullMainParticleSystemErrorMessage == null)
                m_cachedNullMainParticleSystemErrorMessage = string.Concat("Null main particle system on ", GetSceneHierarchy(), ".");
            return m_cachedNullMainParticleSystemErrorMessage;
        }
        private string GetSceneHierarchy() // 06002e59: real TransformUtils dependency remains required.
        {
            if (m_cachedSceneHierarchy == null) m_cachedSceneHierarchy = TransformUtils.GetSceneHierarchy(transform);
            return m_cachedSceneHierarchy;
        }
        public TimeScaledParticleSystem() { } // 06002e5a: bounds2 then genuine SystemRef then base.
    }
}
