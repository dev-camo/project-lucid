// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.AdvancedBinder 0x0200000a; bodies translated from the 1.10.1 x86-64 binary.
// 0x06000009 OnEnable 0x19F3010; 0x0600000a OnDisable 0x19F33A0; 0x0600000b .ctor 0x19F3700.
// <>c 0x0200000b: .cctor 0x19F3710; .ctor 0x19F3780; <OnEnable>b__1_0 0x19F3790; <OnDisable>b__2_0 0x19F37C0.
// Play mode caches the node array on enable and reuses it on disable; the editor path never caches.

using System.Collections.Generic;
using UnityEngine;

namespace Hardlight.UI.Binding
{
    [AddComponentMenu("Hardlight UI/Binding/Binder (Advanced)")]
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class AdvancedBinder : MonoBehaviour
    {
        private BinderNode[] cachedNodes;

        protected virtual void OnEnable()
        {
            if (Application.isPlaying)
            {
                cachedNodes = cachedNodes ?? GetComponents<BinderNode>();
                for (int i = 0; i < cachedNodes.Length; i++)
                {
                    if (cachedNodes[i].ShowInNodeEditor) cachedNodes[i].enabled = true;
                }
            }
            else
            {
                foreach (BinderNode node in new List<BinderNode>(GetComponents<BinderNode>()).FindAll(n => n.ShowInNodeEditor))
                    node.enabled = true;
            }
        }

        protected virtual void OnDisable()
        {
            if (Application.isPlaying)
            {
                // Assumes OnEnable has cached the nodes: a null cachedNodes throws here (0x19F348B has no null guard).
                for (int i = 0; i < cachedNodes.Length; i++)
                {
                    if (cachedNodes[i].ShowInNodeEditor) cachedNodes[i].enabled = false;
                }
            }
            else
            {
                foreach (BinderNode node in new List<BinderNode>(GetComponents<BinderNode>()).FindAll(n => n.ShowInNodeEditor))
                    node.enabled = false;
            }
        }
    }
}
