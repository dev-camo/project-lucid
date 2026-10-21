using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace HardlightProject
{
    // Original Game.Runtime 020006dd: the base of a displayable ornament prefab. The thumbnail camera stays off,
    // and only the shadow keyword of the skinned renderer's material follows the enabled state.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class Ornament : MonoBehaviour
    {
        [SerializeField]
        private Camera m_thumbnailCamera;
        [SerializeField]
        private SmoothedTransformRotator m_transformRotator;
        [SerializeField]
        private SkinnedMeshRenderer m_rendererToEnableShadowsOn;
        [SerializeField]
        private float m_yRotationOffset;
        private readonly int m_castShadows = Shader.PropertyToID(CastShadowsKeyword);
        private const string CastShadowsKeyword = "_CastShadows";

        // 06002503..05
        public SmoothedTransformRotator TransformRotator => m_transformRotator;
        public Camera ThumbnailCamera => m_thumbnailCamera;
        public float YRotationOffset => m_yRotationOffset;

        // 06002506 (0x59CE90)
        private void Awake()
        {
            if (m_thumbnailCamera != null)
                m_thumbnailCamera.gameObject.SetActive(false);
        }

        // 06002507 (0x59CF10): besides the keyword, OnEnable turns on shadow casting and updateWhenOffscreen, which OnDisable
        // does not undo. Both bodies store 1.0 in _CastShadows (one constant, 0x29A6BE0).
        private void OnEnable()
        {
            if (m_rendererToEnableShadowsOn == null)
                return;
            m_rendererToEnableShadowsOn.shadowCastingMode = ShadowCastingMode.On;
            m_rendererToEnableShadowsOn.updateWhenOffscreen = true;
            m_rendererToEnableShadowsOn.material.EnableKeyword(CastShadowsKeyword);
            m_rendererToEnableShadowsOn.material.SetFloat(m_castShadows, 1f);
        }

        // 06002508 (0x59CFE0)
        private void OnDisable()
        {
            if (m_rendererToEnableShadowsOn == null)
                return;
            m_rendererToEnableShadowsOn.material.SetFloat(m_castShadows, 1f);
            m_rendererToEnableShadowsOn.material.DisableKeyword(CastShadowsKeyword);
        }

        // 06002509 (0x59D090): the camera test is a plain reference test, the rotator test is Unity's; a rotator is
        // added only when none sits on the object.
        protected virtual void OnValidate()
        {
            m_thumbnailCamera ??= gameObject.GetComponentInChildren<Camera>();
            if (m_transformRotator != null)
                return;
            if (gameObject.TryGetComponent(out m_transformRotator))
                return;
            m_transformRotator = gameObject.AddComponent<SmoothedTransformRotator>();
        }

    }
}
