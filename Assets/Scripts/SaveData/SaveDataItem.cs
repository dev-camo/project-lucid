using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class SaveDataItem
    {
        private bool m_dirty;
        private bool m_savingEnabled = true;
        // Game.Runtime.dll 0x06002c3c.
        protected SaveDataItem()
        {
            // Both original architectures set the field before Object..ctor;
            // the field initializer above preserves that ordering.
        }

        // Game.Runtime.dll 0x06002c33.
        public void MarkSaved()
        {
            if (!m_savingEnabled)
                return;
            // 0x06002c33 clears the parent before invoking each child.
            m_dirty = false;
            IterateChildren(childItem => childItem.MarkSaved());
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
            bool dirty = false;
            // 0x06002c36 queries every child, even after a true result.
            IterateChildren(childItem => dirty |= childItem.HasChangesToSave());
            m_dirty = dirty;
            return dirty;
        }

        protected abstract void IterateChildren(Action<SaveDataItem> action);
        // Game.Runtime.dll 0x06002c38.
        public virtual void Initialise()
        {
            IterateChildren(childItem => childItem.Initialise());
        }

        // Original 0x06002c39, ARM 0x5c0cc8; the saving-enabled test
        // precedes registry lookup. The missing system is not guarded.
        public void RequestSave()
        {
            if (!m_savingEnabled) return;
            ProcessManager.GetSystem<SaveManager>().RequestSave();
        }

        // Game.Runtime.dll 0x06002c3a.
        protected static void ListToDictionary<TKey, TValue>(List<TValue> sourceList, Dictionary<TKey, TValue> targetDictionary, Func<TValue, TKey> keyFunc)
        {
            targetDictionary.Clear();
            foreach (TValue item in sourceList)
                targetDictionary[keyFunc(item)] = item;
        }

        // Game.Runtime.dll 0x06002c3b.
        protected static void DictionaryToList<TKey, TValue>(Dictionary<TKey, TValue> sourceDictionary, ref List<TValue> targetList)
        {
            if (targetList == null)
                targetList = new List<TValue>(sourceDictionary.Count);
            else
                targetList.Clear();
            foreach (KeyValuePair<TKey, TValue> pair in sourceDictionary)
            {
                pair.Deconstruct(out TKey key, out TValue value);
                targetList.Add(value);
            }
        }
    }
}
