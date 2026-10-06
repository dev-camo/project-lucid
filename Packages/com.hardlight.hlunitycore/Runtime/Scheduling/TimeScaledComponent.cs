using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime02000248. This is Actor's real inherited time
    // lifecycle; authored descendants supply the abstract InternalUpdate body.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class TimeScaledComponent : MonoBehaviour, ITimeScaled
    {
        private const UpdateOn DefaultUpdateOn = UpdateOn.FixedUpdate;
        [SerializeField]
        [Tooltip("Category for the time to be updated to allow time scaling affect.")]
        private TimeCategoryObject m_timeCategoryObject;
        [SerializeField]
        [Tooltip("The type of update, ie frame or physics time.")]
        protected UpdateOn m_updateOn = DefaultUpdateOn;
        [Tooltip("If object is enabled by an activator then the initial scene load enable will be ignored for synchronisation.")]
        [SerializeField]
        private bool m_enabledByActivator;
        public UpdateOn UpdateOn { get => m_updateOn; set => m_updateOn = value; }
        public bool IsPaused { get; set; }
        private bool m_ignoreEnable;
        // Original06000ec7 is this initializer, retaining BeforeFieldInit. The
        // real static reference is shared by every descendant and instance.
        private static readonly SystemRef<TimeManager> m_timeManagerRef =
            ProcessManager.GetSystemRef<TimeManager>(null, true);
        public TimeCategoryObject TimeCategoryObject
        {
            get => m_timeCategoryObject;
            set => m_timeCategoryObject = value;
        }

        // Original06000eb0 copies the authored activator flag once at Awake.
        protected virtual void Awake() { m_ignoreEnable = m_enabledByActivator; }
        protected virtual void OnEnable()
        {
            if (m_ignoreEnable)
            {
                m_ignoreEnable = false;
                return;
            }
            m_timeManagerRef.InvokeOnValid(Initialise);
        }
        protected virtual void Initialise(TimeManager timeManager)
        {
            timeManager.Subscribe(this, m_timeCategoryObject, m_updateOn, "");
        }
        protected float GetTotalTime()
        {
            TimeManager timeManager = m_timeManagerRef.Get();
            UpdateOn updateOn = m_updateOn;
            TimeCategoryObject category = m_timeCategoryObject;
            return updateOn == DefaultUpdateOn
                ? timeManager.GetTotalFixedTime(category) : timeManager.GetTotalTime(category);
        }
        protected float GetDeltaTime()
        {
            // Snapshot receiver and category before the real Unity time getter.
            TimeManager timeManager = m_timeManagerRef.Get();
            UpdateOn updateOn = m_updateOn;
            TimeCategoryObject category = m_timeCategoryObject;
            float deltaTime = updateOn == DefaultUpdateOn ? Time.fixedDeltaTime : Time.deltaTime;
            return deltaTime * timeManager.GetTimescale(category);
        }
        public virtual void OnUpdate(float deltaTime) { InternalUpdate(deltaTime); }
        public virtual void OnFixedUpdate(float deltaTime) { InternalUpdate(deltaTime); }
        public virtual void OnLateUpdate(float deltaTime) { InternalUpdate(deltaTime); }
        protected virtual void Shutdown()
        {
            if (m_timeManagerRef.TryGet(out TimeManager timeManager))
                timeManager.Unsubscribe(this, m_timeCategoryObject, m_updateOn);
        }
        protected virtual void OnDisable() { Shutdown(); }
        // These three hooks are genuine original native RET bodies; pause state
        // changes belong to TimeManager rather than these optional callbacks.
        protected virtual void OnValidate() { }
        public virtual void OnPause() { }
        public virtual void OnResume() { }
        public virtual bool CheckIsValid() { return true; }
        protected abstract void InternalUpdate(float deltaTime); // 06000ebe contract0.
        protected virtual TimeCategoryObject GetDefaultTimeCategory() { return null; }
        protected virtual UpdateOn GetDefaultUpdateOn() { return DefaultUpdateOn; }
        protected virtual void Reset()
        {
            m_timeCategoryObject = GetDefaultTimeCategory();
            m_updateOn = GetDefaultUpdateOn();
        }
        public virtual void OnDestroy() { Shutdown(); }
        protected float GetTimescale()
        {
            TimeManager timeManager = m_timeManagerRef.Get();
            return timeManager.GetTimescale(m_timeCategoryObject);
        }
        protected void UpdateTimeSetting(StackableDataHandle timeOverride, TimeSetting timeSetting)
        {
            // Resolve the required real manager even for a null handle; only the
            // subsequent stack operation is conditional in the shipped body.
            TimeManager timeManager = m_timeManagerRef.Get();
            if (timeOverride != null) timeManager.UpdateTimeSetting(timeOverride, timeSetting);
        }
        protected void RemoveTimeSetting(StackableDataHandle timeOverride)
        {
            TimeManager timeManager = m_timeManagerRef.Get();
            if (timeOverride != null) timeManager.RemoveTimeSetting(timeOverride);
        }
        protected TimeScaledComponent() { }
    }
}
