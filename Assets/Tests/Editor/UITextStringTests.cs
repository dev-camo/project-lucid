using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Utils;
using HardlightProject;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UITextString (Game.Runtime 0x02000a3c, x86-64 0x6B93D0..0x6B94A0), a plain serializable class, over an owned string table
    // (OwnedStringTable) and a real TextMeshProUGUI on an owned inactive host. The serialized label is authored data and is set
    // directly.
    public sealed class UITextStringTests
    {
        private const Strings Greeting = Strings.CULTURE_NUMBER_BILLIONS;

        // SetText reads the default style sheet from TMP_Settings.instance (TMP_Text.GetStyle). The Resources "TMP Settings" asset of
        // the recovered project is not bound to this TextMesh Pro package's settings script, so no instance loads; an owned empty
        // settings object stands in for the case and the loaded one (none) is put back.
        private static void LeaseTextSettings(Owned owned)
        {
            FieldInfo instance = typeof(TMP_Settings).GetField("s_Instance", BindingFlags.Static | BindingFlags.NonPublic);
            object prior = instance.GetValue(null);
            owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => instance.SetValue(null, prior)));
            instance.SetValue(null, owned.Track(ScriptableObject.CreateInstance<TMP_Settings>()));
        }

        private static TextMeshProUGUI NewLabel(Owned owned)
        {
            LeaseTextSettings(owned);
            GameObject host = owned.Track(new GameObject("Lucid ui text string label", typeof(RectTransform)));
            host.SetActive(false);
            return host.AddComponent<TextMeshProUGUI>();
        }

        // dump.cs TypeDefIndex 2619 and the DummyDll rows (fields 0x04002af7..0x04002af9).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UITextString), 0x00102001,
                new (string, int)[] { ("m_stringID", 0x0001), ("m_textMeshPro", 0x0001), ("m_string", 0x0001) },
                new (string, int)[] { (".ctor", 0x1886), ("Initialise", 0x0086), ("Apply", 0x0086) });
            AssertIl2CppOptions(typeof(UITextString), 1, 2);
            Assert.That(typeof(UITextString).BaseType, Is.EqualTo(typeof(object)));
            Assert.That(AttributeTypes(typeof(UITextString).GetField("m_stringID", Declared)),
                Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField), typeof(HashEnumAttribute) }));
            Assert.That(Tooltip(typeof(UITextString).GetField("m_stringID", Declared)), Is.EqualTo("Id for the localised string."));
            Assert.That(AttributeTypes(typeof(UITextString).GetField("m_textMeshPro", Declared)),
                Is.EqualTo(new[] { typeof(SerializeField), typeof(TooltipAttribute) }));
            Assert.That(Tooltip(typeof(UITextString).GetField("m_textMeshPro", Declared)), Is.EqualTo("Text component to set with string."));
            Assert.That(AttributeTypes(typeof(UITextString).GetField("m_string", Declared)), Is.Empty);
        }

        // The constructor 0x6B93D0 only stores the id; the label and the looked up string stay unset.
        [Test]
        public void TheConstructorStoresOnlyTheStringId()
        {
            var text = new UITextString(Greeting);

            Assert.That(Get<Strings>(text, "m_stringID"), Is.EqualTo(Greeting));
            Assert.That(Get<TextMeshProUGUI>(text, "m_textMeshPro"), Is.Null);
            Assert.That(Get<string>(text, "m_string"), Is.Null);
        }

        // Initialise 0x6B93F0 stores StringTable.GetString(id); Apply 0x6B9460 formats that string with the arguments and sets the
        // label's text (the virtual TMP_Text.text setter).
        [Test]
        public void ApplyFormatsTheLocalisedStringIntoTheLabel()
        {
            Owned.Run(owned =>
            {
                TextMeshProUGUI label = NewLabel(owned);
                var text = new UITextString(Greeting);
                Set(text, "m_textMeshPro", label);
                OwnedStringTable.Run(new Dictionary<Strings, string> { { Greeting, "{1} collected {0} rings" } }, () =>
                {
                    text.Initialise();
                    Assert.That(Get<string>(text, "m_string"), Is.EqualTo("{1} collected {0} rings"));

                    text.Apply(new object[] { 12, "Sonic" });
                    Assert.That(label.text, Is.EqualTo("Sonic collected 12 rings"));

                    text.Apply(new object[] { 3, "Tails" });
                    Assert.That(label.text, Is.EqualTo("Tails collected 3 rings"), "each Apply formats the stored string afresh");
                });
            });
        }

        // string.Format (0x6B946F) is handed the stored string, so Apply before Initialise (no string) and Apply with no arguments
        // both fault in string.Format, before the label is touched.
        [Test]
        public void ApplyFaultsWithoutAnInitialisedStringOrWithoutArguments()
        {
            Owned.Run(owned =>
            {
                TextMeshProUGUI label = NewLabel(owned);
                var text = new UITextString(Greeting);
                Set(text, "m_textMeshPro", label);
                Assert.Throws<ArgumentNullException>(() => text.Apply(new object[] { 1 }), "no Initialise yet");

                OwnedStringTable.Run(new Dictionary<Strings, string> { { Greeting, "{0}" } }, () =>
                {
                    text.Initialise();
                    Assert.Throws<ArgumentNullException>(() => text.Apply(null));
                });
                Assert.That(label.text, Is.Null.Or.Empty);
            });
        }
    }
}
