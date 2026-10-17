using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of Hardlight.HLExclusiveStateGroup (HLUnityUI.Runtime
    // 0x06000633..3d) and its dependency HLStateSwitcher (0x060006f4..f8),
    // against the shipped x86-64 bodies. Each case owns its inactive,
    // hidden hosts and listeners; HLOutput errors go to an owned capture.
    [TestFixture]
    public sealed class OriginalHLExclusiveStateGroupTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags Declared = Any | BindingFlags.Static | BindingFlags.DeclaredOnly;
        static readonly Type ElementType = typeof(HLExclusiveStateGroup).GetNestedType("StateGroupElement", BindingFlags.NonPublic);

        sealed class Rig : IDisposable
        {
            readonly List<GameObject> hosts = new List<GameObject>();
            public readonly List<string> Events = new List<string>();
            public readonly HLStateSwitcher[] Switchers;
            public readonly object[] Elements;
            public readonly HLExclusiveStateGroup Group;

            // Hosts stay inactive so no Unity message runs on its own.
            public Rig(params bool[] enabled)
            {
                try
                {
                    Switchers = new HLStateSwitcher[enabled.Length];
                    Elements = new object[enabled.Length];
                    for (int i = 0; i < enabled.Length; i++)
                    {
                        Switchers[i] = NewSwitcher(i.ToString(), enabled[i]);
                        Elements[i] = NewElement(Switchers[i]);
                    }
                    Group = NewHost("HLExclusiveStateGroup fixture").AddComponent<HLExclusiveStateGroup>();
                    Array array = Array.CreateInstance(ElementType, enabled.Length);
                    for (int i = 0; i < enabled.Length; i++) array.SetValue(Elements[i], i);
                    Set(Group, "m_stateSwitchers", array);
                    Set(Group, "m_defaultStateSwitcher", NewElement(null));
                }
                catch { Dispose(); throw; }
            }

            GameObject NewHost(string name)
            {
                var host = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
                hosts.Add(host);
                host.SetActive(false);
                return host;
            }

            public HLStateSwitcher NewSwitcher(string name, bool enabled)
            {
                var switcher = NewHost("HLStateSwitcher fixture " + name).AddComponent<HLStateSwitcher>();
                var onEnabled = new UnityEvent();
                var onDisabled = new UnityEvent();
                onEnabled.AddListener(() => Events.Add(name + "+"));
                onDisabled.AddListener(() => Events.Add(name + "-"));
                Set(switcher, "m_enabled", enabled);
                Set(switcher, "m_onEnabled", onEnabled);
                Set(switcher, "m_onDisabled", onDisabled);
                return switcher;
            }

            public object Default => Get(Group, "m_defaultStateSwitcher");
            public HLStateSwitcher DefaultSwitcher { set => Set(Default, "StateSwitcher", value); }

            public string States()
            {
                var text = "";
                foreach (HLStateSwitcher switcher in Switchers) text += switcher.Enabled ? "1" : "0";
                return text;
            }

            public void Dispose()
            {
                foreach (GameObject host in hosts) if (host != null) Object.DestroyImmediate(host);
            }
        }

        static object NewElement(HLStateSwitcher switcher)
        {
            object element = Activator.CreateInstance(ElementType);
            Set(element, "StateSwitcher", switcher);
            return element;
        }

        static object Get(object target, string name) => target.GetType().GetField(name, Any).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Any).SetValue(target, value);
        static int Key(object element) => (int)ElementType.GetProperty("Key", Any).GetValue(element);

        static object Call(object target, string name, params object[] args)
        {
            try { return target.GetType().GetMethod(name, Any).Invoke(target, args); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        // Declarations from dump.cs TypeDefIndex 12050/12051/12071: type
        // attribute order, Serializable on the private nested element, and
        // field order, visibility, types and SerializeField placement.
        [Test]
        public void DeclarationsKeepTheShippedAttributesAndFields()
        {
            IList<CustomAttributeData> switcher = typeof(HLStateSwitcher).GetCustomAttributesData();
            CollectionAssert.AreEqual(new[] { typeof(Il2CppSetOptionAttribute), typeof(ExecuteInEditMode), typeof(Il2CppSetOptionAttribute) },
                switcher.Select(a => a.AttributeType));
            RequireOption(switcher[0], Option.ArrayBoundsChecks);
            Assert.IsEmpty(switcher[1].ConstructorArguments);
            Assert.IsEmpty(switcher[1].NamedArguments);
            RequireOption(switcher[2], Option.NullChecks);
            Assert.IsEmpty(typeof(HLExclusiveStateGroup).GetCustomAttributesData());

            // Serializable is a TypeDef flag that Mono projects as one attribute.
            Assert.IsTrue(ElementType.IsNestedPrivate);
            Assert.IsFalse(ElementType.IsSealed);
            CollectionAssert.AreEqual(new[] { typeof(SerializableAttribute) }, ElementType.GetCustomAttributesData().Select(a => a.AttributeType));
            PropertyInfo key = ElementType.GetProperty("Key", Any);
            Assert.IsTrue(key.GetGetMethod(true).IsPublic);
            Assert.IsTrue(key.GetSetMethod(true).IsPrivate);

            CollectionAssert.AreEqual(new[]
            {
                "m_stateSwitchers|Private|StateGroupElement[]|SerializeField",
                "m_defaultStateSwitcher|Private|StateGroupElement|SerializeField",
            }, Fields(typeof(HLExclusiveStateGroup)));
            CollectionAssert.AreEqual(new[]
            {
                "StateSwitcher|Public|HLStateSwitcher|",
                "m_key|Private|Int32|",
                "m_keysSet|Private|Boolean|",
            }, Fields(ElementType));
            CollectionAssert.AreEqual(new[]
            {
                "m_enabled|Private|Boolean|SerializeField",
                "m_onEnabled|Private|UnityEvent|SerializeField",
                "m_onDisabled|Private|UnityEvent|SerializeField",
            }, Fields(typeof(HLStateSwitcher)));
        }

        static string[] Fields(Type type) => type.GetFields(Declared).OrderBy(f => f.MetadataToken)
            .Select(f => f.Name + "|" + f.Attributes + "|" + f.FieldType.Name + "|"
                + string.Join(",", f.GetCustomAttributesData().Select(a => a.AttributeType.Name)))
            .ToArray();

        static void RequireOption(CustomAttributeData attribute, Option option)
        {
            Assert.AreEqual(typeof(Il2CppSetOptionAttribute), attribute.AttributeType);
            Assert.IsEmpty(attribute.NamedArguments);
            Assert.AreEqual(2, attribute.ConstructorArguments.Count);
            Assert.AreEqual(typeof(Option), attribute.ConstructorArguments[0].ArgumentType);
            Assert.AreEqual((int)option, attribute.ConstructorArguments[0].Value);
            Assert.AreEqual(typeof(bool), attribute.ConstructorArguments[1].ArgumentType);
            Assert.AreEqual(false, attribute.ConstructorArguments[1].Value);
        }

        // set_Enabled 0x1b6fd10 compares before storing, so only a change
        // raises an event; Start 0x1b73940 replays the stored state.
        // Mutations caught: unconditional invoke, swapped events, Start no-op.
        [Test]
        public void SwitcherEnabledRaisesOnlyOnChangeAndStartReplaysTheState()
        {
            using (var rig = new Rig(false))
            {
                HLStateSwitcher switcher = rig.Switchers[0];
                switcher.Enabled = false;
                Assert.IsEmpty(rig.Events, "no change, no event");
                switcher.Enabled = true;
                switcher.Enabled = true;
                switcher.Enabled = false;
                CollectionAssert.AreEqual(new[] { "0+", "0-" }, rig.Events);
                Assert.IsFalse(switcher.Enabled);

                rig.Events.Clear();
                Call(switcher, "Start");
                Set(switcher, "m_enabled", true);
                Call(switcher, "Start");
                CollectionAssert.AreEqual(new[] { "0-", "0+" }, rig.Events);
            }
        }

        // InvokeEvents 0x1b73910 (and its inlined copies) reloads m_onDisabled
        // (+0x30) when enabled and m_onEnabled (+0x28) is null; with both null
        // nothing is invoked. Mutation caught: (enabled ? on : off)?.Invoke().
        [Test]
        public void SwitcherEnabledWithoutOnEnabledFallsThroughToOnDisabled()
        {
            using (var rig = new Rig(false))
            {
                HLStateSwitcher switcher = rig.Switchers[0];
                Set(switcher, "m_onEnabled", null);
                switcher.Enabled = true;
                Call(switcher, "Start");
                CollectionAssert.AreEqual(new[] { "0-", "0-" }, rig.Events);

                Set(switcher, "m_onDisabled", null);
                switcher.Enabled = false;
                switcher.Enabled = true;
                Assert.IsTrue(switcher.Enabled);
                Assert.AreEqual(2, rig.Events.Count, "both events null invokes nothing");
            }
        }

        // get_Key 0x1b6fce0 returns the switcher's instance ID until SetKey
        // 0x1b6fc30 stores a key; the private setter 0x1b6fd60 is a bare ret.
        [Test]
        public void ElementKeyIsTheInstanceIdUntilSetKeyAndTheSetterIsEmpty()
        {
            using (var rig = new Rig(false))
            {
                object element = rig.Elements[0];
                Assert.AreEqual(rig.Switchers[0].GetInstanceID(), Key(element));
                ElementType.GetProperty("Key", Any).SetValue(element, 99);
                Assert.AreEqual(rig.Switchers[0].GetInstanceID(), Key(element), "setter stores nothing");
                Assert.IsFalse((bool)Get(element, "m_keysSet"));

                Call(element, "SetKey", 7);
                Assert.AreEqual(7, Key(element));
                Assert.AreEqual(7, (int)Get(element, "m_key"));
                Assert.IsTrue((bool)Get(element, "m_keysSet"));
            }
        }

        // ActivateStateGroupElement 0x1b6f910 disables each non-match in array
        // order, then enables the match; already-disabled switchers raise
        // nothing. All three public entry points route to it, and
        // Action_Activate(HLStateSwitcher) 0x1b6f870 ignores null and
        // destroyed switchers through UnityEngine.Object inequality.
        [Test]
        public void ActivateDisablesOthersInOrderThenEnablesTheMatch()
        {
            using (var rig = new Rig(true, false, true))
            {
                rig.Group.Activate(rig.Switchers[1].GetInstanceID());
                Assert.AreEqual("010", rig.States());
                CollectionAssert.AreEqual(new[] { "0-", "2-", "1+" }, rig.Events);

                rig.Events.Clear();
                rig.Group.Action_Activate(rig.Switchers[2].GetInstanceID());
                Assert.AreEqual("001", rig.States());
                CollectionAssert.AreEqual(new[] { "1-", "2+" }, rig.Events);

                rig.Events.Clear();
                rig.Group.Action_Activate(rig.Switchers[0]);
                Assert.AreEqual("100", rig.States());
                CollectionAssert.AreEqual(new[] { "2-", "0+" }, rig.Events);

                rig.Events.Clear();
                rig.Group.Action_Activate((HLStateSwitcher)null);
                HLStateSwitcher destroyed = rig.NewSwitcher("gone", false);
                Object.DestroyImmediate(destroyed.gameObject);
                rig.Group.Action_Activate(destroyed);
                Assert.AreEqual("100", rig.States());
                Assert.IsEmpty(rig.Events);
            }
        }

        // Matching elements are never disabled and only the last match is
        // enabled (r15 is overwritten per match). With no match every
        // switcher is disabled before index -1 throws (jae to the
        // index-out-of-range raise). Mutations caught: first-match break,
        // disabling earlier matches, an early return on no match.
        [Test]
        public void DuplicateKeysEnableTheLastMatchAndNoMatchThrowsAfterDisabling()
        {
            using (var rig = new Rig(true, false, true))
            {
                rig.DefaultSwitcher = rig.Switchers[2];
                rig.Group.SetKeys(new[] { 5, 5, 6 });
                rig.Group.Activate(5);
                Assert.AreEqual("110", rig.States());
                CollectionAssert.AreEqual(new[] { "2-", "1+" }, rig.Events);

                rig.Events.Clear();
                Assert.Throws<IndexOutOfRangeException>(() => rig.Group.Activate(rig.Switchers[2].GetInstanceID()),
                    "keyed elements no longer match their instance IDs");
                Assert.AreEqual("000", rig.States());
                CollectionAssert.AreEqual(new[] { "0-", "1-" }, rig.Events);
            }

            // An earlier match that starts disabled stays disabled: 0x1b6f940
            // only records its index. Mutation caught: enabling every match.
            using (var rig = new Rig(false, false, true))
            {
                rig.DefaultSwitcher = rig.Switchers[2];
                rig.Group.SetKeys(new[] { 5, 5, 6 });
                rig.Group.Activate(5);
                Assert.AreEqual("010", rig.States());
                CollectionAssert.AreEqual(new[] { "2-", "1+" }, rig.Events);
            }

            using (var empty = new Rig())
            {
                Assert.Throws<IndexOutOfRangeException>(() => empty.Group.Activate(0));
            }
        }

        // SetKeys 0x1b6fa60 pairs keys with elements by index and copies the
        // key to the separate default element whose switcher has the same
        // instance ID. OnEnable 0x1b6fc40 activates by the default's Key and
        // does nothing when the default has no switcher.
        [Test]
        public void SetKeysAlsoKeysTheDefaultElementThatOnEnableActivates()
        {
            using (var rig = new Rig(true, false, false))
            {
                Call(rig.Group, "OnEnable");
                Assert.AreEqual("100", rig.States(), "no default switcher: no activation");
                Assert.IsEmpty(rig.Events);

                rig.DefaultSwitcher = rig.Switchers[1];
                Call(rig.Group, "OnEnable");
                Assert.AreEqual("010", rig.States(), "default keyed by instance ID");

                rig.Group.SetKeys(new[] { 10, 20, 30 });
                CollectionAssert.AreEqual(new[] { 10, 20, 30 }, Array.ConvertAll(rig.Elements, Key));
                Assert.AreEqual(20, Key(rig.Default));

                rig.Events.Clear();
                rig.Group.Activate(30);
                Call(rig.Group, "OnEnable");
                Assert.AreEqual("010", rig.States());
                CollectionAssert.AreEqual(new[] { "1-", "2+", "2-", "1+" }, rig.Events);
            }
        }

        // SetKeys logs the native literal once per element past the keys and
        // once when keys remain after the loop; the keyed prefix is still set.
        // Equal lengths log nothing.
        [Test]
        public void SetKeysLogsEachMissingKeyAndSurplusKeysOnce()
        {
            using (var capture = new OwnedHLOutputErrorCapture())
            {
                using (var rig = new Rig(false, false, false))
                {
                    rig.DefaultSwitcher = rig.Switchers[0];
                    rig.Group.SetKeys(new[] { 4 });
                    const string missing = "Not enough keys provided, more state switchers exist than keys|0";
                    CollectionAssert.AreEqual(new[] { missing, missing }, capture.Errors);
                    Assert.AreEqual(4, Key(rig.Elements[0]));
                    Assert.AreEqual(rig.Switchers[1].GetInstanceID(), Key(rig.Elements[1]));

                    capture.Errors.Clear();
                    rig.Group.SetKeys(new[] { 1, 2, 3 });
                    Assert.IsEmpty(capture.Errors);
                }

                using (var rig = new Rig(false))
                {
                    rig.DefaultSwitcher = rig.Switchers[0];
                    rig.Group.SetKeys(new[] { 8, 9, 10 });
                    CollectionAssert.AreEqual(
                        new[] { "Not enough state switchers provided, more keys exist than state switchers|0" }, capture.Errors);
                    Assert.AreEqual(8, Key(rig.Elements[0]));
                }

                capture.Errors.Clear();
                using (var empty = new Rig())
                {
                    empty.Group.SetKeys(new int[0]);
                    Assert.IsEmpty(capture.Errors);
                    empty.Group.SetKeys(new[] { 1 });
                    Assert.AreEqual(1, capture.Errors.Count, "an empty group still reports surplus keys");
                }
            }
        }

        // SetKeys dereferences keys even for an empty group (the native body
        // null-checks it before the loop) and reads the default element's
        // switcher instance ID for every keyed index, so a default without a
        // switcher throws before the first key is stored.
        [Test]
        public void SetKeysThrowsForNullKeysAndForADefaultWithoutASwitcher()
        {
            using (var empty = new Rig())
            {
                Assert.Throws<NullReferenceException>(() => empty.Group.SetKeys(null));
            }

            using (var rig = new Rig(false, false))
            {
                Assert.Throws<NullReferenceException>(() => rig.Group.SetKeys(new[] { 1, 2 }));
                Assert.IsFalse((bool)Get(rig.Elements[0], "m_keysSet"), "nothing stored before the throw");
            }
        }
    }
}
