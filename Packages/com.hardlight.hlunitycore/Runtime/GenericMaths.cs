using System;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 02000180; bodies translated from the 1.10.1 x86-64 binary.
	// Only the fully shared instantiations shipped, so each nested implementation is one
	// __Il2CppFullySharedGenericType body; the class RGCTX tables fix the type arguments.
	public static class GenericMaths<T>
	{
		// BeforeFieldInit with a .cctor (0x06000ab9, 0x138e850): a field initialiser.
		private static readonly IMaths<T> s_implementation = Create();

		// Original 0x06000aaf (0x138bf20): an empty body.
		public static void Initialise()
		{
		}

		// 0x06000ab0..0x06000ab7 forward to interface slots 0..7 with no null guard
		// of their own; a null implementation raises NullReferenceException.
		public static T Add(T left, T right)
		{
			return s_implementation.Add(left, right);
		}

		public static T Subtract(T left, T right)
		{
			return s_implementation.Subtract(left, right);
		}

		public static T Multiply(T left, T right)
		{
			return s_implementation.Multiply(left, right);
		}

		public static T Divide(T left, T right)
		{
			return s_implementation.Divide(left, right);
		}

		public static bool GreaterThan(T left, T right)
		{
			return s_implementation.GreaterThan(left, right);
		}

		public static bool GreaterThanOrEqual(T left, T right)
		{
			return s_implementation.GreaterThanOrEqual(left, right);
		}

		public static bool LessThan(T left, T right)
		{
			return s_implementation.LessThan(left, right);
		}

		public static bool LessThanOrEqual(T left, T right)
		{
			return s_implementation.LessThanOrEqual(left, right);
		}

		// Original 0x06000ab8 (0x138db60). A T that implements IMaths<T> itself is
		// instantiated with Activator, so calls go to a fresh instance rather than the
		// operands. Otherwise the type code picks an implementation, in RGCTX order;
		// Object only covers the three Unity vectors. An enum reports its underlying
		// code, so its implementation fails the final cast to IMaths<T>.
		private static IMaths<T> Create()
		{
			Type type = typeof(T);
			if (typeof(IMaths<T>).IsAssignableFrom(type))
			{
				return (IMaths<T>)Activator.CreateInstance(type);
			}

			switch (Type.GetTypeCode(type))
			{
				case TypeCode.Char:
					return (IMaths<T>)new CharMaths();
				case TypeCode.SByte:
					return (IMaths<T>)new SByteMaths();
				case TypeCode.Byte:
					return (IMaths<T>)new ByteMaths();
				case TypeCode.Int16:
					return (IMaths<T>)new ShortMaths();
				case TypeCode.UInt16:
					return (IMaths<T>)new UShortMaths();
				case TypeCode.Int32:
					return (IMaths<T>)new IntegerMaths();
				case TypeCode.UInt32:
					return (IMaths<T>)new UIntegerMaths();
				case TypeCode.Int64:
					return (IMaths<T>)new LongMaths();
				case TypeCode.UInt64:
					return (IMaths<T>)new ULongMaths();
				case TypeCode.Single:
					return (IMaths<T>)new FloatMaths();
				case TypeCode.Double:
					return (IMaths<T>)new DoubleMaths();
				case TypeCode.Decimal:
					return (IMaths<T>)new DecimalMaths();
				case TypeCode.String:
					return (IMaths<T>)new StringMaths();
				case TypeCode.Object:
					if (type == typeof(Vector2))
					{
						return (IMaths<T>)new Vector2Maths();
					}
					if (type == typeof(Vector3))
					{
						return (IMaths<T>)new Vector3Maths();
					}
					if (type == typeof(Vector4))
					{
						return (IMaths<T>)new Vector4Maths();
					}
					break;
			}

			throw new NotSupportedException("Type " + type.Name + " does not support generic maths. Add support by implementing the IMaths<T> interface.");
		}

		// Original 02000181. Every operation is virtual and unsupported by default;
		// the message names TMath (class RGCTX entry 0), not the outer T.
		private abstract class BaseMaths<TMath> : IMaths<TMath>
		{
			public virtual TMath Add(TMath left, TMath right)
			{
				throw new NotSupportedException("Type: " + typeof(TMath).Name + " does not support IMaths<T>.Add()");
			}

			public virtual TMath Subtract(TMath left, TMath right)
			{
				throw new NotSupportedException("Type: " + typeof(TMath).Name + " does not support IMaths<T>.Subtract()");
			}

			public virtual TMath Multiply(TMath left, TMath right)
			{
				throw new NotSupportedException("Type: " + typeof(TMath).Name + " does not support IMaths<T>.Multiply()");
			}

			public virtual TMath Divide(TMath left, TMath right)
			{
				throw new NotSupportedException("Type: " + typeof(TMath).Name + " does not support IMaths<T>.Divide()");
			}

			public virtual bool GreaterThan(TMath left, TMath right)
			{
				throw new NotSupportedException("Type: " + typeof(TMath).Name + " does not support IMaths<T>.GreaterThan()");
			}

			public virtual bool GreaterThanOrEqual(TMath left, TMath right)
			{
				throw new NotSupportedException("Type: " + typeof(TMath).Name + " does not support IMaths<T>.GreaterThanOrEqual()");
			}

			public virtual bool LessThan(TMath left, TMath right)
			{
				throw new NotSupportedException("Type: " + typeof(TMath).Name + " does not support IMaths<T>.LessThan()");
			}

			public virtual bool LessThanOrEqual(TMath left, TMath right)
			{
				throw new NotSupportedException("Type: " + typeof(TMath).Name + " does not support IMaths<T>.LessThanOrEqual()");
			}
		}

		// Sub-int implementations wrap through unchecked narrowing (0xe5d290 etc.).
		private class CharMaths : BaseMaths<char>
		{
			public override char Add(char left, char right)
			{
				return (char)(left + right);
			}

			public override char Subtract(char left, char right)
			{
				return (char)(left - right);
			}

			public override char Multiply(char left, char right)
			{
				return (char)(left * right);
			}

			public override char Divide(char left, char right)
			{
				return (char)(left / right);
			}

			public override bool GreaterThan(char left, char right)
			{
				return left > right;
			}

			public override bool GreaterThanOrEqual(char left, char right)
			{
				return left >= right;
			}

			public override bool LessThan(char left, char right)
			{
				return left < right;
			}

			public override bool LessThanOrEqual(char left, char right)
			{
				return left <= right;
			}
		}

		private class SByteMaths : BaseMaths<sbyte>
		{
			public override sbyte Add(sbyte left, sbyte right)
			{
				return (sbyte)(left + right);
			}

			public override sbyte Subtract(sbyte left, sbyte right)
			{
				return (sbyte)(left - right);
			}

			public override sbyte Multiply(sbyte left, sbyte right)
			{
				return (sbyte)(left * right);
			}

			public override sbyte Divide(sbyte left, sbyte right)
			{
				return (sbyte)(left / right);
			}

			public override bool GreaterThan(sbyte left, sbyte right)
			{
				return left > right;
			}

			public override bool GreaterThanOrEqual(sbyte left, sbyte right)
			{
				return left >= right;
			}

			public override bool LessThan(sbyte left, sbyte right)
			{
				return left < right;
			}

			public override bool LessThanOrEqual(sbyte left, sbyte right)
			{
				return left <= right;
			}
		}

		private class ByteMaths : BaseMaths<byte>
		{
			public override byte Add(byte left, byte right)
			{
				return (byte)(left + right);
			}

			public override byte Subtract(byte left, byte right)
			{
				return (byte)(left - right);
			}

			public override byte Multiply(byte left, byte right)
			{
				return (byte)(left * right);
			}

			public override byte Divide(byte left, byte right)
			{
				return (byte)(left / right);
			}

			public override bool GreaterThan(byte left, byte right)
			{
				return left > right;
			}

			public override bool GreaterThanOrEqual(byte left, byte right)
			{
				return left >= right;
			}

			public override bool LessThan(byte left, byte right)
			{
				return left < right;
			}

			public override bool LessThanOrEqual(byte left, byte right)
			{
				return left <= right;
			}
		}

		private class ShortMaths : BaseMaths<short>
		{
			public override short Add(short left, short right)
			{
				return (short)(left + right);
			}

			public override short Subtract(short left, short right)
			{
				return (short)(left - right);
			}

			public override short Multiply(short left, short right)
			{
				return (short)(left * right);
			}

			public override short Divide(short left, short right)
			{
				return (short)(left / right);
			}

			public override bool GreaterThan(short left, short right)
			{
				return left > right;
			}

			public override bool GreaterThanOrEqual(short left, short right)
			{
				return left >= right;
			}

			public override bool LessThan(short left, short right)
			{
				return left < right;
			}

			public override bool LessThanOrEqual(short left, short right)
			{
				return left <= right;
			}
		}

		private class UShortMaths : BaseMaths<ushort>
		{
			public override ushort Add(ushort left, ushort right)
			{
				return (ushort)(left + right);
			}

			public override ushort Subtract(ushort left, ushort right)
			{
				return (ushort)(left - right);
			}

			public override ushort Multiply(ushort left, ushort right)
			{
				return (ushort)(left * right);
			}

			public override ushort Divide(ushort left, ushort right)
			{
				return (ushort)(left / right);
			}

			public override bool GreaterThan(ushort left, ushort right)
			{
				return left > right;
			}

			public override bool GreaterThanOrEqual(ushort left, ushort right)
			{
				return left >= right;
			}

			public override bool LessThan(ushort left, ushort right)
			{
				return left < right;
			}

			public override bool LessThanOrEqual(ushort left, ushort right)
			{
				return left <= right;
			}
		}

		private class IntegerMaths : BaseMaths<int>
		{
			public override int Add(int left, int right)
			{
				return left + right;
			}

			public override int Subtract(int left, int right)
			{
				return left - right;
			}

			public override int Multiply(int left, int right)
			{
				return left * right;
			}

			public override int Divide(int left, int right)
			{
				return left / right;
			}

			public override bool GreaterThan(int left, int right)
			{
				return left > right;
			}

			public override bool GreaterThanOrEqual(int left, int right)
			{
				return left >= right;
			}

			public override bool LessThan(int left, int right)
			{
				return left < right;
			}

			public override bool LessThanOrEqual(int left, int right)
			{
				return left <= right;
			}
		}

		private class UIntegerMaths : BaseMaths<uint>
		{
			public override uint Add(uint left, uint right)
			{
				return left + right;
			}

			public override uint Subtract(uint left, uint right)
			{
				return left - right;
			}

			public override uint Multiply(uint left, uint right)
			{
				return left * right;
			}

			public override uint Divide(uint left, uint right)
			{
				return left / right;
			}

			public override bool GreaterThan(uint left, uint right)
			{
				return left > right;
			}

			public override bool GreaterThanOrEqual(uint left, uint right)
			{
				return left >= right;
			}

			public override bool LessThan(uint left, uint right)
			{
				return left < right;
			}

			public override bool LessThanOrEqual(uint left, uint right)
			{
				return left <= right;
			}
		}

		private class LongMaths : BaseMaths<long>
		{
			public override long Add(long left, long right)
			{
				return left + right;
			}

			public override long Subtract(long left, long right)
			{
				return left - right;
			}

			public override long Multiply(long left, long right)
			{
				return left * right;
			}

			public override long Divide(long left, long right)
			{
				return left / right;
			}

			public override bool GreaterThan(long left, long right)
			{
				return left > right;
			}

			public override bool GreaterThanOrEqual(long left, long right)
			{
				return left >= right;
			}

			public override bool LessThan(long left, long right)
			{
				return left < right;
			}

			public override bool LessThanOrEqual(long left, long right)
			{
				return left <= right;
			}
		}

		private class ULongMaths : BaseMaths<ulong>
		{
			public override ulong Add(ulong left, ulong right)
			{
				return left + right;
			}

			public override ulong Subtract(ulong left, ulong right)
			{
				return left - right;
			}

			public override ulong Multiply(ulong left, ulong right)
			{
				return left * right;
			}

			public override ulong Divide(ulong left, ulong right)
			{
				return left / right;
			}

			public override bool GreaterThan(ulong left, ulong right)
			{
				return left > right;
			}

			public override bool GreaterThanOrEqual(ulong left, ulong right)
			{
				return left >= right;
			}

			public override bool LessThan(ulong left, ulong right)
			{
				return left < right;
			}

			public override bool LessThanOrEqual(ulong left, ulong right)
			{
				return left <= right;
			}
		}

		// Floating-point comparisons are ordered (ucomiss/ucomisd), so NaN is false.
		private class FloatMaths : BaseMaths<float>
		{
			public override float Add(float left, float right)
			{
				return left + right;
			}

			public override float Subtract(float left, float right)
			{
				return left - right;
			}

			public override float Multiply(float left, float right)
			{
				return left * right;
			}

			public override float Divide(float left, float right)
			{
				return left / right;
			}

			public override bool GreaterThan(float left, float right)
			{
				return left > right;
			}

			public override bool GreaterThanOrEqual(float left, float right)
			{
				return left >= right;
			}

			public override bool LessThan(float left, float right)
			{
				return left < right;
			}

			public override bool LessThanOrEqual(float left, float right)
			{
				return left <= right;
			}
		}

		private class DoubleMaths : BaseMaths<double>
		{
			public override double Add(double left, double right)
			{
				return left + right;
			}

			public override double Subtract(double left, double right)
			{
				return left - right;
			}

			public override double Multiply(double left, double right)
			{
				return left * right;
			}

			public override double Divide(double left, double right)
			{
				return left / right;
			}

			public override bool GreaterThan(double left, double right)
			{
				return left > right;
			}

			public override bool GreaterThanOrEqual(double left, double right)
			{
				return left >= right;
			}

			public override bool LessThan(double left, double right)
			{
				return left < right;
			}

			public override bool LessThanOrEqual(double left, double right)
			{
				return left <= right;
			}
		}

		// Each body tail-calls the matching System.Decimal operator.
		private class DecimalMaths : BaseMaths<decimal>
		{
			public override decimal Add(decimal left, decimal right)
			{
				return left + right;
			}

			public override decimal Subtract(decimal left, decimal right)
			{
				return left - right;
			}

			public override decimal Multiply(decimal left, decimal right)
			{
				return left * right;
			}

			public override decimal Divide(decimal left, decimal right)
			{
				return left / right;
			}

			public override bool GreaterThan(decimal left, decimal right)
			{
				return left > right;
			}

			public override bool GreaterThanOrEqual(decimal left, decimal right)
			{
				return left >= right;
			}

			public override bool LessThan(decimal left, decimal right)
			{
				return left < right;
			}

			public override bool LessThanOrEqual(decimal left, decimal right)
			{
				return left <= right;
			}
		}

		// Original 0x06000b2f: only concatenation (String.Concat(string, string));
		// every other operation keeps the BaseMaths NotSupportedException.
		private class StringMaths : BaseMaths<string>
		{
			public override string Add(string left, string right)
			{
				return left + right;
			}
		}

		// The vector types override only Add and Subtract (inlined operators).
		private class Vector2Maths : BaseMaths<Vector2>
		{
			public override Vector2 Add(Vector2 left, Vector2 right)
			{
				return left + right;
			}

			public override Vector2 Subtract(Vector2 left, Vector2 right)
			{
				return left - right;
			}
		}

		private class Vector3Maths : BaseMaths<Vector3>
		{
			public override Vector3 Add(Vector3 left, Vector3 right)
			{
				return left + right;
			}

			public override Vector3 Subtract(Vector3 left, Vector3 right)
			{
				return left - right;
			}
		}

		private class Vector4Maths : BaseMaths<Vector4>
		{
			public override Vector4 Add(Vector4 left, Vector4 right)
			{
				return left + right;
			}

			public override Vector4 Subtract(Vector4 left, Vector4 right)
			{
				return left - right;
			}
		}
	}
}
