using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000a11: applies one colour through a fresh
    // property block, replacing any block already on the renderer.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class MaterialPropertyBlockSetter : MonoBehaviour
    {
        [SerializeField]
        private Renderer m_renderer;
        [SerializeField]
        private string m_colourPropertyName = "_Color";
        [SerializeField]
        private Color m_colorToSet = Color.white;

        private void Awake()
        {
            var propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetColor(m_colourPropertyName, m_colorToSet);
            m_renderer.SetPropertyBlock(propertyBlock);
        }
    }
}
