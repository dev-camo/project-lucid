using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [RequireComponent(typeof(Rigidbody))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class RigidbodyGravity : CustomGravity, ITimeScaled
    {
        [SerializeField]
        [Tooltip("The time category for gravity to be applied in.")]
        private TimeCategory m_timeCategory = TimeCategory.PlayerPhysics;
        public bool IsPaused { get; set; }
        public Vector3 Gravity => m_gravity;
        public Vector3 GravityNormalised => m_gravityNormalised;
        public float GravityMagnitude => m_gravityMagnitude;
        public bool CanApplyForce { get; set; } = true;
        private Vector3 m_gravity;
        private Vector3 m_gravityNormalised;
        private float m_gravityMagnitude;
        private Rigidbody m_body;
        private SystemRef<TimeManager> m_timeManagerRef;

        // Original 0600298a: store the actual body, disable engine gravity, then query.
        private void Awake()
        {
            m_body = GetComponent<Rigidbody>();
            m_body.useGravity = false;
            SetGravity();
        }

        private void OnEnable()
        {
            m_timeManagerRef = ProcessManager.GetSystemRef<TimeManager>(null, true);
            m_timeManagerRef.InvokeOnValid(SubscribeToTimeManager);
            SetGravity();
        }

        private void SubscribeToTimeManager(TimeManager timeManager)
        {
            timeManager.Subscribe(this, m_timeCategory, UpdateOn.FixedUpdate, "");
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (m_timeManagerRef.IsNull())
                return;
            m_timeManagerRef.Get().Unsubscribe(this, m_timeCategory, UpdateOn.FixedUpdate);
        }

        // 0600298e: refresh gravity even when force is disabled. The supplied game
        // squares the category/global ratio and uses the mass-scaled vector with
        // ForceMode.Acceleration; preserve that behavior and the unused deltaTime.
        public void OnFixedUpdate(float deltaTime)
        {
            SetGravity();
            if (!CanApplyForce)
                return;
            TimeManager timeManager = m_timeManagerRef.Get();
            float ratio = timeManager.GetTimescale(m_timeCategory) /
                          timeManager.GetTimescale(TimeCategory.UnityGlobal);
            m_body.AddForce(m_gravity * (ratio * ratio), ForceMode.Acceleration);
        }

        // 0600298f/90 are genuine RET callbacks in the supplied player.
        public void OnUpdate(float deltaTime) { }
        public void OnLateUpdate(float deltaTime) { }

        // 06002991 deliberately divides by zero magnitude. Do not substitute the
        // engine's guarded Vector3.normalized, which would erase original NaNs.
        private void SetGravity()
        {
            float mass = m_body.mass;
            m_gravity = mass * GetGravity(m_body.position);
            float magnitude = m_gravity.magnitude;
            m_gravityNormalised = m_gravity / magnitude;
            m_gravityMagnitude = magnitude;
        }
    }
}
