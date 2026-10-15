using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using Element = HardlightProject.DefinitionDataType<string, Hardlight.FiniteStateMachineScriptableObject>.DefinitionElement<Hardlight.FiniteStateMachineScriptableObject>;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    public sealed class OriginalDefinitionDataTypeGapTests
    {
        // DefinitionDataType<TKey,TData>.GetData 0x06001c45, shared x86-64
        // 0xf301d0: the GetKeyComparer virtual call 0xf301ee precedes the
        // Dictionary ctor 0xf30234 and the first m_elements (+0x18) read
        // 0xf30239. Each row calls GetElementKey 0xf30269 with Data, then reads
        // Data (+0x18) again 0xf3026f for Dictionary.Add 0xf3028e. Reading the
        // array before the comparer, caching Data across the key callback, or
        // replacing Add with an indexer/TryAdd fails here.
        [Test]
        public void GetDataAsksForTheComparerFirstAndRereadsDataAfterEachKey()
        {
            var owned = new List<Object>();
            try
            {
                var group = Own(owned, ScriptableObject.CreateInstance<ProjectLucidRecordingDefinitionGroup>());
                FiniteStateMachineScriptableObject first = Own(owned, Fsm("Lucid definition first"));
                FiniteStateMachineScriptableObject second = Own(owned, Fsm("Lucid definition second"));
                group.Comparer = StringComparer.OrdinalIgnoreCase;
                group.Key = data => data.name;

                group.m_elements = null;
                Assert.Throws<NullReferenceException>(() => group.GetData());
                Assert.That(group.Calls, Is.EqualTo(new[] { "comparer" }), "the comparer is requested before the null array is read");

                var swapped = new Element(first);
                group.m_elements = new[] { swapped };
                group.Key = data =>
                {
                    swapped.Data = second;
                    return "lucid-key";
                };
                group.Calls.Clear();
                Dictionary<string, FiniteStateMachineScriptableObject> result = group.GetData();
                Assert.That(group.Calls, Is.EqualTo(new[] { "comparer", "key" }));
                Assert.That(result.Comparer, Is.SameAs(StringComparer.OrdinalIgnoreCase));
                Assert.That(result.Count, Is.EqualTo(1));
                Assert.That(result["LUCID-KEY"], Is.SameAs(second), "Data is read again after its key is generated");

                group.m_elements = new[] { new Element(first), new Element(second) };
                group.Key = data => data == first ? "Lucid" : "LUCID";
                group.Calls.Clear();
                Assert.Throws<ArgumentException>(() => group.GetData(), "keys equal under the supplied comparer are not overwritten");
                Assert.That(group.Calls, Is.EqualTo(new[] { "comparer", "key", "key" }));
            }
            finally
            {
                foreach (Object value in owned) if (value != null) Object.DestroyImmediate(value);
            }
        }

        private static FiniteStateMachineScriptableObject Fsm(string name)
        {
            var value = ScriptableObject.CreateInstance<FiniteStateMachineScriptableObject>();
            value.name = name;
            return value;
        }

        private static T Own<T>(List<Object> owned, T value) where T : Object
        {
            owned.Add(value);
            return value;
        }
    }
}
