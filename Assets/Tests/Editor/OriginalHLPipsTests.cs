using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of HLPips (HLUnityUI.Runtime 0x06000076..7a) and HLPipsElement
    // (0x0600007b) against the shipped x86-64 bodies. Each case owns an inactive
    // HideAndDontSave root (pip root, element prefab and any hand-built pips) that
    // TearDown destroys. The prefab's initialise events rename the instance through
    // persistent calls (set to EditorAndRuntime), which Instantiate retargets to
    // each clone, so the clone names show which event SetupPips raised. DestroyPips
    // uses Object.Destroy and is covered by OriginalHLPipsPlayTests.
    [TestFixture]
    public sealed class OriginalHLPipsTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags Declared = Any | BindingFlags.Static | BindingFlags.DeclaredOnly;

        readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void DestroyOwnedRoots()
        {
            foreach (GameObject root in roots)
                if (root != null) Object.DestroyImmediate(root);
            roots.Clear();
        }

        sealed class Rig
        {
            public GameObject Root;
            public HLPips Pips;
            public RectTransform PipRoot;
            public HLPipsElement Prefab;
            public readonly List<string> Log = new List<string>();
        }

        Rig Build(bool withPrefab = true)
        {
            var rig = new Rig { Root = new GameObject("ProjectLucid.HLPips.Owned", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave } };
            roots.Add(rig.Root);
            rig.Root.SetActive(false);
            rig.Pips = rig.Root.AddComponent<HLPips>();
            rig.PipRoot = (RectTransform)new GameObject("PipRoot", typeof(RectTransform)).transform;
            rig.PipRoot.gameObject.hideFlags = HideFlags.HideAndDontSave;
            rig.PipRoot.SetParent(rig.Root.transform, false);
            Set(rig.Pips, "m_pipRoot", rig.PipRoot);
            if (withPrefab)
            {
                rig.Prefab = NewElement(rig, "Element");
                RenameOn(rig.Prefab.OnInitialiseActive, rig.Prefab.gameObject, "active");
                RenameOn(rig.Prefab.OnInitialiseInactive, rig.Prefab.gameObject, "inactive");
                Set(rig.Pips, "m_pipsElementPrefab", rig.Prefab);
            }
            return rig;
        }

        static HLPipsElement NewElement(Rig rig, string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(rig.Root.transform, false);
            var element = go.AddComponent<HLPipsElement>();
            element.OnActivate = new UnityEvent();
            element.OnDeactivate = new UnityEvent();
            element.OnInitialiseActive = new UnityEvent();
            element.OnInitialiseInactive = new UnityEvent();
            return element;
        }

        static void RenameOn(UnityEvent unityEvent, GameObject target, string name)
        {
            MethodInfo setName = typeof(Object).GetProperty("name").GetSetMethod();
            UnityEventTools.AddStringPersistentListener(unityEvent,
                (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), target, setName), name);
            unityEvent.SetPersistentListenerState(unityEvent.GetPersistentEventCount() - 1, UnityEventCallState.EditorAndRuntime);
        }

        // Hand-built pips whose activate/deactivate listeners log "<index> <event>".
        static List<HLPipsElement> Logged(Rig rig, int count)
        {
            var pips = new List<HLPipsElement>();
            for (int i = 0; i < count; i++)
            {
                HLPipsElement pip = NewElement(rig, "Pip" + i);
                int index = i;
                pip.OnActivate.AddListener(() => rig.Log.Add(index + " activate"));
                pip.OnDeactivate.AddListener(() => rig.Log.Add(index + " deactivate"));
                pip.OnInitialiseActive.AddListener(() => rig.Log.Add(index + " initialise active"));
                pip.OnInitialiseInactive.AddListener(() => rig.Log.Add(index + " initialise inactive"));
                pips.Add(pip);
            }
            return pips;
        }

        static object Get(object target, string name) => typeof(HLPips).GetField(name, Any).GetValue(target);
        static void Set(object target, string name, object value) => typeof(HLPips).GetField(name, Any).SetValue(target, value);
        static List<HLPipsElement> PipList(HLPips pips) => (List<HLPipsElement>)Get(pips, "m_pips");

        static void SetState(HLPips pips, int count, int current, int previous)
        {
            Set(pips, "m_pipCount", count);
            Set(pips, "m_currentPipIndex", current);
            Set(pips, "m_previousPipIndex", previous);
        }

        static int[] State(HLPips pips) =>
            new[] { (int)Get(pips, "m_pipCount"), (int)Get(pips, "m_currentPipIndex"), (int)Get(pips, "m_previousPipIndex") };

        static string[] PipNames(Rig rig) =>
            Enumerable.Range(0, rig.PipRoot.childCount).Select(i => rig.PipRoot.GetChild(i).name).ToArray();

        // dump.cs TypeDefIndex 11838..11839.
        [Test]
        public void DeclarationsKeepTheDumpedFieldsAttributesAndMembers()
        {
            Type type = typeof(HLPips);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.FullName, Is.EqualTo("HLPips"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(type.IsSealed, Is.True);
            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "m_pipRoot", "m_pipsElementPrefab", "InvalidPipIndex", "m_pipCount", "m_currentPipIndex", "m_previousPipIndex", "m_pips",
            }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[]
            {
                typeof(RectTransform), typeof(HLPipsElement), typeof(int), typeof(int), typeof(int), typeof(int), typeof(List<HLPipsElement>),
            }));
            Assert.That(fields.All(f => f.IsPrivate), Is.True);
            Assert.That(fields[2].IsLiteral && (int)fields[2].GetRawConstantValue() == -1, Is.True);
            Assert.That(fields[0].GetCustomAttributesData().Select(a => a.AttributeType).ToArray(),
                Is.EqualTo(new[] { typeof(SerializeField), typeof(TooltipAttribute) }));
            Assert.That(fields[1].GetCustomAttributesData().Select(a => a.AttributeType).ToArray(),
                Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }));
            Assert.That(fields[0].GetCustomAttribute<TooltipAttribute>().tooltip, Is.EqualTo("Root to spawn pips under."));
            Assert.That(fields[1].GetCustomAttribute<TooltipAttribute>().tooltip, Is.EqualTo("Pip element prefab."));
            Assert.That(fields.Skip(2).Any(f => f.GetCustomAttributesData().Count > 0), Is.False);
            Assert.That(type.GetMethods(Declared).Select(m => m.Name).ToArray(),
                Is.EquivalentTo(new[] { "SetupPips", "DestroyPips", "Next", "Previous" }));
            ParameterInfo[] setup = type.GetMethod("SetupPips").GetParameters();
            Assert.That(setup.Select(p => p.Name).ToArray(), Is.EqualTo(new[] { "pipCount", "activePipIndex" }));
            Assert.That(setup[1].DefaultValue, Is.EqualTo(0));

            Type element = typeof(HLPipsElement);
            Assert.That(element.FullName, Is.EqualTo("HLPipsElement"));
            Assert.That(element.IsSealed && element.BaseType == typeof(MonoBehaviour), Is.True);
            FieldInfo[] events = element.GetFields(Declared);
            Assert.That(events.Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "OnActivate", "OnDeactivate", "OnInitialiseActive", "OnInitialiseInactive" }));
            Assert.That(events.All(f => f.IsPublic && f.FieldType == typeof(UnityEvent) && f.GetCustomAttributesData().Count == 0), Is.True);
            Assert.That(element.GetMethods(Declared), Is.Empty);
        }

        // 0x1B3BE10: m_previousPipIndex = -1 and a new list; the other ints stay 0.
        [Test]
        public void ConstructorStartsWithNoPreviousPipAndAnEmptyList()
        {
            Rig rig = Build(false);
            Assert.That(State(rig.Pips), Is.EqualTo(new[] { 0, 0, -1 }));
            Assert.That(PipList(rig.Pips), Is.Not.Null.And.Empty);
        }

        // 0x1B3BAC0: one instance per pip under m_pipRoot, OnInitialiseActive
        // (field +0x30) only for the active index and OnInitialiseInactive (+0x38)
        // otherwise, then m_currentPipIndex; m_previousPipIndex is not written.
        [Test]
        public void SetupPipsInstantiatesUnderTheRootAndInitialisesOnlyTheActivePip()
        {
            Rig rig = Build();
            SetState(rig.Pips, 9, 9, 7);

            rig.Pips.SetupPips(3, 1);

            Assert.That(PipNames(rig), Is.EqualTo(new[] { "inactive", "active", "inactive" }));
            Assert.That(PipList(rig.Pips).Select(p => p.transform).ToArray(),
                Is.EqualTo(Enumerable.Range(0, 3).Select(i => rig.PipRoot.GetChild(i)).ToArray()));
            Assert.That(State(rig.Pips), Is.EqualTo(new[] { 3, 1, 7 }));
            Assert.That(rig.Prefab.name, Is.EqualTo("Element"));
        }

        // The pip count is replaced but earlier pips are neither destroyed nor
        // removed from the list.
        [Test]
        public void SetupPipsAppendsToThePipsOfAnEarlierSetup()
        {
            Rig rig = Build();
            rig.Pips.SetupPips(2, 0);

            rig.Pips.SetupPips(1);

            Assert.That(PipNames(rig), Is.EqualTo(new[] { "active", "inactive", "active" }));
            Assert.That(PipList(rig.Pips).Count, Is.EqualTo(3));
            Assert.That(State(rig.Pips), Is.EqualTo(new[] { 1, 0, -1 }));
        }

        // The active index is not validated: no pip is initialised active and the
        // index is stored as given.
        [Test]
        public void SetupPipsWithAnOutOfRangeActiveIndexInitialisesEveryPipInactive()
        {
            Rig rig = Build();

            rig.Pips.SetupPips(3, 5);

            Assert.That(PipNames(rig), Is.EqualTo(new[] { "inactive", "inactive", "inactive" }));
            Assert.That(State(rig.Pips), Is.EqualTo(new[] { 3, 5, -1 }));
        }

        // Without a prefab (0x1b3bb34) the method returns before writing the count.
        [Test]
        public void SetupPipsWithoutAPrefabChangesNothing()
        {
            Rig rig = Build(false);
            SetState(rig.Pips, 4, 2, 1);

            rig.Pips.SetupPips(3, 0);

            Assert.That(rig.PipRoot.childCount, Is.EqualTo(0));
            Assert.That(PipList(rig.Pips), Is.Empty);
            Assert.That(State(rig.Pips), Is.EqualTo(new[] { 4, 2, 1 }));
        }

        // 0x1B3BD10: OnDeactivate (+0x28) on the old pip, then OnActivate (+0x20)
        // on the new one; at m_pipCount - 1 nothing happens.
        [Test]
        public void NextDeactivatesTheCurrentPipThenActivatesTheNextUntilTheLast()
        {
            Rig rig = Build(false);
            PipList(rig.Pips).AddRange(Logged(rig, 3));
            SetState(rig.Pips, 3, 1, -1);

            rig.Pips.Next();
            Assert.That(rig.Log, Is.EqualTo(new[] { "1 deactivate", "2 activate" }));
            Assert.That(State(rig.Pips), Is.EqualTo(new[] { 3, 2, 1 }));

            rig.Pips.Next();
            Assert.That(rig.Log.Count, Is.EqualTo(2));
            Assert.That(State(rig.Pips), Is.EqualTo(new[] { 3, 2, 1 }));
        }

        // 0x1B3BD90: the mirror of Next, stopping at index 0.
        [Test]
        public void PreviousDeactivatesTheCurrentPipThenActivatesThePreviousUntilTheFirst()
        {
            Rig rig = Build(false);
            PipList(rig.Pips).AddRange(Logged(rig, 3));
            SetState(rig.Pips, 3, 1, -1);

            rig.Pips.Previous();
            Assert.That(rig.Log, Is.EqualTo(new[] { "1 deactivate", "0 activate" }));
            Assert.That(State(rig.Pips), Is.EqualTo(new[] { 3, 0, 1 }));

            rig.Pips.Previous();
            Assert.That(rig.Log.Count, Is.EqualTo(2));
            Assert.That(State(rig.Pips), Is.EqualTo(new[] { 3, 0, 1 }));
        }

        // The end test uses m_pipCount (0x1b3bd36), not the list size.
        [Test]
        public void NextStopsAtThePipCountEvenWithMorePipsInTheList()
        {
            Rig rig = Build(false);
            PipList(rig.Pips).AddRange(Logged(rig, 3));
            SetState(rig.Pips, 2, 1, -1);

            rig.Pips.Next();

            Assert.That(rig.Log, Is.Empty);
            Assert.That(State(rig.Pips), Is.EqualTo(new[] { 2, 1, -1 }));
        }

        // With no pips the indices advance before List.get_Item throws.
        [Test]
        public void NextWithoutPipsAdvancesTheIndicesBeforeThrowing()
        {
            Rig rig = Build(false);

            Assert.Throws<ArgumentOutOfRangeException>(() => rig.Pips.Next());
            Assert.That(State(rig.Pips), Is.EqualTo(new[] { 0, 1, 0 }));
        }
    }
}
