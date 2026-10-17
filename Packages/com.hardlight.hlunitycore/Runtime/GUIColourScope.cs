using System;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x0200026d, methods 0x06000f68..0x06000f6a.
    // All three colours are captured first; a null argument re-applies the
    // captured colour, and Dispose restores all three.
    public readonly struct GUIColourScope : IDisposable
    {
        private readonly Color m_originalColour;
        private readonly Color m_originalContentColour;
        private readonly Color m_originalBackgroundColour;

        // 0x06000f68.
        public GUIColourScope(bool condition, Color? newColour, Color? newContentColour, Color? newBackgroundColour)
        {
            m_originalColour = GUI.color;
            m_originalContentColour = GUI.contentColor;
            m_originalBackgroundColour = GUI.backgroundColor;
            if (condition)
            {
                GUI.color = newColour ?? m_originalColour;
                GUI.contentColor = newContentColour ?? m_originalContentColour;
                GUI.backgroundColor = newBackgroundColour ?? m_originalBackgroundColour;
            }
        }

        // 0x06000f69: tail call into the conditional constructor with true.
        public GUIColourScope(Color? newColour, Color? newContentColour, Color? newBackgroundColour)
            : this(true, newColour, newContentColour, newBackgroundColour) { }

        // 0x06000f6a.
        public void Dispose()
        {
            GUI.color = m_originalColour;
            GUI.contentColor = m_originalContentColour;
            GUI.backgroundColor = m_originalBackgroundColour;
        }
    }
}
