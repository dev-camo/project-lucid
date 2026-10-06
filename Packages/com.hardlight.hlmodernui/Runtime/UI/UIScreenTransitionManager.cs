using System;
using System.Collections;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIScreenTransitionManager : MonoBehaviour, ISystem
    {
        [SerializeField] private int m_maximumTimeoutSeconds = 15;
        [SerializeField] private UIContainerIdentifier m_defaultTransitionIdentifier;
        private const int StackableDataMidpointWaitID = 0;
        private readonly SystemRef<UIManager> m_uiManagerRef = ProcessManager.GetSystemRef<UIManager>();
        private readonly StackableData m_transitionStackableData = new StackableData();
        private UIContainerIdentifier m_currentTransitionIdentifier;
        private IUIScreenTransition m_currentTransition;
        private Coroutine m_maximumTimeoutCoroutine;

        // HLModernUI.Runtime06000125: true LogicalAnd base value precedes registration.
        private void Awake()
        {
            m_transitionStackableData.SetBaseValue(StackableDataMidpointWaitID, true, StackableData.RetrievalOperation.LogicalAnd);
            ProcessManager.RegisterSystem(this);
        }
        //06000126/127: the authored identifier fallback uses the original GUID
        // equality operator. A live null-GUID identifier is still unequal to null.
        public void QueueTransition(Action onMidpointCallback = null, Action onCompletionCallback = null)
        {
            QueueTransition(m_defaultTransitionIdentifier, onMidpointCallback, onCompletionCallback);
        }
        public void QueueTransition(UIContainerIdentifier containerIdentifier, Action onMidpointCallback = null,
            Action onCompletionCallback = null, Action onTimeoutCallback = null)
        {
            if (containerIdentifier == null) containerIdentifier = m_defaultTransitionIdentifier;
            StartCoroutine(WaitForTransition(containerIdentifier, onMidpointCallback, onCompletionCallback, onTimeoutCallback));
        }
        //06000128/135 + natural d__11 six-method iterator: even an idle manager
        // first yields its real WaitUntil object. No eager start fast path.
        private IEnumerator WaitForTransition(UIContainerIdentifier containerIdentifier, Action onMidpointCallback,
            Action onCompletionCallback, Action onTimeoutCallback)
        {
            yield return new WaitUntil(() => !IsTransitionActive());
            StartTransition(containerIdentifier, onMidpointCallback, onCompletionCallback, onTimeoutCallback);
        }

        //06000129 + natural DisplayClass12_0 callbacks: acquire the actual UI
        // container/component before publishing transition fields and stopping old timeout.
        private void StartTransition(UIContainerIdentifier containerIdentifier, Action onMidpointCallback,
            Action onCompletionCallback, Action onTimeoutCallback)
        {
            IUIScreenTransition transition = null;
            if (m_uiManagerRef.Get().GetOrCreate(containerIdentifier).TryGetComponent(out transition))
            {
                m_currentTransitionIdentifier = containerIdentifier;
                m_currentTransition = transition;
                this.SafeStopCoroutine(ref m_maximumTimeoutCoroutine);
                Action timeoutCallback = onTimeoutCallback ?? onCompletionCallback;
                if (m_maximumTimeoutCoroutine == null)
                    m_maximumTimeoutCoroutine = StartCoroutine(OnTransitionMaximumTimeout(timeoutCallback));
                m_currentTransition.StartTransition(() => OnReachMidpoint(onMidpointCallback),
                    () => OnTransitionComplete(onCompletionCallback));
            }
        }
        //0600012a: the caller callback runs before reading the wait stack. It can
        // add/release overrides, affecting the same invocation's continuation.
        private void OnReachMidpoint(Action onMidpointCallback)
        {
            onMidpointCallback?.Invoke();
            if (m_transitionStackableData.Get<bool>(StackableDataMidpointWaitID)) ContinueTransition();
            else m_transitionStackableData.OnDataUpdated += OnMidpointWaitValueUpdated;
        }
        //0600012b: stop timeout, invoke current transition, then unsubscribe.
        // No finally hides exceptions or prevents the original reentrant event behavior.
        private void ContinueTransition()
        {
            this.SafeStopCoroutine(ref m_maximumTimeoutCoroutine);
            m_currentTransition.ContinueTransition();
            m_transitionStackableData.OnDataUpdated -= OnMidpointWaitValueUpdated;
        }
        //0600012c: only the authored wait ID and a true computed result continue.
        private void OnMidpointWaitValueUpdated(int id)
        {
            if (id == StackableDataMidpointWaitID && m_transitionStackableData.Get<bool>(StackableDataMidpointWaitID))
                ContinueTransition();
        }
        //0600012d: guard the identifier first, invoke the callback, then read the
        // live manager/current identifier again. Clear only after UIManager.Close returns.
        private void OnTransitionComplete(Action onCompletionCallback)
        {
            if (m_currentTransitionIdentifier == null) return;
            onCompletionCallback?.Invoke();
            m_uiManagerRef.Get().Close(m_currentTransitionIdentifier);
            m_currentTransitionIdentifier = null;
            m_currentTransition = null;
            m_transitionStackableData.OnDataUpdated -= OnMidpointWaitValueUpdated;
            this.SafeStopCoroutine(ref m_maximumTimeoutCoroutine);
        }
        //0600012e + natural d__17 six-method iterator: original integer timeout
        // becomes a float realtime wait on first MoveNext, not wrapper construction.
        private IEnumerator OnTransitionMaximumTimeout(Action onCompletionCallback)
        {
            yield return new WaitForSecondsRealtime(m_maximumTimeoutSeconds);
            EndMaximumTimeout(onCompletionCallback);
        }
        //0600012f: clear the owner handle before the authored GUID activity test.
        private void EndMaximumTimeout(Action onCompletionCallback)
        {
            m_maximumTimeoutCoroutine = null;
            if (IsTransitionActive()) OnTransitionComplete(onCompletionCallback);
        }
        //06000130/131: activity is GUID inequality; midpoint delegates directly
        // and retains the native missing-current-transition failure boundary.
        public bool IsTransitionActive() { return m_currentTransitionIdentifier != null; }
        public bool HasReachedMidPoint() { return m_currentTransition.HasReachedMidpoint; }
        //06000132/133: false overrides participate in the existing LogicalAnd stack.
        public StackableDataHandle AddWaitForMidpointOverride()
        {
            return m_transitionStackableData.AddOverride(StackableDataMidpointWaitID, false);
        }
        public void ReleaseWaitForMidpointHandle(StackableDataHandle handle)
        {
            m_transitionStackableData.RemoveOverrides(handle);
        }
        //06000134: original field initializers preserve timeout/ref/stack/base order.
    }
}
