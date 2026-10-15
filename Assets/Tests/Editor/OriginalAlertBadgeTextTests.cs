using HardlightProject;
using NUnit.Framework;
using RTLTMPro;
using UnityEditor;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // UIWidgetSDTAlertBadge.SetAlert 0x06003825, x86-64 0x69f5d0, writing a genuine
    // RTLTextMeshPro on owned inactive objects (no Awake, layout or rendering).
    public sealed class OriginalAlertBadgeTextTests
    {
        // Activation precedes text (0x69f60b). alert=true writes m_alertText through
        // the text virtual setter (0x69f614..0x69f637) regardless of the count.
        // Otherwise `cmp m_maxValue, displayNumber; jge` (0x69f63d) keeps the plain
        // Int32.ToString for counts up to the maximum, and only a larger count uses
        // String.Format(m_textFormatOverMaxValue, m_maxValue.ToString()) (0x69f650..
        // 0x69f65d). Defaults "!", 99 and "{0}+" are the constructor literals.
        // Catches `>=` at the limit ("99+" for 99), formatting the requested count
        // ("100+"), letting the count win over the alert, or a non-positive
        // non-alert call rewriting the text.
        [Test]
        public void AlertWinsOverCountAndOnlyCountsAboveTheMaximumUseTheFormat()
        {
            GameObject root = null;
            try
            {
                root = new GameObject("Owned inactive alert badge text", typeof(RectTransform));
                root.SetActive(false);
                var target = new GameObject("Owned alert badge activation target");
                target.transform.SetParent(root.transform, false);
                target.SetActive(false);
                RTLTextMeshPro text = root.AddComponent<RTLTextMeshPro>();
                UIWidgetSDTAlertBadge badge = root.AddComponent<UIWidgetSDTAlertBadge>();
                using (var serialized = new SerializedObject(badge))
                {
                    serialized.FindProperty("m_text").objectReferenceValue = text;
                    serialized.FindProperty("m_enableObjectWhileActive").objectReferenceValue = target;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                badge.SetAlert(false, 100);
                Assert.IsTrue(target.activeSelf);
                Assert.AreEqual("99+", text.text);

                badge.SetAlert(false, 99);
                Assert.AreEqual("99", text.text, "The maximum itself is shown plainly.");

                badge.SetAlert(false, 5);
                Assert.AreEqual("5", text.text);

                badge.SetAlert(true, 500);
                Assert.AreEqual("!", text.text, "The alert text wins over any count.");

                badge.SetAlert(false, 7);
                target.SetActive(true);
                badge.SetAlert(true, 0);
                Assert.IsTrue(target.activeSelf);
                Assert.AreEqual("!", text.text, "An alert with no count still shows the alert text.");

                badge.SetAlert(false, 0);
                Assert.IsFalse(target.activeSelf);
                Assert.AreEqual("!", text.text, "The inactive branch leaves the previous text.");
                Assert.IsFalse(root.activeInHierarchy);
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
            }
        }
    }
}
