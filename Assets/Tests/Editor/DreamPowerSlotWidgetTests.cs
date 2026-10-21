using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.DreamPowerSlotRigs;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The dream power slot widgets and the power description widget as real components on owned hosts:
    // UIWidgetButtonDreamPowerSlot (Game.Runtime 02000015, x86-64 0x507C20..0x508100),
    // UIWidgetImageDreamPowerSlot (02000017, 0x508220..0x5084C0) and UIWidgetDreamPowerDescription
    // (020009b2, 0x69DC00..0x69DEC0). The power's icon wrapper already holds its sprite, so the asynchronous
    // load completes at once and no Addressables call is made.
    public sealed class DreamPowerSlotWidgetTests
    {
        private const HLAudioClipIdentifier ClipEquipped = HLAudioClipIdentifier.AmbientBossCrabOcean;
        private const HLAudioClipIdentifier ClipUnequipped = HLAudioClipIdentifier.AmbientBossCrabWind;
        private const HLAudioClipIdentifier ClipDisabled = HLAudioClipIdentifier.Boss3_Guardian_angry_4_clicks;

        private static void AssignClips(ButtonSlot slot)
        {
            Set(slot.Widget, "m_clipEquipped", ClipEquipped); // Reflection: authored serialized field
            Set(slot.Widget, "m_clipUnequipped", ClipUnequipped); // Reflection: authored serialized field
            Set(slot.Widget, "m_clipDisabled", ClipDisabled); // Reflection: authored serialized field
        }

        private static HLAudioClipIdentifier ButtonClip(ButtonSlot slot) =>
            Get<HLAudioClipIdentifier>(slot.Sfx, "m_audioClipIdentifier");

        // 0x507DF0: the equipped flag is set first, the power's icon is requested, the equip clip is queued
        // (0x507E95) and the debug name is reset to the empty string (0x507EB2).
        [Test]
        public void EquippingASlotShowsTheIconAndPlaysTheEquipClip()
        {
            Owned.Run(owned =>
            {
                ButtonSlot slot = NewButtonSlot(owned);
                Sprite icon = NewSprite(owned);
                DreamPowerDefinition definition = NewDefinition(owned, "lucid-slot-power", Strings.MISSING, icon);
                slot.Widget.DebugName.Value = "stale";

                slot.Widget.Equip(definition);

                Assert.That(slot.Widget.IsEquipped, Is.True);
                Assert.That(slot.Widget.Icon.Value, Is.SameAs(icon));
                Assert.That(slot.Widget.DebugName.Value, Is.EqualTo(string.Empty));
                Assert.That(slot.Animations.Animation.IsPlaying(slot.EquipClip.name), Is.True);
                Assert.That(Get<int>(definition.IconAsset, "m_refCount"), Is.EqualTo(1), "the icon is retained, not released, by the slot");
            });
        }

        // 0x507EE0: the icon is cleared (0x507F18), the unequip clip is queued (0x507F2A), the debug name is reset
        // and the flag cleared last (0x507F5A). The loaded icon is not unloaded.
        [Test]
        public void UnequippingASlotClearsTheIconAndPlaysTheUnequipClip()
        {
            Owned.Run(owned =>
            {
                ButtonSlot slot = NewButtonSlot(owned);
                DreamPowerDefinition definition = NewDefinition(owned, "lucid-slot-power", Strings.MISSING, NewSprite(owned));
                slot.Widget.Equip(definition);
                slot.Widget.DebugName.Value = "stale";

                slot.Widget.Unequip();

                Assert.That(slot.Widget.IsEquipped, Is.False);
                Assert.That(slot.Widget.Icon.Value, Is.Null);
                Assert.That(slot.Widget.DebugName.Value, Is.EqualTo(string.Empty));
                Assert.That(slot.Animations.Animation.IsPlaying(slot.UnequipClip.name), Is.True);
                Assert.That(Get<int>(definition.IconAsset, "m_refCount"), Is.EqualTo(1), "Unequip leaves the icon retained");
            });
        }

        // 0x507F77: the slot is valid for equipping exactly when it is not equipped and the item state is 2
        // (Bought). The truth table separates "any state from Bought on", "Purchasable" and "ignore equipped".
        // The clip is the equipped one for a valid slot (0x507FB0), else the disabled one (0x507FC5) while empty.
        [Test]
        public void AnEmptySlotIsValidForEquipOnlyForABoughtPower()
        {
            Owned.Run(owned =>
            {
                ButtonSlot slot = NewButtonSlot(owned, false);
                AssignClips(slot);
                var expected = new Dictionary<UIDreamPowerStoreItemState, bool>
                {
                    { UIDreamPowerStoreItemState.LockedByProgression, false },
                    { UIDreamPowerStoreItemState.LockedByArchetype, false },
                    { UIDreamPowerStoreItemState.Bought, true },
                    { UIDreamPowerStoreItemState.Purchasable, false },
                    { UIDreamPowerStoreItemState.Insufficient, false },
                    { UIDreamPowerStoreItemState.Equipped, false },
                };
                foreach (var row in expected)
                {
                    slot.Widget.SetSlotState(row.Key);
                    Assert.That(slot.Widget.ValidForEquip.Value, Is.EqualTo(row.Value), row.Key.ToString());
                    Assert.That(ButtonClip(slot), Is.EqualTo(row.Value ? ClipEquipped : ClipDisabled), row.Key.ToString());
                }
            });
        }

        // The equipped flag is tested before the state (0x507F7A): an equipped slot is never valid and takes the
        // unequipped clip (0x507FBF) whatever the state.
        [Test]
        public void AnEquippedSlotIsNeverValidForEquipAndTakesTheUnequippedClip()
        {
            Owned.Run(owned =>
            {
                ButtonSlot slot = NewButtonSlot(owned);
                AssignClips(slot);
                slot.Widget.Equip(NewDefinition(owned, "lucid-slot-power", Strings.MISSING, NewSprite(owned)));
                foreach (UIDreamPowerStoreItemState state in Enum.GetValues(typeof(UIDreamPowerStoreItemState)))
                {
                    slot.Widget.ValidForEquip.Value = true;
                    slot.Widget.SetSlotState(state);
                    Assert.That(slot.Widget.ValidForEquip.Value, Is.False, state.ToString());
                    Assert.That(ButtonClip(slot), Is.EqualTo(ClipUnequipped), state.ToString());
                }
            });
        }

        // set_Interactable 0x507C60: the transition is queued (0x507CC9) with Disabled (4) for false and Normal (0) for
        // true, then the button (0x507CDB) and the inherited flag (0x507CF2) are written.
        [Test]
        public void SettingInteractableQueuesTheStateWritesTheButtonAndTheBaseFlag()
        {
            Owned.Run(owned =>
            {
                ButtonSlot slot = NewButtonSlot(owned);

                slot.Widget.Interactable = false;
                Assert.That(slot.Widget.Interactable, Is.False);
                Assert.That(slot.Widget.IsInteractable(), Is.False);
                Assert.That(slot.Button.interactable, Is.False);
                Assert.That(slot.Animations.Animation.IsPlaying(slot.Animations.Clips["state Disabled"].name), Is.True);

                slot.Widget.Interactable = true;
                Assert.That(slot.Widget.Interactable, Is.True);
                Assert.That(slot.Button.interactable, Is.True);
                Assert.That(slot.Animations.Animation.IsPlaying(slot.Animations.Clips["state Normal"].name), Is.True);
            });
        }

        // The transition is only queued when one is assigned (0x507CAF); the button and base flag are written either way.
        [Test]
        public void ASlotWithoutATransitionStillWritesTheButtonAndTheBaseFlag()
        {
            Owned.Run(owned =>
            {
                ButtonSlot slot = NewButtonSlot(owned, false);

                Assert.DoesNotThrow(() => slot.Widget.Interactable = false);

                Assert.That(slot.Button.interactable, Is.False);
                Assert.That(slot.Widget.IsInteractable(), Is.False);
            });
        }

        // SetInteractable 0x507D00 compares the virtual getter (0x507D17) first and only writes a change, so the
        // base setter's value-changed event fires once per real change.
        [Test]
        public void SetInteractableOnlyWritesAChange()
        {
            Owned.Run(owned =>
            {
                ButtonSlot slot = NewButtonSlot(owned);
                var seen = new List<bool>();
                var changed = Get<UnityEvent<bool>>(slot.Widget, "m_onInteractableValueChanged");
                if (changed == null)
                {
                    changed = new UnityEvent<bool>();
                    Set(slot.Widget, "m_onInteractableValueChanged", changed); // Reflection: the serialized event a prefab would carry
                }
                changed.AddListener(seen.Add);

                slot.Widget.SetInteractable(true);
                Assert.That(seen, Is.Empty, "already interactable");
                slot.Widget.SetInteractable(false);
                slot.Widget.SetInteractable(false);
                slot.Widget.SetInteractable(true);

                Assert.That(seen, Is.EqualTo(new[] { false, true }));
            });
        }

        // 0x507D50 publishes eventId under UIModernMessage(8, OnClick) on the auto-created exchange, broadcast Both (3).
        [Test]
        public void TheSlotPublishesItsEventUnderOnClick()
        {
            Owned.Run(owned =>
            {
                string name = ProcessManager.GetDefaultName<MessageExchangeBoundCallbackArg<UIModernMessage>>();
                owned.LeaseActions(SystemAction.Shutdown);
                var row = owned.Lease(new VerificationFixtureOwnership.RegistryRow(name));
                var exchange = new MessageExchangeBoundCallbackArg<UIModernMessage>();
                row.Register(exchange);
                ButtonSlot slot = NewButtonSlot(owned, false);
                UIModernEvent eventId = owned.Track(ScriptableObject.CreateInstance<UIModernEvent>());
                var received = new List<(UIModernEventType, UIModernEvent)>();
                var unsubscribe = new List<Action>();
                foreach (UIModernEventType type in Enum.GetValues(typeof(UIModernEventType)))
                {
                    UIModernEventType subscribed = type;
                    var message = new UIModernMessage(type);
                    MessageCallback<UIModernEvent> callback = (in UIModernEvent value) => received.Add((subscribed, value));
                    exchange.SubscribeToMessage(in message, callback);
                    unsubscribe.Add(() => exchange.UnsubscribeFromMessage(in message, callback));
                }
                try
                {
                    slot.Widget.Action_PublishUIMessage(eventId);
                }
                finally
                {
                    foreach (Action step in unsubscribe) step();
                }

                Assert.That(received.Count, Is.EqualTo(1));
                Assert.That(received[0].Item1, Is.EqualTo(UIModernEventType.OnClick));
                Assert.That(received[0].Item2, Is.SameAs(eventId));
            });
        }

        private sealed class ImageSlot
        {
            public UIWidgetImageDreamPowerSlot Widget;
            public Animation Animation;
            public AnimationClip Equip, Unequip, Enabled, Disabled;
        }

        private static ImageSlot NewImageSlot(Owned owned)
        {
            GameObject host = owned.Host("Lucid dream power slot image", false);
            var slot = new ImageSlot { Animation = host.AddComponent<Animation>() };
            slot.Animation.playAutomatically = false;
            slot.Widget = host.AddComponent<UIWidgetImageDreamPowerSlot>();
            slot.Equip = AddClip(owned, slot.Animation, "image equip");
            slot.Unequip = AddClip(owned, slot.Animation, "image unequip");
            slot.Enabled = AddClip(owned, slot.Animation, "image enabled");
            slot.Disabled = AddClip(owned, slot.Animation, "image disabled");
            Set(slot.Widget, "m_animation", slot.Animation); // Reflection: serialized reference the editor assigns
            Set(slot.Widget, "m_equipClip", slot.Equip); // Reflection: authored serialized field
            Set(slot.Widget, "m_unequipClip", slot.Unequip); // Reflection: authored serialized field
            Set(slot.Widget, "m_enabledClip", slot.Enabled); // Reflection: authored serialized field
            Set(slot.Widget, "m_disabledClip", slot.Disabled); // Reflection: authored serialized field
            host.SetActive(true);
            return slot;
        }

        // 0x508220: the icon is requested, the equip clip played by name (0x5082C9), the debug name reset (0x5082F7).
        [Test]
        public void TheImageSlotEquipShowsTheIconAndPlaysTheEquipClip()
        {
            Owned.Run(owned =>
            {
                ImageSlot slot = NewImageSlot(owned);
                Sprite icon = NewSprite(owned);
                DreamPowerDefinition definition = NewDefinition(owned, "lucid-image-power", Strings.MISSING, icon);
                slot.Widget.DebugName.Value = "stale";

                slot.Widget.Equip(definition);

                Assert.That(slot.Widget.Icon.Value, Is.SameAs(icon));
                Assert.That(slot.Widget.DebugName.Value, Is.EqualTo(string.Empty));
                Assert.That(slot.Animation.IsPlaying(slot.Equip.name), Is.True);
            });
        }

        // 0x508310: the icon is cleared first (0x508346), then the unequip clip plays and the debug name is reset.
        [Test]
        public void TheImageSlotUnequipClearsTheIconAndPlaysTheUnequipClip()
        {
            Owned.Run(owned =>
            {
                ImageSlot slot = NewImageSlot(owned);
                slot.Widget.Equip(NewDefinition(owned, "lucid-image-power", Strings.MISSING, NewSprite(owned)));
                slot.Widget.DebugName.Value = "stale";

                slot.Widget.Unequip();

                Assert.That(slot.Widget.Icon.Value, Is.Null);
                Assert.That(slot.Widget.DebugName.Value, Is.EqualTo(string.Empty));
                Assert.That(slot.Animation.IsPlaying(slot.Unequip.name), Is.True);
                Assert.That(slot.Animation.IsPlaying(slot.Equip.name), Is.False, "same-layer playback replaces the equip clip");
            });
        }

        // Enable 0x5083A0 reads the enabled clip's name (+0x38) and Disable 0x5083D0 the disabled clip's (+0x40).
        [Test]
        public void TheImageSlotEnableAndDisablePlayTheirOwnClips()
        {
            Owned.Run(owned =>
            {
                ImageSlot slot = NewImageSlot(owned);

                slot.Widget.Disable();
                Assert.That(slot.Animation.IsPlaying(slot.Disabled.name), Is.True);
                Assert.That(slot.Animation.IsPlaying(slot.Enabled.name), Is.False);

                slot.Widget.Enable();
                Assert.That(slot.Animation.IsPlaying(slot.Enabled.name), Is.True);
                Assert.That(slot.Animation.IsPlaying(slot.Disabled.name), Is.False);
            });
        }

        // Setup 0x69DC00: with a power the empty flag is cleared (0x69DCAB), the name copied from definition +0x44 and the icon
        // loaded (the wrapper's refcount rises to one); OnDestroy 0x69DD40 then releases that load.
        [Test]
        public void ThePowerDescriptionShowsTheNameAndIconAndReleasesThemOnDestroy()
        {
            Owned.Run(owned =>
            {
                var widget = owned.Host("Lucid dream power description", false).AddComponent<UIWidgetDreamPowerDescription>();
                Sprite icon = NewSprite(owned);
                DreamPowerDefinition definition = NewDefinition(owned, "lucid-description-power", Strings.CULTURE_NUMBER_MILLIONS, icon);
                widget.Empty.Value = true;

                widget.Setup(new UIWidgetDreamPowerDescriptionParameters(definition));

                Assert.That(widget.Empty.Value, Is.False);
                Assert.That(widget.Name.Value, Is.EqualTo(Strings.CULTURE_NUMBER_MILLIONS));
                Assert.That(widget.Icon.Value, Is.SameAs(icon));
                Assert.That(Get<int>(definition.IconAsset, "m_refCount"), Is.EqualTo(1));

                Call(widget, "OnDestroy");

                Assert.That(Get<int>(definition.IconAsset, "m_refCount"), Is.Zero);
                Assert.That(Get<Sprite>(definition.IconAsset, "m_loadedAsset"), Is.Null);
            });
        }

        // Without a power (0x69DCA9..0x69DD20) only the empty flag is set; the name and icon are left alone and
        // OnDestroy has nothing to release.
        [Test]
        public void ThePowerDescriptionWithoutAPowerOnlySetsTheEmptyFlag()
        {
            Owned.Run(owned =>
            {
                var widget = owned.Host("Lucid dream power description", false).AddComponent<UIWidgetDreamPowerDescription>();
                widget.Name.Value = Strings.CULTURE_NUMBER_MILLIONS;

                widget.Setup(new UIWidgetDreamPowerDescriptionParameters(null));

                Assert.That(widget.Empty.Value, Is.True);
                Assert.That(widget.Name.Value, Is.EqualTo(Strings.CULTURE_NUMBER_MILLIONS));
                Assert.That(widget.Icon.Value, Is.Null);
                Assert.DoesNotThrow(() => Call(widget, "OnDestroy"));
            });
        }

        // OnDestroy 0x69DD40 tests the parameters (0x69DD69) before the definition, so a widget that was never set up releases nothing.
        [Test]
        public void ThePowerDescriptionDestroyedBeforeSetupReleasesNothing()
        {
            Owned.Run(owned =>
            {
                var widget = owned.Host("Lucid dream power description", false).AddComponent<UIWidgetDreamPowerDescription>();

                Assert.DoesNotThrow(() => Call(widget, "OnDestroy"));
            });
        }
    }
}
