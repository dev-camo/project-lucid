using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Complete original owner candidate. Native and engine acceptance is pending.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class MaterialManipulator : MonoBehaviour
    {
        [SerializeField] private List<MaterialManipulatorSetting> m_materialSettings;
        private SystemRef<LevelManager> m_levelManagerRef;

        //060024c7: publish the system reference during Start, then register the
        //original named callback. Authored settings remain null until serialized.
        private void Start()
        {
            m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>();
            m_levelManagerRef.InvokeOnValid(OnLevelManagerValid);
        }

        //060024c8: an already-valid current level is cached directly; otherwise
        //the persistent level-loaded delegate is registered by the real manager.
        private void OnLevelManagerValid(LevelManager levelManager)
        {
            if (levelManager.TryGetCurrentLevel(out LevelManagerLevel level)) CacheValues(level);
            else levelManager.InvokeOnLevelLoaded(CacheValues);
        }

        //060024c9: unsubscribe before accessing supplied level data. Renderer
        //registration order is outermost; every matching setting may overwrite
        //its previous cache. Foreach disposal and partial failure are retained.
        private void CacheValues(LevelManagerLevel level)
        {
            if (m_levelManagerRef.IsNull()) return;
            m_levelManagerRef.Get().RemoveLevelLoadedAction(CacheValues);
            foreach (var (_, registered) in level.Data.LevelRenderers)
            {
                foreach (MaterialManipulatorSetting setting in m_materialSettings)
                    setting.Cache(registered.Identifier, registered.Renderer);
            }
        }

        //060024ca: both the reference and original current-level validity gate
        //the traversal. Renderer dictionaries and material arrays stay live.
        public void Action_Apply()
        {
            if (m_levelManagerRef.IsNull()) return;
            if (!m_levelManagerRef.Get().TryGetCurrentLevel(out LevelManagerLevel level)) return;
            foreach (var (_, registered) in level.Data.LevelRenderers)
            {
                foreach (MaterialManipulatorSetting setting in m_materialSettings)
                    setting.Apply(registered.Identifier, registered.Renderer);
            }
        }

        //060024cb: replay cached values with the same traversal and failure order.
        public void Action_Restore()
        {
            if (m_levelManagerRef.IsNull()) return;
            if (!m_levelManagerRef.Get().TryGetCurrentLevel(out LevelManagerLevel level)) return;
            foreach (var (_, registered) in level.Data.LevelRenderers)
            {
                foreach (MaterialManipulatorSetting setting in m_materialSettings)
                    setting.Restore(registered.Identifier, registered.Renderer);
            }
        }

        public MaterialManipulator() { }

        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Serializable]
        private sealed class MaterialManipulatorSetting
        {
            [SerializeField] private LevelRendererIdentifier m_identifier;
            [Min(0f), SerializeField] private int m_materialIndex;
            [SerializeField] private int m_renderQueue;
            [SerializeField] private string m_property;
            [SerializeField] private float m_value;
            private int m_originalRenderQueue { get; set; }
            private float m_originalValue { get; set; }

            //060024d1: the original comparison rejects only the upper bound. A
            //negative indices are not rejected here; shipping array checks are
            //disabled. sharedMaterials is obtained twice.
            //Queue publication precedes the optional float-property operation.
            public void Apply(LevelRendererIdentifier identifier, Renderer renderer)
            {
                if (m_identifier != identifier) return;
                if (m_materialIndex >= renderer.sharedMaterials.Length) return;
                Material material = renderer.sharedMaterials[m_materialIndex];
                material.renderQueue = m_renderQueue;
                if (!string.IsNullOrWhiteSpace(m_property)) material.SetFloat(m_property, m_value);
            }

            //060024d2: queue is cached before the property check/read. Whitespace
            //leaves the previous float cache intact; no cache-valid flag exists.
            public void Cache(LevelRendererIdentifier identifier, Renderer renderer)
            {
                if (m_identifier != identifier) return;
                if (m_materialIndex >= renderer.sharedMaterials.Length) return;
                Material material = renderer.sharedMaterials[m_materialIndex];
                m_originalRenderQueue = material.renderQueue;
                if (!string.IsNullOrWhiteSpace(m_property)) m_originalValue = material.GetFloat(m_property);
            }

            //060024d3: restore the cached queue even if the property is blank.
            public void Restore(LevelRendererIdentifier identifier, Renderer renderer)
            {
                if (m_identifier != identifier) return;
                if (m_materialIndex >= renderer.sharedMaterials.Length) return;
                Material material = renderer.sharedMaterials[m_materialIndex];
                material.renderQueue = m_originalRenderQueue;
                if (!string.IsNullOrWhiteSpace(m_property)) material.SetFloat(m_property, m_originalValue);
            }

            public MaterialManipulatorSetting() { }
        }
    }
}
