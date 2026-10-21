using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000855; bodies translated from the 1.10.1 x86-64 binary.
    // A target indicator drawn as an image on a canvas; the position is in canvas units.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ViewTargetIndicator : ViewTarget
    {
        [SerializeField] private Image m_targetImage;
        [SerializeField] private Canvas m_canvas;
        [Tooltip("Indicator will keep tracking the transform of the target for these homing target types.")]
        [SerializeField] private List<HomingTargetType> m_continuousTrackingTargetTypes = new List<HomingTargetType>();
        [SerializeField] private GameObject m_targetParent;
        private RectTransform m_targetImageTransform;

        // 0x0600304b (0x60E610): the native body calls ShowTarget(false) and then caches the image's transform; the base Awake
        // is the same single call.
        protected override void Awake()
        {
            base.Awake();
            m_targetImageTransform = (RectTransform)m_targetImage.transform;
        }

        // 0x0600304c (0x60E6B0)
        private void Reset()
        {
            m_canvas = GetComponent<Canvas>();
        }

        // 0x0600304d (0x60E710): the base setup is inlined in the native body. The indicator sprite is only replaced when
        // the parameters are the indicator's own type (a type check, not a cast: plain target parameters return quietly)
        // and carry one.
        protected override void OnSetup(ViewParameters viewParams)
        {
            base.OnSetup(viewParams);
            if (viewParams is ViewTargetIndicatorParameters parameters && parameters.TargetIndicator != null)
                m_targetImage.sprite = parameters.TargetIndicator;
        }

        protected override Vector3 TargetPosition
        {
            // 0x0600304e (0x60E860)
            get => m_targetImageTransform.anchoredPosition3D;
            // 0x0600304f (0x60E880): the canvas scale factor is divided out.
            set => m_targetImageTransform.anchoredPosition3D = value / m_canvas.scaleFactor;
        }

        protected override Vector3 TargetScale
        {
            // 0x06003050 (0x60E8D0)
            get => m_targetImage.transform.localScale;
            // 0x06003051 (0x60E8F0)
            set => m_targetImage.transform.localScale = value;
        }

        protected override Color TargetColour
        {
            // 0x06003052 (0x60E930)
            get => m_targetImage.color;
            // 0x06003053 (0x60E950)
            set => m_targetImage.color = value;
        }

        // 0x06003054 (0x60E970)
        protected override void ShowTarget(bool isActive)
        {
            m_targetParent.SetActive(isActive);
        }

        // 0x06003055 (0x60E990)
        protected override bool ProjectToScreenSpace => true;

        // 0x06003056 (0x60E9A0): a target outside the render distance hides the indicator.
        protected override bool TargetInRange(Vector3 targetScreenPosition)
        {
            if (targetScreenPosition.z < MinRenderDistance || targetScreenPosition.z > MaxRenderDistance)
            {
                ShowTarget(false);
                return false;
            }
            return true;
        }

        // 0x06003057 (0x60E9E0): the target is always cleared, then set to the surface position for a listed homing type
        // detected on a surface.
        public override void UpdateFromDetectorMessage(SurfaceDetectorUpdateMessage surfaceDetectorUpdateMessage)
        {
            ClearTarget();
            if (surfaceDetectorUpdateMessage.SurfaceLocation.m_surface != null && m_showForHomingTargetTypes.Contains(surfaceDetectorUpdateMessage.Type))
                SetTargetPosition(surfaceDetectorUpdateMessage.SurfaceLocation.m_worldPosition);
        }

        // 0x06003058 (0x60EAD0): a target that can be activated and is of a listed type is followed through its collider or
        // transform if its type is tracked continuously, otherwise pinned at its position when the message arrived. Any other
        // message clears the target.
        public override void UpdateFromTargetMessage(HomingTargetUpdateMessage homingTargetUpdateMessage)
        {
            if (!homingTargetUpdateMessage.CanActivate() || !m_showForHomingTargetTypes.Contains(homingTargetUpdateMessage.TargetValue.Object.Type))
            {
                ClearTarget();
                return;
            }
            CharacterTargetObjectData targetObject = homingTargetUpdateMessage.TargetValue.Object;
            if (m_continuousTrackingTargetTypes.Contains(targetObject.Type))
            {
                if (targetObject.TryGetMetaDataAsType(out HomingObjectMetaData homingMetaData))
                    SetTargetCollider(homingMetaData.HomingTarget.Collider);
                else
                    SetTargetTransform(targetObject.Target.transform);
            }
            else
            {
                ClearTarget();
                SetTargetPosition(targetObject.WorldPosition);
            }
        }

        // 0x06003059 (0x60ED90)
        public ViewTargetIndicator() { }
    }
}
