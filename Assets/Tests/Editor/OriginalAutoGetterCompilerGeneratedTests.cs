using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HardlightProject;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // Oracle: original Game.Runtime DummyDll metadata. Each getter below
    // carries exactly one custom attribute, CompilerGeneratedAttribute (blob
    // 01000000); each property row has no flags or attributes; each backing
    // field carries CompilerGeneratedAttribute and is Private|InitOnly
    // (0x0021). An explicit [CompilerGenerated] on an automatic accessor
    // compiles to two.
    public sealed class OriginalAutoGetterCompilerGeneratedTests
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.DeclaredOnly;

        private static void AssertOriginalAutoGetter(Type type, string property)
        {
            PropertyInfo info = type.GetProperty(property, Own);
            Assert.That(info, Is.Not.Null, type.FullName + "." + property);
            Assert.That(info.SetMethod, Is.Null, property + " stays get-only");
            Assert.That(info.GetCustomAttributesData(), Is.Empty, property + " property attributes");
            // The getter's whole attribute list, not only its CompilerGenerated count.
            Assert.That(info.GetMethod.GetCustomAttributesData().Select(x => x.AttributeType),
                Is.EqualTo(new[] { typeof(CompilerGeneratedAttribute) }), type.FullName + ".get_" + property);
            AssertSingleCompilerGenerated(info.GetMethod, type.FullName + ".get_" + property);
            FieldInfo field = type.GetField("<" + property + ">k__BackingField", Own);
            Assert.That(field, Is.Not.Null, property + " backing field");
            Assert.That((int)field.Attributes, Is.EqualTo(0x0021), property + " backing field flags");
            AssertSingleCompilerGenerated(field, field.Name);
        }

        private static void AssertSingleCompilerGenerated(MemberInfo member, string label)
        {
            Assert.That(member.GetCustomAttributesData()
                .Count(x => x.AttributeType == typeof(CompilerGeneratedAttribute)), Is.EqualTo(1), label);
            Assert.That(member.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Length,
                Is.EqualTo(1), label);
            // Singular lookup throws AmbiguousMatchException when duplicated.
            Assert.That(member.GetCustomAttribute<CompilerGeneratedAttribute>(false), Is.Not.Null, label);
        }

        // Original 0x06003b56, backing field at offset 0x10.
        [Test]
        public void EntityActivationInstanceEntityGetterCarriesOneCompilerGenerated()
        {
            AssertOriginalAutoGetter(typeof(EntityActivationInstance), "Entity");
        }

        // Original 0x060032f9/0x060032fa.
        [Test]
        public void UIContainerLoadingParametersGettersCarryOneCompilerGenerated()
        {
            AssertOriginalAutoGetter(typeof(UIContainerLoadingParameters), "LevelDefinition");
            AssertOriginalAutoGetter(typeof(UIContainerLoadingParameters), "MissionDefinition");
        }

        // Original 0x06002500/0x06002501.
        [Test]
        public void MetaGameStateGettersCarryOneCompilerGenerated()
        {
            AssertOriginalAutoGetter(typeof(MetaGameState), "LevelDefinition");
            AssertOriginalAutoGetter(typeof(MetaGameState), "MissionDefinition");
        }
    }
}
