using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000a0c: a rendering test bench that lays out
    // prefab instances in a grid and steps through components and renderer
    // features one at a time.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class InstancedObjectSpawner : MonoBehaviour
    {
        [SerializeField]
        private GameObject m_prefab;
        [SerializeField]
        private int m_maxRow = 16;
        [SerializeField]
        private int m_maxColumn = 9;
        [SerializeField]
        private Behaviour[] m_toggleableComponents = Array.Empty<Behaviour>();
        [SerializeField]
        private ScriptableRendererFeature[] m_rendererFeatures = Array.Empty<ScriptableRendererFeature>();
        private int m_currentComponentIndex;
        private int m_currentFeatureIndex;

        // One instance per material, filling rows of m_maxColumn, then layers
        // of m_maxRow rows. Nothing in the player build fills the list, so the
        // first read throws there (or the layer loop never ends when either
        // bound is not positive); any editor-only material search is not part
        // of the shipped code.
        [ContextMenu("CreateInstances")]
        public void CreateInstances()
        {
            var materials = new List<Material>();
            int materialIndex = 0;
            for (int layer = 0; ; layer++)
            {
                for (int row = 0; row < m_maxRow; row++)
                {
                    for (int column = 0; column < m_maxColumn; column++)
                    {
                        Material material = materials[materialIndex];
                        GameObject instance = Instantiate(m_prefab, transform);
                        // The original reads GameObject.gameObject here.
                        instance.gameObject.name = material.name;
                        instance.transform.localPosition = new Vector3(row, column, layer);
                        instance.GetComponent<Renderer>().sharedMaterial = material;
                        materialIndex++;
                        if (materialIndex >= materials.Count) return;
                    }
                }
            }
        }

        public void ResetComponents()
        {
            foreach (Behaviour component in m_toggleableComponents)
                component.enabled = false;
            m_currentComponentIndex = 0;
        }

        public void ToggleNextComponent()
        {
            if (!HasComponentsToToggle()) return;
            m_toggleableComponents[m_currentComponentIndex].enabled = true;
            m_currentComponentIndex++;
        }

        // The last component is never reached: the bound is Length - 1.
        public bool HasComponentsToToggle()
        {
            return m_currentComponentIndex < m_toggleableComponents.Length - 1;
        }

        public void ResetRenderingFeatures()
        {
            foreach (ScriptableRendererFeature feature in m_rendererFeatures)
                feature.SetActive(false);
            m_currentFeatureIndex = 0;
        }

        public void ToggleNextRenderFeature()
        {
            if (!HasFeaturesToToggle()) return;
            m_rendererFeatures[m_currentFeatureIndex].SetActive(true);
            m_currentFeatureIndex++;
        }

        public bool HasFeaturesToToggle()
        {
            return m_currentFeatureIndex < m_rendererFeatures.Length - 1;
        }

        [ContextMenu("CleanUp")]
        public void CleanUp()
        {
            Transform root = transform;
            for (int i = root.childCount - 1; i >= 0; i--)
                DestroyImmediate(root.GetChild(i).gameObject);
        }
    }
}
