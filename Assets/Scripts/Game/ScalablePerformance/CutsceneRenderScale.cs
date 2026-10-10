using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CutsceneRenderScale : VisualQualityConfiguration
    {
        [SerializeField] private Vector2 m_renderSize;

        // Game.Runtime 0x06002d90: required original manager lookup precedes the
        // render-size read. Its original public setter method writes the backing
        // field directly; no missing-manager or size validation is introduced.
        public override void Apply()
        {
            ProcessManager.GetSystem<VisualQualityManager_SDT>(null, true).SetCutsceneRenderSize(m_renderSize);
        }

        // 0x06002d91: original zero vector and genuine configuration base ctor.
        public CutsceneRenderScale() { }
    }
}
