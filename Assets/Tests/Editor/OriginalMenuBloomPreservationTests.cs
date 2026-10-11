using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    public sealed class OriginalMenuBloomPreservationTests
    {
        private const BindingFlags Own = BindingFlags.DeclaredOnly | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        [Test]
        public void OriginalOwnerRetainsFieldsAttributesDefaultAndNaturalIteratorContract()
        {
            Type type = typeof(MenuBloomTransition);
            Assert.That(type.FullName, Is.EqualTo("Hardlight.MenuBloomTransition"));
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That((int)type.Attributes, Is.EqualTo(1048577));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoSingleton<MenuBloomTransition>)));
            FieldInfo[] fields = type.GetFields(Own).OrderBy(x => x.MetadataToken).ToArray();
            Assert.That(fields.Select(x => x.Name), Is.EqualTo(new[] {
                "m_maxAlpha", "m_canvasGroup", "m_enabled", "m_stalled" }));
            Assert.That(fields.Select(x => x.FieldType), Is.EqualTo(new[] {
                typeof(float), typeof(CanvasGroup), typeof(bool), typeof(bool) }));
            Assert.That(fields.All(x => (int)x.Attributes == 1), Is.True);
            Assert.That(fields[0].GetCustomAttributesData().Single().AttributeType,
                Is.EqualTo(typeof(SerializeField)));
            Assert.That(fields.Skip(1).All(x => x.GetCustomAttributesData().Count == 0), Is.True);
            Assert.That(type.GetCustomAttributesData().Select(x => x.AttributeType),
                Is.EqualTo(new[] { typeof(Il2CppSetOptionAttribute), typeof(RequireComponent),
                    typeof(Il2CppSetOptionAttribute), typeof(RequireComponent) }));
            var attributes = type.GetCustomAttributesData();
            Assert.That((int)attributes[0].ConstructorArguments[0].Value,
                Is.EqualTo((int)Option.ArrayBoundsChecks));
            Assert.That(attributes[0].ConstructorArguments[1].Value, Is.EqualTo(false));
            Assert.That(attributes[1].ConstructorArguments[0].Value, Is.EqualTo(typeof(CanvasGroup)));
            Assert.That((int)attributes[2].ConstructorArguments[0].Value,
                Is.EqualTo((int)Option.NullChecks));
            Assert.That(attributes[2].ConstructorArguments[1].Value, Is.EqualTo(false));
            Assert.That(attributes[3].ConstructorArguments[0].Value, Is.EqualTo(typeof(Canvas)));
            MethodInfo[] methods = type.GetMethods(Own).OrderBy(x => x.MetadataToken).ToArray();
            Assert.That(methods.Select(x => x.Name), Is.EqualTo(new[] {
                "StartTransition", "FinishTransition", "Awake", "DoTransition" }));
            Assert.That(methods.Select(x => (int)x.Attributes), Is.EqualTo(new[] { 134, 134, 196, 129 }));
            ParameterInfo[] parameters = methods[0].GetParameters();
            Assert.That(parameters.Select(x => x.Name), Is.EqualTo(new[] { "halfDuration", "stopAtWhite" }));
            Assert.That(parameters[1].IsOptional && parameters[1].HasDefaultValue, Is.True);
            Assert.That(parameters[1].DefaultValue, Is.EqualTo(false));
            Assert.That(methods[3].ReturnType, Is.EqualTo(typeof(IEnumerator)));
            Assert.That(type.GetConstructors(Own).Single().GetParameters().Length, Is.EqualTo(0));
            Type iterator = type.GetNestedTypes(Own).Single();
            Assert.That(typeof(IEnumerator).IsAssignableFrom(iterator), Is.True);
            Assert.That(typeof(IDisposable).IsAssignableFrom(iterator), Is.True);
            Assert.That(iterator.GetFields(Own).Length, Is.EqualTo(6));
            Assert.That(iterator.GetMethods(Own).Length, Is.EqualTo(5));
            // The compiler generates the iterator. Its local name and method
            // tokens are not treated as the original IL2CPP layout or identities.
        }

        [Test]
        public void NormalInactiveAttachmentRetainsConstructorDefaultAndRequiredComponents()
        {
            var host = new GameObject("Project Lucid owned bloom declaration fixture");
            host.SetActive(false);
            try
            {
                MenuBloomTransition bloom = host.AddComponent<MenuBloomTransition>();
                Assert.That(host.GetComponent<Canvas>(), Is.Not.Null);
                Assert.That(host.GetComponent<CanvasGroup>(), Is.Not.Null);
                Assert.That((float)typeof(MenuBloomTransition).GetField("m_maxAlpha", Own).GetValue(bloom),
                    Is.EqualTo(1f));
                Assert.That((bool)typeof(MenuBloomTransition).GetField("m_enabled", Own).GetValue(bloom),
                    Is.False);
                Assert.That((bool)typeof(MenuBloomTransition).GetField("m_stalled", Own).GetValue(bloom),
                    Is.False);
                // This fixture does not invoke Awake or write any private field.
                // Scheduled behavior is exercised by the separate PlayMode cases.
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
