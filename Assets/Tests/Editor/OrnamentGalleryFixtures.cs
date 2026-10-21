using System;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using UnityEngine;
using UnityEngine.UI;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The scene wiring of one ornament thumbnail toggle: the images, sprites and marker the inspector assigns.
    internal sealed class OrnamentToggleRig
    {
        public UIWidgetOrnamentToggle Toggle;
        public UIToggle UiToggle;
        public UIToggleGroupElement Element;
        public Image Background, Border, Outline;
        public RawImage Ornament;
        public GameObject NewIcon;
        public Sprite BackgroundLocked, BackgroundUnlocked, BorderLocked, BorderUnlocked;
        public Color LockedColour;
    }

    // Shared setup for the ornament gallery fixtures: real ornament definitions and thumbnail toggles built on inactive owned hosts.
    internal static class OrnamentGalleryFixtures
    {
        internal static readonly Color LockedColour = new Color(0.25f, 0.5f, 0.75f, 1f);

        internal static OrnamentManager.OrnamentStatus Status(bool unlocked, bool seen, bool collected) =>
            new OrnamentManager.OrnamentStatus(unlocked, seen, collected);

        // An ornament definition built through its constructor, its authored fields set by Unity's serializer. The thumbnail wrapper
        // (built by OnEnable over the unassigned reference) already holds its texture, so no Addressables load is made.
        internal static OrnamentDefinition NewDefinition(Owned owned, string guid, OrnamentCategory category, OrnamentIdentifier identifier,
            out Texture2D texture, Strings name = Strings.NONE, Strings lore = Strings.NONE, Strings tip = Strings.NONE, int act = 0)
        {
            OrnamentDefinition definition = Create<OrnamentDefinition>(owned,
                "{\"m_guid\":\"" + guid + "\",\"m_identifier\":" + (int)identifier + ",\"m_category\":" + (int)category +
                ",\"m_localisedName\":" + (int)name + ",\"m_localisedUnlockedLore\":" + (int)lore +
                ",\"m_localisedLockedTip\":" + (int)tip + ",\"m_actNumber\":" + act + "}");
            texture = owned.Track(new Texture2D(2, 2));
            Cache(definition.ThumbnailAsset, texture);
            return definition;
        }

        internal static OrnamentIdentifier Identifier(int index) => ((OrnamentIdentifier[])Enum.GetValues(typeof(OrnamentIdentifier)))[index + 1];

        // A toggle with every serialized reference the original prefab assigns. The reflection writes stand for that authored wiring
        // (the editor Reset would only fill the toggle itself).
        internal static OrnamentToggleRig NewToggleRig(Owned owned, HLAudioSfxComponent sound)
        {
            var rig = new OrnamentToggleRig { LockedColour = LockedColour };
            GameObject host = owned.Host("Lucid ornament toggle", false);
            rig.Toggle = host.AddComponent<UIWidgetOrnamentToggle>();
            rig.UiToggle = host.GetComponent<UIToggle>();
            Set(rig.Toggle, "m_toggle", rig.UiToggle);
            rig.Element = host.AddComponent<UIToggleGroupElement>();
            Set(rig.Element, "m_toggle", rig.UiToggle);
            rig.Background = owned.Host("background", false, host.transform).AddComponent<Image>();
            rig.Border = owned.Host("border", false, host.transform).AddComponent<Image>();
            rig.Outline = owned.Host("outline", false, host.transform).AddComponent<Image>();
            rig.Ornament = owned.Host("ornament", false, host.transform).AddComponent<RawImage>();
            rig.NewIcon = owned.Host("new unlock icon", true, host.transform);
            Set(rig.Toggle, "m_backgroundImage", rig.Background);
            Set(rig.Toggle, "m_backgroundSpriteLocked", rig.BackgroundLocked = NewSprite(owned));
            Set(rig.Toggle, "m_backgroundSpriteUnlocked", rig.BackgroundUnlocked = NewSprite(owned));
            Set(rig.Toggle, "m_ornamentImage", rig.Ornament);
            Set(rig.Toggle, "m_ornamentImageLockedColour", LockedColour);
            Set(rig.Toggle, "m_borderImage", rig.Border);
            Set(rig.Toggle, "m_borderSpriteLocked", rig.BorderLocked = NewSprite(owned));
            Set(rig.Toggle, "m_borderSpriteUnlocked", rig.BorderUnlocked = NewSprite(owned));
            Set(rig.Toggle, "m_selectedOutline", rig.Outline);
            Set(rig.Toggle, "m_groupElement", rig.Element);
            Set(rig.Toggle, "m_newUnlockIcon", rig.NewIcon);
            Set(rig.Toggle, "m_sfxComponent", sound);
            return rig;
        }
    }
}
