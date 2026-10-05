using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class SaveDataItem
    {
        private bool m_dirty;
        private bool m_savingEnabled;
        // Game.Runtime.dll 0x06002c3c.
        protected SaveDataItem()
        {
            m_savingEnabled = true;
        }

        // Game.Runtime.dll 0x06002c33.
        public void MarkSaved()
        {
            if (!m_savingEnabled)
                return;
            // 0x06002c33 clears the parent before invoking each child.
            m_dirty = false;
            IterateChildren(child => child.MarkSaved());
        }

        // Game.Runtime.dll 0x06002c34.
        public void MarkDirty()
        {
            m_dirty = true;
        }

        // Game.Runtime.dll 0x06002c35.
        public void DisableSaving()
        {
            m_savingEnabled = false;
        }

        // Game.Runtime.dll 0x06002c36.
        public bool HasChangesToSave()
        {
            if (!m_savingEnabled)
                return false;
            if (m_dirty)
                return true;
            bool hasChanges = false;
            // 0x06002c36 queries every child, even after a true result.
            IterateChildren(child => hasChanges |= child.HasChangesToSave());
            m_dirty = hasChanges;
            return hasChanges;
        }

        protected abstract void IterateChildren(Action<SaveDataItem> action);
        // Game.Runtime.dll 0x06002c38.
        public virtual void Initialise()
        {
            IterateChildren(child => child.Initialise());
        }

        // Game.Runtime.dll 0x06002c3a.
        protected static void ListToDictionary<TKey, TValue>(List<TValue> list, Dictionary<TKey, TValue> dict, Func<TValue, TKey> keyGetter)
        {
            dict.Clear();
            foreach (TValue item in list)
                dict[keyGetter(item)] = item;
        }

        // Game.Runtime.dll 0x06002c3b.
        protected static void DictionaryToList<TKey, TValue>(Dictionary<TKey, TValue> dict, ref List<TValue> list)
        {
            if (list == null)
                list = new List<TValue>(dict.Count);
            else
                list.Clear();
            foreach (KeyValuePair<TKey, TValue> pair in dict)
                list.Add(pair.Value);
        }
    }
}
