using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight.Generics;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class OriginalPairPreservationTests
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        [Test]
        public void OriginalOwnerRetainsUnconstrainedMutableStaticAndGeneratedProperties()
        {
            Type type = typeof(Pair<,>);
            Assert.That(type.FullName, Is.EqualTo("Hardlight.Generics.Pair`2"));
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityCore.Runtime"));
            Assert.That((int)type.Attributes, Is.EqualTo(1048577));
            Assert.That(type.BaseType, Is.EqualTo(typeof(object)));
            Assert.That(type.GetInterfaces(), Is.Empty);
            Assert.That(type.GetNestedTypes(Own), Is.Empty);
            Assert.That(type.GetEvents(Own), Is.Empty);
            Assert.That(type.GetCustomAttributesData(), Is.Empty);
            Type[] parameters = type.GetGenericArguments();
            Assert.That(parameters.Select(x => x.Name), Is.EqualTo(new[] { "T", "U" }));
            Assert.That(parameters.All(x => (int)x.GenericParameterAttributes == 0 &&
                x.GetGenericParameterConstraints().Length == 0), Is.True);
            FieldInfo[] fields = type.GetFields(Own).OrderBy(x => x.MetadataToken).ToArray();
            Assert.That(fields.Select(x => x.Name), Is.EqualTo(new[] {
                "<First>k__BackingField", "<Second>k__BackingField", "s_instance" }));
            Assert.That(fields.Select(x => (int)x.Attributes), Is.EqualTo(new[] { 1, 1, 17 }));
            Assert.That(fields[0].FieldType, Is.EqualTo(parameters[0]));
            Assert.That(fields[1].FieldType, Is.EqualTo(parameters[1]));
            Assert.That(fields[2].FieldType, Is.EqualTo(type));
            foreach (FieldInfo field in fields.Take(2))
                Assert.That(field.GetCustomAttributesData().Single().AttributeType,
                    Is.EqualTo(typeof(CompilerGeneratedAttribute)));
            Assert.That(fields[2].GetCustomAttributesData(), Is.Empty);
            Assert.That(type.TypeInitializer, Is.Not.Null);
            Assert.That(type.GetConstructors(Own & ~BindingFlags.Static)
                .Select(x => x.GetParameters().Length).OrderBy(x => x),
                Is.EqualTo(new[] { 0, 2 }));
            Assert.That(type.GetMethods(Own).OrderBy(x => x.MetadataToken).Select(x => x.Name),
                Is.EqualTo(new[] { "Instance", "get_First", "set_First", "get_Second", "set_Second" }));
            Assert.That(type.GetProperties(Own).OrderBy(x => x.MetadataToken).Select(x => x.Name),
                Is.EqualTo(new[] { "First", "Second" }));
            // Compiler tokens and shared native dispatch remain distinct from
            // this observed managed contract; no native ordinal is substituted.
        }

        [Test]
        public void ConstructorsAndPropertiesRetainDefaultAndSuppliedValues()
        {
            var empty = new Pair<OwnedReference, int>();
            Assert.That(empty.First, Is.Null);
            Assert.That(empty.Second, Is.EqualTo(0));
            var supplied = new OwnedReference();
            var pair = new Pair<OwnedReference, int>(supplied, -7);
            Assert.That(pair.First, Is.SameAs(supplied));
            Assert.That(pair.Second, Is.EqualTo(-7));
            var replacement = new OwnedReference();
            pair.First = replacement;
            pair.Second = 23;
            Assert.That(pair.First, Is.SameAs(replacement));
            Assert.That(pair.Second, Is.EqualTo(23));
            Assert.That(empty.First, Is.Null);
            Assert.That(empty.Second, Is.EqualTo(0));
        }

        [Test]
        public void InstanceReusesOneMutablePairForEachOwnedClosedType()
        {
            // These private fixture types give the real generic implementation
            // its own closed statics. No global registry or static field reset
            // is needed, and production closed pairs are untouched.
            var first = new OwnedReference();
            var returned = Pair<OwnedReference, int>.Instance(first, 13);
            Assert.That(returned.First, Is.SameAs(first));
            Assert.That(returned.Second, Is.EqualTo(13));
            var second = new OwnedReference();
            var reused = Pair<OwnedReference, int>.Instance(second, -4);
            Assert.That(reused, Is.SameAs(returned));
            Assert.That(returned.First, Is.SameAs(second));
            Assert.That(returned.Second, Is.EqualTo(-4));
            var other = Pair<OwnedReference, long>.Instance(first, 71L);
            Assert.That(other, Is.Not.SameAs(returned));
            Assert.That(other.First, Is.SameAs(first));
            Assert.That(other.Second, Is.EqualTo(71L));
            Assert.That(returned.First, Is.SameAs(second));
            Assert.That(returned.Second, Is.EqualTo(-4));
            Assert.That(Pair<OwnedReference, int>.Instance(null, 0), Is.SameAs(returned));
            Assert.That(returned.First, Is.Null);
            Assert.That(returned.Second, Is.EqualTo(0));
            Assert.That(other.Second, Is.EqualTo(71L));
        }

        private sealed class OwnedReference { }
    }
}
