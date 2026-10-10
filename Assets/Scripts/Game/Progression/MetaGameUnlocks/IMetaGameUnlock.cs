using System;
using Hardlight.Enums;
using UnityEngine;

namespace HardlightProject
{
    // Game.Runtime.dll 0x0200078d, all twelve original abstract interface APIs.
    // These declarations contain no native method bodies or recovery credit.
    public interface IMetaGameUnlock
    {
        string GetId();
        PlayerProgressionTypes GetProgressionType();
        UIWidgetProgression GetWidget();
        Strings GetTitle();
        Strings GetBody();
        HLAudioClipIdentifier GetAudio();
        void Save(SaveManager saveManager);
        void GetCustomTexture(Action<Texture> onLoaded);
        void GetCustomSprite(Action<Sprite> onLoaded);
        void ReleaseAssets();
        bool CanExitToMenu();
        bool UsesAlternativeExitToMenuEvent();
    }
}
