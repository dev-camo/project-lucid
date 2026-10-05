using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "DataDefinitions", menuName = "HardlightProject/DefinitionData/Groups/All Definitions")]
    public class DataDefinitions : ScriptableObject
    {
        public const string AssetMenu = "HardlightProject/DefinitionData/";
        public const string DefinitionsMenu = "HardlightProject/DefinitionData/Definitions/";
        public const string GroupsMenu = "HardlightProject/DefinitionData/Groups/";
        public DefinitionDataType[] DataDefinitionsArray;

        // Game.Runtime 0x06001c29: retains the first dictionary and its comparer, then merges every later match.
        // The original helper inserts through ICollection.Add, preserving duplicate-key and enumeration failures.
        public Dictionary<TKey, TData> Get<TKey, TData>() where TData : ScriptableObject
        {
            Dictionary<TKey, TData> result = null;
            foreach (var definition in DataDefinitionsArray)
            {
                if (!(definition is DefinitionDataType<TKey, TData> group)) continue;
                var data = group.GetData();
                if (result == null) result = data;
                else result.AddRange(data);
            }
            return result;
        }

        // Game.Runtime 0x06001c2a: first CLR type match, including a destroyed Unity object; no Unity null operator.
        public T GetGroup<T>() where T : ScriptableObject
        {
            foreach (var definition in DataDefinitionsArray)
                if (definition is T) return (T)(object)definition;
            return null;
        }

        // Game.Runtime 0x06001c2b: ScriptableObject base only; the authored array remains null initially.
        public DataDefinitions() { }
    }
}
