using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // Owned normal dictionaries exercise preserved wrapper behavior. No game
    // provider, private backing field, platform service or Unity object is used.
    public sealed class OriginalDefaultAndNewDictionaryOwnedBehaviorTests
    {
        [Test]
        public void MissingLookupInsertsOneFreshValuePerKeyOnlyInDictionaryWithNew()
        {
            var creating = new DictionaryWithNew<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            var fallback = new DictionaryWithDefault<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            List<int> created, repeated, second, missing;

            Assert.That(creating.TryGetValue("player", out created), Is.True);
            Assert.That(created, Is.Not.Null.And.Empty);
            Assert.That(creating.ContainsKey("player"), Is.True);
            Assert.That(creating.Count, Is.EqualTo(1));
            created.Add(7);
            Assert.That(creating.TryGetValue("PLAYER", out repeated), Is.True);
            Assert.That(repeated, Is.SameAs(created));
            CollectionAssert.AreEqual(new[] { 7 }, repeated);
            Assert.That(creating.Count, Is.EqualTo(1));
            Assert.That(creating.TryGetValue("other-player", out second), Is.True);
            Assert.That(second, Is.Not.Null.And.Empty);
            Assert.That(second, Is.Not.SameAs(created));
            Assert.That(creating.Count, Is.EqualTo(2));

            // The indexers preserve the same distinction for another missing key.
            var indexed = creating["checkpoint"];
            Assert.That(indexed, Is.Not.Null.And.Empty);
            Assert.That(indexed, Is.Not.SameAs(created).And.Not.SameAs(second));
            Assert.That(creating["CHECKPOINT"], Is.SameAs(indexed));
            Assert.That(creating.Count, Is.EqualTo(3));

            Assert.That(fallback.TryGetValue("player", out missing), Is.True);
            Assert.That(missing, Is.Null);
            Assert.That(fallback["PLAYER"], Is.Null);
            Assert.That(fallback.ContainsKey("player"), Is.False);
            Assert.That(fallback.Count, Is.Zero);
        }

        [Test]
        public void ExistingNullAndExistingValueRemainPresentWithoutReplacement()
        {
            IDictionary<string, List<int>>[] dictionaries = {
                new DictionaryWithNew<string, List<int>>(2, StringComparer.OrdinalIgnoreCase),
                new DictionaryWithDefault<string, List<int>>(2, StringComparer.OrdinalIgnoreCase)
            };
            foreach (var dictionary in dictionaries)
            {
                var owned = new List<int> { 11 };
                dictionary.Add("empty", null);
                dictionary.Add("stored", owned);
                List<int> empty, stored;
                Assert.That(dictionary.TryGetValue("EMPTY", out empty), Is.True);
                Assert.That(empty, Is.Null);
                Assert.That(dictionary.ContainsKey("empty"), Is.True);
                Assert.That(dictionary["EMPTY"], Is.Null);
                Assert.That(dictionary.TryGetValue("STORED", out stored), Is.True);
                Assert.That(stored, Is.SameAs(owned));
                stored.Add(12);
                Assert.That(dictionary["stored"], Is.SameAs(owned));
                CollectionAssert.AreEqual(new[] { 11, 12 }, dictionary["stored"]);
                Assert.That(dictionary.Count, Is.EqualTo(2));
            }
        }

        [Test]
        public void OriginalUnsupportedObjectOperationsThrowWithoutChangingOwnedContents()
        {
            IDictionary[] dictionaries = {
                new DictionaryWithNew<string, List<int>>(),
                new DictionaryWithDefault<string, List<int>>()
            };
            foreach (var dictionary in dictionaries)
            {
                var stored = new List<int> { 19 };
                dictionary["stored"] = stored;
                Assert.Throws<NotImplementedException>(() => dictionary.Add("other", new List<int>()));
                Assert.Throws<NotImplementedException>(() => dictionary.Contains("stored"));
                Assert.Throws<NotImplementedException>(() => dictionary.Remove("stored"));
                var destination = new object[] { stored };
                Assert.Throws<NotImplementedException>(() => ((ICollection)dictionary).CopyTo(destination, 0));
                Assert.That(destination[0], Is.SameAs(stored));
                Assert.That(dictionary.Count, Is.EqualTo(1));
                Assert.That(dictionary["stored"], Is.SameAs(stored));
                CollectionAssert.AreEqual(new[] { 19 }, stored);
            }
        }

        [Test]
        public void NongenericEnumerationWorksForNewButRemainsUnsupportedForDefault()
        {
            var stored = new List<int> { 23 };
            var creating = new DictionaryWithNew<string, List<int>>(1);
            var fallback = new DictionaryWithDefault<string, List<int>>(1);
            creating.Add("stored", stored);
            fallback.Add("stored", stored);
            var creatingEnumerator = ((IDictionary)creating).GetEnumerator();
            try
            {
                Assert.That(creatingEnumerator.MoveNext(), Is.True);
                Assert.That(creatingEnumerator.Key, Is.EqualTo("stored"));
                Assert.That(creatingEnumerator.Value, Is.SameAs(stored));
                Assert.That(creatingEnumerator.Entry.Key, Is.EqualTo("stored"));
                Assert.That(creatingEnumerator.Entry.Value, Is.SameAs(stored));
                Assert.That(creatingEnumerator.MoveNext(), Is.False);
            }
            finally { DisposeOwnedEnumerator(creatingEnumerator); }
            Assert.Throws<NotImplementedException>(() => ((IDictionary)fallback).GetEnumerator());

            // The original explicit IEnumerable route remains usable in Default.
            var fallbackEnumerator = ((IEnumerable)fallback).GetEnumerator();
            try
            {
                Assert.That(fallbackEnumerator.MoveNext(), Is.True);
                var pair = (KeyValuePair<string, List<int>>)fallbackEnumerator.Current;
                Assert.That(pair.Key, Is.EqualTo("stored"));
                Assert.That(pair.Value, Is.SameAs(stored));
                Assert.That(fallbackEnumerator.MoveNext(), Is.False);
            }
            finally { DisposeOwnedEnumerator(fallbackEnumerator); }
            Assert.That(creating.Count, Is.EqualTo(1));
            Assert.That(fallback.Count, Is.EqualTo(1));
        }

        [Test]
        public void FindValueVisitsTheObservedValueOrderAndStopsAtItsFirstMatch()
        {
            var dictionary = CreateOwnedSearchDictionary();
            // Use the genuine current collection's order, without imposing a
            // particular BCL hash-table enumeration order across desktop runtimes.
            var expected = new List<List<int>>(dictionary.Values);
            var visited = new List<List<int>>();
            var target = expected[1];
            var found = dictionary.FindValue(value => {
                visited.Add(value);
                return ReferenceEquals(value, target);
            });
            Assert.That(found, Is.SameAs(target));
            AssertReferenceSequence(expected.GetRange(0, 2), visited);

            visited.Clear();
            var absent = dictionary.FindValue(value => { visited.Add(value); return false; });
            Assert.That(absent, Is.Null);
            AssertReferenceSequence(expected, visited);
            AssertReferenceSequence(expected, new List<List<int>>(dictionary.Values));
            Assert.That(dictionary.Count, Is.EqualTo(3));
        }

        [Test]
        public void FindValueLeavesAnEmptyNullPredicateUncalledAndPropagatesRealCallbackFaults()
        {
            var dictionary = new DictionaryWithDefault<string, List<int>>();
            Assert.That(dictionary.FindValue(null), Is.Null);
            dictionary.Add("stored", new List<int> { 31 });
            var expected = new List<List<int>>(dictionary.Values);
            Assert.Throws<NullReferenceException>(() => dictionary.FindValue(null));

            var fault = new InvalidOperationException("owned predicate failure");
            var visited = new List<List<int>>();
            var actual = Assert.Throws<InvalidOperationException>(() => dictionary.FindValue(value => {
                visited.Add(value);
                throw fault;
            }));
            Assert.That(actual, Is.SameAs(fault));
            AssertReferenceSequence(expected, visited);
            AssertReferenceSequence(expected, new List<List<int>>(dictionary.Values));
            Assert.That(dictionary.FindValue(value => true), Is.SameAs(expected[0]));

            // A stored null still reaches the predicate; it is not skipped or
            // converted into a newly constructed value by this original wrapper.
            var nullDictionary = new DictionaryWithDefault<string, List<int>>();
            nullDictionary.Add("null", null);
            int calls = 0;
            Assert.That(nullDictionary.FindValue(value => {
                calls++;
                Assert.That(value, Is.Null);
                return true;
            }), Is.Null);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(nullDictionary.ContainsKey("null"), Is.True);
            Assert.That(nullDictionary.Count, Is.EqualTo(1));
        }

        private static DictionaryWithDefault<string, List<int>> CreateOwnedSearchDictionary()
        {
            var dictionary = new DictionaryWithDefault<string, List<int>>();
            dictionary.Add("first", new List<int> { 41 });
            dictionary.Add("second", new List<int> { 42 });
            dictionary.Add("third", new List<int> { 43 });
            return dictionary;
        }

        private static void AssertReferenceSequence(IList<List<int>> expected, IList<List<int>> actual)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            for (int index = 0; index < expected.Count; index++)
                Assert.That(actual[index], Is.SameAs(expected[index]));
        }

        private static void DisposeOwnedEnumerator(IEnumerator enumerator)
        {
            var disposable = enumerator as IDisposable;
            if (disposable != null) disposable.Dispose();
        }
    }
}
