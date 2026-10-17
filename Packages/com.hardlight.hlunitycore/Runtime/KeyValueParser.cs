using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
	// HLUnityCore.Runtime TypeDefIndex 9387 (0x0200027c), methods 0x06000f8e..0x06000f94;
	// bodies from the 1.10.1 x86-64 binary (0x1b1f550..0x1b1f7c0). A fluent parser for
	// "key=value" lines.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class KeyValueParser
	{
		public const char DefaultEntrySeparator = '\n';

		public const char DefaultKeyValueSeparator = '=';

		// 0x1b1f7c0: the constructor stores true here and both separators; the
		// null-value flag keeps its default.
		private bool m_trimWhiteSpace = true;

		private bool m_trimNullValues;

		private char m_entrySeparator = DefaultEntrySeparator;

		private char m_keyValueSeparator = DefaultKeyValueSeparator;

		// 0x06000f8e.
		public KeyValueParser TrimWhiteSpace(bool shouldTrim)
		{
			m_trimWhiteSpace = shouldTrim;
			return this;
		}

		// 0x06000f8f.
		public KeyValueParser TrimNullValues(bool shouldTrim)
		{
			m_trimNullValues = shouldTrim;
			return this;
		}

		// 0x06000f90.
		public KeyValueParser SetEntrySeparator(char separator)
		{
			m_entrySeparator = separator;
			return this;
		}

		// 0x06000f91.
		public KeyValueParser SetKeyValueSeparator(char separator)
		{
			m_keyValueSeparator = separator;
			return this;
		}

		// 0x06000f92, 0x1b1f590: blank text gives an empty dictionary. The split uses
		// the char[] overload (String.Split 0x0600024f), not Split(char, options).
		public Dictionary<string, string> Parse(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return new Dictionary<string, string>();
			}
			return Parse(text.Split(new char[] { m_entrySeparator }));
		}

		// 0x06000f93, 0x1b1f650: blank lines are skipped and each line splits at the
		// first separator only. Later keys overwrite earlier ones. A line without a
		// separator stores a null value unless null values are trimmed. Whitespace
		// trimming covers the key and a present value.
		public Dictionary<string, string> Parse(string[] textLines)
		{
			Dictionary<string, string> result = new Dictionary<string, string>();
			if (textLines != null)
			{
				char[] separator = new char[] { m_keyValueSeparator };
				for (int i = 0; i < textLines.Length; i++)
				{
					if (string.IsNullOrWhiteSpace(textLines[i]))
					{
						continue;
					}

					string[] keyValue = textLines[i].Split(separator, 2);
					string key = keyValue[0];
					if (m_trimWhiteSpace)
					{
						key = key.Trim();
					}

					string value = keyValue.Length >= 2 ? keyValue[1] : null;
					if (m_trimWhiteSpace && value != null)
					{
						value = value.Trim();
					}

					if (value == null && m_trimNullValues)
					{
						continue;
					}
					result[key] = value;
				}
			}
			return result;
		}
	}
}
