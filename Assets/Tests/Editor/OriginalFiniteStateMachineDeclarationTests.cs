using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using Unity.Profiling;

namespace ProjectLucid.Tests.Editor
{
    // Declaration-only check against the shipped field rows; it does not run any
    // FSM behaviour.
    public sealed class OriginalFiniteStateMachineDeclarationTests
    {
        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        // dump.cs TypeDefIndex 8894, Hardlight.FiniteStateMachine 0x0200008f: FSMId 0x10,
        // <DoMultipleTransitions>k__BackingField 0x14, <UpdateTransientStates>k__BackingField
        // 0x15, <DefaultState>k__BackingField 0x18, m_ownedStates 0x20, m_ownedTransitions
        // 0x28, m_profilerMarker 0x30. Declaring the auto-properties after the m_ fields
        // emits the backing fields last and fails here.
        [Test]
        public void FieldRowsKeepOriginalBackingFieldPlacement()
        {
            FieldInfo[] fields = typeof(FiniteStateMachine).GetFields(Declared).OrderBy(f => f.MetadataToken).ToArray();
            CollectionAssert.AreEqual(new[] { "FSMId", "<DoMultipleTransitions>k__BackingField",
                "<UpdateTransientStates>k__BackingField", "<DefaultState>k__BackingField",
                "m_ownedStates", "m_ownedTransitions", "m_profilerMarker" }, fields.Select(f => f.Name));
            CollectionAssert.AreEqual(new[] { typeof(int), typeof(bool), typeof(bool), typeof(IFSMState),
                typeof(Dictionary<int, IFSMState>), typeof(Dictionary<int, IFSMTransition>), typeof(ProfilerMarker) },
                fields.Select(f => f.FieldType));
            CollectionAssert.AreEqual(new[] { true, false, false, false, false, false, false }, fields.Select(f => f.IsPublic));
            CollectionAssert.AreEqual(new[] { true, false, false, false, true, true, true }, fields.Select(f => f.IsInitOnly));
            Assert.That(fields.All(f => !f.IsStatic), Is.True);
        }
    }
}
