using System;
using System.Text;

namespace Hardlight
{
    public readonly struct ScopedStringBuilder : IDisposable
    {
        private readonly StringBuilder m_stringBuilder;
        private readonly Action<StringBuilder> m_disposeCallback;

        // HLUnityCore.Runtime.dll:Hardlight.ScopedStringBuilder:0x06000a97;
        // arm64 0x1b07f5c, null builder reports zero.
        public int Length => m_stringBuilder == null ? 0 : m_stringBuilder.Length;

        // Original token 0x06000a98; arm64 0x1b079f0 clears the supplied builder.
        public ScopedStringBuilder(StringBuilder stringBuilder, Action<StringBuilder> disposeCallback)
        {
            m_stringBuilder = stringBuilder;
            if (m_stringBuilder != null) m_stringBuilder.Clear();
            m_disposeCallback = disposeCallback;
        }

        // Original token 0x06000a99; arm64 0x1b07f70.
        public void Append(string text)
        {
            if (m_stringBuilder != null) m_stringBuilder.Append(text);
        }

        // Original token 0x06000a9a; arm64 0x1b07f84.
        public void AppendLine(string text = null)
        {
            if (m_stringBuilder != null) m_stringBuilder.AppendLine(text);
        }

        // Original token 0x06000a9b; arm64 0x1b07f98. A null builder is
        // still passed to the callback; repeated disposal repeats the callback.
        public void Dispose()
        {
            if (m_disposeCallback != null) m_disposeCallback(m_stringBuilder);
        }
    }
}
