using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Utils
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class Achievement
    {
        private readonly string m_platformID;
        private float m_target;
        private float m_prerequisiteTarget;
        protected float m_progress;
        private Func<int> m_tracker;
        private readonly Action m_cleanupCallback;
        private bool m_reportedComplete;
        private bool m_currentlyReporting;
        private bool m_reportIsQueued;
        private const double MaxPercent = 100.0;

        // Original HLUnityCore.Runtime 06001101, ARM1b35328/x861b2eb70.
        public bool ReportedComplete => m_reportedComplete;

        // Original06001102: platform ID only; target and callback retain zero/null defaults.
        public Achievement(string platformID)
        {
            m_platformID = platformID;
        }

        // Original06001103: subscription precedes publication of the cleanup closure.
        public Achievement(string platformID, float target, float prerequisiteTarget,
            ref Action evaluateOn, Func<int> tracker, Action<Action> cleanupCallback)
        {
            m_platformID = platformID;
            m_target = Math.Abs(target);
            m_prerequisiteTarget = prerequisiteTarget;
            m_tracker = tracker;
            m_progress = 0f;
            evaluateOn += Evaluate;
            m_cleanupCallback = () => cleanupCallback(Evaluate);
        }

        // Original06001104 copies the platform progress directly without rescaling.
        public void SyncPlatformValues(float percentageProgress, bool reportedComplete)
        {
            m_progress = percentageProgress;
            m_reportedComplete = reportedComplete;
        }

        // Original06001105: ARM CNEG and x86 NEG/CMOV retain int.MinValue as negative.
        // The explicit unchecked expression preserves that shipped operand behavior.
        public void AddBehaviour(int target, ref Action evaluateOn,
            Func<int> trackerFunction, int prerequisiteTarget)
        {
            m_target = unchecked(target < 0 ? -target : target);
            evaluateOn += Evaluate;
            m_tracker = trackerFunction;
            m_prerequisiteTarget = prerequisiteTarget;
        }

        // Original06001106 and natural0600110f/110: queue, callback state and retry order.
        #if PROJECT_LUCID_ORIGINAL_GAMECENTER
        protected void ReportProgress()
        {
            if (m_currentlyReporting)
            {
                m_reportIsQueued = true;
                return;
            }
            float completionProgress = Mathf.Clamp01(m_target == 0f ? 1f : m_progress / m_target);
            if (m_prerequisiteTarget != 0f && m_progress < m_prerequisiteTarget)
                return;
            if (Social.localUser.authenticated)
            {
                m_currentlyReporting = true;
                Social.ReportProgress(m_platformID, (double)completionProgress * MaxPercent, success =>
                {
                    if (success)
                        m_reportedComplete = (int)completionProgress == 1;
                    m_currentlyReporting = false;
                    if (m_reportIsQueued)
                    {
                        m_reportIsQueued = false;
                        ReportProgress();
                    }
                });
            }
            else
            {
                m_reportedComplete = (int)completionProgress == 1;
            }
        }
#else
        protected void ReportProgress()
        {
            if (m_currentlyReporting)
            {
                m_reportIsQueued = true;
                return;
            }
            float completionProgress = Mathf.Clamp01(m_target == 0f ? 1f : m_progress / m_target);
            if (m_prerequisiteTarget != 0f && m_progress < m_prerequisiteTarget)
                return;
            m_reportedComplete = ProjectLucid.Offline.LocalAchievementRuntime.ReportCompletion(this, m_platformID, (int)completionProgress == 1);
        }
#endif

        // Original06001107 retains virtual tracking and target fallback.
        protected virtual void TrackProgress()
        {
            m_progress = m_tracker != null ? m_tracker() : m_target;
        }

        // Original06001108 dispatches to the original virtual Evaluate method only.
        public void RefreshProgress()
        {
            Evaluate();
        }

        // Original06001109: reported guard, tracking, report, then completion cleanup.
        protected virtual void Evaluate()
        {
            if (m_reportedComplete)
                return;
            TrackProgress();
            ReportProgress();
            Cleanup(true);
        }

        // Original0600110a skips only ordered progress<target; NaN still reaches cleanup.
        protected virtual void Cleanup(bool onlyIfComplete)
        {
            if (onlyIfComplete && m_progress < m_target)
                return;
            m_cleanupCallback?.Invoke();
        }

        // Original0600110b conditionally tracks, then always tests completion cleanup.
        public virtual void OnLogin()
        {
            if (m_tracker != null)
                TrackProgress();
            Cleanup(true);
        }

        // Original0600110c performs unconditional cleanup, without reporting.
        public virtual void OnDestroy()
        {
            Cleanup(false);
        }
    }
}
