using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
	// Original HLUnityUI.Runtime 0x0200005d (TypeDefIndex 11917); x86-64 bodies.
	// Two parameter sets are equal when they have the same runtime type.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Serializable]
	public class ViewParameters : ICloneable
	{
		// 0x060002b7 (0x1b4d180)
		public object Clone()
		{
			return MemberwiseClone();
		}

		// 0x060002b8 (0x1b4d190)
		public override bool Equals(object obj)
		{
			if (!(obj is ViewParameters))
			{
				return false;
			}
			return obj.GetType() == GetType();
		}

		// 0x060002b9 (0x1b4d240)
		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		// 0x060002ba (0x1b4d250)
		public ViewParameters()
		{
		}
	}
}
