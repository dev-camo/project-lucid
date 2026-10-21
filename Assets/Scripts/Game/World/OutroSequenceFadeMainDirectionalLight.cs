using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a8d; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class OutroSequenceFadeMainDirectionalLight : MonoBehaviour
    {
        private Light m_directionalLight;
        private float m_defaultDirectionalLightIntensity;
        private float m_lerpTime;
        [SerializeField] private float lerpSpeedMultiplier = 0.5f;
        private const float MinIntensity = 0.01f;

        // 0x06003cbb (0x6CE8B0): without a scene sun there is nothing to fade.
        private void Start()
        {
            m_directionalLight = RenderSettings.sun;
            if (m_directionalLight == null)
            {
                enabled = false;
                return;
            }
            m_defaultDirectionalLightIntensity = m_directionalLight.intensity;
        }

        // 0x06003cbc (0x6CE940): lerps from the scene intensity to MinIntensity and
        // switches itself off once the unclamped lerp time reaches one.
        private void Update()
        {
            m_lerpTime += lerpSpeedMultiplier * Time.deltaTime;
            m_directionalLight.intensity = Mathf.Lerp(m_defaultDirectionalLightIntensity, MinIntensity, m_lerpTime);
            if (m_lerpTime >= 1f)
                enabled = false;
        }

        // 0x06003cbd (0x6CE9E0): restores the scene intensity unconditionally.
        private void OnDestroy()
        {
            m_directionalLight.intensity = m_defaultDirectionalLightIntensity;
        }
    }
}
