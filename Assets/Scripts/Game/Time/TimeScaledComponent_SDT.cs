// Complete original game time-scaled descendant; real LevelManager remains required.
// Native-derived private source candidate; no fabricated callback/manager or acceptance.
using Hardlight;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.IL2CPP.CompilerServices;
namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class TimeScaledComponent_SDT : TimeScaledComponent // original020007fc
    {
        private const TimeCategory DefaultTimeCategory = TimeCategory.Environment;
        [Tooltip("Category for the time to be updated to allow time scaling affect."), SerializeField,
         FormerlySerializedAs("m_timeCategory")]
        private TimeCategory m_timeCategoryEnum = DefaultTimeCategory;
        [SerializeField, Tooltip("Whether synchronisation is active and whether it is from awake or enable.")]
        private SynchroniseType m_synchronise;
        private bool m_synchroniseActive;
        private float m_enableTime;
        private float m_syncTime;
        private SystemRef<LevelManager> m_levelManagerRef;
        private static TimeCategoryLookup s_config_Internal;
        private static TimeCategoryLookup s_config // 2e39: Unity destroyed-object null semantics.
        {
            get
            {
                if (s_config_Internal == null) s_config_Internal = SystemConfiguration.GetConfig<TimeCategoryLookup>();
                return s_config_Internal;
            }
        }
        public TimeCategory TimeCategoryEnum => m_timeCategoryEnum; // 2e3a
        protected override void Awake() { ApplyTimeCategoryObjectConversion(); base.Awake(); } // 2e3b
        protected override void Initialise(TimeManager timeManager) // 2e3c
        {
            base.Initialise(timeManager);
            if (m_synchronise == SynchroniseType.None) return;
            m_syncTime = GetTotalTime();
            if (!m_synchroniseActive)
            {
                m_synchroniseActive = true;
                m_enableTime = m_syncTime;
            }
            OnSynchronise(m_syncTime); // Original return value is deliberately unused.
        }
        public override void OnUpdate(float deltaTime) { if (enabled) SynchroniseUpdate(deltaTime); } // 2e3d
        public override void OnFixedUpdate(float deltaTime) { if (enabled) SynchroniseUpdate(deltaTime); } // 2e3e
        public override void OnLateUpdate(float deltaTime) { if (enabled) SynchroniseUpdate(deltaTime); } // 2e3f
        public virtual void ResetSynchronise() { m_synchroniseActive = false; } // 2e40
        protected virtual float OnSynchronise(float time) // 2e41
        {
            switch (m_synchronise)
            {
                case SynchroniseType.OnAwake: return time;
                case SynchroniseType.OnEnable: return time - m_enableTime;
                case SynchroniseType.OnLevelActivation:
                    m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>();
                    m_levelManagerRef.InvokeOnValid(OnLevelManagerValid);
                    return 0f;
                default: return 0f;
            }
        }
        private void SynchroniseUpdate(float deltaTime) // 2e42; exact zero suppresses update, NaN does not.
        {
            if (m_synchronise != SynchroniseType.None)
            {
                float previousTime = m_syncTime;
                m_syncTime = GetTotalTime();
                deltaTime = m_syncTime - previousTime;
                if (deltaTime == 0f) return;
            }
            InternalUpdate(deltaTime);
        }
        private void OnLevelManagerValid(LevelManager levelManager) // 2e43
        { levelManager.InvokeOnLevelActivated(OnLevelActivated, true); }
        private void OnLevelActivated(LevelManagerLevel levelManagerLevel) { ResetSynchronise(); } // 2e44
        protected override void OnValidate() { ApplyTimeCategoryObjectConversion(); base.OnValidate(); } // 2e45
        private void ApplyTimeCategoryObjectConversion() // 2e46: preserve already assigned object identity.
        {
            if (TimeCategoryObject != null) return;
            if (s_config.Dictionary.TryGetValue(m_timeCategoryEnum, out TimeCategoryObject category))
                TimeCategoryObject = category;
        }
        protected override TimeCategoryObject GetDefaultTimeCategory() // 2e47
        {
            m_timeCategoryEnum = GetDefaultTimeCategoryEnum();
            ApplyTimeCategoryObjectConversion();
            return TimeCategoryObject;
        }
        protected virtual TimeCategory GetDefaultTimeCategoryEnum() { return DefaultTimeCategory; } // 2e48
        protected override void Shutdown() // 2e49; invalid reference stays retained.
        {
            base.Shutdown();
            if (m_levelManagerRef == null || m_levelManagerRef.IsNull()) return;
            m_levelManagerRef.Get().RemoveLevelActivatedAction(OnLevelActivated);
            m_levelManagerRef = null;
        }
        protected TimeScaledComponent_SDT() { } // 2e4a; only enum default initializer.
        public enum SynchroniseType // genuine enum literals; native body credit0.
        {
            None = 0,
            OnAwake = 1,
            OnEnable = 2,
            OnLevelActivation = 3
        }
    }
}
