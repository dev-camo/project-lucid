using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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

        // dump.cs TypeDefIndex 8894, DummyDll Hardlight.FiniteStateMachine 0x0200008f field
        // rows 0x040001df..e5: FSMId (flags 0x26), the three [CompilerGenerated] backing
        // fields (0x01), then m_ownedStates, m_ownedTransitions, m_profilerMarker (0x21).
        // Declaring the auto-properties after the m_ fields emits the backing fields last.
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
            CollectionAssert.AreEqual(new[] { 0x26, 0x01, 0x01, 0x01, 0x21, 0x21, 0x21 }, fields.Select(f => (int)f.Attributes));
            CollectionAssert.AreEqual(new[] { false, true, true, true, false, false, false },
                fields.Select(f => f.IsDefined(typeof(CompilerGeneratedAttribute), false)));
        }
    }
}
