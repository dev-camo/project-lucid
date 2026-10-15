using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectLucid.Tests
{
    public sealed class OriginalHLButtonWeakRemediationTests
    {
        // HLButton 0x020000db accessors 0x060005ee..f7, x86-64: OnClick 0x1b6cce0/0x1b6ccf0 use
        // [this+0x1a0], OnDisabledClick 0x1b6cd10/20 [+0x1a8], OnOfflineClick 0x1b6cda0/b0
        // [+0x1b0], OnButtonDown 0x1b6cd40/50 [+0x1b8], OnButtonUp 0x1b6cd70/80 [+0x1c0]. The
        // .ctor 0x1b6d7a0 fills the same offsets in field-row order m_onClick 0x0400044b,
        // m_onDisabledClick, m_onOfflineClick, m_onButtonDown, m_onButtonUp 0x0400044f.
        // Distinct events per property catch any getter or setter wired to another field.
        [Test]
        public void InactiveButtonPropertiesRouteToTheirOwnBackingFields()
        {
            var logs = new List<string>();
            Application.LogCallback hook = (message, stack, type) => logs.Add(type + ": " + message);
            Application.logMessageReceived += hook;
            var root = new GameObject("owned-original-button-routes", typeof(RectTransform));
            try
            {
                root.SetActive(false);
                HLButton button = root.AddComponent<HLButton>();
                Assert.That(button.IsActive(), Is.False);
                string[] fields = { "m_onClick", "m_onDisabledClick", "m_onOfflineClick", "m_onButtonDown", "m_onButtonUp" };
                UnityEvent[] initial = Read(button, fields);
                CollectionAssert.AreEqual(initial, new[] { button.OnClick, button.OnDisabledClick, button.OnOfflineClick, button.OnButtonDown, button.OnButtonUp });

                var replacements = new UnityEvent[fields.Length];
                for (int i = 0; i < replacements.Length; ++i) replacements[i] = new UnityEvent();
                button.OnClick = replacements[0];
                button.OnDisabledClick = replacements[1];
                button.OnOfflineClick = replacements[2];
                button.OnButtonDown = replacements[3];
                button.OnButtonUp = replacements[4];
                UnityEvent[] stored = Read(button, fields);
                for (int i = 0; i < fields.Length; ++i)
                    Assert.That(ReferenceEquals(stored[i], replacements[i]), Is.True, fields[i]);
                Assert.That(ReferenceEquals(button.OnClick, replacements[0]), Is.True);
                Assert.That(ReferenceEquals(button.OnDisabledClick, replacements[1]), Is.True);
                Assert.That(ReferenceEquals(button.OnOfflineClick, replacements[2]), Is.True);
                Assert.That(ReferenceEquals(button.OnButtonDown, replacements[3]), Is.True);
                Assert.That(ReferenceEquals(button.OnButtonUp, replacements[4]), Is.True);
                Assert.That(ReferenceEquals(button.OnDisabledSelected, null), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Application.logMessageReceived -= hook;
            }
            Assert.That(logs, Is.Empty);
        }

        private static UnityEvent[] Read(HLButton button, string[] names)
        {
            var values = new UnityEvent[names.Length];
            for (int i = 0; i < names.Length; ++i)
            {
                FieldInfo field = typeof(HLButton).GetField(names[i], BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                Assert.That(field, Is.Not.Null, names[i]);
                values[i] = (UnityEvent)field.GetValue(button);
            }
            return values;
        }
    }
}
