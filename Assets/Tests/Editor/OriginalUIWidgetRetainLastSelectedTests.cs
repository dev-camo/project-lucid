using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // Declarations of the original Assembly-CSharp-firstpass UIWidgetRetainLastSelected (1.10.1).
    // Test assemblies cannot reference the predefined firstpass assembly, so it is reached by name.
    public sealed class OriginalUIWidgetRetainLastSelectedTests
    {
        private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static Type Widget => Type.GetType("Hardlight.UIWidgetRetainLastSelected, Assembly-CSharp-firstpass", true);

        [Test]
        public void DeclarationsMatchTheShippedFirstpassAssembly()
        {
            Type type = Widget;
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100001));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(type.GetCustomAttributesData().Where(data => data.AttributeType == typeof(Il2CppSetOptionAttribute))
                .Select(data => (Option)data.ConstructorArguments[0].Value), Is.EqualTo(new[] { Option.NullChecks, Option.ArrayBoundsChecks }));
            Assert.That(type.GetCustomAttribute<RequireComponent>().m_Type0, Is.EqualTo(typeof(UIContainer)));
            Assert.That(type.GetFields(Declared).Select(field => field.Name + ":" + field.FieldType.Name),
                Is.EqualTo(new[] { "m_uiContainer:UIContainer", "m_uiContainerNavigation:UIContainerNavigation",
                    "m_selectWhenExclusivelyOpen:UIContainerIdentifier[]", "m_uiManager:UIManager", "m_uiNavigationManager:UINavigationManager" }));
            Assert.That(type.GetMethods(Declared).Select(method => method.Name + ":" + ((int)method.Attributes).ToString("x")),
                Is.EquivalentTo(new[] { "Reset:81", "Awake:81", "OnDestroy:81", "OnUIContainerClosed:81", "RetrieveLastSelected:81", "OnDisable:81", "OnSelected:81" }));
            Assert.That(type.GetNestedTypes(BindingFlags.NonPublic).Select(nested => nested.Name), Is.EquivalentTo(new[] { "<>c__DisplayClass9_0", "<>c__DisplayClass11_0" }));
            Assert.That(type.GetNestedType("<>c__DisplayClass9_0", BindingFlags.NonPublic).GetFields().Select(field => field.Name),
                Is.EqualTo(new[] { "navigableWidget", "<>4__this" }));
        }
    }
}
