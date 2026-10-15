using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class OriginalMenuDataWeakRemediationTests
{
    // MenuPage .ctor 0x060001bf, x86-64 0x1b47a10: mov byte [this+0xe8], 1 before the
    // GuiPage base constructor, so m_fadeTransition (0x0400011b) defaults to true.
    // Catches dropping or inverting the field initializer.
    [Test]
    public void MenuPageFadeTransitionDefaultsTrueOnOwnedComponent()
    {
        var host = new GameObject("Owned MenuPage default fixture");
        try
        {
            host.SetActive(false);
            MenuPage page = host.AddComponent<MenuPage>();
            Assert.That(page, Is.Not.Null);
            FieldInfo field = typeof(MenuPage).GetField("m_fadeTransition", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.That(field.GetValue(page), Is.EqualTo(true));
            using (var serialized = new SerializedObject(page))
            {
                SerializedProperty property = serialized.FindProperty("m_fadeTransition");
                Assert.That(property, Is.Not.Null);
                Assert.That(property.propertyType, Is.EqualTo(SerializedPropertyType.Boolean));
                Assert.That(property.boolValue, Is.True);
            }
        }
        finally { Object.DestroyImmediate(host); }
        // The page never activates, so GuiPage Awake/Start/OnDestroy and the registry stay untouched.
        LogAssert.NoUnexpectedReceived();
    }
}
