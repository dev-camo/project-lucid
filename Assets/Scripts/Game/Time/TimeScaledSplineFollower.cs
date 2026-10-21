using Dreamteck.Splines;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000801 (1.10.1). Scales a Dreamteck SplineFollower's speed by its
    // time category, restoring the authored speed whenever something else changed it.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class TimeScaledSplineFollower : TimeScaledComponent_SDT
    {
        [SerializeField] private SplineFollower m_splineFollower;
        private float m_followSpeed;
        private float m_lastFollowSpeed;

        // 06002e60 (RVA 0x5FB790): Unity null semantics.
        public override bool CheckIsValid()
        {
            return m_splineFollower != null;
        }

        // 06002e61 (RVA 0x5FB7F0): only a valid follower subscribes and captures its authored speed.
        protected override void Initialise(TimeManager timeManager)
        {
            if (CheckIsValid())
            {
                base.Initialise(timeManager);
                m_followSpeed = m_lastFollowSpeed = m_splineFollower.followSpeed;
            }
        }

        // 06002e62 (RVA 0x5FB840)
        protected override void OnValidate()
        {
            base.OnValidate();
            if (gameObject.CanValidate() && m_splineFollower == null)
                m_splineFollower = GetComponent<SplineFollower>();
        }

        // 06002e63 (RVA 0x5FB8F0): the delta time is unused.
        protected override void InternalUpdate(float deltaTime)
        {
            UpdateTimescale();
        }

        // 06002e64 (RVA 0x5FBAD0)
        public override void OnPause()
        {
            base.OnPause();
            UpdateTimescale();
        }

        // 06002e65 (RVA 0x5FB9E0): a speed that differs from the last one written was set by
        // someone else, so it becomes the new unscaled speed.
        private void UpdateTimescale()
        {
            if (!Mathf.Approximately(m_splineFollower.followSpeed, m_lastFollowSpeed))
                m_followSpeed = m_splineFollower.followSpeed;
            m_splineFollower.followSpeed = GetTimescale() * m_followSpeed;
            m_lastFollowSpeed = m_splineFollower.followSpeed;
        }
    }
}
