using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using Hardlight.JSON;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // Original HLUnityCore.Runtime PayloadJSON 0x020001e9, PayloadReaderJSON 0x020001ef and
    // PayloadPopulator 0x020001e6, checked against the 1.10.1 x86-64 bodies. JSON values are
    // built with their public constructors, never Spawn, so no ObjectPool state is touched;
    // populators are constructed but never asked to update, so no CoroutineUtils host is
    // created. Logged failures are read through an owned, inactive HLUnityCore.
    public sealed class OriginalPayloadJsonTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private sealed class ProbePayload : PayloadJSON
        {
            public override void UpdateData(IJsonObject data) { }

            public override string GetIdentifier() => "probe";

            public bool ReadBool(JSONHashtable data, string identifier, out bool result) => TryGetBool(data, identifier, out result);

            public bool ReadDouble(JSONHashtable data, string identifier, out double result) => TryGetDouble(data, identifier, out result);

            public bool ReadString(JSONHashtable data, string identifier, out string result) => TryGetString(data, identifier, out result);

            public bool ReadStringArray(JSONHashtable data, string identifier, ref List<string> result) => TryGetStringArray(data, identifier, ref result);
        }

        public sealed class CountPayload : IPayload<int>
        {
            public string GetIdentifier() => "count";

            public void UpdateData(int newData) { }
        }

        private sealed class GatedPopulator : PayloadPopulator
        {
            public bool Safe;

            public CountPayload Count { get; set; }

            protected override bool IsSafePointToUpdateProperties() => Safe;
        }

        private sealed class DefaultPopulator : PayloadPopulator
        {
            public CountPayload Count { get; set; }
        }

        private static JSONString Text(string value) => new JSONString { Value = value };

        private static JSONArray Entries(params IJsonObject[] entries)
        {
            var array = new JSONArray();
            foreach (IJsonObject entry in entries)
            {
                array.Add(entry);
            }
            return array;
        }

        private static object Builder(PayloadPopulator populator)
        {
            return typeof(PayloadPopulator).GetField("m_payloadPropertyBuilder", Private).GetValue(populator);
        }

        private static bool InvokeGate(PayloadPopulator populator)
        {
            object builder = Builder(populator);
            var gate = (Delegate)builder.GetType().GetField("m_isAllowedToUpdateDelegate", Private).GetValue(builder);
            return (bool)gate.DynamicInvoke();
        }

        // 0x1b07270..0x1b072b0 forward to the JSONHashtable typed lookups, so a value of the
        // wrong JSON type fails instead of converting.
        [Test]
        public void ScalarHelpersReadOnlyMatchingJsonTypes()
        {
            var probe = new ProbePayload();
            var data = new JSONHashtable();
            data.Add("flag", new JSONBool { Value = true });
            data.Add("name", Text("sonic"));

            Assert.That(probe.ReadBool(data, "flag", out bool flag), Is.True);
            Assert.That(flag, Is.True);
            Assert.That(probe.ReadString(data, "name", out string name), Is.True);
            Assert.That(name, Is.EqualTo("sonic"));
            Assert.That(probe.ReadString(data, "flag", out string wrong), Is.False);
            Assert.That(wrong, Is.Null);
            Assert.That(probe.ReadDouble(data, "missing", out double missing), Is.False);
            Assert.That(missing, Is.EqualTo(0d));
        }

        // 0x1b07352: an existing list is cleared before the lookup, so a missing array
        // still empties it and the call reports false.
        [Test]
        public void StringArrayClearsTheExistingListEvenWhenTheArrayIsMissing()
        {
            var probe = new ProbePayload();
            var data = new JSONHashtable();
            data.Add("text", Text("not an array"));
            var result = new List<string> { "stale" };

            Assert.That(probe.ReadStringArray(data, "text", ref result), Is.False);
            Assert.That(result, Is.Empty);
        }

        // 0x1b0740a: a null list is created only when the first entry converts, and the
        // same list instance is reused when one is supplied.
        [Test]
        public void StringArrayCreatesTheListOnlyForAConvertedEntry()
        {
            var probe = new ProbePayload();
            var data = new JSONHashtable();
            data.Add("names", Entries(Text("a"), Text("b")));
            data.Add("empty", Entries());

            List<string> created = null;
            Assert.That(probe.ReadStringArray(data, "names", ref created), Is.True);
            Assert.That(created, Is.EqualTo(new[] { "a", "b" }));

            List<string> untouched = null;
            Assert.That(probe.ReadStringArray(data, "empty", ref untouched), Is.True);
            Assert.That(untouched, Is.Null);

            var supplied = new List<string> { "stale" };
            List<string> reference = supplied;
            Assert.That(probe.ReadStringArray(data, "names", ref reference), Is.True);
            Assert.That(reference, Is.SameAs(supplied));
            Assert.That(supplied, Is.EqualTo(new[] { "a", "b" }));
        }

        // 0x1b074a0: a non-string entry is logged with String.Format of the entry and
        // skipped; the result still reports the array as found.
        [Test]
        public void StringArrayLogsAndSkipsEntriesThatAreNotStrings()
        {
            var probe = new ProbePayload();
            var data = new JSONHashtable();
            data.Add("mixed", Entries(Text("a"), new JSONLong { Value = 7 }, Text("b")));
            List<string> result = null;

            using (var capture = new OwnedHLOutputErrorCapture())
            {
                Assert.That(probe.ReadStringArray(data, "mixed", ref result), Is.True);
                Assert.That(capture.Errors, Is.EqualTo(new[]
                {
                    "Failed to convert " + typeof(JSONLong).FullName + " to a string entry from a received JSONArray.|0",
                }));
            }
            Assert.That(result, Is.EqualTo(new[] { "a", "b" }));
        }

        // 0x1b08090 and 0x1b080f0: the reader targets PayloadJSON and accepts every entry,
        // passing its key and value through unchanged, a null value included.
        [Test]
        public void ReaderTargetsPayloadJsonAndAcceptsEveryEntry()
        {
            var reader = new PayloadReaderJSON();
            JSONString value = Text("v");

            Assert.That(reader.GetPayloadType(), Is.SameAs(typeof(PayloadJSON)));
            Assert.That(reader.GetKeyValueFromData(new KeyValuePair<string, IJsonObject>("k", value), out string identifier, out IJsonObject output), Is.True);
            Assert.That(identifier, Is.EqualTo("k"));
            Assert.That(output, Is.SameAs(value));
            Assert.That(reader.GetKeyValueFromData(new KeyValuePair<string, IJsonObject>(null, null), out identifier, out output), Is.True);
            Assert.That(identifier, Is.Null);
            Assert.That(output, Is.Null);
        }

        // 0x1b06fb0: the builder is created from this after the object constructor, so it
        // fills the derived instance's missing payload properties, and its gate is bound
        // through vtable slot 4 to the override.
        [Test]
        public void PopulatorBuildsItsPropertiesAndBindsTheGateToTheOverride()
        {
            var populator = new GatedPopulator();

            Assert.That(populator.Count, Is.Not.Null);
            object builder = Builder(populator);
            Assert.That(builder.GetType().GetField("m_parent", Private).GetValue(builder), Is.SameAs(populator));

            Assert.That(InvokeGate(populator), Is.False);
            populator.Safe = true;
            Assert.That(InvokeGate(populator), Is.True);
        }

        // 0x1b07260: without an override the gate is always open.
        [Test]
        public void PopulatorGateDefaultsToOpen()
        {
            var populator = new DefaultPopulator();

            Assert.That(populator.Count, Is.Not.Null);
            Assert.That(InvokeGate(populator), Is.True);
        }
    }
}
