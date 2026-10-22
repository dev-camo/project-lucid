using System;
using System.Collections;
using System.Reflection;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerQRCode (Game.Runtime 0x02000919; Setup 0x678530, UpdateBinders 0x6785A0) as a real component on an owned
    // inactive host over an owned string table. The authored lookup (a serialized dictionary of private structs) is filled
    // by reflection the way the engine serializer delivers it.
    public sealed class UIContainerQRCodeTests
    {
        private const BindingFlags NonPublic = BindingFlags.NonPublic | BindingFlags.Public;
        private static readonly Type LookUpType = typeof(UIContainerQRCode).GetNestedType("QRCodeSpriteLookUp", NonPublic);
        private static readonly Type PairType = typeof(UIContainerQRCode).GetNestedType("LanguageToSpritePair", NonPublic);
        private const Strings UrlString = Strings.MISSING;

        // The language the string table reports; the first of two others that differs from it.
        private static Languages Current => StringTable.StringsLanguage;

        private static Languages Other => Current == Languages.French ? Languages.German : Languages.French;

        // Reflection: the pair is a private serialized nested type.
        private static object Pair(Languages language, Sprite sprite)
        {
            object pair = Activator.CreateInstance(PairType);
            PairType.GetField("Language").SetValue(pair, language);
            PairType.GetField("Sprite").SetValue(pair, sprite);
            return pair;
        }

        // Reflection: the lookup and its entries are private serialized types.
        private static void Author(UIContainerQRCode container, Strings key, Sprite defaultSprite, params object[] overrides)
        {
            IList list = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(PairType));
            foreach (object pair in overrides) list.Add(pair);
            object lookUp = Activator.CreateInstance(LookUpType);
            LookUpType.GetField("DefaultSprite").SetValue(lookUp, defaultSprite);
            LookUpType.GetField("LanguageSpriteOverride").SetValue(lookUp, list);
            ((IDictionary)Get<object>(container, "m_stringToQRCodeLookup"))[key] = lookUp;
        }

        private static void Show(Action<Owned, UIContainerQRCode> body) => Owned.Run(owned =>
            OwnedStringTable.Run(new System.Collections.Generic.Dictionary<Strings, string> { { UrlString, "https://qr.lucid.test/x" } }, () =>
                body(owned, owned.Host("Lucid qr code", false).AddComponent<UIContainerQRCode>())));

        // Setup 0x678530 calls the base, stores the parameters and runs UpdateBinders: the URL text is the localised string.
        [Test]
        public void SetupShowsTheLocalisedAddress()
        {
            Show((owned, container) =>
            {
                container.Setup(new UIContainerQRCodeParameters(UrlString));

                Assert.That(container.URLText.Value, Is.EqualTo("https://qr.lucid.test/x"));
                Assert.That(Get<UIContainerQRCodeParameters>(container, "m_parameters").URLStringId, Is.EqualTo(UrlString));
            });
        }

        // The sprite is the entry's default, replaced by the sprite of the first override for the current string language
        // (the loop at 0x6786F0 stores the sprite and leaves at the first match, 0x678706). Two overrides name the current
        // language with different sprites, and one of another language comes before them.
        [Test]
        public void TheFirstOverrideForTheCurrentLanguageReplacesTheDefaultSprite()
        {
            Show((owned, container) =>
            {
                Sprite fallback = NewSprite(owned), foreign = NewSprite(owned), first = NewSprite(owned), second = NewSprite(owned);
                Author(container, UrlString, fallback, Pair(Other, foreign), Pair(Current, first), Pair(Current, second));

                container.Setup(new UIContainerQRCodeParameters(UrlString));

                Assert.That(container.QRCodeSprite.Value, Is.SameAs(first));
            });
        }

        // Without an override for the current language the default sprite is used (r14 keeps the default, 0x6786BC).
        [Test]
        public void WithoutAnOverrideForTheCurrentLanguageTheDefaultSpriteIsShown()
        {
            Show((owned, container) =>
            {
                Sprite fallback = NewSprite(owned), foreign = NewSprite(owned);
                Author(container, UrlString, fallback, Pair(Other, foreign));

                container.Setup(new UIContainerQRCodeParameters(UrlString));

                Assert.That(container.QRCodeSprite.Value, Is.SameAs(fallback));
            });
        }

        // TryGetValue false (0x67867A) skips the sprite entirely: the address is still shown, the sprite is left as it was.
        [Test]
        public void AStringWithoutALookupEntryKeepsTheSpriteAsItWas()
        {
            Show((owned, container) =>
            {
                Sprite earlier = NewSprite(owned);
                container.QRCodeSprite.Value = earlier;
                Author(container, Strings.DECIMAL_SEPARATOR, NewSprite(owned));

                container.Setup(new UIContainerQRCodeParameters(UrlString));

                Assert.That(container.URLText.Value, Is.EqualTo("https://qr.lucid.test/x"));
                Assert.That(container.QRCodeSprite.Value, Is.SameAs(earlier));
            });
        }
    }
}
