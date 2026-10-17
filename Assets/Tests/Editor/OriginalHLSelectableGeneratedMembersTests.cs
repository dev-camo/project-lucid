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
        // holding gameInputIndex (+0x10) and <>4__this (+0x18); its <ButtonSelected>b__0
        // (0x06000480, x86-64 0x1b5f5f0) is the per-entry closure that ButtonSelected
        // (0x06000476) appends. Ordinal 12 places both nested types before the fields.
        [Test]
        public void ButtonSelectedClosureKeepsTheShippingDisplayClassName()
        {
            Type[] generated = typeof(HLSelectableInputListener).GetNestedTypes(Declared)
                .Where(nested => nested.IsDefined(typeof(CompilerGeneratedAttribute), false)).ToArray();
            Assert.That(generated.Select(nested => nested.Name), Is.EqualTo(new[] { "<>c__DisplayClass12_0" }));
            Type closure = generated[0];
            Assert.That(closure.GetFields(Declared).OrderBy(field => field.MetadataToken).Select(field => field.Name),
                Is.EqualTo(new[] { "gameInputIndex", "<>4__this" }));
            MethodInfo body = closure.GetMethod("<ButtonSelected>b__0", Declared, null, new[] { typeof(float) }, null);
            Assert.That(body, Is.Not.Null);
            Assert.That(body.ReturnType, Is.EqualTo(typeof(void)));
        }

        // Kickoff 0x060006c7 (x86-64 0x1b722b0) only builds the machine with state 0
        // and <>4__this. MoveNext 0x060006f0 (0x1b737a0): state 0 sets state -1,
        // clears <>2__current, sets state 1 and returns true; state 1 sets state -1,
        // stores gameObject.scene.name into m_sceneName (+0xf0) and calls
        // ActiveSelectables.Add(this, m_sceneName), then returns false; any other
        // state returns false untouched. Registering before the first yield, or on
        // every later MoveNext, fails here.
        [Test]
        public void WaitForRootObjectsYieldsOnceThenRecordsTheSceneAndRegisters()
        {
            using (var fixture = new Fixture())
            {
                HLSelectable selectable = fixture.Selectable;
                FieldInfo sceneName = typeof(HLSelectable).GetField("m_sceneName", Declared);
                const string sentinel = "lucid-owned-sentinel-scene";
                sceneName.SetValue(selectable, sentinel);

                var routine = (IEnumerator)typeof(HLSelectable).GetMethod("WaitForRootObjects", Declared).Invoke(selectable, null);
                Assert.That(routine, Is.Not.Null);
                Assert.That(State(routine), Is.EqualTo(0));
                Assert.That(ReferenceEquals(This(routine), selectable), Is.True);
                Assert.That(sceneName.GetValue(selectable), Is.EqualTo(sentinel), "kickoff runs no body");
                Assert.That(fixture.Registered(selectable), Is.Zero);

                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(routine.Current, Is.Null);
                Assert.That(State(routine), Is.EqualTo(1));
                Assert.That(sceneName.GetValue(selectable), Is.EqualTo(sentinel), "the scene is read only after the yield");
                Assert.That(fixture.Registered(selectable), Is.Zero);

                string expected = selectable.gameObject.scene.name;
                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(State(routine), Is.EqualTo(-1));
                Assert.That(sceneName.GetValue(selectable), Is.EqualTo(expected));
                Assert.That(selectable.SceneName, Is.EqualTo(expected));
                Assert.That(fixture.Registered(selectable), Is.EqualTo(1));
                if (!string.IsNullOrWhiteSpace(expected))
                    Assert.That(fixture.RegisteredInScene(selectable, expected), Is.EqualTo(1));

                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(routine.Current, Is.Null);
                Assert.That(fixture.Registered(selectable), Is.EqualTo(1), "a finished machine does not register again");
            }
        }

        // Reset 0x060006f2 (x86-64 0x1b738b0) raises NotSupportedException.
        [Test]
        public void WaitForRootObjectsResetIsNotSupported()
        {
            using (var fixture = new Fixture())
            {
                var routine = (IEnumerator)typeof(HLSelectable).GetMethod("WaitForRootObjects", Declared).Invoke(fixture.Selectable, null);
                Assert.Throws<NotSupportedException>(() => routine.Reset());
                Assert.That(State(routine), Is.EqualTo(0));
                Assert.That(fixture.Registered(fixture.Selectable), Is.Zero);
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

            public int RegisteredInScene(HLSelectable selectable, string scene)
            {
                List<HLSelectable> list;
                return m_byScene.TryGetValue(scene, out list) ? list.Count(entry => ReferenceEquals(entry, selectable)) : 0;
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
