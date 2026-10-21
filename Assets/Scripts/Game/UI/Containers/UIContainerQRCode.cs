using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000919; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerQRCode : UIContainer
    {
        public readonly Bindable<string> URLText = new Bindable<string>();
        public readonly Bindable<Sprite> QRCodeSprite = new Bindable<Sprite>();
        [SerializeField] private SerializableDictionary<Strings, QRCodeSpriteLookUp> m_stringToQRCodeLookup = new SerializableDictionary<Strings, QRCodeSpriteLookUp>(Hardlight.Enums.EnumComparers.StringsComparer);
        private UIContainerQRCodeParameters m_parameters;

        // 0x0600348e (0x678530)
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            m_parameters = parameters.GetAs<UIContainerQRCodeParameters>();
            UpdateBinders();
        }

        // 0x0600348f (0x6785A0): shows the localised address and, when the string has a code, the sprite of the first
        // override for the current string language, otherwise the code's default sprite. Without an entry the sprite is untouched.
        private void UpdateBinders()
        {
            Strings urlStringId = m_parameters.URLStringId;
            URLText.Value = StringTable.GetString(urlStringId);
            if (!m_stringToQRCodeLookup.TryGetValue(urlStringId, out QRCodeSpriteLookUp lookUp))
                return;

            Sprite sprite = lookUp.DefaultSprite;
            Languages language = StringTable.StringsLanguage;
            foreach (LanguageToSpritePair pair in lookUp.LanguageSpriteOverride)
            {
                if (pair.Language == language)
                {
                    sprite = pair.Sprite;
                    break;
                }
            }

            QRCodeSprite.Value = sprite;
        }

        // 0x06003490 (0x6787F0)
        public UIContainerQRCode()
        {
        }

        [Serializable]
        private struct QRCodeSpriteLookUp
        {
            public Sprite DefaultSprite;
            public List<LanguageToSpritePair> LanguageSpriteOverride;
        }

        [Serializable]
        private struct LanguageToSpritePair
        {
            public Languages Language;
            public Sprite Sprite;
        }
    }
}
