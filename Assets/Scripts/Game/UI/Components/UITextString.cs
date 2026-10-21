using System;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.Utils;
using TMPro;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000a3c; bodies translated from the 1.10.1 x86-64 binary.
    // A localised string shown through a TextMeshPro component, with optional format arguments.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Serializable]
    public class UITextString
    {
        [Tooltip("Id for the localised string.")]
        [SerializeField] [HashEnum(null)] private Strings m_stringID;

        [SerializeField]
        [Tooltip("Text component to set with string.")]
        private TextMeshProUGUI m_textMeshPro;

        private string m_string;

        // 0x06003a8b
        public UITextString(Strings stringID)
        {
            m_stringID = stringID;
        }

        // 0x06003a8c: looks the localised format string up.
        public void Initialise()
        {
            m_string = StringTable.GetString(m_stringID);
        }

        // 0x06003a8d: formats the string looked up by Initialise (a null argument array throws).
        public void Apply(object[] args)
        {
            m_textMeshPro.text = string.Format(m_string, args);
        }
    }
}
