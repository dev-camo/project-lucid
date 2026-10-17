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
    public sealed class OriginalAbilityInterfaceEdgeTests
    {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private const TypeAttributes PublicAbstractInterface = TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract;

        [Test]
        public void TriggerableInheritsTheTriggerGetterAndDeclaresOnlyItsTwoContracts()
        {
            Type type = typeof(IAbilityTriggerable);
            Assert.AreEqual(PublicAbstractInterface, type.Attributes);
            CollectionAssert.AreEqual(new[] { typeof(IAbilityTrigger) }, type.GetInterfaces());
            Assert.IsTrue(typeof(IAbilityTrigger).IsAssignableFrom(type));
            CollectionAssert.AreEqual(new[] { "DoTrigger", "ClearTrigger" }, DeclaredMethodNames(type));
            Assert.AreEqual(0, type.GetFields(Declared).Length);

            Type trigger = typeof(IAbilityTrigger);
            Assert.AreEqual(PublicAbstractInterface, trigger.Attributes);
            Assert.AreEqual(0, trigger.GetInterfaces().Length);
            CollectionAssert.AreEqual(new[] { "get_IsTriggered" }, DeclaredMethodNames(trigger));
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
                "Reset", "get_CanActivate", "get_IsTriggered", "set_IsTriggered",
                "get_OverridesTargeting", "ShouldProjectFromCamera", "GetAdditionalSpeed", "get_TargetingDefinition",
            }, DeclaredMethodNames(type));
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

        private static string[] DeclaredMethodNames(Type type) =>
            type.GetMethods(Declared).OrderBy(method => method.MetadataToken).Select(method => method.Name).ToArray();
    }
}
