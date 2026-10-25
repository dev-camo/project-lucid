using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    public sealed class TimeSettingsConversionOrderTests
    {
        private sealed class ReplaceableSettings : TimeSettings_SDT
        {
            internal void ReplaceDestination(SerializableDictionary<TimeCategoryObject, float> destination) =>
                m_overridesDictionary = destination;
        }

        private sealed class ReplacingLookup : Dictionary<TimeCategory, TimeCategoryObject>, IDictionary<TimeCategory, TimeCategoryObject>
        {
            private readonly ReplaceableSettings settings;
            private readonly SerializableDictionary<TimeCategoryObject, float> replacement;
            internal readonly List<TimeCategory> RequestedKeys = new List<TimeCategory>();

            internal ReplacingLookup(ReplaceableSettings settings, SerializableDictionary<TimeCategoryObject, float> replacement)
            {
                this.settings = settings;
                this.replacement = replacement;
            }

            TimeCategoryObject IDictionary<TimeCategory, TimeCategoryObject>.this[TimeCategory key]
            {
                get
                {
                    RequestedKeys.Add(key);
                    settings.ReplaceDestination(replacement);
                    return base[key];
                }
                set => base[key] = value;
            }
        }

        [Test]
        public void CategoryGetterRunsBeforeTheDestinationFieldIsRead()
        {
            var category = ScriptableObject.CreateInstance<TimeCategoryObject>();
            try
            {
                var settings = new ReplaceableSettings();
                IReadOnlyDictionary<TimeCategoryObject, float> original = settings.OverrideDictionaryLookup;
                var replacement = new SerializableDictionary<TimeCategoryObject, float>();
                var authored = (SerializableDictionary<TimeCategory, float>)typeof(TimeSettings_SDT)
                    .GetField("m_overrides", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(settings);
                const float value = 0.375f;
                authored.Add(TimeCategory.PlayerMovement, value);
                var lookup = new ReplacingLookup(settings, replacement) { { TimeCategory.PlayerMovement, category } };
                Assert.That(original, Is.Empty);
                Assert.That(replacement, Is.Empty);
                Assert.That(original, Is.Not.SameAs(replacement));

                settings.ApplyTimeCategoryObjectConversion(lookup);

                TestContext.WriteLine("Destination counts after conversion: original=" + original.Count + ", replacement=" + replacement.Count);
                // Original 06002e70 / x86-64 5FBF40: getter 5fc19c precedes destination load
                // 5fc19e and setter 5fc1ae. The supplied getter can legally replace that field.
                Assert.That(lookup.RequestedKeys.Count, Is.EqualTo(1));
                Assert.That(lookup.RequestedKeys, Is.EqualTo(new[] { TimeCategory.PlayerMovement }));
                Assert.That(original, Is.Empty, "the destination read must happen after the getter replacement");
                Assert.That(settings.OverrideDictionaryLookup, Is.SameAs(replacement));
                Assert.That(replacement.Count, Is.EqualTo(1));
                Assert.That(replacement.Keys.SingleOrDefault(), Is.SameAs(category));
                Assert.That(replacement.TryGetValue(category, out float actual), Is.True);
                Assert.That(actual, Is.EqualTo(value));
                Assert.That(authored.Count, Is.EqualTo(1));
                Assert.That(authored[TimeCategory.PlayerMovement], Is.EqualTo(value));
            }
            finally { Object.DestroyImmediate(category); }
        }
    }
}
