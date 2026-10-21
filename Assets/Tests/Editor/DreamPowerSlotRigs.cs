using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using UnityEngine;
using UnityEngine.UI;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Owned scenery shared by the dream power slot and character select fixtures.
    internal static class DreamPowerSlotRigs
    {
        // A UITransitionAnimation on the host with a legacy clip for each given state, and a clip for each custom name,
        // all added to the host's Animation. The transition's Awake is run as the original scene would at start.
        internal sealed class Transitions
        {
            public Animation Animation;
            public UITransitionAnimation Transition;
            public readonly Dictionary<string, AnimationClip> Clips = new Dictionary<string, AnimationClip>();
        }

        internal static Transitions AddTransitions(Owned owned, GameObject host, UIInteractable interactable, UITransitionState[] states, string[] customClips)
        {
            var rig = new Transitions();
            rig.Animation = host.AddComponent<Animation>();
            rig.Animation.playAutomatically = false;
            rig.Transition = host.AddComponent<UITransitionAnimation>();
            var settings = new List<UITransitionAnimationSetting>();
            foreach (UITransitionState state in states)
            {
                AnimationClip clip = NewClip(owned, rig, "state " + state);
                var setting = new UITransitionAnimationSetting();
                Set(setting, "m_state", state); // Reflection: authored serialized field
                Set(setting, "m_animationClip", clip); // Reflection: authored serialized field
                settings.Add(setting);
            }
            foreach (string name in customClips) NewClip(owned, rig, name);
            Set(rig.Transition, "m_animation", rig.Animation); // Reflection: serialized reference the editor assigns
            Set(rig.Transition, "m_uiInteractable", interactable); // Reflection: serialized reference the editor assigns
            Set(rig.Transition, "m_settings", settings); // Reflection: authored serialized list
            host.SetActive(true);
            Call(rig.Transition, "Awake"); // Edit Mode does not schedule Awake for this component
            return rig;
        }

        private static AnimationClip NewClip(Owned owned, Transitions rig, string name)
        {
            AnimationClip clip = AddClip(owned, rig.Animation, name);
            rig.Clips[name] = clip;
            return clip;
        }

        // A legacy clip, named after the given label, added to the animation under its own name.
        internal static AnimationClip AddClip(Owned owned, Animation animation, string label)
        {
            var clip = owned.Track(new AnimationClip { name = "Lucid " + label, legacy = true });
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Constant(0f, 1f, 0f));
            animation.AddClip(clip, clip.name);
            return clip;
        }

        // A dream power whose icon wrapper already holds its sprite, so the asynchronous load completes at once.
        internal static DreamPowerDefinition NewDefinition(Owned owned, string guid, Strings name, Sprite icon)
        {
            var definition = owned.Track(ScriptableObject.CreateInstance<DreamPowerDefinition>());
            Set(definition, "m_guid", guid); // Reflection: authored GUID of a serialized definition
            Set(definition, "m_name", name); // Reflection: authored serialized field
            Set(definition.IconAsset, "m_loadedAsset", icon); // Reflection: the sprite an earlier Addressables load would hold
            return definition;
        }

        internal sealed class ButtonSlot
        {
            public UIWidgetButtonDreamPowerSlot Widget;
            public Button Button;
            public HLAudioSfxComponentButton Sfx;
            public Transitions Animations;
            public AnimationClip EquipClip, UnequipClip;
        }

        // A slot button with its Button, audio component and a transition holding Normal and Disabled states.
        // The audio row is leased once per case: pass false for the second and later slots of a case.
        internal static ButtonSlot NewButtonSlot(Owned owned, bool withTransition = true, bool leaseAudioRow = true)
        {
            if (leaseAudioRow) owned.LeaseRow<AudioManager>(); // The audio component's field initialiser caches this row
            GameObject host = owned.Host("Lucid dream power slot button", false);
            var slot = new ButtonSlot();
            slot.Button = host.AddComponent<Button>();
            slot.Sfx = host.AddComponent<HLAudioSfxComponentButton>();
            slot.Widget = host.AddComponent<UIWidgetButtonDreamPowerSlot>();
            Set(slot.Widget, "m_button", slot.Button); // Reflection: serialized reference the editor assigns
            Set(slot.Widget, "m_sfxComponentButton", slot.Sfx); // Reflection: serialized reference the editor assigns
            if (withTransition)
            {
                slot.Animations = AddTransitions(owned, host, slot.Widget, new[] { UITransitionState.Normal, UITransitionState.Disabled }, new[] { "equip", "unequip" });
                slot.EquipClip = slot.Animations.Clips["equip"];
                slot.UnequipClip = slot.Animations.Clips["unequip"];
                Set(slot.Widget, "m_transition", slot.Animations.Transition); // Reflection: serialized reference the editor assigns
                Set(slot.Widget, "m_equipClip", slot.EquipClip); // Reflection: authored serialized field
                Set(slot.Widget, "m_unequipClip", slot.UnequipClip); // Reflection: authored serialized field
            }
            else
            {
                host.SetActive(true);
            }
            return slot;
        }

        internal sealed class ImageSlot
        {
            public UIWidgetImageDreamPowerSlot Widget;
            public Animation Animation;
            public AnimationClip Equip, Unequip, Enabled, Disabled;
        }

        internal static ImageSlot NewImageSlot(Owned owned)
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
    }
}
