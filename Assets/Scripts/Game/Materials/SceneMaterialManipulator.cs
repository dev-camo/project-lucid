using System;
using System.Collections;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime owner 020006cb; methods 060024dd..060024e1, nested
    // MaterialColourConfig 020006cc and iterator <AnimateMaterial>d__5 020006cd.
    // The nested config is declared first so the iterator keeps its ordinal.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SceneMaterialManipulator : MonoBehaviour
    {
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Serializable]
        public class MaterialColourConfig
        {
            public AnimationCurve Multiplier;
            public Material Material;
            public MaterialManipulationTriggerType Trigger;
            public Color ColourToSet;
            public string ColourName;
            public TimeCategory TimeCategory;
            [HideInInspector]
            public Coroutine ManipulatingRoutine;
            [HideInInspector]
            public Color CachedColourForEditor;
        }

        [SerializeField]
        private MaterialColourConfig[] m_materialColourConfigs;
        private TimeManager m_timeManager;

        public void Start()
        {
            m_timeManager = ProcessManager.GetSystem<TimeManager>();
        }

        // 060024de: every config with the matching trigger restarts its routine.
        public void Trigger(MaterialManipulationTriggerType triggerType)
        {
            foreach (MaterialColourConfig materialConfig in m_materialColourConfigs)
            {
                if (materialConfig.Trigger != triggerType)
                    continue;
                this.SafeStopCoroutine(ref materialConfig.ManipulatingRoutine);
                materialConfig.ManipulatingRoutine = StartCoroutine(AnimateMaterial(materialConfig));
            }
        }

        // 060024df (MoveNext 060024e5): the last key time is reread on every
        // step and the colour is set before the timer advances, so the curve is
        // never evaluated at or past the last key's time. The multiplier is
        // evaluated (0x59b998) before the material is read (0x59b99d).
        private IEnumerator AnimateMaterial(MaterialColourConfig materialConfig)
        {
            float timer = 0f;
            while (timer < materialConfig.Multiplier[materialConfig.Multiplier.length - 1].time)
            {
                float multiplier = materialConfig.Multiplier.Evaluate(timer);
                materialConfig.Material.SetColor(materialConfig.ColourName, materialConfig.ColourToSet * multiplier);
                timer += m_timeManager.GetDeltaTime(materialConfig.TimeCategory);
                yield return null;
            }
        }

        // 060024e0 is an empty body in the shipping build.
        private void OnDestroy()
        {
        }
    }
}
