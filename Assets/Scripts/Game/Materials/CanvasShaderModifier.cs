using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020006c6. Writes straight into each renderer's
    // first material, so the change is shared by everything using it.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class CanvasShaderModifier
    {
        private readonly IEnumerable<CanvasRenderer> m_renderers;

        public CanvasShaderModifier(IEnumerable<CanvasRenderer> renderers)
        {
            m_renderers = renderers;
        }

        // Sets the x offset (the z component) of the _ST vector. The first
        // renderer without a material ends the whole pass, not just its turn.
        public void SetDreamNoiseStandardOffset(float offset)
        {
            foreach (CanvasRenderer renderer in m_renderers)
            {
                if (renderer.materialCount <= 0) break;
                Material material = renderer.GetMaterial(0);
                Vector4 scaleOffset = material.GetVector(ShaderProperties.DreamNoiseST);
                scaleOffset.z = offset;
                material.SetVector(ShaderProperties.DreamNoiseST, scaleOffset);
            }
        }
    }
}
