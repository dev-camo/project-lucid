using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using ProjectLucid.Tests.UISelectionShared;
using Value = ProjectLucid.Tests.UISelectionShared.EnumSelectorProbeValue;

namespace ProjectLucid.Tests
{
    // Behaviour specification for the original HLUnityUI.Runtime HLEnumSelector`1
    // (TypeDef 0x0200000b, global namespace), from its fully shared x86-64 bodies
    // 0x1392370..0x1392F80 and the type RGCTX ([5] Init, [6] EnumUtilities.GetValues<T>,
    // [7] SetStartingValue). Field slots by FieldInfo offset: +0x00 m_startingValue,
    // +0x20 m_label, +0x40 m_currentValue, +0x60 m_values, +0x80 m_valueCount,
    // +0xA0 m_currentIndex. Validate is vtable slot 4 (+0x178, direct call); Compare is
    // vtable slot 5, called through its MethodInfo invoker at +0x190 (fully shared).
    // The probe enum is declared as Seven, Zero, Two, AlsoTwo, so GetValues gives 0, 2, 2, 7.
    public sealed class OriginalHLEnumSelectorTests
    {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static readonly Type Open = typeof(HLEnumSelector<>);
        private static readonly Type Closed = typeof(HLEnumSelector<Value>);

        private readonly List<GameObject> m_owned = new List<GameObject>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            foreach (GameObject owner in m_owned)
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
            m_owned.Clear();
        }

        // dump.cs 11835 and the DummyDll metadata: abstract, BeforeFieldInit, one
        // unconstrained T with no variance flags, the six fields with their accessibility and
        // [SerializeField] rows, the getter-only Value property, and methods 0x06000063..6d.
        [Test]
        public void EnumSelectorRetainsTheOriginalDeclaration()
        {
            Assert.That(Open.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(Open.Namespace, Is.Null);
            Assert.That(Open.Name, Is.EqualTo("HLEnumSelector`1"));
            Assert.That(Open.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That((int)Open.Attributes, Is.EqualTo(1048705));
            Assert.That(Open.GetCustomAttributesData(), Is.Empty);
            Assert.That(Open.GetNestedTypes(Declared), Is.Empty);
            Type t = Open.GetGenericArguments().Single();
            Assert.That(t.Name, Is.EqualTo("T"));
            Assert.That(t.GenericParameterAttributes, Is.EqualTo(GenericParameterAttributes.None));
            Assert.That(t.GetGenericParameterConstraints(), Is.Empty);

            FieldInfo[] fields = Open.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "m_startingValue", "m_label", "m_currentValue", "m_values", "m_valueCount", "m_currentIndex" }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[] { t, typeof(TMP_Text), t, t.MakeArrayType(), typeof(int), typeof(int) }));
            Assert.That(fields.Select(f => (int)f.Attributes).ToArray(), Is.EqualTo(new[] { 1, 4, 4, 4, 1, 1 }));
            Assert.That(fields.Select(f => f.GetCustomAttributesData().Count).ToArray(), Is.EqualTo(new[] { 1, 1, 0, 0, 0, 0 }));
            Assert.That(fields[0].GetCustomAttributesData()[0].AttributeType, Is.EqualTo(typeof(SerializeField)));
            Assert.That(fields[1].GetCustomAttributesData()[0].AttributeType, Is.EqualTo(typeof(SerializeField)));

            PropertyInfo property = Open.GetProperties(Declared).Single();
            Assert.That(property.Name, Is.EqualTo("Value"));
            Assert.That(property.PropertyType, Is.EqualTo(t));
            Assert.That(property.CanWrite, Is.False);

            MethodInfo[] methods = Open.GetMethods(Declared);
            Assert.That(methods.Select(m => m.Name).ToArray(), Is.EqualTo(new[] { "get_Value", "Action_Increment", "Action_Decrement", "SetCurrentValue", "OnEnable", "OnValidate", "Init", "Validate", "Compare", "SetStartingValue" }));
            Assert.That(methods.Select(m => (int)m.Attributes).ToArray(), Is.EqualTo(new[] { 2182, 134, 134, 134, 129, 129, 134, 1476, 1476, 129 }));
            Assert.That(methods.Select(m => string.Join(",", m.GetParameters().Select(p => p.Name))).ToArray(), Is.EqualTo(new[] { "", "", "", "value", "", "", "", "", "a,b", "" }));
            Assert.That(methods.Single(m => m.Name == "SetCurrentValue").GetParameters()[0].ParameterType, Is.EqualTo(t));
            Assert.That(methods.Single(m => m.Name == "Compare").GetParameters().Select(p => p.ParameterType).ToArray(), Is.EqualTo(new[] { t, t }));
            Assert.That(methods.Single(m => m.Name == "Compare").ReturnType, Is.EqualTo(typeof(bool)));
            Assert.That(methods.Single(m => m.Name == "get_Value").ReturnType, Is.EqualTo(t));
            Assert.That(methods.Where(m => m.Name != "Compare" && m.Name != "get_Value").All(m => m.ReturnType == typeof(void)), Is.True);

            ConstructorInfo constructor = Open.GetConstructors(Declared).Single();
            Assert.That((int)constructor.Attributes, Is.EqualTo(6276));
            Assert.That(constructor.GetParameters(), Is.Empty);
        }

        // Init 0x06000069 @0x1392B00: m_values = EnumUtilities.GetValues<T>() (RGCTX [6]),
        // m_valueCount = m_values.Length (+0x18), SetStartingValue (RGCTX [7]), then
        // Validate. SetStartingValue 0x0600006c @0x1392C30: index = 0, then (the boxed
        // starting value is never null for an enum) Compare(m_values[i], m_startingValue)
        // until the first match, then m_currentValue = m_values[index]. Starting value 2
        // stops at index 1 of 0, 2, 2, 7 after two compares; a last-match search gives 2.
        [Test]
        public void InitLoadsTheEnumValuesAndStartsAtTheFirstMatch()
        {
            EnumSelectorProbe probe = Make("OriginalHLEnumSelector-init");
            Set(probe, "m_startingValue", Value.Two);
            probe.Init();

            Assert.That(Get(probe, "m_values"), Is.EqualTo(new[] { Value.Zero, Value.Two, Value.AlsoTwo, Value.Seven }));
            Assert.That(Get(probe, "m_valueCount"), Is.EqualTo(4));
            Assert.That(Get(probe, "m_currentIndex"), Is.EqualTo(1));
            Assert.That(probe.Value, Is.EqualTo(Value.Two));
            Assert.That(probe.Log, Is.EqualTo(new[] { "Compare 0,2", "Compare 2,2", "Validate 2 values=4" }));
        }

        // 0x1392CB5 writes index 0 before the search; with no match the index stays 0 and the
        // current value falls back to m_values[0], not to the starting value.
        [Test]
        public void InitWithoutAMatchFallsBackToTheFirstValue()
        {
            EnumSelectorProbe probe = Make("OriginalHLEnumSelector-init-nomatch");
            Set(probe, "m_startingValue", Value.Seven);
            Set(probe, "m_currentIndex", 3);
            probe.Comparison = (a, b) => false;
            probe.Init();

            Assert.That(Get(probe, "m_currentIndex"), Is.EqualTo(0));
            Assert.That(probe.Value, Is.EqualTo(Value.Zero));
            Assert.That(probe.Log, Is.EqualTo(new[] { "Compare 0,7", "Compare 2,7", "Compare 2,7", "Compare 7,7", "Validate 0 values=4" }));
        }

        // The search uses the subclass Compare, not equality: an always-true Compare stops at
        // index 0 even though the starting value is the last entry.
        [Test]
        public void InitSearchesWithTheSubclassCompare()
        {
            EnumSelectorProbe probe = Make("OriginalHLEnumSelector-init-compare");
            Set(probe, "m_startingValue", Value.Seven);
            probe.Comparison = (a, b) => true;
            probe.Init();
            Assert.That(Get(probe, "m_currentIndex"), Is.EqualTo(0));
            Assert.That(probe.Value, Is.EqualTo(Value.Zero));
            Assert.That(probe.Log, Is.EqualTo(new[] { "Compare 0,7", "Validate 0 values=4" }));
        }

        // OnEnable 0x06000067 and OnValidate 0x06000068 are both a tail jump to Init
        // (RGCTX [5]).
        [Test]
        public void OnEnableRunsInit() => CheckLifecycleMessageRunsInit("OnEnable");

        [Test]
        public void OnValidateRunsInit() => CheckLifecycleMessageRunsInit("OnValidate");

        private void CheckLifecycleMessageRunsInit(string message)
        {
            EnumSelectorProbe probe = Make("OriginalHLEnumSelector-" + message);
            Set(probe, "m_values", null);
            Set(probe, "m_startingValue", Value.Seven);
            Closed.GetMethod(message, Declared).Invoke(probe, null);
            Assert.That(Get(probe, "m_valueCount"), Is.EqualTo(4));
            Assert.That(Get(probe, "m_currentIndex"), Is.EqualTo(3));
            Assert.That(probe.Value, Is.EqualTo(Value.Seven));
            Assert.That(probe.Log.Last(), Is.EqualTo("Validate 7 values=4"));
        }

        // get_Value 0x06000063 @0x1392370 copies +0x40 m_currentValue.
        [Test]
        public void ValueReadsTheCurrentValueField()
        {
            EnumSelectorProbe probe = Make("OriginalHLEnumSelector-value");
            Set(probe, "m_currentValue", Value.Seven);
            Assert.That(probe.Value, Is.EqualTo(Value.Seven));
            Set(probe, "m_currentValue", (Value)5);
            Assert.That(probe.Value, Is.EqualTo((Value)5));
        }

        // Action_Increment 0x06000064 @0x13923C0: ++index; `cmp index, m_valueCount; jl`
        // wraps to 0 at the cached count (not m_values.Length); current = m_values[index];
        // Validate. A count of 2 over four values wraps from 1 to 0.
        [Test]
        public void IncrementWrapsAtTheCachedValueCount()
        {
            EnumSelectorProbe probe = MakeInitialised("OriginalHLEnumSelector-increment");
            probe.Action_Increment();
            Assert.That(Get(probe, "m_currentIndex"), Is.EqualTo(1));
            Assert.That(probe.Value, Is.EqualTo(Value.Two));
            probe.Action_Increment();
            probe.Action_Increment();
            Assert.That(Get(probe, "m_currentIndex"), Is.EqualTo(3));
            Assert.That(probe.Value, Is.EqualTo(Value.Seven));
            probe.Action_Increment();
            Assert.That(Get(probe, "m_currentIndex"), Is.EqualTo(0));
            Assert.That(probe.Value, Is.EqualTo(Value.Zero));
            Assert.That(probe.Log, Is.EqualTo(new[] { "Validate 2 values=4", "Validate 2 values=4", "Validate 7 values=4", "Validate 0 values=4" }));

            Set(probe, "m_valueCount", 2);
            Set(probe, "m_currentIndex", 1);
            probe.Action_Increment();
            Assert.That(Get(probe, "m_currentIndex"), Is.EqualTo(0));
            Assert.That(probe.Value, Is.EqualTo(Value.Zero));
        }

        // Action_Decrement 0x06000065 @0x1392600: --index; `jns` keeps a non-negative index,
        // otherwise index = m_valueCount - 1; current = m_values[index]; Validate. Neither
        // direction compares.
        [Test]
        public void DecrementWrapsToTheLastCachedIndex()
        {
            EnumSelectorProbe probe = MakeInitialised("OriginalHLEnumSelector-decrement");
            probe.Action_Decrement();
            Assert.That(Get(probe, "m_currentIndex"), Is.EqualTo(3));
            Assert.That(probe.Value, Is.EqualTo(Value.Seven));
            probe.Action_Decrement();
            Assert.That(Get(probe, "m_currentIndex"), Is.EqualTo(2));
            Assert.That(probe.Value, Is.EqualTo(Value.AlsoTwo));
            Assert.That(probe.Log, Is.EqualTo(new[] { "Validate 7 values=4", "Validate 2 values=4" }));

            Set(probe, "m_valueCount", 2);
            Set(probe, "m_currentIndex", 0);
            probe.Action_Decrement();
            Assert.That(Get(probe, "m_currentIndex"), Is.EqualTo(1));
            Assert.That(probe.Value, Is.EqualTo(Value.Two));
        }

        // 0x13926DE `cmp [m_currentIndex],0; jns` wraps only a negative index, so 1 steps
        // down to 0 (a `<= 0` wrap would give index 3, Seven).
        [Test]
        public void DecrementFromOneKeepsIndexZero()
        {
            EnumSelectorProbe probe = MakeInitialised("OriginalHLEnumSelector-decrement-zero");
            Set(probe, "m_currentIndex", 1);
            probe.Action_Decrement();
            Assert.That(Get(probe, "m_currentIndex"), Is.EqualTo(0));
            Assert.That(probe.Value, Is.EqualTo(Value.Zero));
            Assert.That(probe.Log, Is.EqualTo(new[] { "Validate 0 values=4" }));
        }

        // SetCurrentValue 0x06000066 @0x1392850: m_currentValue = value first, then
        // Compare(m_values[i], m_currentValue) for i < m_valueCount, index = first match,
        // then Validate. Value 2 matches at index 1 after two compares.
        [Test]
        public void SetCurrentValueStoresTheValueAndMovesToTheFirstMatch()
        {
            EnumSelectorProbe probe = MakeInitialised("OriginalHLEnumSelector-set");
            probe.SetCurrentValue(Value.AlsoTwo);
            Assert.That(Get(probe, "m_currentIndex"), Is.EqualTo(1));
            Assert.That(probe.Value, Is.EqualTo(Value.Two));
            Assert.That(probe.Log, Is.EqualTo(new[] { "Compare 0,2", "Compare 2,2", "Validate 2 values=4" }));
        }

        // Without a match the stored value is kept as given (even one outside the enum) and
        // the index stays where it was; Validate still runs.
        [Test]
        public void SetCurrentValueWithoutAMatchKeepsTheValueAndTheIndex()
        {
            EnumSelectorProbe probe = MakeInitialised("OriginalHLEnumSelector-set-nomatch");
            Set(probe, "m_currentIndex", 2);
            probe.SetCurrentValue((Value)5);
            Assert.That(Get(probe, "m_currentIndex"), Is.EqualTo(2));
            Assert.That(probe.Value, Is.EqualTo((Value)5));
            Assert.That(probe.Log, Is.EqualTo(new[] { "Compare 0,5", "Compare 2,5", "Compare 2,5", "Compare 7,5", "Validate 5 values=4" }));
        }

        // The loop bound is the cached count: with m_valueCount = 2, value 7 is never reached.
        [Test]
        public void SetCurrentValueSearchesOnlyTheCachedCount()
        {
            EnumSelectorProbe probe = MakeInitialised("OriginalHLEnumSelector-set-count");
            Set(probe, "m_valueCount", 2);
            probe.SetCurrentValue(Value.Seven);
            Assert.That(Get(probe, "m_currentIndex"), Is.EqualTo(0));
            Assert.That(probe.Log, Is.EqualTo(new[] { "Compare 0,7", "Compare 2,7", "Validate 7 values=4" }));
        }

        // An enum with no values: Init stores the empty array and a zero count, the search
        // never runs, and SetStartingValue's m_values[0] (0x1392EEB) throws before Validate.
        // The Editor may already send OnValidate (and so Init) during AddComponent, which
        // would log the same exception; only that call tolerates failing logs (LogScope
        // 1.1.33 filters them as they arrive), so the explicit Init below is still checked.
        [Test]
        public void InitOverAnEmptyEnumThrowsBeforeValidate()
        {
            var owner = new GameObject("OriginalHLEnumSelector-empty");
            m_owned.Add(owner);
            owner.SetActive(false);
            EmptyEnumSelectorProbe probe;
            bool priorIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                probe = owner.AddComponent<EmptyEnumSelectorProbe>();
            }
            finally
            {
                LogAssert.ignoreFailingMessages = priorIgnoreFailingMessages;
            }
            probe.Log = new List<string>();
            Assert.Throws<IndexOutOfRangeException>(() => probe.Init());
            Type closed = typeof(HLEnumSelector<EmptyEnumSelectorProbeValue>);
            Assert.That(closed.GetField("m_values", Declared).GetValue(probe), Is.Empty);
            Assert.That(closed.GetField("m_valueCount", Declared).GetValue(probe), Is.EqualTo(0));
            Assert.That(probe.Log, Is.Empty);
        }

        private EnumSelectorProbe Make(string name)
        {
            var owner = new GameObject(name);
            m_owned.Add(owner);
            owner.SetActive(false);
            EnumSelectorProbe probe = owner.AddComponent<EnumSelectorProbe>();
            probe.Log = new List<string>();
            return probe;
        }

        // Starts at index 0 (value 0) with a clear log.
        private EnumSelectorProbe MakeInitialised(string name)
        {
            EnumSelectorProbe probe = Make(name);
            Set(probe, "m_startingValue", Value.Zero);
            probe.Init();
            Assert.That(Get(probe, "m_currentIndex"), Is.EqualTo(0));
            probe.Log.Clear();
            return probe;
        }

        private static object Get(object target, string name) => Closed.GetField(name, Declared).GetValue(target);
        private static void Set(object target, string name, object value) => Closed.GetField(name, Declared).SetValue(target, value);
    }
}
