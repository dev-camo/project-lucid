using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    public sealed class OriginalScreenOrientationExtensionsPreservationTests
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        [Test]
        public void OriginalDeclarationsRetainStaticExtensionsAndEnumOnlyConstraint()
        {
            foreach (Type type in new[] { typeof(ScreenOrientationExtensions), typeof(GenericExtensions) })
            {
                Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityCore.Runtime"));
                Assert.That((int)type.Attributes, Is.EqualTo(1048961));
                Assert.That(type.BaseType, Is.EqualTo(typeof(object)));
                Assert.That(type.GetFields(Own), Is.Empty);
                Assert.That(type.GetConstructors(Own), Is.Empty);
                Assert.That(type.TypeInitializer, Is.Null);
                Assert.That(type.GetInterfaces(), Is.Empty);
                Assert.That(type.GetNestedTypes(Own), Is.Empty);
                Assert.That(type.GetProperties(Own), Is.Empty);
                Assert.That(type.GetEvents(Own), Is.Empty);
                Assert.That(type.GetCustomAttributesData().Single().AttributeType,
                    Is.EqualTo(typeof(ExtensionAttribute)));
                MethodInfo method = type.GetMethods(Own).Single();
                Assert.That((int)method.Attributes, Is.EqualTo(150));
                Assert.That(method.GetCustomAttributesData().Single().AttributeType,
                    Is.EqualTo(typeof(ExtensionAttribute)));
                Assert.That(method.ReturnType, Is.EqualTo(typeof(bool)));
                Assert.That(method.GetParameters().Length, Is.EqualTo(1));
            }
            MethodInfo landscape = typeof(ScreenOrientationExtensions).GetMethods(Own).Single();
            Assert.That(landscape.Name, Is.EqualTo("IsLandscape"));
            Assert.That(landscape.GetParameters()[0].ParameterType, Is.EqualTo(typeof(ScreenOrientation)));
            MethodInfo obsolete = typeof(GenericExtensions).GetMethods(Own).Single();
            Assert.That(obsolete.Name, Is.EqualTo("IsObsolete"));
            Type parameter = obsolete.GetGenericArguments().Single();
            Assert.That(parameter.Name, Is.EqualTo("T"));
            Assert.That((int)parameter.GenericParameterAttributes, Is.EqualTo(0));
            Assert.That(parameter.GetGenericParameterConstraints(), Is.EqualTo(new[] { typeof(Enum) }));
            Assert.That(obsolete.GetParameters()[0].ParameterType, Is.EqualTo(parameter));
        }

        [Test]
        public void LandscapeAcceptsOnlyTheTwoOriginalUnderlyingValues()
        {
            foreach (int value in new[] { int.MinValue, -1, 0, 1, 2, 3, 4, 5, 6, int.MaxValue })
                Assert.That(((ScreenOrientation)value).IsLandscape(), Is.EqualTo(value == 3 || value == 4),
                    "Underlying orientation " + value);
            // The original Landscape alias is obsolete with isError=true.
            // Read the genuine public enum field without a forbidden direct
            // source reference; its original value still exercises the helper.
            FieldInfo alias = typeof(ScreenOrientation).GetField("Landscape",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(alias, Is.Not.Null);
            Assert.That(alias.FieldType, Is.EqualTo(typeof(ScreenOrientation)));
            var aliasValue = (ScreenOrientation)alias.GetValue(null);
            Assert.That((int)aliasValue, Is.EqualTo(3));
            Assert.That(aliasValue.IsLandscape(), Is.True);
        }

        [Test]
        public void ObsoleteAliasesRetainAnyNonobsoleteEquivalentValue()
        {
#pragma warning disable 0612
            Assert.That(OwnedEnum.Current.IsObsolete(), Is.False);
            Assert.That(OwnedEnum.Retired.IsObsolete(), Is.True);
            Assert.That(OwnedEnum.MixedRetired.IsObsolete(), Is.False);
            Assert.That(OwnedEnum.MixedCurrent.IsObsolete(), Is.False);
            Assert.That(OwnedEnum.AllRetiredFirst.IsObsolete(), Is.True);
            Assert.That(OwnedEnum.AllRetiredSecond.IsObsolete(), Is.True);
#pragma warning restore 0612
            // Alias selection/reflection order is supplied by the real runtime.
            // These results follow whichever equal-valued field it selects.
        }

        [Test]
        public void EnumBaseConstraintUsesTheActualBoxedEnumType()
        {
#pragma warning disable 0612
            Enum retired = OwnedEnum.Retired;
            Enum mixed = OwnedEnum.MixedRetired;
#pragma warning restore 0612
            Assert.That(retired.IsObsolete(), Is.True);
            Assert.That(mixed.IsObsolete(), Is.False);
            Assert.That(((Enum)OwnedEnum.Current).IsObsolete(), Is.False);
        }

        [Test]
        public void NullAndUndefinedValuesKeepManagedReflectionFaults()
        {
            Enum absent = null;
            Assert.Throws<NullReferenceException>(() => absent.IsObsolete());
            Assert.Throws<ArgumentNullException>(() => ((OwnedEnum)99).IsObsolete());
            // These are the rebuilt managed runtime's faults. Native null/fault
            // equivalence remains separate from this owned reflection fixture.
        }

        private enum OwnedEnum
        {
            Current = 0,
            [Obsolete] Retired = 1,
            [Obsolete] MixedRetired = 2,
            MixedCurrent = 2,
            [Obsolete] AllRetiredFirst = 3,
            [Obsolete] AllRetiredSecond = 3
        }
    }
}
