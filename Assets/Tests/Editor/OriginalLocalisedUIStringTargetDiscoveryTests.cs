using Hardlight.Localisation;
using NUnit.Framework;
using RTLTMPro;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectLucid.Tests
{
    // LocalisedUIString.FindTextTargets 0x06000032, x86-64 0x1a35bb0, on owned
    // inactive objects (no OnEnable, string lookup or table subscription).
    public sealed class OriginalLocalisedUIStringTargetDiscoveryTests
    {
        // 0x1a35c11..0x1a35c60 null all four targets first; 0x1a35c65..0x1a35cc3 query
        // Text, TextMeshPro and RTLTextMeshPro; 0x1a35ceb..0x1a35d06 query
        // TextMeshProUGUI only when the RTL result is Unity-null. RTLTextMeshPro is
        // itself a TextMeshProUGUI, so an unconditional query would store the RTL
        // component in the UGUI slot, and a skipped reset would keep the stale one.
        [Test]
        public void RightToLeftTextSuppressesUguiDiscoveryAfterEveryTargetIsReset()
        {
            GameObject owned = null, foreign = null;
            try
            {
                owned = new GameObject("Owned localised RTL target", typeof(RectTransform));
                owned.SetActive(false);
                foreign = new GameObject("Owned stale uGUI target", typeof(RectTransform));
                foreign.SetActive(false);
                RTLTextMeshPro rtl = owned.AddComponent<RTLTextMeshPro>();
                Text stale = foreign.AddComponent<Text>();
                LocalisedUIString localised = owned.AddComponent<LocalisedUIString>();
                Assert.IsTrue(rtl is TextMeshProUGUI, "The RTL component is also a TextMeshProUGUI.");
                Assert.AreSame(rtl, owned.GetComponent<TextMeshProUGUI>());
                Set(localised, "m_uguiTextTarget", stale);
                Set(localised, "m_tmpUguiTextTarget", rtl);

                localised.FindTextTargets();

                Assert.IsNull(Get(localised, "m_uguiTextTarget"), "The stale Text target is reset.");
                Assert.IsNull(Get(localised, "m_tmpTextTarget"));
                Assert.AreSame(rtl, Get(localised, "m_tmpRTLTextTarget"));
                Assert.IsNull(Get(localised, "m_tmpUguiTextTarget"), "RTL suppresses the TextMeshProUGUI query.");
                Assert.IsFalse(owned.activeInHierarchy);
            }
            finally
            {
                if (foreign != null) Object.DestroyImmediate(foreign);
                if (owned != null) Object.DestroyImmediate(owned);
            }
        }

        // Positive control: without an RTL component the same query fills the UGUI slot.
        [Test]
        public void PlainTextMeshProUguiIsDiscoveredWithoutRightToLeftText()
        {
            GameObject owned = null;
            try
            {
                owned = new GameObject("Owned localised TMP UGUI target", typeof(RectTransform));
                owned.SetActive(false);
                TextMeshProUGUI text = owned.AddComponent<TextMeshProUGUI>();
                LocalisedUIString localised = owned.AddComponent<LocalisedUIString>();

                localised.FindTextTargets();

                Assert.AreSame(text, Get(localised, "m_tmpUguiTextTarget"));
                Assert.IsNull(Get(localised, "m_tmpRTLTextTarget"));
                Assert.IsNull(Get(localised, "m_uguiTextTarget"));
                Assert.IsNull(Get(localised, "m_tmpTextTarget"));
            }
            finally
            {
                if (owned != null) Object.DestroyImmediate(owned);
            }
        }

        private static void Set(LocalisedUIString localised, string name, Object value)
        {
            using (var serialized = new SerializedObject(localised))
            {
                serialized.FindProperty(name).objectReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Assert.AreSame(value, Get(localised, name));
        }

        private static Object Get(LocalisedUIString localised, string name)
        {
            using (var serialized = new SerializedObject(localised))
            {
                SerializedProperty property = serialized.FindProperty(name);
                Assert.IsNotNull(property, name);
                return property.objectReferenceValue;
            }
        }
    }
}
