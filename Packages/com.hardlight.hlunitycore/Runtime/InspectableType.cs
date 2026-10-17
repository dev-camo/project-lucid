using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// HLUnityCore.Runtime TypeDefIndex 9383 (0x02000278), methods 0x06000f82..0x06000f87;
	// bodies from the 1.10.1 x86-64 binary (0x1b1f2b0..0x1b1f4b0). A serialisable
	// System.Type reference stored as "FullName, AssemblyName".
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Serializable]
	public sealed class InspectableType : ISerializationCallbackReceiver
	{
		[SerializeField]
		private string m_serializedType;

		private Type m_type;

		public Type Type => m_type;

		// 0x06000f83: the type is stored before its serialised name.
		public InspectableType(Type type)
		{
			m_type = type;
			m_serializedType = GetAssemblyName(type);
		}

		// 0x06000f84, 0x1b1f310: the simple assembly name (Assembly.GetName().Name), not
		// the assembly-qualified name, so version and culture are left out.
		public static string GetAssemblyName(Type type)
		{
			return type != null ? type.FullName + ", " + type.Assembly.GetName().Name : "";
		}

		// 0x06000f85: an empty name clears the type; an unresolved name gives null.
		void ISerializationCallbackReceiver.OnAfterDeserialize()
		{
			m_type = string.IsNullOrEmpty(m_serializedType) ? null : Type.GetType(m_serializedType);
		}

		// 0x06000f86: empty; serialising never refreshes m_serializedType from m_type, so
		// only the constructor or Unity deserialisation sets it.
		void ISerializationCallbackReceiver.OnBeforeSerialize()
		{
		}

		// 0x06000f87.
		public override string ToString()
		{
			return m_type != null ? m_type.FullName : "(None)";
		}
	}
}
