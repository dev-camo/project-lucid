using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.InOutTestSupport;

namespace ProjectLucid.Tests
{
    // Original HLUnityUI.Runtime Hardlight.AnimatableEvent (TypeDef 0x020000d7, dump.cs 12039) against
    // the x86-64 bodies 0x1B6B2D0..0x1B6B510. Each case owns an inactive host and its events; the
    // duplicate-name report is read from an owned HLUnityCore error log.
    public sealed class OriginalAnimatableEventTests
    {
        static NamedUnityEvent Named(Owned owned, string name, string label) =>
            new NamedUnityEvent { m_eventName = name, m_event = owned.Event(label) };

        static AnimatableEvent Create(Owned owned, string hostName, params NamedUnityEvent[] events)
        {
            AnimatableEvent component = owned.Host(hostName, false).AddComponent<AnimatableEvent>();
            Set(component, "m_namedEvents", events);
            return component;
        }

        static Dictionary<string, UnityEvent> Table(AnimatableEvent component) =>
            Get<Dictionary<string, UnityEvent>>(component, "m_namedEventDictionary");

        // dump.cs 12039: sealed, ArrayBoundsChecks then NullChecks, AddComponentMenu with the menu path
        // only; the serialized array then the private dictionary; Awake and PopulateDictionary private.
        [Test]
        public void AnimatableEventRetainsWholeDeclaration()
        {
            Type type = typeof(AnimatableEvent);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.Namespace, Is.EqualTo("Hardlight"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That((int)type.Attributes, Is.EqualTo(0x100101));
            OriginalHLUIAnimationInOutTests.CheckOptions(type, 2, 1);
            CustomAttributeData[] attributes = type.GetCustomAttributesData().ToArray();
            Assert.That(attributes.Length, Is.EqualTo(3));
            Assert.That(attributes[2].AttributeType, Is.EqualTo(typeof(AddComponentMenu)));
            Assert.That(attributes[2].ConstructorArguments.Select(a => a.Value).ToArray(), Is.EqualTo(new object[] { "Hardlight UI/Events/Animatable Event" }));

            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "m_namedEvents", "m_namedEventDictionary" }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[] { typeof(NamedUnityEvent[]), typeof(Dictionary<string, UnityEvent>) }));
            Assert.That(fields.All(f => f.IsPrivate), Is.True);
            Assert.That(fields[0].GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(SerializeField) }));
            Assert.That(fields[1].GetCustomAttributesData(), Is.Empty);

            MethodInfo[] methods = type.GetMethods(Declared);
            Assert.That(methods.Select(m => m.Name).ToArray(), Is.EqualTo(new[] { "Awake", "PopulateDictionary", "TriggerEvent" }));
            Assert.That(methods.Select(m => (int)m.Attributes).ToArray(), Is.EqualTo(new[] { 0x81, 0x81, 0x86 }));
            Assert.That(methods[2].GetParameters().Single().Name, Is.EqualTo("eventName"));
            Assert.That(methods.SelectMany(m => m.GetCustomAttributesData()), Is.Empty);
            Assert.That(type.GetNestedTypes(Declared), Is.Empty);
        }

        // .ctor 0x060005d0 @0x1B6B4A0: a new empty dictionary is stored before MonoBehaviour..ctor.
        [Test]
        public void ConstructionCreatesAnEmptyDictionary()
        {
            using (var owned = new Owned())
            {
                AnimatableEvent component = Create(owned, "AnimatableEvent-ctor");
                Dictionary<string, UnityEvent> table = Table(component);
                Assert.That(table, Is.Not.Null);
                Assert.That(table, Is.Empty);
            }
        }

        // Awake 0x060005cd tail-calls PopulateDictionary 0x060005ce: Clear, then for each element in
        // order ContainsKey(name) ? LogError(gameObject name + literal + name) : Add(name, event).
        // The first event of a name wins; the duplicate is reported once per extra occurrence.
        [Test]
        public void AwakeMapsEachNameToItsFirstEventAndReportsDuplicates()
        {
            using (var log = new OwnedCoreErrorLog())
            using (var owned = new Owned())
            {
                AnimatableEvent component = Create(owned, "AnimatableEvent-awake",
                    Named(owned, "Open", "open-1"), Named(owned, "Close", "close"), Named(owned, "Open", "open-2"), Named(owned, "Open", "open-3"));
                Call(component, "Awake");
                Assert.That(Table(component).Keys.ToArray(), Is.EqualTo(new[] { "Open", "Close" }));
                Assert.That(log.Errors, Is.EqualTo(new[]
                {
                    "AnimatableEvent-awake:: AnimatableEvent Contains more than 1 event named: Open:0",
                    "AnimatableEvent-awake:: AnimatableEvent Contains more than 1 event named: Open:0",
                }));
                component.TriggerEvent("Open");
                component.TriggerEvent("Close");
                Assert.That(owned.Log, Is.EqualTo(new[] { "open-1", "close" }));
            }
        }

        // 0x1B6B34B: the dictionary is cleared first, so repopulating from a new array forgets names
        // that are no longer present instead of reporting them as duplicates.
        [Test]
        public void RepopulatingClearsThePreviousNames()
        {
            using (var log = new OwnedCoreErrorLog())
            using (var owned = new Owned())
            {
                AnimatableEvent component = Create(owned, "AnimatableEvent-repopulate", Named(owned, "Open", "open-old"));
                Call(component, "Awake");
                Set(component, "m_namedEvents", new[] { Named(owned, "Open", "open-new"), Named(owned, "Shut", "shut") });
                Call(component, "PopulateDictionary");
                Assert.That(log.Errors, Is.Empty);
                component.TriggerEvent("Open");
                Assert.That(owned.Log, Is.EqualTo(new[] { "open-new" }));
            }
        }

        // TriggerEvent 0x060005cf @0x1B6B420: string.IsNullOrEmpty returns first (so an event named ""
        // is unreachable and null never reaches the dictionary); otherwise TryGetValue and Invoke.
        // Lookup is ordinal and case-sensitive.
        [Test]
        public void TriggerEventIgnoresNullEmptyAndUnknownNames()
        {
            using (var owned = new Owned())
            {
                AnimatableEvent component = Create(owned, "AnimatableEvent-trigger", Named(owned, "", "empty"), Named(owned, "Go", "go"));
                Call(component, "Awake");
                Assert.That(Table(component).ContainsKey(""), Is.True);
                component.TriggerEvent(null);
                component.TriggerEvent("");
                component.TriggerEvent("go");
                component.TriggerEvent("Missing");
                Assert.That(owned.Log, Is.Empty);
                component.TriggerEvent("Go");
                component.TriggerEvent("Go");
                Assert.That(owned.Log, Is.EqualTo(new[] { "go", "go" }));
            }
        }

        // ContainsKey(null) at 0x1B6B3B6 throws from the dictionary; nothing guards a null name.
        [Test]
        public void ANullEventNameFaultsThePopulation()
        {
            using (var owned = new Owned())
            {
                AnimatableEvent component = Create(owned, "AnimatableEvent-null", Named(owned, "First", "first"), Named(owned, null, "null"));
                Assert.Throws<ArgumentNullException>(() => Call(component, "Awake"));
                Assert.That(Table(component).Keys.ToArray(), Is.EqualTo(new[] { "First" }));
            }
        }
    }
}
