using System;
using System.Collections.Generic;
using HardlightProject;
using NUnit.Framework;
using RTLTMPro;
using UnityEngine;
using UnityEngine.UI;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetToggleCollectionsParameters (Game.Runtime 0x020009d1, constructor x86-64 0x6A32C0) and UIWidgetToggleCollections
    // (0x020009d0, Setup 0x6A3200, UpdateBadge 0x6A3260, Action_OnValueChanged 0x6A3280) as a real component on an owned inactive
    // host with a real alert badge writing a genuine RTLTextMeshPro (no Awake, layout or rendering).
    public sealed class UIWidgetToggleCollectionsTests
    {
        private sealed class Rig
        {
            public UIWidgetToggleCollections Widget;
            public Image Target;
            public Sprite Off, On;
            public RTLTextMeshPro BadgeText;
            public GameObject BadgeTarget;
        }

        private static void Run(Action<Rig> body) => Owned.Run(owned =>
        {
            var rig = new Rig();
            GameObject host = owned.Host("Lucid collections toggle", false);
            rig.Widget = host.AddComponent<UIWidgetToggleCollections>();
            rig.Target = host.AddComponent<Image>();
            rig.Off = NewSprite(owned);
            rig.On = NewSprite(owned);
            Set(rig.Widget, "m_target", rig.Target); // Reflection: serialized reference the editor assigns
            Set(rig.Widget, "m_imageWhenOff", rig.Off); // Reflection: authored serialized field
            Set(rig.Widget, "m_imageWhenOn", rig.On); // Reflection: authored serialized field

            GameObject badgeHost = owned.Track(new GameObject("Lucid collections toggle badge", typeof(RectTransform)));
            badgeHost.SetActive(false);
            rig.BadgeTarget = owned.Track(new GameObject("Lucid collections toggle badge target"));
            rig.BadgeTarget.transform.SetParent(badgeHost.transform, false);
            rig.BadgeTarget.SetActive(false);
            rig.BadgeText = badgeHost.AddComponent<RTLTextMeshPro>();
            var badge = badgeHost.AddComponent<UIWidgetSDTAlertBadge>();
            Set(badge, "m_text", rig.BadgeText); // Reflection: serialized reference the editor assigns
            Set(badge, "m_enableObjectWhileActive", rig.BadgeTarget); // Reflection: serialized reference the editor assigns
            Set(rig.Widget, "m_alertBadge", badge); // Reflection: serialized reference the editor assigns
            body(rig);
        });

        // 0x6A32C0 passes string.Empty (the static field read at 0x6A32F8..0x6A32FF), not null, with the callback to the
        // (string, Action<bool>) base constructor (0x1A49F90), then stores the display number at +0x20; the default is zero.
        [Test]
        public void TheParametersCarryAnEmptyLabelTheCallbackAndTheDisplayNumber()
        {
            Action<bool> callback = _ => { };

            var counted = new UIWidgetToggleCollectionsParameters(callback, 7);
            var uncounted = new UIWidgetToggleCollectionsParameters(callback);

            Assert.That(counted.Label, Is.Not.Null.And.Empty);
            Assert.That(counted.Callback, Is.SameAs(callback));
            Assert.That(counted.DisplayNumber, Is.EqualTo(7));
            Assert.That(uncounted.DisplayNumber, Is.Zero);
        }

        // Setup 0x6A3200: the base toggle setup first (label and callback, 0x6A3231), then SetAlert on the badge (0x6A324B..0x6A3255)
        // with the alert flag fixed to false (xor esi, esi) and the display number read at +0x20. A count above the badge limit shows
        // the capped text, and zero hides the badge.
        [Test]
        public void SetupShowsTheDisplayNumberOnTheBadgeAndNeverTheAlertMark()
        {
            Run(rig =>
            {
                rig.Widget.Setup(new UIWidgetToggleCollectionsParameters(_ => { }, 5));
                Assert.That(rig.BadgeTarget.activeSelf, Is.True);
                Assert.That(rig.BadgeText.text, Is.EqualTo("5"));

                rig.Widget.Setup(new UIWidgetToggleCollectionsParameters(_ => { }, 100));
                Assert.That(rig.BadgeText.text, Is.EqualTo("99+"));

                rig.Widget.Setup(new UIWidgetToggleCollectionsParameters(_ => { }));
                Assert.That(rig.BadgeTarget.activeSelf, Is.False);
                Assert.That(rig.Widget.Label.Value, Is.Empty);
            });
        }

        // UpdateBadge 0x6A3260 calls SetAlert(false, number) and nothing else.
        [Test]
        public void UpdatingTheBadgeRewritesTheCount()
        {
            Run(rig =>
            {
                rig.Widget.UpdateBadge(3);
                Assert.That(rig.BadgeTarget.activeSelf, Is.True);
                Assert.That(rig.BadgeText.text, Is.EqualTo("3"));

                rig.Widget.UpdateBadge(0);
                Assert.That(rig.BadgeTarget.activeSelf, Is.False);
            });
        }

        // Action_OnValueChanged 0x6A3280: the base callback runs first (0x6A328E), then the sprite is chosen from the pair of fields
        // behind the target image (0x6A3299 indexes m_imageWhenOff/m_imageWhenOn by the value): on gives the on sprite. The callback
        // sees the sprite from before the change.
        [Test]
        public void ChangingTheValueRunsTheCallbackThenPicksTheSpriteOfTheNewValue()
        {
            Run(rig =>
            {
                var seen = new List<(bool value, Sprite sprite)>();
                rig.Widget.Setup(new UIWidgetToggleCollectionsParameters(value => seen.Add((value, rig.Target.sprite)), 0));
                rig.Target.sprite = rig.Off;

                rig.Widget.Action_OnValueChanged(true);
                Assert.That(rig.Target.sprite, Is.SameAs(rig.On));
                rig.Widget.Action_OnValueChanged(false);
                Assert.That(rig.Target.sprite, Is.SameAs(rig.Off));

                Assert.That(seen.Count, Is.EqualTo(2));
                Assert.That(seen[0].value, Is.True);
                Assert.That(seen[0].sprite, Is.SameAs(rig.Off), "the callback runs before the sprite changes");
                Assert.That(seen[1].value, Is.False);
                Assert.That(seen[1].sprite, Is.SameAs(rig.On));
            });
        }
    }
}
