using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000853; 0x60DD70..0x60E600 and the constructor 0x607800.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class ViewTarget : ViewBase
    {
        [Tooltip("World offset applied to targeted position.")]
        [SerializeField]
        protected Vector3 m_worldOffset;
        [Tooltip("Distance to fade alpha over.")]
        [SerializeField]
        private bool m_fadeOverDistance;
        [Tooltip("Normalised distance alpha values.")]
        [SerializeField]
        private AnimationCurve m_fadeCurve;
        [Tooltip("Distance to scale target over.")]
        [SerializeField]
        private bool m_resizeOverDistance;
        [Tooltip("Normalised distance scale values.")]
        [SerializeField]
        private AnimationCurve m_sizeCurve;
        [SerializeField]
        private float m_minRenderDistance;
        [SerializeField]
        private float m_maxRenderDistance = 100f;
        [SerializeField]
        protected List<HomingTargetType> m_showForHomingTargetTypes = new List<HomingTargetType>();
        [SerializeField]
        protected bool m_targetIsActiveBehindCamera = true;
        protected Vector3? m_targetWorldPosition;
        protected readonly SystemRef<CinemachineCameraManager> m_cameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>(null, true);
        private float m_timer;
        private Vector3 m_scale = Vector3.one;

        public virtual bool IsActive => gameObject.activeSelf;
        public Vector3 TargetWorldPosition => m_targetWorldPosition ?? Vector3.zero;
        private Collider m_targetCollider { get; set; }
        private Transform m_targetTransform { get; set; }
        protected abstract Vector3 TargetPosition { get; set; }
        protected abstract Vector3 TargetScale { get; set; }
        protected abstract Color TargetColour { get; set; }
        protected abstract bool ProjectToScreenSpace { get; }
        protected float MinRenderDistance => m_minRenderDistance;
        protected float MaxRenderDistance => m_maxRenderDistance;

        protected virtual void Awake()
        {
            ShowTarget(false);
        }

        // 0x607070: the parameters are cast directly (no as/is guard), so a wrong type throws, and the
        // current local scale becomes the base scale of the world-space resize.
        protected override void OnSetup(ViewParameters viewParams)
        {
            m_targetWorldPosition = ((ViewTargetParameters)viewParams).WorldspaceTarget;
            m_scale = transform.localScale;
        }

        public virtual void SetTargetCollider(Collider otherCollider)
        {
            m_targetCollider = otherCollider;
            m_targetWorldPosition = m_targetCollider.bounds.center;
        }

        public virtual void SetTargetTransform(Transform target)
        {
            m_targetTransform = target;
            m_targetWorldPosition = m_targetTransform.position;
        }

        public void SetTargetPosition(Vector3? target)
        {
            m_targetWorldPosition = target;
        }

        public void ClearTarget()
        {
            m_targetCollider = null;
            m_targetTransform = null;
            m_targetWorldPosition = null;
        }

        protected virtual bool TargetInRange(Vector3 targetScreenPosition)
        {
            return true;
        }

        protected virtual void Refresh()
        {
            m_timer = 0f;
        }

        // 0x60E070: a collider wins over a transform when refreshing the position.
        // The behind-camera test runs for both space modes and the visibility flags are
        // combined with a non-short-circuit and, so each update still executes.
        protected virtual void LateUpdate()
        {
            if (!m_targetWorldPosition.HasValue || m_cameraManagerRef.IsNull())
            {
                ShowTarget(false);
                return;
            }
            if (m_targetCollider != null)
                m_targetWorldPosition = m_targetCollider.bounds.center;
            else if (m_targetTransform != null)
                m_targetWorldPosition = m_targetTransform.position;
            bool isActive = IsActive;
            if (ProjectToScreenSpace)
                isActive &= UpdateTargetInScreenSpace(m_cameraManagerRef.Get().MainCamera);
            else
                UpdateTargetInWorldSpace();
            if (!m_targetIsActiveBehindCamera)
                isActive &= TargetPosition.z > 0f;
            ShowTarget(isActive);
        }

        // 0x60E270: the screen depth is normalised into the render range (1 at the
        // minimum distance, 0 at the maximum) to evaluate the fade and size curves.
        private bool UpdateTargetInScreenSpace(Camera camera)
        {
            Vector3 screenPosition = camera.WorldToScreenPoint(m_targetWorldPosition.Value + m_worldOffset);
            TargetPosition = screenPosition;
            if (m_maxRenderDistance > m_minRenderDistance)
            {
                if (!TargetInRange(screenPosition))
                    return false;
                float normalised = 1f - (screenPosition.z - m_minRenderDistance) / (m_maxRenderDistance - m_minRenderDistance);
                if (m_fadeOverDistance)
                {
                    Color colour = TargetColour;
                    colour.a = m_fadeCurve.Evaluate(normalised);
                    TargetColour = colour;
                }
                if (m_resizeOverDistance)
                    TargetScale = Vector3.one * m_sizeCurve.Evaluate(normalised);
            }
            return true;
        }

        // 0x60E430: the curves play over time since Refresh, clamped to their last key.
        private void UpdateTargetInWorldSpace()
        {
            m_timer += Time.deltaTime;
            if (m_fadeCurve != null && m_fadeCurve.length > 0)
            {
                float fadeDuration = m_fadeCurve[m_fadeCurve.length - 1].time;
                if (fadeDuration > 0f)
                {
                    Color colour = TargetColour;
                    colour.a = m_fadeCurve.Evaluate(Mathf.Min(m_timer, fadeDuration));
                    TargetColour = colour;
                }
            }
            if (m_sizeCurve != null && m_sizeCurve.length > 0)
            {
                float sizeDuration = m_sizeCurve[m_sizeCurve.length - 1].time;
                if (sizeDuration > 0f)
                    TargetScale = m_scale * m_sizeCurve.Evaluate(Mathf.Min(m_timer, sizeDuration));
            }
        }

        protected abstract void ShowTarget(bool isActive);

        public virtual void UpdateFromDetectorMessage(SurfaceDetectorUpdateMessage surfaceDetectorUpdateMessage)
        {
        }

        public virtual void UpdateFromTargetMessage(HomingTargetUpdateMessage homingTargetUpdateMessage)
        {
        }
    }
}
