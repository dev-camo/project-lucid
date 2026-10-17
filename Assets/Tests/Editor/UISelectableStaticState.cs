using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;

namespace ProjectLucid.Tests
{
    // Owned copy of UISelectable's static state for the UISelectable and
    // UISelectableController fixtures: the global and per-scene selectable
    // tables, both highlight actions and OnSfx. Construction saves and clears
    // them; Dispose puts the saved lists and their contents back.
    internal sealed class UISelectableStaticState : IDisposable
    {
        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly FieldInfo AllSelectablesField = typeof(UISelectable).GetField("s_allSelectables", AnyStatic);
        static readonly FieldInfo SceneSelectablesField = typeof(UISelectable).GetField("s_selectables", AnyStatic);
        static readonly FieldInfo HighlightChangedField = typeof(UISelectable).GetField("m_onShouldHighlightChanged", AnyStatic);

        readonly List<UISelectable> m_all;
        readonly Dictionary<string, KeyValuePair<List<UISelectable>, List<UISelectable>>> m_scenes;
        readonly object m_highlightChanged;
        readonly FastAction<FastAction<bool>, Action<bool>> m_init;
        readonly Dictionary<UIEventType, FastAction<HLAudioTypes>> m_sfx;

        public static List<UISelectable> AllSelectables => (List<UISelectable>)AllSelectablesField.GetValue(null);
        public static Dictionary<string, List<UISelectable>> SceneSelectables => (Dictionary<string, List<UISelectable>>)SceneSelectablesField.GetValue(null);
        public static FastAction<bool> HighlightChanged => (FastAction<bool>)HighlightChangedField.GetValue(null);

        public UISelectableStaticState()
        {
            m_all = AllSelectables.ToList();
            m_scenes = SceneSelectables.ToDictionary(p => p.Key, p => new KeyValuePair<List<UISelectable>, List<UISelectable>>(p.Value, p.Value.ToList()));
            m_highlightChanged = HighlightChangedField.GetValue(null);
            m_init = UISelectable.InitShouldHighlight;
            m_sfx = new Dictionary<UIEventType, FastAction<HLAudioTypes>>(UISelectable.OnSfx);
            AllSelectables.Clear();
            SceneSelectables.Clear();
            HighlightChangedField.SetValue(null, null);
            UISelectable.InitShouldHighlight = null;
            UISelectable.OnSfx.Clear();
        }

        public void Dispose()
        {
            AllSelectables.Clear();
            AllSelectables.AddRange(m_all);
            SceneSelectables.Clear();
            foreach (var pair in m_scenes)
            {
                List<UISelectable> list = pair.Value.Key;
                list.Clear();
                list.AddRange(pair.Value.Value);
                SceneSelectables[pair.Key] = list;
            }
            HighlightChangedField.SetValue(null, m_highlightChanged);
            UISelectable.InitShouldHighlight = m_init;
            UISelectable.OnSfx.Clear();
            foreach (var pair in m_sfx) UISelectable.OnSfx[pair.Key] = pair.Value;
        }
    }
}
