using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Assembly-CSharp; bodies translated from the 1.10.1 x86-64 binary.
    [ExecuteInEditMode]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class DreamDissolveManagerDebug : MonoBehaviour
    {
        private Camera m_mainCamera;
        private Shader m_dreamDissolve;
        [SerializeField] private float m_maskPosition;
        [Range(0f, 1f)]
        [SerializeField] private float m_opacity = 1f;

        private const string camToWorld = "_CamToWorld";
        private const string maskPosition = "_Mask_Position";
        private const string opacity = "_Overall_Effect_Opacity";

        // 06000043. The found shader is kept but never used.
        private void Start()
        {
            m_dreamDissolve = Shader.Find("HL_Dream_FullScreen_Dissolve_RenderFeature");
            m_mainCamera = Camera.main;
        }

        // 06000044
        private void Update()
        {
            if (m_mainCamera == null)
                m_mainCamera = Camera.main;
            Shader.SetGlobalMatrix(camToWorld, m_mainCamera.cameraToWorldMatrix);
            Shader.SetGlobalFloat(maskPosition, m_maskPosition);
            Shader.SetGlobalFloat(opacity, m_opacity);
        }
    }
}
