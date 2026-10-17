using System;
using Hardlight;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Behaviour of the original HLUnityCore.Runtime GenericMaths<T> (02000180)
    // against the fully shared x86-64 bodies. The only shared state is each
    // closed type's readonly s_implementation, which cannot be reset; fixtures
    // that need a custom or failing initialisation use types owned by this file.
    [TestFixture]
    public sealed class OriginalGenericMathsTests
    {
        // Owned: implements IMaths<T> itself, so Create (0x138db60) takes the
        // Activator.CreateInstance branch before any type-code dispatch.
        public sealed class SelfMaths : IMaths<SelfMaths>
        {
            public readonly bool CreatedByActivator;
            public readonly int Value;
            public SelfMaths Handler;

            public SelfMaths() { CreatedByActivator = true; }
            public SelfMaths(int value) { Value = value; }

            public SelfMaths Add(SelfMaths left, SelfMaths right) => new SelfMaths(left.Value + right.Value) { Handler = this };
            public SelfMaths Subtract(SelfMaths left, SelfMaths right) => new SelfMaths(left.Value - right.Value) { Handler = this };
            public SelfMaths Multiply(SelfMaths left, SelfMaths right) => throw new InvalidOperationException();
            public SelfMaths Divide(SelfMaths left, SelfMaths right) => throw new InvalidOperationException();
            public bool GreaterThan(SelfMaths left, SelfMaths right) => left.Value > right.Value;
            public bool GreaterThanOrEqual(SelfMaths left, SelfMaths right) => left.Value >= right.Value;
            public bool LessThan(SelfMaths left, SelfMaths right) => left.Value < right.Value;
            public bool LessThanOrEqual(SelfMaths left, SelfMaths right) => left.Value <= right.Value;
        }

        // Owned: TypeCode.Object but not a Unity vector, so Create falls through
        // to the NotSupportedException; only this file's closed type is poisoned.
        public struct Unsupported
        {
        }

        // Native bodies 0xe4c440..0xe4c4b0 (byte), 0xbccfd0..0xbcd050 (sbyte),
        // 0xe5d290..0xe5d310 (char), 0xbe4740/0xcaa020 (short/ushort): unchecked
        // arithmetic narrowed with movzx/movsx, unsigned seta/setb comparisons for
        // byte/char/ushort and signed setg/setl for sbyte/short. Mutations caught:
        // checked arithmetic, widened results, or swapped signedness.
        [Test]
        public void SubIntegerImplementationsWrapAndCompareWithTheirOwnSignedness()
        {
            Assert.That(GenericMaths<byte>.Add(200, 100), Is.EqualTo((byte)44));
            Assert.That(GenericMaths<byte>.Subtract(1, 2), Is.EqualTo((byte)255));
            Assert.That(GenericMaths<byte>.Multiply(16, 16), Is.EqualTo((byte)0));
            Assert.That(GenericMaths<byte>.Divide(255, 2), Is.EqualTo((byte)127));
            Assert.That(GenericMaths<byte>.GreaterThan(200, 100), Is.True);

            Assert.That(GenericMaths<sbyte>.Add(127, 1), Is.EqualTo((sbyte)-128));
            Assert.That(GenericMaths<sbyte>.Divide(-128, -1), Is.EqualTo((sbyte)-128), "idivw then movsx keeps the wrapped quotient");
            Assert.That(GenericMaths<sbyte>.GreaterThan(-1, 0), Is.False);
            Assert.That(GenericMaths<sbyte>.LessThanOrEqual(-128, -128), Is.True);

            Assert.That(GenericMaths<char>.Add('￿', (char)1), Is.EqualTo('\0'));
            Assert.That(GenericMaths<char>.Divide('d', (char)2), Is.EqualTo('2'));
            Assert.That(GenericMaths<char>.GreaterThan('￿', 'a'), Is.True);

            Assert.That(GenericMaths<short>.Add(short.MaxValue, 1), Is.EqualTo(short.MinValue));
            Assert.That(GenericMaths<short>.LessThan(-1, 0), Is.True);
            Assert.That(GenericMaths<ushort>.Subtract(0, 1), Is.EqualTo(ushort.MaxValue));
            Assert.That(GenericMaths<ushort>.LessThan(1, 0x8000), Is.True);
        }

        // Native 0x13cb6a0..0x13cb710 (int), 0xca7730..0xca77a0 (uint),
        // 0x17b5700..0x17b5790 (long), 0xca77d0..0xca7860 (ulong): unchecked
        // add/sub/imul, truncating idiv/div and the 64-bit divq path for operands
        // above 32 bits. Mutations caught: checked overflow, floor division, or
        // signed comparisons for the unsigned implementations.
        [Test]
        public void WordImplementationsUseUncheckedTruncatingArithmetic()
        {
            Assert.That(GenericMaths<int>.Add(int.MaxValue, 1), Is.EqualTo(int.MinValue));
            Assert.That(GenericMaths<int>.Multiply(0x10000, 0x10000), Is.EqualTo(0));
            Assert.That(GenericMaths<int>.Divide(-7, 2), Is.EqualTo(-3));
            Assert.That(GenericMaths<int>.GreaterThanOrEqual(-1, 0), Is.False);

            Assert.That(GenericMaths<uint>.Subtract(0u, 1u), Is.EqualTo(uint.MaxValue));
            Assert.That(GenericMaths<uint>.GreaterThan(0x80000000u, 1u), Is.True);
            Assert.That(GenericMaths<uint>.Divide(uint.MaxValue, 2u), Is.EqualTo(0x7fffffffu));

            Assert.That(GenericMaths<long>.Add(long.MaxValue, 1L), Is.EqualTo(long.MinValue));
            Assert.That(GenericMaths<long>.Divide(-9L, 2L), Is.EqualTo(-4L));
            Assert.That(GenericMaths<long>.Divide(-0x200000000L, 2L), Is.EqualTo(-0x100000000L));
            Assert.That(GenericMaths<long>.LessThan(long.MinValue, 0L), Is.True);

            Assert.That(GenericMaths<ulong>.Divide(ulong.MaxValue, 2UL), Is.EqualTo((ulong)long.MaxValue));
            Assert.That(GenericMaths<ulong>.Multiply(1UL << 63, 2UL), Is.EqualTo(0UL));
            Assert.That(GenericMaths<ulong>.GreaterThan(1UL << 63, 1UL), Is.True);
            Assert.That(GenericMaths<ulong>.LessThanOrEqual(1UL << 63, 1UL), Is.False);
        }

        // Native 0x136d150..0x136d180 and 0x1113cf0..0x1113d20 use ucomiss/ucomisd
        // with seta/setae (operands swapped for the less-than pair), which are
        // false when either side is NaN. Mutations caught: implementing
        // LessThanOrEqual as !GreaterThan or GreaterThanOrEqual as !LessThan.
        [Test]
        public void FloatingComparisonsAreFalseForNaNAndInclusiveOnlyWhereNamed()
        {
            Assert.That(GenericMaths<float>.GreaterThan(float.NaN, 0f) || GenericMaths<float>.GreaterThan(0f, float.NaN), Is.False);
            Assert.That(GenericMaths<float>.GreaterThanOrEqual(float.NaN, 0f) || GenericMaths<float>.GreaterThanOrEqual(0f, float.NaN), Is.False);
            Assert.That(GenericMaths<float>.LessThan(float.NaN, 0f) || GenericMaths<float>.LessThan(0f, float.NaN), Is.False);
            Assert.That(GenericMaths<float>.LessThanOrEqual(float.NaN, 0f) || GenericMaths<float>.LessThanOrEqual(0f, float.NaN), Is.False);
            Assert.That(GenericMaths<double>.GreaterThan(double.NaN, 0d) || GenericMaths<double>.GreaterThan(0d, double.NaN), Is.False);
            Assert.That(GenericMaths<double>.GreaterThanOrEqual(double.NaN, 0d) || GenericMaths<double>.GreaterThanOrEqual(0d, double.NaN), Is.False);
            Assert.That(GenericMaths<double>.LessThan(double.NaN, 0d) || GenericMaths<double>.LessThan(0d, double.NaN), Is.False);
            Assert.That(GenericMaths<double>.LessThanOrEqual(double.NaN, 0d) || GenericMaths<double>.LessThanOrEqual(0d, double.NaN), Is.False);

            Assert.That(GenericMaths<float>.GreaterThan(1f, 1f), Is.False);
            Assert.That(GenericMaths<float>.GreaterThanOrEqual(1f, 1f), Is.True);
            Assert.That(GenericMaths<float>.LessThan(1f, 2f), Is.True);
            Assert.That(GenericMaths<double>.LessThanOrEqual(2d, 2d), Is.True);
            Assert.That(GenericMaths<double>.LessThan(2d, 2d), Is.False);

            Assert.That(GenericMaths<float>.Divide(1f, 0f), Is.EqualTo(float.PositiveInfinity));
            Assert.That(GenericMaths<double>.Subtract(0.5d, 0.25d), Is.EqualTo(0.25d));
            Assert.That(GenericMaths<float>.Multiply(1.5f, 4f), Is.EqualTo(6f));
        }

        // Native 0xf2dc80..0xf2df90 tail-call the System.Decimal operators.
        [Test]
        public void DecimalForwardsToTheDecimalOperators()
        {
            Assert.That(GenericMaths<decimal>.Add(0.1m, 0.2m), Is.EqualTo(0.3m));
            Assert.That(GenericMaths<decimal>.Divide(1m, 3m), Is.EqualTo(1m / 3m));
            Assert.That(GenericMaths<decimal>.Multiply(1.5m, 1.5m), Is.EqualTo(2.25m));
            Assert.That(GenericMaths<decimal>.Subtract(1m, 0.75m), Is.EqualTo(0.25m));
            Assert.That(GenericMaths<decimal>.GreaterThan(0.3m, 0.2m), Is.True);
            Assert.That(GenericMaths<decimal>.LessThanOrEqual(0.2m, 0.2m), Is.True);
            Assert.Throws<DivideByZeroException>(() => GenericMaths<decimal>.Divide(1m, 0m));
        }

        // StringMaths (0x06000b2f, 0xc008c0) overrides only Add with
        // String.Concat(string, string); the rest are the BaseMaths throws
        // (0xe1aa80..0xe1af00) whose messages name typeof(TMath).
        [Test]
        public void StringOnlyConcatenatesAndRejectsTheOtherOperationsByName()
        {
            Assert.That(GenericMaths<string>.Add("ab", "cd"), Is.EqualTo("abcd"));
            Assert.That(GenericMaths<string>.Add(null, "cd"), Is.EqualTo("cd"));
            Assert.That(GenericMaths<string>.Add(null, null), Is.EqualTo(string.Empty));

            ExpectUnsupported("String", "Subtract", () => GenericMaths<string>.Subtract("a", "b"));
            ExpectUnsupported("String", "Multiply", () => GenericMaths<string>.Multiply("a", "b"));
            ExpectUnsupported("String", "Divide", () => GenericMaths<string>.Divide("a", "b"));
            ExpectUnsupported("String", "GreaterThan", () => GenericMaths<string>.GreaterThan("a", "b"));
            ExpectUnsupported("String", "GreaterThanOrEqual", () => GenericMaths<string>.GreaterThanOrEqual("a", "b"));
            ExpectUnsupported("String", "LessThan", () => GenericMaths<string>.LessThan("a", "b"));
            ExpectUnsupported("String", "LessThanOrEqual", () => GenericMaths<string>.LessThanOrEqual("a", "b"));
        }

        // The Object type code reaches the Vector2/3/4 implementations
        // (0xda5890..0xda5920), which override only Add and Subtract.
        [Test]
        public void UnityVectorsOnlyAddAndSubtract()
        {
            Assert.That(GenericMaths<Vector2>.Add(new Vector2(1f, 2f), new Vector2(3f, 5f)), Is.EqualTo(new Vector2(4f, 7f)));
            Assert.That(GenericMaths<Vector2>.Subtract(new Vector2(1f, 2f), new Vector2(3f, 5f)), Is.EqualTo(new Vector2(-2f, -3f)));
            Assert.That(GenericMaths<Vector3>.Add(new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f)), Is.EqualTo(new Vector3(5f, 7f, 9f)));
            Assert.That(GenericMaths<Vector3>.Subtract(new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f)), Is.EqualTo(new Vector3(-3f, -3f, -3f)));
            Assert.That(GenericMaths<Vector4>.Add(new Vector4(1f, 2f, 3f, 4f), new Vector4(4f, 3f, 2f, 1f)), Is.EqualTo(new Vector4(5f, 5f, 5f, 5f)));
            Assert.That(GenericMaths<Vector4>.Subtract(new Vector4(1f, 2f, 3f, 4f), new Vector4(4f, 3f, 2f, 1f)), Is.EqualTo(new Vector4(-3f, -1f, 1f, 3f)));

            ExpectUnsupported("Vector2", "Multiply", () => GenericMaths<Vector2>.Multiply(Vector2.one, Vector2.one));
            ExpectUnsupported("Vector3", "Divide", () => GenericMaths<Vector3>.Divide(Vector3.one, Vector3.one));
            ExpectUnsupported("Vector4", "GreaterThan", () => GenericMaths<Vector4>.GreaterThan(Vector4.one, Vector4.one));
            ExpectUnsupported("Vector3", "LessThanOrEqual", () => GenericMaths<Vector3>.LessThanOrEqual(Vector3.one, Vector3.one));
        }

        // Create (0x138db60) checks typeof(IMaths<T>).IsAssignableFrom(typeof(T))
        // first and returns Activator.CreateInstance(typeof(T)): the operations run
        // on one default-constructed instance shared by every call, never on an
        // operand.
        [Test]
        public void SelfImplementingTypesRunOnOneActivatorCreatedInstance()
        {
            var left = new SelfMaths(5);
            var right = new SelfMaths(3);
            SelfMaths sum = GenericMaths<SelfMaths>.Add(left, right);
            SelfMaths difference = GenericMaths<SelfMaths>.Subtract(left, right);

            Assert.That(sum.Value, Is.EqualTo(8));
            Assert.That(difference.Value, Is.EqualTo(2));
            Assert.That(sum.Handler, Is.Not.SameAs(left).And.Not.SameAs(right));
            Assert.That(sum.Handler.CreatedByActivator, Is.True);
            Assert.That(difference.Handler, Is.SameAs(sum.Handler));
            Assert.That(GenericMaths<SelfMaths>.GreaterThan(left, right), Is.True);
            Assert.Throws<InvalidOperationException>(() => GenericMaths<SelfMaths>.Multiply(left, right));
        }

        // Create's fall-through (0x138e77f) throws from the static initialiser, so
        // every access reports a TypeInitializationException around the original
        // message, which names type.Name with no colon (unlike BaseMaths).
        [Test]
        public void UnsupportedTypesFailInitialisationWithTheOriginalMessage()
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var failure = Assert.Throws<TypeInitializationException>(() => GenericMaths<Unsupported>.Add(default, default));
                Assert.That(failure.InnerException, Is.TypeOf<NotSupportedException>());
                Assert.That(failure.InnerException.Message, Is.EqualTo(
                    "Type Unsupported does not support generic maths. Add support by implementing the IMaths<T> interface."));
            }
        }

        static void ExpectUnsupported(string typeName, string operation, TestDelegate call)
        {
            var failure = Assert.Throws<NotSupportedException>(call);
            Assert.That(failure.Message, Is.EqualTo("Type: " + typeName + " does not support IMaths<T>." + operation + "()"));
        }
    }
}
