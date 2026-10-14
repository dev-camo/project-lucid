using System;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;

namespace ProjectLucid.Editor
{
    // Compiled type flags must match the original 1.10.1 Game.Runtime TypeDef rows.
    // This covers declaration identity only, not gameplay or native layout.
    public sealed class OriginalSealedTypeFlagsTests
    {
        // Original flags 0x00100101: Public | Sealed | BeforeFieldInit.
        private const TypeAttributes OriginalFlags = TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit;

        [Test]
        public void LevelManagerLevelRetainsOriginalSealedFlags() // token 0x020006be
        {
            AssertOriginalFlags(typeof(LevelManagerLevel), typeof(object));
        }

        [Test]
        public void GameTipsDefinitionRetainsOriginalSealedFlags() // token 0x0200055a
        {
            AssertOriginalFlags(typeof(GameTipsDefinition), typeof(UnityEngine.ScriptableObject));
        }

        private static void AssertOriginalFlags(Type type, Type baseType)
        {
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("Game.Runtime"));
            Assert.That(type.BaseType, Is.EqualTo(baseType));
            Assert.That(type.Attributes, Is.EqualTo(OriginalFlags));
        }
    }
}
