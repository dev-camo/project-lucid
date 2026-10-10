using Hardlight;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000415; complete eight-method configuration owner.
    [CreateAssetMenu(fileName = "DebugRailConfiguration", menuName = "HardlightProject/Config/Debug Rail")]
    public class DebugRailConfiguration : SystemConfigurationAsset
    {
        private const string AssetMenu = "HardlightProject/Config/";
        [SerializeField] private Material m_railLeftMaterial;
        [SerializeField] private Material m_railRightMaterial;
        [SerializeField] private float m_spaceOffset = 1f;
        [SerializeField] private float m_radius = 1f;
        [SerializeField] private int m_segments = 6;

        // Original 06001800..04 are direct field reads, in this order.
        public Material RailLeftMaterial => m_railLeftMaterial;
        public Material RailRightMaterial => m_railRightMaterial;
        public float SpaceOffset => m_spaceOffset;
        public float Radius => m_radius;
        public int Segments => m_segments;

        // Original 06001805 checks each Unity object separately; no base callback.
        private void OnEnable()
        {
            if (m_railLeftMaterial == null)
                m_railLeftMaterial = new Material(Shader.Find("Diffuse"));
            if (m_railRightMaterial == null)
                m_railRightMaterial = new Material(Shader.Find("Diffuse"));
        }

        // Original 06001806 returns immediately on both CPUs.
        public override void Validate() { }
        // Original 06001807 initializes 1, 1, 6 before the genuine base constructor.
        public DebugRailConfiguration() { }
    }
}
