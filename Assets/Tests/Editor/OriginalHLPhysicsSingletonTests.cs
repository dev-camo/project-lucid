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

        // The sole member is the original public parameterless instance constructor: MethodDef
        // 0x06000001, method flags 0x1886 (Public | HideBySig | SpecialName | RTSpecialName) and
        // implementation flags 0 (IL, managed). Catches a private, parameterised or static replacement.
        [Test]
        public void SoleConstructorIsTheOriginalPublicParameterlessOne()
        {
            const System.Reflection.BindingFlags Declared = System.Reflection.BindingFlags.DeclaredOnly | System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            System.Reflection.ConstructorInfo[] constructors = typeof(HLPhysics).GetConstructors(Declared);
            Assert.That(constructors, Has.Length.EqualTo(1));
            System.Reflection.ConstructorInfo constructor = constructors[0];
            Assert.That(constructor.IsPublic && !constructor.IsStatic, Is.True);
            Assert.That(constructor.GetParameters(), Is.Empty);
            Assert.That((int)constructor.Attributes, Is.EqualTo(0x1886));
            Assert.That((int)constructor.GetMethodImplementationFlags(), Is.EqualTo(0));
            Assert.That(typeof(HLPhysics).GetConstructor(System.Type.EmptyTypes), Is.SameAs(constructor), "the public parameterless lookup finds it");
        }
    }
}
