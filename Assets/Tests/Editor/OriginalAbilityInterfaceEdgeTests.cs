using System;
using System.Linq;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // Oracle: original DummyDll Game.Runtime metadata, not the maintained source.
    //   IAbilityTrigger     0x02000080 flags 0xa1, no interfaces, methods { get_IsTriggered }
    //   IAbilityTriggerable 0x02000081 flags 0xa1, implements IAbilityTrigger, methods { DoTrigger, ClearTrigger }
    //   IHomingAbility      0x02000310 flags 0xa1, implements IAbility, eight methods below
    //   IAbility            0x0200040e flags 0xa1, no interfaces, no members
    // Each method row is checked by return type, name, parameter types and names, and flags:
    // 0x05c6 Public|Virtual|HideBySig|NewSlot|Abstract, 0x0dc6 adds SpecialName on accessors.
    public sealed class OriginalAbilityInterfaceEdgeTests
    {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private const TypeAttributes PublicAbstractInterface = TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract;
        private const string Method = "0x05c6";
        private const string Accessor = "0x0dc6";

        [Test]
        public void TriggerableInheritsTheTriggerGetterAndDeclaresOnlyItsTwoContracts()
        {
            Type type = typeof(IAbilityTriggerable);
            Assert.AreEqual(PublicAbstractInterface, type.Attributes);
            CollectionAssert.AreEqual(new[] { typeof(IAbilityTrigger) }, type.GetInterfaces());
            Assert.IsTrue(typeof(IAbilityTrigger).IsAssignableFrom(type));
            CollectionAssert.AreEqual(new[]
            {
                Method + " System.Void DoTrigger()",
                Method + " System.Void ClearTrigger()",
            }, DeclaredMethodRows(type));
            Assert.AreEqual(0, type.GetFields(Declared).Length);

            Type trigger = typeof(IAbilityTrigger);
            Assert.AreEqual(PublicAbstractInterface, trigger.Attributes);
            Assert.AreEqual(0, trigger.GetInterfaces().Length);
            CollectionAssert.AreEqual(new[] { Accessor + " System.Boolean get_IsTriggered()" }, DeclaredMethodRows(trigger));
            // The inherited getter is IAbilityTrigger's own row, not a redeclaration.
            Assert.IsNull(type.GetProperty("IsTriggered", Declared));
        }

        [Test]
        public void HomingAbilityIsAnAbilityAndDeclaresItsEightRowsInMetadataOrder()
        {
            Type type = typeof(IHomingAbility);
            Assert.AreEqual(PublicAbstractInterface, type.Attributes);
            CollectionAssert.AreEqual(new[] { typeof(IAbility) }, type.GetInterfaces());
            Assert.IsTrue(typeof(IAbility).IsAssignableFrom(type));
            CollectionAssert.AreEqual(new[]
            {
                Method + " System.Void Reset()",
                Accessor + " System.Boolean get_CanActivate()",
                Accessor + " System.Boolean get_IsTriggered()",
                Accessor + " System.Void set_IsTriggered(System.Boolean value)",
                Accessor + " System.Boolean get_OverridesTargeting()",
                Method + " System.Boolean ShouldProjectFromCamera()",
                Method + " System.Single GetAdditionalSpeed(System.Single time)",
                Accessor + " HardlightProject.CharacterAbilityDefinition_Targeting get_TargetingDefinition()",
            }, DeclaredMethodRows(type));
            Assert.AreEqual(0, type.GetFields(Declared).Length);

            // Negative control: IHomingAbility declares its own IsTriggered get/set and has no
            // IAbilityTrigger edge, so it must not become an IAbilityTrigger.
            Assert.IsFalse(typeof(IAbilityTrigger).IsAssignableFrom(type));
            Assert.AreEqual(type, type.GetProperty("IsTriggered", Declared).DeclaringType);
        }

        [Test]
        public void AbilityIsAMemberlessRootInterface()
        {
            Type type = typeof(IAbility);
            Assert.AreEqual(PublicAbstractInterface, type.Attributes);
            Assert.AreEqual(0, type.GetInterfaces().Length);
            Assert.AreEqual(0, type.GetMembers(Declared).Length);
            Assert.IsFalse(typeof(IAbilityTrigger).IsAssignableFrom(type));
            Assert.IsFalse(type.IsAssignableFrom(typeof(IAbilityTriggerable)));
        }

        // One row per declared method in metadata order: flags, return type, name and parameters.
        private static string[] DeclaredMethodRows(Type type) =>
            type.GetMethods(Declared).OrderBy(method => method.MetadataToken).Select(method =>
                "0x" + ((int)method.Attributes).ToString("x4") + " " + method.ReturnType.FullName + " " + method.Name + "("
                + string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.FullName + " " + parameter.Name)) + ")").ToArray();
    }
}
