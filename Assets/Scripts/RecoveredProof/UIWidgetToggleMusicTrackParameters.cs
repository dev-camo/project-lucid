using System;
using Hardlight;
using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime020009d8, all ten readonly fields and its one genuine constructor.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIWidgetToggleMusicTrackParameters : IUIWidgetParameters
    {
        public readonly int Number;
        public readonly ManagedAddressableAsset<Sprite> Image;
        public readonly Strings Name;
        public readonly Strings Composer;
        public readonly bool Selected;
        public readonly bool Locked;
        public readonly bool IsNew;
        public readonly UIToggleGroup ToggleGroup;
        public readonly UnityAction<bool> OnValueChanged;
        public readonly Action<RectTransform> OnHighlighted;

        // Original060038ae: Object constructor first; Locked is assigned before Selected on both CPUs.
        public UIWidgetToggleMusicTrackParameters(int number, ManagedAddressableAsset<Sprite> image,
            Strings name, Strings composer, bool selected, bool locked, bool isNew,
            UIToggleGroup toggleGroup, UnityAction<bool> onValueChanged, Action<RectTransform> onHighlighted)
        {
            Number = number;
            Image = image;
            Name = name;
            Composer = composer;
            Locked = locked;
            Selected = selected;
            IsNew = isNew;
            ToggleGroup = toggleGroup;
            OnValueChanged = onValueChanged;
            OnHighlighted = onHighlighted;
        }
    }
}
