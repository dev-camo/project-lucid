using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// HLUnityCore.Runtime TypeDefIndex 9297 (0x02000222), methods 0x06000ddc..0x06000de0;
	// bodies from the 1.10.1 x86-64 binary (0x1b133c0..0x1b134a0). Serialises any enum
	// value as its assembly-qualified type name and its ToString() text.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Serializable]
	public class SerialisableEnum : ISerializationCallbackReceiver
	{
		[SerializeField]
		private string m_enumType;

		[SerializeField]
		private string m_enumValue;

		private Enum m_value;

		// 0x06000ddc: the operand is not null-checked (NullChecks are off).
		public static implicit operator Enum(SerialisableEnum serialisableEnum)
		{
			return serialisableEnum.m_value;
		}

		// 0x06000ddd: the serialised strings stay unset until OnBeforeSerialize.
		public SerialisableEnum(Enum enumValue = null)
		{
			m_value = enumValue;
		}

		// 0x06000dde.
		public void Assign(Enum value)
		{
			m_value = value;
		}

		// 0x06000ddf: a null value writes null to both strings.
		void ISerializationCallbackReceiver.OnBeforeSerialize()
		{
			m_enumType = m_value?.GetType().AssemblyQualifiedName;
			m_enumValue = m_value?.ToString();
		}

		// 0x06000de0, 0x1b134a0: an unknown type is reported only when a value string
		// is present; either failure leaves the value null.
		void ISerializationCallbackReceiver.OnAfterDeserialize()
		{
			Type enumType = Type.GetType(m_enumType);
			if (enumType == null)
			{
				if (!string.IsNullOrEmpty(m_enumValue))
				{
					HLOutput.LogError("Could not find enum type: " + m_enumType);
				}
				m_value = null;
				return;
			}

			if (Enum.TryParse(enumType, m_enumValue, out object result))
			{
				m_value = (Enum)result;
			}
			else
			{
				HLOutput.LogError("Unable to parse Enum type (" + m_enumType + ") for (" + m_enumValue + ").");
				m_value = null;
			}
		}
	}
}
