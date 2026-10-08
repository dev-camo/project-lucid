using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HardlightProject
{
    // Game.Runtime 0x02000343. Original cutscene rig correspondence, recovered
    // from both shipping CPU slices. This contains no offline replacement logic.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterRigLookup : MonoBehaviour
    {
        [SerializeField] private GameObject m_characterRoot;
        [SerializeField] private Animator m_characterAnimator;
        [SerializeField] private Animator m_meshAnimator;
        [SerializeField]
        [Tooltip("Add any referenced game objects that are needed by cutscene Timelines here")]
        private GameObject[] m_additionalObjects = Array.Empty<GameObject>();
        [SerializeField] private Transform[] m_attachPoints;
        [SerializeField]
        [Tooltip("Reference each mesh renderer used by the rig that needs ghosting")]
        private Renderer[] m_meshRenderers;
        [Tooltip("Set the materials to use for each renderer when ghosted. They must be in the correct order for the indexed meshes above.")]
        [SerializeField] private List<MaterialList> m_ghostedMaterials;
        private const string GhostedMaterialTag = "ghosted";
        private readonly List<ComponentCache> m_objectCache = new List<ComponentCache>();

        // 0x02000344 / 0x0600148b: the original constructor leaves Materials null.
        [Serializable]
        private class MaterialList
        {
            public List<Material> Materials;
        }

        // 0x02000345 / 0x0600148c: construction allocates the component map.
        private class ComponentCache
        {
            public GameObject RootObject;
            public readonly Dictionary<Type, Component> Components = new Dictionary<Type, Component>();
        }

        // 0x0600147f..0x06001482: expose the authored references directly.
        public GameObject CharacterRoot => m_characterRoot;
        public Animator CharacterAnimator => m_characterAnimator;
        public Animator MeshAnimator => m_meshAnimator;
        public Transform[] AttachPoints => m_attachPoints;

        // 0x06001483. The two rigs must cache corresponding objects in this order.
        // Repeated initialization appends entries; the shipped code never clears it.
        public void Initialize()
        {
            CacheComponents(m_characterRoot);
            CacheComponents(m_characterAnimator.gameObject);
            CacheComponents(m_meshAnimator.gameObject);
            foreach (GameObject rootObject in m_additionalObjects)
                CacheComponents(rootObject);
        }

        // 0x06001484. Only the last component of an exact type is retained.
        // Append happens after traversal, preserving partial failure behavior.
        private void CacheComponents(GameObject rootObject)
        {
            var cache = new ComponentCache { RootObject = rootObject };
            foreach (Component component in rootObject.GetComponents<Component>())
                cache.Components[component.GetType()] = component;
            m_objectCache.Add(cache);
        }

        // 0x06001485. Matching uses the original object's cache index and exact
        // component type. The first matching object owns success or failure.
        public bool TryGetReplacement(Object original, CharacterRigLookup replacementRig, out Object replacement)
        {
            replacement = null;
            Type originalType = original.GetType();
            GameObject rootObject;
            if (original is GameObject gameObject)
                rootObject = gameObject;
            else if (original is Component component)
                rootObject = component.gameObject;
            else
            {
                HLOutput.LogError(string.Format("{0} is an unexpected type: {1}", original.name, originalType));
                return false;
            }

            for (int i = 0; i < m_objectCache.Count; i++)
            {
                ComponentCache cache = m_objectCache[i];
                if (cache.RootObject != rootObject)
                    continue;
                if (originalType == typeof(GameObject))
                {
                    replacement = replacementRig.m_objectCache[i].RootObject;
                    return true;
                }

                if (!cache.Components.TryGetValue(originalType, out Component originalComponent))
                    return false;
                if (original != originalComponent)
                    return false;
                if (replacementRig.m_objectCache[i].Components.TryGetValue(originalType, out Component replacementComponent))
                {
                    replacement = replacementComponent;
                    return true;
                }
                HLOutput.LogError(string.Format("{0}, type \"{1}\" is not found in replacement lookup", original.name, originalType));
                return false;
            }
            return false;
        }

        // 0x06001486. Move children in reverse order using SetParent's original
        // one-argument overload. The outer loop rereads the authored array.
        public void MoveAttachments(CharacterRigLookup replacementRig)
        {
            for (int i = 0; i < m_attachPoints.Length; i++)
            {
                Transform attachPoint = m_attachPoints[i];
                if (attachPoint == null || attachPoint.childCount == 0)
                    continue;
                Transform replacementAttachPoint = replacementRig.m_attachPoints[i];
                for (int j = attachPoint.childCount - 1; j >= 0; j--)
                    attachPoint.GetChild(j).transform.SetParent(replacementAttachPoint);
            }
        }

        // 0x06001487. Transform lookups and animator enabling retain their
        // original order; mesh scale and the mesh animator's enabled state stay.
        public void SetPositions(CharacterRigLookup replacementRig)
        {
            replacementRig.m_characterRoot.transform.SetLocalPositionAndRotation(
                m_characterRoot.transform.localPosition, m_characterRoot.transform.localRotation);
            replacementRig.m_characterAnimator.transform.SetLocalPositionAndRotation(
                m_characterAnimator.transform.localPosition, m_characterAnimator.transform.localRotation);
            replacementRig.m_characterAnimator.enabled = true;
            replacementRig.m_meshAnimator.transform.SetLocalPositionAndRotation(
                m_meshAnimator.transform.localPosition, m_meshAnimator.transform.localRotation);
        }

        // 0x06001488. The shipped test inspects only the first renderer and uses
        // the exact, case-sensitive material-name substring.
        public bool IsGhosted()
        {
            foreach (Material material in m_meshRenderers[0].sharedMaterials)
                if (material.name.Contains(GhostedMaterialTag))
                    return true;
            return false;
        }

        // 0x06001489. Each authored list maps to the renderer at the same index.
        public void ApplyGhostedMaterials()
        {
            for (int i = 0; i < m_meshRenderers.Length; i++)
            {
                Renderer renderer = m_meshRenderers[i];
                renderer.SetMaterials(m_ghostedMaterials[i].Materials);
            }
        }

        // 0x0600148a is supplied by C#: initialize m_additionalObjects, then
        // m_objectCache, then call MonoBehaviour's constructor.
    }
}
