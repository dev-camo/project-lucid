using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009b4; bodies translated from the 1.10.1 x86-64 binary.
    // Shows either a character archetype icon (normal, alternate or silhouette) or a character's
    // render, loading the sprite on setup and unloading it on destruction.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIWidgetImageArchetype : UIWidget
    {
        [SerializeField] private ImageType m_imageType;
        public readonly Bindable<Sprite> ArchetypeSprite = new Bindable<Sprite>();
        public readonly Bindable<Sprite> CharacterSprite = new Bindable<Sprite>();
        public CharacterArchetype Archetype { get; private set; }
        private UIWidgetImageArchetypeParameters m_parameters;

        // 0x06003806: an archetype definition wins over a character definition. The archetype
        // is recorded even when its image type has no icon.
        public override void Setup(IUIWidgetParameters parameters)
        {
            base.Setup(parameters);
            m_parameters = parameters.GetAs<UIWidgetImageArchetypeParameters>();
            Archetype = CharacterArchetype.None;
            CharacterArchetypeDefinition archetypeDefinition = m_parameters.CharacterArchetypeDefinition;
            CharacterDefinition characterDefinition = m_parameters.CharacterDefinition;
            if (archetypeDefinition != null)
            {
                Archetype = archetypeDefinition.CharacterArchetype;
                ManagedAddressableAsset<Sprite> icon = GetUIIcon(archetypeDefinition);
                if (icon != null)
                    ArchetypeSprite.Value = icon.Load();
            }
            else if (characterDefinition != null)
            {
                CharacterSprite.Value = characterDefinition.Render.Load();
            }
        }

        // 0x06003807
        private ManagedAddressableAsset<Sprite> GetUIIcon(CharacterArchetypeDefinition archetypeDefinition)
        {
            switch (m_imageType)
            {
                case ImageType.Normal: return archetypeDefinition.UIImage;
                case ImageType.Alternate: return archetypeDefinition.AlternateImage;
                case ImageType.Silhouette: return archetypeDefinition.SilhouetteImage;
                default: return null;
            }
        }

        // 0x06003808: releases whatever Setup loaded.
        private void OnDestroy()
        {
            if (m_parameters == null)
                return;
            if (m_parameters.CharacterArchetypeDefinition != null)
            {
                ManagedAddressableAsset<Sprite> icon = GetUIIcon(m_parameters.CharacterArchetypeDefinition);
                if (icon != null)
                    icon.Unload();
            }
            else if (m_parameters.CharacterDefinition != null)
            {
                m_parameters.CharacterDefinition.Render.Unload();
            }
        }

        // 0x06003809
        public UIWidgetImageArchetype() { }

        private enum ImageType
        {
            Normal = 0,
            Alternate = 1,
            Silhouette = 2
        }
    }
}
