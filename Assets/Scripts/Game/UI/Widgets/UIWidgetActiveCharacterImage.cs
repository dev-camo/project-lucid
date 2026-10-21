using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000999; bodies translated from the 1.10.1 x86-64 binary.
    // A button image that follows the active character's portrait.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIWidgetActiveCharacterImage : UIWidgetButtonImageBase
    {
        public readonly Bindable<Sprite> CharacterSprite = new Bindable<Sprite>();
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>();
        private CharacterDefinition m_characterDefinition;

        // 0x0600378e: the abstract base Start has an empty override.
        protected override void Start() { }

        // 0x0600378f
        private void OnEnable()
        {
            m_characterManagerRef.InvokeOnValid(OnCharacterManagerValid);
        }

        // 0x06003790: nothing is released when the character manager is gone.
        private void OnDisable()
        {
            if (m_characterManagerRef.IsNull())
                return;
            m_characterManagerRef.Get().ReleaseOnCharacterChange(OnCharacterChange);
            UnloadImage(); // Native inlines UnloadImage here.
        }

        // 0x06003791
        private void OnCharacterManagerValid(CharacterManager characterManager)
        {
            characterManager.RegisterOnCharacterChange(OnCharacterChange, true);
        }

        // 0x06003792: the previous portrait is unloaded first; the lookup writes the field even when it misses.
        private void OnCharacterChange(Character character)
        {
            UnloadImage(); // Native inlines UnloadImage here.
            if (m_dataManagerRef.Get().Characters.TryGetValue(character.IdType, out m_characterDefinition))
                CharacterSprite.Value = m_characterDefinition.Image.Load();
        }

        // 0x06003793
        private void UnloadImage()
        {
            if (m_characterDefinition != null)
                m_characterDefinition.Image.Unload();
        }

        // 0x06003794
        public UIWidgetActiveCharacterImage() { }
    }
}
