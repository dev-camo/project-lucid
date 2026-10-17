using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Compiler-generated members of HLSelectable and HLSelectableInputListener.
    // The selectable lives on an inactive owned root, so Awake/OnEnable never run;
    // the ActiveSelectables registry is snapshotted and restored in place.
    public sealed class OriginalHLSelectableGeneratedMembersTests
    {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // Shipping metadata: TypeDef 0x020000f6 HLSelectable+<WaitForRootObjects>d__77
        // with fields <>1__state (+0x10), <>2__current (+0x18), <>4__this (+0x20).
        // The ordinal follows the member declaration order, so a reordering that
        // still compiles shows up here as d__76 or d__78.
        [Test]
        public void WaitForRootObjectsStateMachineKeepsTheShippingName()
        {
            MethodInfo kickoff = typeof(HLSelectable).GetMethod("WaitForRootObjects", Declared);
            Assert.That(kickoff, Is.Not.Null);
            Assert.That(kickoff.ReturnType, Is.EqualTo(typeof(IEnumerator)));
            var attribute = kickoff.GetCustomAttribute<IteratorStateMachineAttribute>();
            Assert.That(attribute, Is.Not.Null);
            Type machine = attribute.StateMachineType;
            Assert.That(machine.Name, Is.EqualTo("<WaitForRootObjects>d__77"));
            Assert.That(machine.DeclaringType, Is.EqualTo(typeof(HLSelectable)));
            Assert.That(machine.IsNestedPrivate && machine.IsSealed, Is.True);
            Assert.That(machine.GetInterfaces(), Is.EquivalentTo(new[] { typeof(IEnumerator<object>), typeof(IEnumerator), typeof(IDisposable) }));
            Assert.That(machine.GetFields(Declared).OrderBy(field => field.MetadataToken).Select(field => field.Name),
                Is.EqualTo(new[] { "<>1__state", "<>2__current", "<>4__this" }));

            string[] generated = typeof(HLSelectable).GetNestedTypes(Declared)
                .Where(nested => nested.IsDefined(typeof(CompilerGeneratedAttribute), false))
                .Select(nested => nested.Name).ToArray();
            Assert.That(generated, Is.EqualTo(new[] { "<WaitForRootObjects>d__77" }));
        }

        // Shipping metadata: TypeDef 0x02000092 HLSelectableInputListener+<>c__DisplayClass12_0
        // is the only compiler-generated nested type. Its fields and <ButtonSelected>b__0
        // are pinned by ListenerWholeNaturalFamilyKeepsOriginalFlagsAndTypedSignatures.
        [Test]
        public void ButtonSelectedClosureKeepsTheShippingDisplayClassName()
        {
            string[] generated = typeof(HLSelectableInputListener).GetNestedTypes(Declared)
                .Where(nested => nested.IsDefined(typeof(CompilerGeneratedAttribute), false))
                .Select(nested => nested.Name).ToArray();
            Assert.That(generated, Is.EqualTo(new[] { "<>c__DisplayClass12_0" }));
        }

        // Emission-shape self-check, not an original-behaviour oracle: Roslyn iterator
        // lowering fixes these for any C# iterator. Kickoff 0x060006c7 (x86-64 0x1b722b0)
        // builds the machine with <>1__state 0 and <>4__this; MoveNext moves it to 1 then
        // -1; Reset 0x060006f2 (0x1b738b0) raises NotSupportedException.
        [Test]
        public void WaitForRootObjectsIteratorEmissionShape()
        {
            using (var fixture = new Fixture())
            {
                var routine = (IEnumerator)typeof(HLSelectable).GetMethod("WaitForRootObjects", Declared).Invoke(fixture.Selectable, null);
                Assert.That(State(routine), Is.EqualTo(0));
                Assert.That(ReferenceEquals(This(routine), fixture.Selectable), Is.True);
                Assert.Throws<NotSupportedException>(() => routine.Reset());
                Assert.That(State(routine), Is.EqualTo(0));
                Assert.That(fixture.Registered(fixture.Selectable), Is.Zero);
                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(State(routine), Is.EqualTo(1));
                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(State(routine), Is.EqualTo(-1));
            }
        }

        private static int State(IEnumerator routine)
        {
            return (int)routine.GetType().GetField("<>1__state", Declared).GetValue(routine);
        }

        private static object This(IEnumerator routine)
        {
            return routine.GetType().GetField("<>4__this", Declared).GetValue(routine);
        }

        private sealed class Fixture : IDisposable
        {
            private static readonly FieldInfo AllField = typeof(ActiveSelectables).GetField("s_allSelectables", BindingFlags.NonPublic | BindingFlags.Static);
            private static readonly FieldInfo ByScene = typeof(ActiveSelectables).GetField("s_selectables", BindingFlags.NonPublic | BindingFlags.Static);
            private readonly List<HLSelectable> m_all;
            private readonly Dictionary<string, List<HLSelectable>> m_byScene;
            private readonly List<HLSelectable> m_allBefore;
            private readonly Dictionary<string, KeyValuePair<List<HLSelectable>, HLSelectable[]>> m_byBefore;
            private readonly GameObject m_root;

            public Fixture()
            {
                m_all = (List<HLSelectable>)AllField.GetValue(null);
                m_byScene = (Dictionary<string, List<HLSelectable>>)ByScene.GetValue(null);
                m_allBefore = new List<HLSelectable>(m_all);
                m_byBefore = m_byScene.ToDictionary(pair => pair.Key,
                    pair => new KeyValuePair<List<HLSelectable>, HLSelectable[]>(pair.Value, pair.Value.ToArray()));
                m_root = new GameObject("Lucid owned inactive selectable", typeof(RectTransform));
                m_root.SetActive(false);
                Selectable = m_root.AddComponent<HLSelectable>();
            }

            public HLSelectable Selectable { get; }

            public int Registered(HLSelectable selectable)
            {
                return m_all.Count(entry => ReferenceEquals(entry, selectable));
            }

            public void Dispose()
            {
                try
                {
                    if (m_root != null) UObject.DestroyImmediate(m_root);
                }
                finally
                {
                    m_all.Clear();
                    m_all.AddRange(m_allBefore);
                    // Same list instances and contents; keys Add created are dropped.
                    m_byScene.Clear();
                    foreach (KeyValuePair<string, KeyValuePair<List<HLSelectable>, HLSelectable[]>> pair in m_byBefore)
                    {
                        List<HLSelectable> list = pair.Value.Key;
                        list.Clear();
                        list.AddRange(pair.Value.Value);
                        m_byScene[pair.Key] = list;
                    }
                }
            }
        }
    }
}
