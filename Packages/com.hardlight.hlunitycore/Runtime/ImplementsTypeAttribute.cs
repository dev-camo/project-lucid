using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// HLUnityCore.Runtime TypeDefIndex 9382 (0x02000277), methods 0x06000f80..0x06000f81;
	// bodies from the 1.10.1 x86-64 binary (0x1b1f1e0, 0x1b1f210). Restricts an
	// InspectableType field to concrete, visible classes assignable to a base type.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
	public sealed class ImplementsTypeAttribute : PropertyAttribute
	{
		private readonly Type m_baseType;

		// 0x06000f80.
		public ImplementsTypeAttribute(Type baseType)
		{
			m_baseType = baseType;
		}

		// 0x06000f81, 0x1b1f210: null, non-visible (nested private or internal),
		// interface, value-type and abstract candidates fail before the
		// IsAssignableFrom (Type vtable slot 22) test.
		public bool IsConstraintSatisfied(Type type)
		{
			if (type == null || !type.IsVisible || !type.IsClass || type.IsAbstract)
			{
				return false;
			}
			return m_baseType.IsAssignableFrom(type);
		}
	}
}
