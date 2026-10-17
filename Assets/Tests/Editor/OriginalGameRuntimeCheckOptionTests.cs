using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HardlightProject;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;

namespace ProjectLucid.Tests
{
    // Own type-level Il2CppSetOption declarations of Game.Runtime owners, read
    // from the DummyDll CustomAttribute rows in metadata order. Reflection only;
    // no instance is created and no shared state is touched.
    [TestFixture]
    public sealed class OriginalGameRuntimeCheckOptionTests
    {
        // Option values: NullChecks = 1, ArrayBoundsChecks = 2.
        static void AssertOwnCheckOptions(Type type, params int[] options)
        {
            IList<CustomAttributeData> attributes = type.GetCustomAttributesData();
            Assert.That(attributes.Select(a => a.AttributeType).ToArray(),
                Is.EqualTo(Enumerable.Repeat(typeof(Il2CppSetOptionAttribute), options.Length).ToArray()), type.FullName);
            for (int i = 0; i < options.Length; ++i)
            {
                // Metadata binds Il2CppSetOptionAttribute::.ctor(Option, object).
                Assert.That(attributes[i].Constructor.GetParameters().Select(p => p.ParameterType).ToArray(),
                    Is.EqualTo(new[] { typeof(Option), typeof(object) }), type.FullName);
                Assert.That(attributes[i].ConstructorArguments.Count, Is.EqualTo(2), type.FullName);
                Assert.That(attributes[i].ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(Option)), type.FullName);
                Assert.That(Convert.ToInt32(attributes[i].ConstructorArguments[0].Value), Is.EqualTo(options[i]), type.FullName);
                Assert.That(attributes[i].ConstructorArguments[1].Value, Is.EqualTo(false), type.FullName);
                Assert.That(attributes[i].ConstructorArguments[1].Value, Is.TypeOf<bool>(), type.FullName);
                Assert.That(attributes[i].NamedArguments, Is.Empty, type.FullName);
            }
        }

        // TypeDef 0x02000a09: blobs 01000200000002000000 then 01000100000002000000.
        [Test]
        public void FSMStateLoaderDisablesArrayBoundsThenNullChecks()
        {
            Assert.That(typeof(FSMStateLoader).Assembly.GetName().Name, Is.EqualTo("Game.Runtime"));
            AssertOwnCheckOptions(typeof(FSMStateLoader), 2, 1);
        }

        // Declaration check of TypeDef 0x02000a0a (<LoadStatesCoroutine>d__14),
        // which carries only CompilerGenerated. Not a control for the owner's
        // options: the compiler never copies them onto its iterator.
        [Test]
        public void FSMStateLoaderIteratorCarriesOnlyCompilerGenerated()
        {
            Type[] iterators = typeof(FSMStateLoader).GetNestedTypes(BindingFlags.NonPublic)
                .Where(t => t.Name.StartsWith("<LoadStatesCoroutine>d__", StringComparison.Ordinal)).ToArray();
            Assert.That(iterators.Length, Is.EqualTo(1));
            Assert.That(iterators[0].GetCustomAttributesData().Select(a => a.AttributeType).ToArray(),
                Is.EqualTo(new[] { typeof(CompilerGeneratedAttribute) }));
        }

        // TypeDef 0x02000a13: ArrayBoundsChecks (2) then NullChecks (1).
        [Test]
        public void MeshGenerationUtilitiesDisablesArrayBoundsThenNullChecks()
        {
            Assert.That(typeof(MeshGenerationUtilities).Assembly.GetName().Name, Is.EqualTo("Game.Runtime"));
            AssertOwnCheckOptions(typeof(MeshGenerationUtilities), 2, 1);
        }

        // Nested TypeDef 0x02000770 lists 2 then 1, while its owner 0x0200076d
        // lists 1 then 2; both orders are kept as recorded.
        [Test]
        public void RigidbodyForceTriggerAndLockRoutineInfoKeepTheirOwnOptionOrders()
        {
            Type lockRoutineInfo = typeof(RigidbodyForceTrigger).GetNestedType("LockRoutineInfo", BindingFlags.NonPublic);
            Assert.That(typeof(RigidbodyForceTrigger).Assembly.GetName().Name, Is.EqualTo("Game.Runtime"));
            Assert.That(lockRoutineInfo, Is.Not.Null);
            Assert.That(lockRoutineInfo.IsNestedPrivate, Is.True);
            AssertOwnCheckOptions(lockRoutineInfo, 2, 1);
            AssertOwnCheckOptions(typeof(RigidbodyForceTrigger), 1, 2);
        }
    }
}
