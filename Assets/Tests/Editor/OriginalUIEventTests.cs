using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of Hardlight.UIEvent (HLUnityUI.Runtime 0x0600037b..7d) against
    // the shipped x86-64 bodies. Each lookup case owns uniquely named objects.
    public sealed class OriginalUIEventTests
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        [Test]
        public void DeclarationIsAFieldlessCreatableAsset()
        {
            Type type = typeof(UIEvent);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.FullName, Is.EqualTo("Hardlight.UIEvent"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(ScriptableObject)));
            Assert.That(type.GetFields(Declared), Is.Empty);

            CustomAttributeData menu = type.GetCustomAttributesData().Single();
            Assert.That(menu.AttributeType, Is.EqualTo(typeof(CreateAssetMenuAttribute)));
            Assert.That(menu.NamedArguments.Select(a => a.MemberName + "=" + a.TypedValue.Value).ToArray(),
                Is.EqualTo(new[] { "fileName=UIEvent", "menuName=Hardlight/HLUnityUI/UIEvent", "order=0" }));

            MethodInfo[] methods = type.GetMethods(Declared);
            Assert.That(methods.Select(m => m.Name).ToArray(), Is.EqualTo(new[] { "FindByName", "FindByNameSafe" }));
            Assert.That(methods.All(m => m.IsStatic && m.IsPublic && m.ReturnType == type), Is.True);
        }

        [Test]
        public void FindByNameReturnsOnlyAUIEventWithThatName()
        {
            string name = "UIEvent fixture " + Guid.NewGuid().ToString("N");
            var other = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            UIEvent uiEvent = null;
            try
            {
                // Both forward to ObjectUtils (0x1B540B8 / 0x1B540F8), whose search is
                // filtered to UIEvent: the same-named GameObject is never a match.
                Assert.That(UIEvent.FindByName(name), Is.Null);
                Assert.That(UIEvent.FindByNameSafe(name), Is.Null);

                uiEvent = ScriptableObject.CreateInstance<UIEvent>();
                uiEvent.name = name;
                uiEvent.hideFlags = HideFlags.HideAndDontSave;
                Assert.That(UIEvent.FindByName(name), Is.SameAs(uiEvent));
                Assert.That(UIEvent.FindByNameSafe(name), Is.SameAs(uiEvent));
                Assert.That(UIEvent.FindByName(name.ToUpperInvariant()), Is.Null);
                Assert.That(UIEvent.FindByName(null), Is.Null);
                Assert.That(UIEvent.FindByNameSafe(string.Empty), Is.Null);
            }
            finally
            {
                if (uiEvent != null) Object.DestroyImmediate(uiEvent);
                Object.DestroyImmediate(other);
            }
        }
    }
}
