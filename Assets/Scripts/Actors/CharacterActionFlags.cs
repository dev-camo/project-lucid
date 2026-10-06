using System;
using System.Collections.Generic;
using Hardlight.Utils;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000314. GameAction values are hashes, so bits use
    // Enum.GetValues ordering rather than the enum's numeric values themselves.
    public struct CharacterActionFlags
    {
        private BitFlags32 m_value;
        public static int ActionCount { get; private set; }
        private static GameAction[] m_actionIndices;
        private static readonly Dictionary<GameAction, int> m_actionMapping = new Dictionary<GameAction, int>();

        public static void Initialise()
        {
            m_actionIndices = (GameAction[])Enum.GetValues(typeof(GameAction));
            ActionCount = m_actionIndices.Length;
            for (int i = 0; i < ActionCount; ++i) m_actionMapping[m_actionIndices[i]] = i;
        }
        public static GameAction GetAction(int actionIndex) { return m_actionIndices[actionIndex]; }
        public bool GetValue(GameAction action)
        {
            if (m_actionMapping.TryGetValue(action, out int index)) return m_value.IsIndexSet(index);
            throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
        public void SetValue(GameAction action, bool value)
        {
            if (m_actionMapping.TryGetValue(action, out int index)) { m_value.SetIndex(index, value); return; }
            throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
        public int ToInt() { return m_value.ToInt(); }
        public void Clear() { m_value.Clear(); }
        public static implicit operator CharacterActionFlags(int value)
        {
            return new CharacterActionFlags { m_value = value };
        }
    }
}
