using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Editor
{
    // The original 1.10.1 HLPhysics.Runtime singleton declares nothing beyond its base.
    public sealed class OriginalHLPhysicsSingletonTests
    {
        [Test]
        public void SingletonMatchesTheShippedDeclaration()
        {
            Assert.That(typeof(HLPhysics).Assembly.GetName().Name, Is.EqualTo("HLPhysics.Runtime"));
            Assert.That((int)typeof(HLPhysics).Attributes, Is.EqualTo(0x00100001));
            Assert.That(typeof(HLPhysics).BaseType, Is.EqualTo(typeof(MonoSingleton<HLPhysics>)));
            Assert.That(typeof(HLPhysics).GetCustomAttributesData(), Is.Empty);
            const System.Reflection.BindingFlags Declared = System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            Assert.That(typeof(HLPhysics).GetMembers(Declared), Has.Length.EqualTo(1), "only the default constructor");
        }
    }
}
