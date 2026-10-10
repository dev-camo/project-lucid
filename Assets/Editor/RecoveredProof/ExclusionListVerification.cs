using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Verification
{
    // Owned verification of the complete original class; this adds no game behavior.
    public static partial class ExclusionListVerification
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Exclusion list verification: " + message);
        }
        static readonly BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        static bool EnabledValue(ExclusionList list)
        {
            FieldInfo field = typeof(ExclusionList).GetField("m_enabled", Declared);
            Require(field != null && field.DeclaringType == typeof(ExclusionList) && field.FieldType == typeof(bool) && field.Attributes == FieldAttributes.Private, "exact genuine private enabled field for read-only observation");
            return (bool)field.GetValue(list);
        }
        sealed class Snapshot
        {
            readonly string name; readonly string[] array; readonly string[] entries; readonly bool enabled;
            public Snapshot(ExclusionList list)
            {
                name = list.Name; array = list.Exclusions; entries = array == null ? null : array.ToArray(); enabled = EnabledValue(list);
            }
            public void Check(ExclusionList list)
            {
                Require(ReferenceEquals(list.Name, name) && ReferenceEquals(list.Exclusions, array) && EnabledValue(list) == enabled, "predicate retains exact name, array and private enabled value");
                if (array != null)
                {
                    Require(array.Length == entries.Length, "owned array length retained");
                    for (int i = 0; i < entries.Length; ++i) Require(ReferenceEquals(array[i], entries[i]), "every ordered owned string/null identity retained");
                }
            }
        }
        static void CheckPredicate(ExclusionList list, string message, bool expected, Type fault = null)
        {
            var before = new Snapshot(list); bool result = false; Exception caught = null;
            try { result = list.IsExcluded(message); }
            catch (Exception exception) { caught = exception; }
            before.Check(list);
            if (fault == null) Require(caught == null && result == expected, "exact original predicate result without mutation");
            else Require(caught != null && caught.GetType() == fault, "exact genuine managed fault " + fault.FullName + " without predicate mutation");
        }
        static void RunOwned(Action<ExclusionList> body)
        {
            // Current three-method witness is generated only from independently qualified fresh Core243.
            CheckCompleteCurrentDeclarationsAndBodies();
            ExclusionList owned = null;
            try { owned = new ExclusionList(); body(owned); }
            finally
            {
                // Plain managed original object: no registry, subscription, disposal or service ownership.
                // Drop only the local reference, even if acquisition or a body assertion fails.
                owned = null;
            }
        }
        public static void VerifyConstructorDefaultsAndWriteOnlyEnabled()
        {
            RunOwned(list =>
            {
                Require(list.Name == null && list.Exclusions == null && EnabledValue(list), "original constructor leaves public references null and enables exclusions");
                CheckPredicate(list, "message", false, typeof(NullReferenceException));
                list.Enabled = false; Require(!EnabledValue(list), "original write-only setter stores false");
                CheckPredicate(list, null, false);
                list.Enabled = true; Require(EnabledValue(list), "original setter stores true without creating an array");
                Require(list.Name == null && list.Exclusions == null, "setter retains public constructor defaults");
                CheckPredicate(list, null, false, typeof(NullReferenceException));
            });
        }
        public static void VerifyDisabledShortCircuitBeforeNullInputs()
        {
            RunOwned(list =>
            {
                list.Enabled = false; list.Name = new string(new[] { 'o', 'w', 'n', 'e', 'd' });
                foreach (string[] array in new[] { (string[])null, Array.Empty<string>(), new string[] { null }, new[] { "hit", null, string.Empty } })
                {
                    list.Exclusions = array;
                    foreach (string message in new[] { null, string.Empty, "hit" }) CheckPredicate(list, message, false);
                }
                Require(!EnabledValue(list), "disabled predicate never reenables list");
            });
        }
        public static void VerifyEnabledArrayAndMessageFaultBoundaries()
        {
            RunOwned(list =>
            {
                list.Exclusions = Array.Empty<string>(); CheckPredicate(list, null, false); CheckPredicate(list, string.Empty, false); CheckPredicate(list, "message", false);
                list.Exclusions = null; CheckPredicate(list, null, false, typeof(NullReferenceException)); CheckPredicate(list, "message", false, typeof(NullReferenceException));
                foreach (string[] array in new[] { new[] { "needle" }, new[] { string.Empty }, new string[] { null } })
                {
                    list.Exclusions = array; CheckPredicate(list, null, false, typeof(NullReferenceException));
                }
                list.Exclusions = new string[] { null }; CheckPredicate(list, "message", false, typeof(ArgumentNullException));
                Require(EnabledValue(list), "all enabled fault paths preserve constructor flag");
            });
        }
        public static void VerifyPartialCaseSensitiveAndEmptyNeedles()
        {
            RunOwned(list =>
            {
                list.Exclusions = new[] { "ring" }; CheckPredicate(list, "spring", true); CheckPredicate(list, "RING", false); CheckPredicate(list, string.Empty, false);
                list.Exclusions = new[] { "Ro", "beta" }; CheckPredicate(list, "Sonic Rover", true); CheckPredicate(list, "sonic rover", false); CheckPredicate(list, "prefix beta suffix", true);
                list.Exclusions = new[] { string.Empty }; CheckPredicate(list, string.Empty, true); CheckPredicate(list, "message", true);
                list.Exclusions = new[] { "a\0b" }; CheckPredicate(list, "xa\0by", true); CheckPredicate(list, "xab", false);
            });
        }
        public static void VerifyIndexedShortCircuitAndNullNeedleOrder()
        {
            RunOwned(list =>
            {
                list.Exclusions = new[] { "hit", null }; CheckPredicate(list, "prefix hit suffix", true);
                list.Exclusions = new[] { null, "hit" }; CheckPredicate(list, "hit", false, typeof(ArgumentNullException));
                list.Exclusions = new[] { "miss", null, "hit" }; CheckPredicate(list, "hit", false, typeof(ArgumentNullException));
                list.Exclusions = new[] { "miss", "hit", null }; CheckPredicate(list, "hit", true);
                list.Exclusions = new[] { string.Empty, null }; CheckPredicate(list, "message", true);
                list.Exclusions = new[] { null, string.Empty }; CheckPredicate(list, "message", false, typeof(ArgumentNullException));
                list.Exclusions = new[] { "miss", "absent" }; CheckPredicate(list, "message", false);
            });
        }
        public static void VerifyLiveArrayReplacementAndIndependentInstances()
        {
            RunOwned(first =>
            {
                ExclusionList second = null;
                try
                {
                    second = new ExclusionList(); Require(second.Name == null && second.Exclusions == null && EnabledValue(second), "second real constructor is independent");
                    string[] shared = { "cat" }; first.Exclusions = shared; second.Exclusions = shared; first.Name = "first"; second.Name = "second";
                    first.Enabled = false; CheckPredicate(first, "cat", false); CheckPredicate(second, "cat", true); Require(!EnabledValue(first) && EnabledValue(second), "write-only enabled state is per instance");
                    shared[0] = "dog"; first.Enabled = true; CheckPredicate(first, "cat", false); CheckPredicate(first, "dog", true); CheckPredicate(second, "dog", true);
                    string[] replacement = { "bird" }; first.Exclusions = replacement;
                    CheckPredicate(first, "dog", false); CheckPredicate(first, "bird", true); CheckPredicate(second, "dog", true); CheckPredicate(second, "bird", false);
                    Require(ReferenceEquals(first.Exclusions, replacement) && ReferenceEquals(second.Exclusions, shared) && shared[0] == "dog", "public array replacement does not cache, copy or mutate previous owned array");
                    first.Name = null; CheckPredicate(first, "bird", true); first.Name = "unrelated display label"; CheckPredicate(first, "bird", true);
                }
                finally { second = null; }
            });
        }
        public static void VerifyOriginalPublicFieldJsonOverwrite()
        {
            RunOwned(source =>
            {
                ExclusionList target = null;
                try
                {
                    source.Name = "owned exclusions"; source.Exclusions = new[] { "ring", "boss" }; source.Enabled = false;
                    var beforeSerialization = new Snapshot(source);
                    string json = JsonUtility.ToJson(source);
                    beforeSerialization.Check(source);
                    Require(json != null && json.Contains("\"Name\":") && json.Contains("\"Exclusions\":") && !json.Contains("\"m_enabled\":") && !json.Contains("\"Enabled\":"), "real Unity JSON uses only original public serialized field identities");
                    target = new ExclusionList(); target.Name = "old"; target.Exclusions = new[] { "old" }; target.Enabled = false;
                    JsonUtility.FromJsonOverwrite(json, target);
                    Require(target.Name == source.Name && target.Exclusions != null && target.Exclusions.SequenceEqual(source.Exclusions) && !EnabledValue(target), "real Unity overwrite retains private nonserialized enabled state and restores original public fields");
                    CheckPredicate(target, "ring", false); target.Enabled = true; CheckPredicate(target, "ring", true); CheckPredicate(target, "boss", true); CheckPredicate(target, "absent", false);
                    Require(!EnabledValue(source) && source.Name == "owned exclusions" && source.Exclusions.SequenceEqual(new[] { "ring", "boss" }), "serialization does not activate or mutate source original object");
                }
                finally { target = null; }
            });
        }
    }
}
