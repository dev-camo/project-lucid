using System;
using Hardlight;
using Hardlight.Enums;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class MetaGameUnlockBase : IMetaGameUnlock
    {
        private MetaGameUnlockDefinition m_definition;

        // Original Game.Runtime06002a55/56/57: true abstract APIs, no native body.
        public abstract string GetId();
        public abstract PlayerProgressionTypes GetProgressionType();
        public abstract UIWidgetProgression GetWidget();

        // Original06002a58/59/5a: nonvirtual interface implementation forwards through the cache.
        public Strings GetTitle() => GetUnlockDefinition().Title;
        public Strings GetBody() => GetUnlockDefinition().Body;
        public HLAudioClipIdentifier GetAudio() => GetUnlockDefinition().UnlockAudio;

        // Original06002a5b: true abstract save API.
        public abstract void Save(SaveManager saveManager);

        // Original06002a5c/5d: optional callback receives null without a provider load.
        public virtual void GetCustomTexture(Action<Texture> onLoaded)
        {
            onLoaded?.Invoke(null);
        }

        public virtual void GetCustomSprite(Action<Sprite> onLoaded)
        {
            onLoaded?.Invoke(null);
        }

        // Original06002a5e: authentic empty body.
        public virtual void ReleaseAssets()
        {
        }

        // Original06002a5f: typed GUID-object inequality, dictionary read before virtual key evaluation.
        private MetaGameUnlockDefinition GetUnlockDefinition()
        {
            if (m_definition != null)
                return m_definition;
            m_definition = ProcessManager.GetSystem<DataManager>().MetaGameUnlockDefinitions[GetProgressionType()];
            return m_definition;
        }

        // Original06002a60/61: both original default values are false.
        public virtual bool CanExitToMenu() => false;
        public virtual bool UsesAlternativeExitToMenuEvent() => false;

        // Original06002a62: base-only protected constructor.
        protected MetaGameUnlockBase()
        {
        }
    }
}
