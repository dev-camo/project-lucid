using System;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;

namespace ProjectLucid.Editor
{
    // Compiled nested enum flags must match the original 1.10.1 Game.Runtime TypeDef rows.
    // This covers declaration identity only, not Unity serialization or gameplay.
    public sealed class OriginalSerializableEnumFlagsTests
    {
        [Test]
        public void LowFilterTypeRetainsOriginalSerializableFlags() // token 0x020001cc, flags 0x2102
        {
            AssertOriginalFlags(typeof(InGameAudioListener).GetNestedType("LowFilterType", BindingFlags.Public),
                TypeAttributes.NestedPublic | TypeAttributes.Sealed | TypeAttributes.Serializable);
        }

        [Test]
        public void ConstrainTypeRetainsOriginalSerializableFlags() // token 0x0200065d, flags 0x2104
        {
            AssertOriginalFlags(typeof(EnemyNavigation).GetNestedType("ConstrainType", BindingFlags.NonPublic),
                TypeAttributes.NestedFamily | TypeAttributes.Sealed | TypeAttributes.Serializable);
        }

        private static void AssertOriginalFlags(Type type, TypeAttributes flags)
        {
            Assert.That(type, Is.Not.Null);
            Assert.That(type.IsEnum && Enum.GetUnderlyingType(type) == typeof(int));
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("Game.Runtime"));
            Assert.That(type.Attributes, Is.EqualTo(flags));
        }
    }
}
