using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Original HLUnityCore.Runtime PayloadPropertyBuilder 0x020001ea, checked against the
    // 1.10.1 x86-64 bodies. Every fixture is owned by the test that builds it; the builder
    // keeps no static state besides the IPayload<> definition it compares against, and the
    // CoroutineUtils host is never touched: the private iterators are driven by hand and
    // seeded pending handles are null, which StopUtilCoroutine ignores.
    public sealed class OriginalPayloadPropertyBuilderTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public sealed class CountPayload : IPayload<int>
        {
            public readonly List<int> Received = new List<int>();
            public string GetIdentifier() => "count";
            public void UpdateData(int newData) => Received.Add(newData);
        }

        public sealed class NamePayload : IPayload<int>
        {
            public readonly List<int> Received = new List<int>();
            public string GetIdentifier() => "name";
            public void UpdateData(int newData) => Received.Add(newData);
        }

        public sealed class EntryPayload : IPayload<int>
        {
            public readonly List<int> Received = new List<int>();
            public string GetIdentifier() => "entry";
            public void UpdateData(int newData) => Received.Add(newData);
        }

        // Has the payload method names but no IPayload<> interface.
        public sealed class LookalikePayload
        {
            public string GetIdentifier() => "lookalike";
            public void UpdateData(int newData) { }
        }

        public abstract class GenericEntryBase<TTag> : IPayload<int>
        {
            public readonly List<int> Received = new List<int>();
            public abstract string GetIdentifier();
            public void UpdateData(int newData) => Received.Add(newData);
        }

        public sealed class TaggedEntry : GenericEntryBase<string>
        {
            public override string GetIdentifier() => "tagged";
        }

        private sealed class MixedParent
        {
            public readonly NamePayload PresetName = new NamePayload();

            public MixedParent() { Name = PresetName; }

            public CountPayload Count { get; set; }
            public NamePayload Name { get; set; }
            public List<EntryPayload> Entries { get; set; }
            public HashSet<EntryPayload> EntrySet { get; set; }
            public LookalikePayload Lookalike { get; set; }
            public string Plain { get; set; }
        }

        private sealed class DuplicateParent
        {
            public CountPayload First { get; set; }
            public CountPayload Second { get; set; }
        }

        private sealed class TaggedParent
        {
            public List<TaggedEntry> Tagged { get; set; } = new List<TaggedEntry>();
            public List<EntryPayload> Entries { get; set; } = new List<EntryPayload>();
        }

        private sealed class RecordingReader : IPayloadReader<KeyValuePair<string, int>, int>
        {
            public readonly List<string> Log = new List<string>();
            public Type PayloadType;

            public Type GetPayloadType()
            {
                Log.Add("type");
                return PayloadType;
            }

            public bool GetKeyValueFromData(KeyValuePair<string, int> input, out string identifier, out int output)
            {
                Log.Add("read:" + input.Key);
                identifier = input.Key;
                output = input.Value;
                return !input.Key.StartsWith("skip", StringComparison.Ordinal);
            }
        }

        private static IEnumerator StartIterator(PayloadPropertyBuilder builder, string name, params object[] arguments)
        {
            MethodInfo method = typeof(PayloadPropertyBuilder).GetMethod(name, Private)
                .MakeGenericMethod(typeof(KeyValuePair<string, int>), typeof(int));
            return (IEnumerator)method.Invoke(builder, arguments);
        }

        private readonly List<Dictionary<Type, Coroutine>> m_seededPendingUpdates = new List<Dictionary<Type, Coroutine>>();

        // Seeded handles are dropped so no builder finalizer later walks them off the main thread.
        [TearDown]
        public void ClearSeededPendingUpdates()
        {
            foreach (Dictionary<Type, Coroutine> pending in m_seededPendingUpdates) pending.Clear();
            m_seededPendingUpdates.Clear();
        }

        private Dictionary<Type, Coroutine> PendingUpdates(PayloadPropertyBuilder builder)
        {
            var pending = (Dictionary<Type, Coroutine>)typeof(PayloadPropertyBuilder)
                .GetField("m_pendingUpdates", Private).GetValue(builder);
            m_seededPendingUpdates.Add(pending);
            return pending;
        }

        private static KeyValuePair<string, int> Pair(string key, int value) => new KeyValuePair<string, int>(key, value);

        // 0x1b07726..0x1b07744 create and assign only a null payload property; 0x1b0771c gives a
        // collection's identifier a throwaway element, so the created list stays empty. HashSet<T>
        // lacks the non-generic "ICollection" (0x1b0766b) and is not itself a payload, so it is
        // skipped like the lookalike type whose interfaces miss IPayload<> (0x1b07db0).
        [Test]
        public void ConstructorCreatesOnlyMissingPayloadPropertiesAndLeavesCreatedCollectionsEmpty()
        {
            var parent = new MixedParent();
            new PayloadPropertyBuilder(parent, () => true);

            Assert.That(parent.Count, Is.Not.Null.And.TypeOf<CountPayload>());
            Assert.That(parent.Count.Received, Is.Empty);
            Assert.That(parent.Name, Is.SameAs(parent.PresetName), "an existing payload is kept");
            Assert.That(parent.Entries, Is.Not.Null.And.Empty);
            Assert.That(parent.EntrySet, Is.Null);
            Assert.That(parent.Lookalike, Is.Null);
            Assert.That(parent.Plain, Is.Null);
        }

        // 0xb257d9 polls the gate before anything else and 0xb2586b..0xb25872 yield null on false.
        // Afterwards the payload type is read once before enumeration (0xb25899), unknown and
        // rejected keys are dropped, every collection key appends a new element (0x985d5f), the
        // pending entry for that type alone is removed (0xb25ca8) and onCompleted runs once.
        [Test]
        public void PropertiesIteratorWaitsForTheGateThenAppliesEveryAcceptedEntryInOrder()
        {
            var parent = new MixedParent();
            bool allowed = false;
            int gateCalls = 0;
            var builder = new PayloadPropertyBuilder(parent, () => { gateCalls++; return allowed; });
            var reader = new RecordingReader { PayloadType = typeof(CountPayload) };
            Dictionary<Type, Coroutine> pending = PendingUpdates(builder);
            pending[typeof(CountPayload)] = null;
            pending[typeof(NamePayload)] = null;
            int completed = 0;
            var data = new[] { Pair("count", 1), Pair("entry", 2), Pair("missing", 3), Pair("skip-count", 4), Pair("entry", 5), Pair("count", 6), Pair("name", 7) };

            IEnumerator iterator = StartIterator(builder, "UpdatePropertiesWhenReady", data, reader, (Action)(() => completed++), false);
            Assert.That(iterator.MoveNext(), Is.True);
            Assert.That(iterator.Current, Is.Null);
            Assert.That(reader.Log, Is.Empty, "nothing is read while the gate is closed");
            Assert.That(completed, Is.Zero);

            allowed = true;
            Assert.That(iterator.MoveNext(), Is.False);
            Assert.That(gateCalls, Is.EqualTo(2));
            Assert.That(reader.Log, Is.EqualTo(new[] { "type", "read:count", "read:entry", "read:missing", "read:skip-count", "read:entry", "read:count", "read:name" }));
            Assert.That(parent.Count.Received, Is.EqualTo(new[] { 1, 6 }));
            Assert.That(parent.Name.Received, Is.EqualTo(new[] { 7 }));
            Assert.That(parent.Entries.Select(entry => entry.Received.Single()), Is.EqualTo(new[] { 2, 5 }));
            Assert.That(pending.Keys, Is.EquivalentTo(new[] { typeof(NamePayload) }));
            Assert.That(completed, Is.EqualTo(1));
        }

        // 0xb258a2..0xb258b1 clear collections of the reader's payload type only when asked, and
        // before any entry is applied; the per-entry update itself never clears (false at 0xb25987).
        [TestCase(true)]
        [TestCase(false)]
        public void PropertiesIteratorClearsMatchingCollectionsOnceBeforeAppending(bool clearCollectionTypes)
        {
            int[] expected = clearCollectionTypes ? new[] { 2, 5 } : new[] { 9, 2, 5 };
            var parent = new MixedParent();
            var builder = new PayloadPropertyBuilder(parent, () => true);
            var existing = new EntryPayload();
            existing.UpdateData(9);
            parent.Entries.Add(existing);
            var reader = new RecordingReader { PayloadType = typeof(EntryPayload) };

            IEnumerator iterator = StartIterator(builder, "UpdatePropertiesWhenReady", new[] { Pair("entry", 2), Pair("entry", 5) }, reader, null, clearCollectionTypes);
            Assert.That(iterator.MoveNext(), Is.False, "a null onCompleted is skipped");
            Assert.That(parent.Entries.Select(entry => entry.Received.Single()), Is.EqualTo(expected));
        }

        // 0xb261a2 reads the entry, 0x985d29 clears only when the flag is set, and the payload type
        // is read afterwards for the pending removal (0xb262e4 before 0xb262fb).
        [TestCase(true)]
        [TestCase(false)]
        public void SinglePropertyIteratorHonoursItsClearFlagAndReadsThePayloadTypeLast(bool clearIfPropertyIsCollection)
        {
            int[] expected = clearIfPropertyIsCollection ? new[] { 4 } : new[] { 9, 4 };
            var parent = new MixedParent();
            var builder = new PayloadPropertyBuilder(parent, () => true);
            var existing = new EntryPayload();
            existing.UpdateData(9);
            parent.Entries.Add(existing);
            var reader = new RecordingReader { PayloadType = typeof(EntryPayload) };
            Dictionary<Type, Coroutine> pending = PendingUpdates(builder);
            pending[typeof(EntryPayload)] = null;
            int completed = 0;

            IEnumerator iterator = StartIterator(builder, "UpdatePropertyWhenReady", Pair("entry", 4), reader, (Action)(() => completed++), clearIfPropertyIsCollection);
            Assert.That(iterator.MoveNext(), Is.False);
            Assert.That(reader.Log, Is.EqualTo(new[] { "read:entry", "type" }));
            Assert.That(parent.Entries.Select(entry => entry.Received.Single()), Is.EqualTo(expected));
            Assert.That(pending, Is.Empty);
            Assert.That(completed, Is.EqualTo(1));
        }

        // A rejected single entry is not applied, but removal and completion still follow.
        [Test]
        public void SinglePropertyIteratorStillCompletesWhenTheReaderRejectsTheEntry()
        {
            var parent = new MixedParent();
            var builder = new PayloadPropertyBuilder(parent, () => true);
            var reader = new RecordingReader { PayloadType = typeof(CountPayload) };
            Dictionary<Type, Coroutine> pending = PendingUpdates(builder);
            pending[typeof(CountPayload)] = null;
            int completed = 0;

            IEnumerator iterator = StartIterator(builder, "UpdatePropertyWhenReady", Pair("skip-count", 4), reader, (Action)(() => completed++), true);
            Assert.That(iterator.MoveNext(), Is.False);
            Assert.That(parent.Count.Received, Is.Empty);
            Assert.That(pending, Is.Empty);
            Assert.That(completed, Is.EqualTo(1));
        }

        // 0x1b07c20..0x1b07cb1 walk the element type's BaseType chain comparing generic
        // definitions, so an open base clears a derived element list but not an unrelated one.
        [Test]
        public void ClearCollectionsOfTypeMatchesOpenGenericBasesOfTheElementType()
        {
            var parent = new TaggedParent();
            var builder = new PayloadPropertyBuilder(parent, () => true);
            parent.Tagged.Add(new TaggedEntry());
            parent.Entries.Add(new EntryPayload());
            MethodInfo clear = typeof(PayloadPropertyBuilder).GetMethod("ClearCollectionsOfType", Private);

            clear.Invoke(builder, new object[] { typeof(GenericEntryBase<string>) });
            Assert.That(parent.Tagged, Has.Count.EqualTo(1), "a closed generic never equals a definition");

            clear.Invoke(builder, new object[] { typeof(GenericEntryBase<>) });
            Assert.That(parent.Tagged, Is.Empty);
            Assert.That(parent.Entries, Has.Count.EqualTo(1));
        }

        // 0x1b077c6: a later property with an identifier already seen is never registered.
        [Test]
        public void DuplicateIdentifierRoutesUpdatesToTheFirstRegisteredPropertyOnly()
        {
            var parent = new DuplicateParent();
            var builder = new PayloadPropertyBuilder(parent, () => true);
            Assert.That(parent.First, Is.Not.Null);
            Assert.That(parent.Second, Is.Not.Null, "both properties are still created");

            var reader = new RecordingReader { PayloadType = typeof(CountPayload) };
            IEnumerator iterator = StartIterator(builder, "UpdatePropertyWhenReady", Pair("count", 3), reader, null, true);
            Assert.That(iterator.MoveNext(), Is.False);

            CountPayload[] properties = typeof(DuplicateParent).GetProperties()
                .Select(property => (CountPayload)property.GetValue(parent)).ToArray();
            Assert.That(properties[0].Received, Is.EqualTo(new[] { 3 }));
            Assert.That(properties[1].Received, Is.Empty);
        }

        // 0x1b07ec0 stops a local copy of the handle and keeps the entry; 0x1b07910 stops every
        // handle and then clears the whole table.
        [Test]
        public void SingleCleanupKeepsThePendingEntryWhileFullCleanupClearsTheTable()
        {
            var builder = new PayloadPropertyBuilder(new MixedParent(), () => true);
            Dictionary<Type, Coroutine> pending = PendingUpdates(builder);
            pending[typeof(CountPayload)] = null;
            pending[typeof(NamePayload)] = null;

            typeof(PayloadPropertyBuilder).GetMethod("CleanupPendingUpdate", Private).Invoke(builder, new object[] { typeof(CountPayload) });
            Assert.That(pending.Keys, Is.EquivalentTo(new[] { typeof(CountPayload), typeof(NamePayload) }));

            typeof(PayloadPropertyBuilder).GetMethod("CleanupPendingUpdates", Private).Invoke(builder, null);
            Assert.That(pending, Is.Empty);
        }

        // Metadata 0x020001ed/0x020001ee: member order fixes the generated ordinals 15 and 16,
        // and each iterator hoists this plus its four parameters (0x0400067e..82, 0x04000685..89).
        // Field order is only compared by name set: Editor Debug compilation also hoists locals and
        // orders parameters differently from the shipped Release build.
        [Test]
        public void IteratorsKeepTheOriginalGeneratedNamesAndHoistedParameters()
        {
            string[] Hoisted(string name) => typeof(PayloadPropertyBuilder).GetNestedType(name, BindingFlags.NonPublic)
                .GetFields(BindingFlags.Instance | BindingFlags.Public).Select(field => field.Name).ToArray();

            Assert.That(Hoisted("<UpdatePropertiesWhenReady>d__15`2"), Is.EquivalentTo(new[]
                { "<>4__this", "payloadReader", "clearCollectionTypes", "payloadData", "onCompleted" }));
            Assert.That(Hoisted("<UpdatePropertyWhenReady>d__16`2"), Is.EquivalentTo(new[]
                { "<>4__this", "payloadReader", "payloadData", "clearIfPropertyIsCollection", "onCompleted" }));
        }
    }
}
