using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SimplifiedBody : TimeScaledComponent_SDT
    {
        [SerializeField] private GravityProvider m_customGravity;
        [SerializeField] private Collider m_collider;
        [SerializeField] private Collider[] m_ignoreCollisionColliders;
        [SerializeField, Range(0f, 1f)] private float m_surfaceDampener;
        [Min(0f), SerializeField] private float m_mass = 1f;
        [SerializeField] private float m_sleepVelocityThreshold;
        [SerializeField] private string m_ignoreTag;
        [SerializeField, Range(0f, 90f), Tooltip("Assume we are on the ground if there is a collision within this angle tolerance of gravity.")]
        private float m_flatGroundAngleToleranceDegrees = 30f;
        [SerializeField, Tooltip("If true, gravity force is initially enabled.")]
        private bool m_gravityForceStartsEnabled = true;
        [SerializeField, Tooltip("If true, gravity force can be enabled.")]
        private bool m_gravityForceCanBeEnabled = true;
        [SerializeField, Tooltip("If true this object will not run depenetration on the player collider.")]
        private bool m_ignoreCharacterCollider = true;
        [SerializeField, Tooltip("Layers used for depenetration but any resulting uplift will be ignored.")]
        private LayerMask m_layersToIgnoreUplift;
        private LayerMask m_ignoreLayers;

        public const float MotionThreshold = 0.000001f;
        public Vector3 WorldVelocity { get; set; }
        public Vector3 Gravity { get; private set; } = Vector3.down;
        public Vector3 GravityNormalised { get; private set; } = Vector3.down;
        public bool GravityForceEnabled => m_gravityForceEnabled;
        public bool CollidingWithGround { get; private set; }
        public Collider Collider => m_collider;
        public GravityProvider CustomGravity => m_customGravity;

        private Transform m_transform;
        private LayerMask m_colliderLayerMask;
        private const int MaxHits = 16;
        private RaycastHit[] m_collisionRaycastHits = new RaycastHit[MaxHits];
        private List<Collider> m_collisionColliders = new List<Collider>(MaxHits);
        private List<GravityTrigger> m_gravityTriggerColliders = new List<GravityTrigger>(MaxHits);
        private List<GravityTrigger> m_activeGravityTriggers = new List<GravityTrigger>(MaxHits);
        private List<ITriggerCollider> m_triggerColliders = new List<ITriggerCollider>(MaxHits);
        private bool m_isSleeping;
        private SystemRef<TrackManager> m_trackManagerRef;
        private TrackManager m_trackManager;
        private SystemRef<SimplifiedBodyManager> m_simplifiedBodyManagerRef;
        private SimplifiedBodyManager m_simplifiedBodyManager;
        private SystemRef<LevelManager> m_levelManagerRef;
        private LevelManager m_levelManager;
        private float m_flatGroundAngleToleranceCosine;
        private bool m_gravityForceEnabled;
        private bool m_initialised;

        private void Start()
        {
            m_gravityForceEnabled = m_gravityForceStartsEnabled;
            m_transform = transform;
            m_trackManagerRef = ProcessManager.GetSystemRef<TrackManager>();
            m_trackManagerRef.InvokeOnValid(RegisterTrackManager);
            m_simplifiedBodyManagerRef = ProcessManager.GetSystemRef<SimplifiedBodyManager>();
            m_simplifiedBodyManagerRef.InvokeOnValid(RegisterSimplifiedBodyManager);
            m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>();
            m_levelManagerRef.InvokeOnValid(LevelManagerValid);
            m_colliderLayerMask = m_collider.GetLayerMask();
            if (m_ignoreCharacterCollider)
                m_ignoreLayers = LayerMask.GetMask("Character");
            m_flatGroundAngleToleranceCosine = Mathf.Cos(m_flatGroundAngleToleranceDegrees * Mathf.Deg2Rad);
            m_initialised = true;
        }

        private void LevelManagerValid(LevelManager levelManager)
        {
            m_levelManager = levelManager;
            m_levelManager.InvokeOnLevelActivated(OnLevelActivated, true);
        }

        private void OnLevelActivated(LevelManagerLevel _)
        {
            if (!m_initialised || m_trackManager != null)
                return;
            m_trackManagerRef.InvokeOnValid(RegisterTrackManager);
        }

        private void RegisterTrackManager(TrackManager trackManager)
        {
            m_trackManager = trackManager;
        }

        private void RegisterSimplifiedBodyManager(SimplifiedBodyManager simplifiedBodyManager)
        {
            m_simplifiedBodyManager = simplifiedBodyManager;
            m_simplifiedBodyManager.RegisterSimplifiedBody(this, m_collider);
        }

        public void Close()
        {
            m_collisionColliders.Clear();
            m_gravityTriggerColliders.Clear();
            m_activeGravityTriggers.Clear();
            m_triggerColliders.Clear();
            if (m_simplifiedBodyManager != null)
                m_simplifiedBodyManager.UnregisterSimplifiedBody(this, m_collider);
        }

        protected override void InternalUpdate(float deltaTime)
        {
            UpdatePhysics(deltaTime);
        }

        private void UpdatePhysics(float deltaTime)
        {
            if (m_trackManager == null)
                return;
            if (m_isSleeping)
            {
                if (WorldVelocity.sqrMagnitude < m_sleepVelocityThreshold)
                    return;
                m_isSleeping = false;
            }
            if (m_customGravity != null)
            {
                Gravity = m_customGravity.GetGravity(m_transform.position);
                GravityNormalised = Gravity.normalized;
                if (m_gravityForceEnabled)
                    WorldVelocity += Gravity * (m_mass * deltaTime);
            }
            CollidingWithGround = false;
            Vector3 velocityDelta = WorldVelocity * deltaTime;
            FindCollisions(velocityDelta);
            Vector3 position = m_transform.position + velocityDelta;
            Vector3 totalDirection = Vector3.zero;
            float totalDistance = 0f;
            foreach (Collider collider in m_collisionColliders)
            {
                Transform colliderTransform = collider.transform;
                if (!Physics.ComputePenetration(m_collider, position, m_transform.rotation,
                    collider, colliderTransform.position, colliderTransform.rotation,
                    out Vector3 direction, out float distance))
                    continue;
                if (m_layersToIgnoreUplift.IncludesLayer(collider.gameObject.layer))
                {
                    direction -= Vector3.Dot(direction, GravityNormalised) * GravityNormalised;
                    position += direction * distance;
                    continue;
                }
                CollidingWithGround = CollidingWithGround ||
                    Mathf.Abs(Vector3.Dot(GravityNormalised, direction)) > m_flatGroundAngleToleranceCosine;
                position += direction * distance;
                totalDirection += direction * distance;
                totalDistance += distance;
            }
            if (totalDistance > 0f)
                WorldVelocity = Vector3.ProjectOnPlane(WorldVelocity, totalDirection / totalDistance);
            if ((m_transform.position - position).sqrMagnitude > MotionThreshold)
                m_transform.position = position;
            if (WorldVelocity.sqrMagnitude < m_sleepVelocityThreshold && m_collisionColliders.Count > 0)
                m_isSleeping = true;
            ResolveGravityTriggers();
            ResolveTriggerColliders();
        }

        private void FindCollisions(Vector3 velocityDelta)
        {
            int hitCount = m_collider.PhysicsCastFromCollider(velocityDelta, m_colliderLayerMask,
                m_collisionRaycastHits, QueryTriggerInteraction.Collide);
            m_collisionColliders.Clear();
            m_gravityTriggerColliders.Clear();
            m_triggerColliders.Clear();
            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = m_collisionRaycastHits[i].collider;
                if (!string.IsNullOrEmpty(m_ignoreTag) && collider.CompareTag(m_ignoreTag))
                    continue;
                if (collider == m_collider)
                    continue;
                bool ignoredCollider = false;
                foreach (Collider ignored in m_ignoreCollisionColliders)
                {
                    if (collider == ignored)
                    {
                        ignoredCollider = true;
                        break;
                    }
                }
                bool ignoredLayer = m_ignoreLayers.IncludesLayer(collider.gameObject.layer);
                if (ignoredCollider || ignoredLayer)
                    continue;
                if (!collider.isTrigger)
                {
                    m_collisionColliders.AddUnique(collider);
                    WorldVelocity *= 1f - m_surfaceDampener;
                }
                else if (m_trackManager.TryGetGravityTriggerForCollider(collider, out GravityTrigger gravityTrigger))
                    m_gravityTriggerColliders.AddUnique(gravityTrigger);
                else if (m_simplifiedBodyManager.TryGetSimplifiedBodyForCollider(collider, out SimplifiedBody simplifiedBody))
                {
                    m_collisionColliders.AddUnique(collider);
                    WorldVelocity *= 1f - m_surfaceDampener;
                }
                else if (m_trackManager.TryGetTrigger(collider, out ITriggerCollider triggerCollider))
                    m_triggerColliders.AddUnique(triggerCollider);
            }
        }

        private void ResolveGravityTriggers()
        {
            if (m_customGravity == null)
                return;
            for (int i = m_activeGravityTriggers.Count - 1; i >= 0; i--)
            {
                GravityTrigger gravityTrigger = m_activeGravityTriggers[i];
                if (m_gravityTriggerColliders.Contains(gravityTrigger))
                    continue;
                gravityTrigger.ManualTriggerExit(m_customGravity);
                m_activeGravityTriggers.RemoveAt(i);
            }
            foreach (GravityTrigger gravityTrigger in m_gravityTriggerColliders)
            {
                if (m_activeGravityTriggers.Contains(gravityTrigger))
                    continue;
                gravityTrigger.ManualTriggerEnter(m_customGravity);
                m_activeGravityTriggers.Add(gravityTrigger);
            }
        }

        private void ResolveTriggerColliders()
        {
            foreach (ITriggerCollider triggerCollider in m_triggerColliders)
                triggerCollider.ManualTriggerEnter(m_collider);
        }

        public void SetGravityForceEnabled(bool gravityForceEnabled)
        {
            m_gravityForceEnabled = m_gravityForceCanBeEnabled && gravityForceEnabled;
        }

        public override void OnDestroy()
        {
            if (m_levelManager == null)
                return;
            m_levelManager.RemoveLevelActivatedAction(OnLevelActivated);
            base.OnDestroy();
        }

        public SimplifiedBody() { }
    }
}
