using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIVisibilityGroupOverrider : MonoBehaviour
    {
        [SerializeField] private List<UIVisibilityGroupOverride> m_overrides;
        private readonly Dictionary<string, bool> m_overridesDictionary = new Dictionary<string, bool>();
        private SystemRef<UIManager> m_uiManagerSystemRef;
        private StackableDataHandle m_stackableDataHandle;

        // HLModernUI.Runtime0600000a: publish the actual UI system reference
        // before enumerating authored rows. Repeated Awake retains previous
        // dictionary keys; later duplicate GUIDs overwrite earlier visibility.
        private void Awake()
        {
            m_uiManagerSystemRef = ProcessManager.GetSystemRef<UIManager>();
            foreach (UIVisibilityGroupOverride row in m_overrides)
                m_overridesDictionary[row.VisibilityGroupDefinition.GetGUID()] = row.Visible;
        }

        //0600000b: original destruction delegates to the same removal path.
        private void OnDestroy() => DeactivateOverrides();

        //0600000c: an unavailable UI service leaves the existing handle intact.
        // Remove callbacks run before the field is cleared and may fail; the
        // original does not clear in a finally or silently swallow the failure.
        public void DeactivateOverrides()
        {
            if (m_stackableDataHandle != null && m_uiManagerSystemRef.IsValid())
            {
                m_uiManagerSystemRef.Get().RemoveVisibilityOverrides(m_stackableDataHandle);
                m_stackableDataHandle = null;
            }
        }

        //0600000d: test the genuine system before touching the current handle.
        // Reactivation removes an old handle before re-reading the system and
        // current dictionary for a fresh override; publication follows callbacks.
        public void ActivateOverrides()
        {
            if (m_uiManagerSystemRef.IsValid())
            {
                if (m_stackableDataHandle != null) DeactivateOverrides();
                m_stackableDataHandle = m_uiManagerSystemRef.Get().AddVisibilityOverrides(m_overridesDictionary);
            }
        }
        //0600000e: the field initializer creates only the readonly dictionary,
        // before MonoBehaviour's constructor. Authored rows are initially null.

        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public class UIVisibilityGroupOverride
        {
            [SerializeField] private UIVisibilityGroupDefinition m_visibilityGroupDefinition;
            [SerializeField] private bool m_visible;
            //0600000f/10: exact original own field getters; ctor06000011
            // only forwards to System.Object, with no default true visibility.
            public UIVisibilityGroupDefinition VisibilityGroupDefinition => m_visibilityGroupDefinition;
            public bool Visible => m_visible;
        }
    }
}
