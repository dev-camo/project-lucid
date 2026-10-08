using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 02000209; complete 06000d6b..06000d75.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class PerformanceDependency : MonoBehaviour
    {
        public enum Behaviour { TriggerEvent = 0, Hide = 1, Destroy = 2, Callback = 3 }
        public enum Comparison { GreaterThanOrEqual = 0, LessThan = 1, Exactly = 2 }

        [SerializeField] private PerformanceAttribute m_requiredPerformanceAttribute;
        [SerializeField] private Comparison m_comparison;
        [SerializeField] private Behaviour m_onDependencyNotMet = Behaviour.Hide;
        [ShowIf("m_onDependencyNotMet", Behaviour.TriggerEvent)]
        [SerializeField] private UnityEvent m_triggeredEvent;
        // Original 06000d75 uses the genuine registry with null name/autoRegister=true.
        private static readonly SystemRef<ScalablePerformance> s_scalablePerformanceRef =
            ProcessManager.GetSystemRef<ScalablePerformance>(null, true);
        // Original 06000d6b/6c retain compiler-generated combine/remove CAS accessors.
        public event Action<bool> ValidateDependencyCallback;

        // 06000d6d: subscribe in startup/shutdown order before checking the live reference.
        private void Awake()
        {
            s_scalablePerformanceRef.OnSystemStartup += ScalablePerformanceStartup;
            s_scalablePerformanceRef.OnSystemShutdown += OnScalablePerformanceShutdown;
            if (s_scalablePerformanceRef.IsValid())
                ScalablePerformanceStartup(s_scalablePerformanceRef.Get());
        }

        // 06000d6e: add the profile callback before reading the current profile.
        private void ScalablePerformanceStartup(ScalablePerformance scalablePerformance)
        {
            scalablePerformance.OnProfileUpdated += OnPerformanceProfileChanged;
            OnPerformanceProfileChanged(scalablePerformance.ActiveProfile);
        }

        // 06000d6f: the event and callback delegate are deliberately not copied.
        public void Copy(PerformanceDependency otherDependency)
        {
            m_requiredPerformanceAttribute = otherDependency.m_requiredPerformanceAttribute;
            m_comparison = otherDependency.m_comparison;
            m_onDependencyNotMet = otherDependency.m_onDependencyNotMet;
        }

        // 06000d70: an absent registry manager performs no validation callback.
        public void ScalablePerformanceRefresh()
        {
            if (s_scalablePerformanceRef.IsValid())
                OnPerformanceProfileChanged(s_scalablePerformanceRef.Get().ActiveProfile);
        }

        // 06000d71.
        private void OnScalablePerformanceShutdown(ScalablePerformance scalablePerformance)
        {
            scalablePerformance.OnProfileUpdated -= OnPerformanceProfileChanged;
        }

        // 06000d72: preserve GUID-based profile equality, then plain reference-null
        // attribute testing. Behaviour is reread after the actual support predicate.
        private void OnPerformanceProfileChanged(PerformanceProfile profile)
        {
            if (profile == null || m_requiredPerformanceAttribute == null)
                return;
            bool valid;
            switch (m_comparison)
            {
                case Comparison.GreaterThanOrEqual:
                    valid = profile.IsSupported(m_requiredPerformanceAttribute);
                    break;
                case Comparison.LessThan:
                    valid = !profile.IsSupported(m_requiredPerformanceAttribute);
                    break;
                case Comparison.Exactly:
                    valid = profile.IsExactlySupported(m_requiredPerformanceAttribute);
                    break;
                default:
                    return;
            }
            switch (m_onDependencyNotMet)
            {
                case Behaviour.TriggerEvent:
                    if (!valid) m_triggeredEvent?.Invoke();
                    break;
                case Behaviour.Hide:
                    gameObject.SetActive(valid);
                    break;
                case Behaviour.Destroy:
                    if (!valid) UnityEngine.Object.Destroy(gameObject);
                    break;
                case Behaviour.Callback:
                    ValidateDependencyCallback?.Invoke(valid);
                    break;
            }
        }

        // 06000d73: remove the live profile callback first, then registry startup
        // and shutdown handlers in that order. Each reference access remains fresh.
        private void OnDestroy()
        {
            if (s_scalablePerformanceRef.IsValid())
                OnScalablePerformanceShutdown(s_scalablePerformanceRef.Get());
            s_scalablePerformanceRef.OnSystemStartup -= ScalablePerformanceStartup;
            s_scalablePerformanceRef.OnSystemShutdown -= OnScalablePerformanceShutdown;
        }

        // 06000d74: the only nonzero instance default is Behaviour.Hide above.
        public PerformanceDependency() { }
    }
}
