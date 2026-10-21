using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200099f; bodies translated from the 1.10.1 x86-64 binary.
    // A button showing a character's head image, with an optional swap icon.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIWidgetButtonImageCharacter : UIWidgetButtonImageBase
    {
        [SerializeField] private CharacterDefinition m_character;
        [SerializeField] private GameObject m_swapIcon;
        public CharacterId CharacterId { get; private set; }

        // 0x060037b4: the base setup runs first (and logs its own error for non button data).
        // Data of the wrong type only logs.
        public override void Setup(IUIWidgetParameters parameters)
        {
            base.Setup(parameters);
            if (!(parameters is UIWidgetButtonCharacterImageParameters data))
            {
                HLOutput.LogError("Could not set up UIWidgetButtonImageCharacter with given data.");
                return;
            }
            m_character = data.CharacterDefinition;
            CharacterId = m_character.Id;
            if (m_swapIcon != null)
                m_swapIcon.SetActive(data.ShowSwapIcon);
            SetCharacterImage();
        }

        // 0x060037b5
        protected override void Start()
        {
            SetCharacterImage();
        }

        // 0x060037b6
        private void SetCharacterImage()
        {
            if (m_character != null)
                Image.Value = m_character.Image.Load();
        }

        // 0x060037b7: releases the image loaded for the character.
        private void OnDestroy()
        {
            if (m_character == null)
                return;
            m_character.Image.Unload();
        }

        // 0x060037b8: the base constructors allocate the Image bindable.
        public UIWidgetButtonImageCharacter() { }
    }
}
