using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SaveDataCharacterCustomisation : SaveDataItem
    {
        [SerializeField]
        private CharacterId m_characterId;

        [SerializeField]
        private string m_skinGUID;

        // Game.Runtime.dll 0x06002ba9.
        public CharacterId CharacterId
        {
            get { return m_characterId; }
        }

        // Game.Runtime.dll 0x06002baa.
        public string SkinGUID
        {
            get { return m_skinGUID; }
        }

        // Game.Runtime.dll 0x06002bab.
        public SaveDataCharacterCustomisation(CharacterId characterId)
        {
            m_characterId = characterId;
        }

        // Game.Runtime.dll 0x06002bac.
        public void ResolveNewData(SaveDataCharacterCustomisation newSaveData)
        {
            m_skinGUID = newSaveData.m_skinGUID;
        }

        // Game.Runtime.dll 0x06002bad.
        public void Set(CharacterSkinDefinition skinDefinition)
        {
            m_skinGUID = skinDefinition.GetGUID();
            MarkDirty();
        }

        // Game.Runtime.dll 0x06002bae.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
            // Original ARM64 body is a single RET: this record has no children.
        }

    }
}
