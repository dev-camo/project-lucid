// Original Game.Runtime TimeScaledCategoryAnimation (0x020007f9), all four own bodies
// and the genuine nested CategoryAnimation (0x020007fa), all three own bodies.
// This private candidate still requires the complete original TimeScaledComponent_SDT
// and its genuine LevelManager providers before the game closure can compile.
using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using UnityEngine.Events;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TimeScaledCategoryAnimation : TimeScaledComponent_SDT
    {
        [SerializeField, Tooltip("Defines the category groups to be animated over time.")]
        private List<CategoryAnimation> m_categoryAnimation;
        [SerializeField, Tooltip("Events to be fired on complete.")]
        private UnityEvent m_onComplete;
        private float m_time;
        private bool m_complete;
        private TimeSetting m_timeSetting;
        private StackableDataHandle m_timeOverride;

        protected override void Initialise(TimeManager timeManager) // 0x06002e2e
        {
            base.Initialise(timeManager);
            m_time = 0f;
            m_timeSetting = TimeSetting.GetDefault(1f);
            m_complete = false;
            m_timeOverride = timeManager.ApplyTimeSetting(m_timeSetting);
        }

        protected override void InternalUpdate(float deltaTime) // 0x06002e2f
        {
            if (m_complete) return;
            m_complete = true;
            m_time += deltaTime;
            foreach (CategoryAnimation categoryAnimation in m_categoryAnimation)
            {
                AnimationCurve animation = categoryAnimation.Animation;
                float scale = animation.Evaluate(m_time);
                foreach (TimeCategoryObject category in categoryAnimation.Categories)
                    m_timeSetting.SetCategory(category, scale);
                // Non-short-circuit accumulation also reads TotalTime for later entries.
                m_complete &= m_time >= animation.TotalTime();
            }
            UpdateTimeSetting(m_timeOverride, m_timeSetting);
            if (m_complete && m_onComplete != null) m_onComplete.Invoke();
        }

        protected override void Shutdown() // 0x06002e30; base failure retains the handle.
        {
            base.Shutdown();
            if (m_timeOverride != null)
            {
                RemoveTimeSetting(m_timeOverride);
                m_timeOverride = null;
            }
        }

        // Original 0x06002e31 allocates no category list, curve or event.
        public TimeScaledCategoryAnimation() { }

        [Serializable]
        public class CategoryAnimation
        {
            [SerializeField, Tooltip("Categories to be animated over time.")]
            private List<TimeCategoryObject> m_categories;
            [Tooltip("Time animation over time."), SerializeField]
            private AnimationCurve m_animation;
            public List<TimeCategoryObject> Categories => m_categories; // 0x06002e32
            public AnimationCurve Animation => m_animation; // 0x06002e33
            public CategoryAnimation() { } // 0x06002e34; both serialized fields stay null.
        }
    }
}
