using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SaveDataDebug : SaveDataItem, ISerializationCallbackReceiver
    {
        [SerializeField]
        private bool m_unlockLevels;
        [SerializeField]
        private bool m_unlockDreamPowers;
        [SerializeField]
        private bool m_gameCenterDebugIsLoggedIn;
        [SerializeField]
        private bool m_gameCenterDebugIsUnderage;
        [SerializeField]
        private int m_gameCenterDebugAccountIDIndex;
        // Game.Runtime.dll 0x06002bbf.
        public bool UnlockLevels
        {
            get
            {
                return m_unlockLevels;
            }

            // 0x06002bc0
            set
            {
                m_unlockLevels = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002bc1.
        public bool UnlockDreamPowers
        {
            get
            {
                return m_unlockDreamPowers;
            }

            // 0x06002bc2
            set
            {
                m_unlockDreamPowers = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002bc3.
        public bool GameCenterDebugIsLoggedIn
        {
            get
            {
                return m_gameCenterDebugIsLoggedIn;
            }

            // 0x06002bc4
            set
            {
                m_gameCenterDebugIsLoggedIn = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002bc5.
        public bool GameCenterDebugIsUnderage
        {
            get
            {
                return m_gameCenterDebugIsUnderage;
            }

            // 0x06002bc6
            set
            {
                m_gameCenterDebugIsUnderage = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002bc7.
        public int GameCenterDebugAccountIDIndex
        {
            get
            {
                return m_gameCenterDebugAccountIDIndex;
            }

            // 0x06002bc8
            set
            {
                m_gameCenterDebugAccountIDIndex = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002bc9.
        public SaveDataDebug()
        {
            UnlockLevels = false;
            GameCenterDebugIsLoggedIn = false;
            GameCenterDebugIsUnderage = true;
            GameCenterDebugAccountIDIndex = 0;
        }

        // Game.Runtime.dll 0x06002bca.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
        // 0x06002bca has no additional native work.
        }

        // Game.Runtime.dll 0x06002bcb.
        public void OnBeforeSerialize()
        {
        // 0x06002bcb has no additional native work.
        }

        // Game.Runtime.dll 0x06002bcc.
        public void OnAfterDeserialize()
        {
        // 0x06002bcc has no additional native work.
        }

        // Game.Runtime.dll 0x06002bcd.
        public void CopyFrom(SaveDataDebug other)
        {
            UnlockLevels = other.UnlockLevels;
            UnlockDreamPowers = other.UnlockDreamPowers;
            GameCenterDebugIsLoggedIn = other.GameCenterDebugIsLoggedIn;
            GameCenterDebugIsUnderage = other.GameCenterDebugIsUnderage;
            GameCenterDebugAccountIDIndex = other.GameCenterDebugAccountIDIndex;
        }
    }
}
