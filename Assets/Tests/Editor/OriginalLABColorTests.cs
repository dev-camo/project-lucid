using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Specification of the original HLUnityUI.Runtime LABColor (TypeDef 0x02000007,
    // methods 0x0600000c..1e) from the x86-64 bodies of the pinned 6c89 dylib.
    // Conversion expectations come from a float32 replay of FromColor 0x1B394B0 and
    // ToColor 0x1B39960 with the constants those bodies load from 0x29A8494..0x29A8520
    // (0.04045, 0.055, 1.055, 2.2, 12.92, the XYZ rows, 1.089, 0.008856, 7.787,
    // 16/116, 6/29, the inverse rows, 0.0031308, 1/2.4). Pow branches run through
    // Mathf.Pow here, so those cases carry a small tolerance.
    public sealed class OriginalLABColorTests
    {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // dump.cs TypeDefIndex 11831: [Serializable] public class in the global namespace,
        // private L/A/B at +0x10/+0x14/+0x18 behind public l/a/b, methods in this order.
        [Test]
        public void DeclarationKeepsTheOriginalFieldsPropertiesAndMethodOrder()
        {
            Type type = typeof(LABColor);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.Namespace, Is.Null);
            Assert.That(type.BaseType, Is.EqualTo(typeof(object)));
            Assert.That(type.IsSerializable, Is.True);
            Assert.That((int)type.Attributes, Is.EqualTo(1056769));
            Assert.That(type.GetInterfaces(), Is.Empty);
            Assert.That(type.GetNestedTypes(Declared), Is.Empty);
            Assert.That(type.GetEvents(Declared), Is.Empty);

            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "L", "A", "B" }));
            foreach (FieldInfo field in fields)
            {
                Assert.That(field.FieldType, Is.EqualTo(typeof(float)), field.Name);
                Assert.That(field.Attributes, Is.EqualTo(FieldAttributes.Private), field.Name);
                Assert.That(field.GetCustomAttributesData(), Is.Empty, field.Name);
            }
            PropertyInfo[] properties = type.GetProperties(Declared);
            Assert.That(properties.Select(p => p.Name).ToArray(), Is.EqualTo(new[] { "l", "a", "b" }));
            Assert.That(properties.All(p => p.PropertyType == typeof(float) && p.CanRead && p.CanWrite), Is.True);

            MethodInfo[] methods = type.GetMethods(Declared);
            Assert.That(methods.Select(m => m.Name).ToArray(), Is.EqualTo(new[]
            {
                "get_l", "set_l", "get_a", "set_a", "get_b", "set_b", "Lerp", "Lerp", "Distance",
                "FromColor", "ToColor", "ToColor", "ToString", "Equals", "GetHashCode", "op_Equality", "op_Inequality",
            }));
            for (int i = 0; i < 6; ++i) Assert.That((int)methods[i].Attributes, Is.EqualTo(2182), methods[i].Name);
            Check(methods[6], typeof(LABColor), 150, new[] { typeof(LABColor), typeof(LABColor), typeof(float) }, "a", "b", "t");
            Check(methods[7], typeof(Color), 150, new[] { typeof(Color), typeof(Color), typeof(float) }, "a", "b", "t");
            Check(methods[8], typeof(float), 150, new[] { typeof(LABColor), typeof(LABColor) }, "a", "b");
            Check(methods[9], typeof(LABColor), 150, new[] { typeof(Color) }, "c");
            Check(methods[10], typeof(Color), 150, new[] { typeof(LABColor) }, "lab");
            Check(methods[11], typeof(Color), 134, Type.EmptyTypes);
            Check(methods[12], typeof(string), 198, Type.EmptyTypes);
            Check(methods[13], typeof(bool), 198, new[] { typeof(object) }, "obj");
            Check(methods[14], typeof(int), 198, Type.EmptyTypes);
            Check(methods[15], typeof(bool), 2198, new[] { typeof(LABColor), typeof(LABColor) }, "item1", "item2");
            Check(methods[16], typeof(bool), 2198, new[] { typeof(LABColor), typeof(LABColor) }, "item1", "item2");

            ConstructorInfo[] constructors = type.GetConstructors(Declared);
            Assert.That(constructors.Length, Is.EqualTo(2));
            Assert.That(constructors.All(c => (int)c.Attributes == 6278), Is.True);
            Assert.That(constructors[0].GetParameters().Select(p => p.Name).ToArray(), Is.EqualTo(new[] { "l", "a", "b" }));
            Assert.That(constructors[0].GetParameters().All(p => p.ParameterType == typeof(float)), Is.True);
            Assert.That(constructors[1].GetParameters().Single().Name, Is.EqualTo("col"));
            Assert.That(constructors[1].GetParameters().Single().ParameterType, Is.EqualTo(typeof(Color)));
        }

        // .ctor 0x06000012 stores l/a/b into +0x10/+0x14/+0x18; each property reads and
        // writes only its own field. Distinct values catch a swapped pair.
        [Test]
        public void ConstructorAndPropertiesUseTheirOwnChannel()
        {
            var lab = new LABColor(1.5f, -2f, 3f);
            Assert.That(Raw(lab), Is.EqualTo(new[] { 1.5f, -2f, 3f }));
            lab.l = 7f;
            lab.a = -8f;
            lab.b = 9f;
            Assert.That(Raw(lab), Is.EqualTo(new[] { 7f, -8f, 9f }));
            Assert.That(new[] { lab.l, lab.a, lab.b }, Is.EqualTo(new[] { 7f, -8f, 9f }));
        }

        // FromColor 0x1B394B0: a mid grey linearises with the 2.2 power (0x29A84A4), not the
        // 2.4 sRGB curve, giving L* 56.4249 (2.4 would give about 53.39). Pure red takes the
        // pow branches of all three cube roots; the dark colour stays on the linear
        // 1/12.92 and 7.787f + 16/116 branches throughout. Alpha is ignored.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void FromColorFollowsTheShippedGammaAndCubeRootBranches(int index)
        {
            float[][] cases =
            {
                // r, g, b -> L*, a*, b*
                new[] { 0.5f, 0.5f, 0.5f, 56.4248657f, 0f, 0f },
                new[] { 1f, 0f, 0f, 53.2328796f, 80.1053009f, 67.2227859f },
                new[] { 0.04f, 0.02f, 0.01f, 1.64508247f, 0.978961587f, 1.43489838f },
                new[] { 0.2f, 0.6f, 0.9f, 63.1736832f, -5.27098751f, -44.4131012f },
            };
            float[] c = cases[index];
            LABColor lab = LABColor.FromColor(new Color(c[0], c[1], c[2], 0.25f));
            Assert.That(lab.l, Is.EqualTo(c[3]).Within(1e-4f));
            Assert.That(lab.a, Is.EqualTo(c[4]).Within(1e-4f));
            Assert.That(lab.b, Is.EqualTo(c[5]).Within(1e-4f));
        }

        // 0x1B39633..0x1B39688: XYZ is clamped to [0, 0.9505] x [0, 1] x [0, 1.089], so
        // colours brighter than white convert like white and negative channels like black.
        [Test]
        public void FromColorClampsXyzToTheWhitePointAndZero()
        {
            LABColor over = LABColor.FromColor(new Color(3f, 3f, 3f));
            Assert.That(over.l, Is.EqualTo(100f).Within(1e-4f));
            Assert.That(over.a, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(over.b, Is.EqualTo(0f).Within(1e-4f));
            LABColor under = LABColor.FromColor(new Color(-1f, -1f, -1f));
            Assert.That(under.l, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(under.a, Is.EqualTo(0f));
            Assert.That(under.b, Is.EqualTo(0f));
        }

        // .ctor(Color) 0x06000013 calls FromColor and copies all three channels.
        [Test]
        public void ColourConstructorCopiesFromColor()
        {
            var lab = new LABColor(new Color(1f, 0f, 0f));
            LABColor expected = LABColor.FromColor(new Color(1f, 0f, 0f));
            Assert.That(Raw(lab), Is.EqualTo(Raw(expected)));
            Assert.That(lab.a, Is.EqualTo(80.1053009f).Within(1e-4f));
        }

        // ToColor 0x1B39960: maxps/maxss against zero clamp only the low side and the
        // alpha lane is the constant 1 (blendps 0x1B39BCF). L* 100 overshoots red to
        // 1.0000858 because the inverse row uses 3.241; L* 50 a* 80 drives green negative.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void ToColorClampsOnlyNegativeChannelsAndSetsOpaqueAlpha(int index)
        {
            float[][] cases =
            {
                // L*, a*, b* -> r, g, b
                new[] { 100f, 0f, 0f, 1.00008583f, 0.959101737f, 0.999965131f },
                new[] { 50f, 80f, 0f, 0.911385f, 0f, 0.478764296f },
                new[] { 60f, -40f, 30f, 0.320502549f, 0.623332202f, 0.351003557f },
                new[] { 5f, 0f, 0f, 0.0660401061f, 0.0613384098f, 0.0660262778f },
                new[] { 0f, 0f, 0f, 0f, 0f, 0f },
            };
            float[] c = cases[index];
            var lab = new LABColor(c[0], c[1], c[2]);
            foreach (Color colour in new[] { LABColor.ToColor(lab), lab.ToColor() })
            {
                Assert.That(colour.r, Is.EqualTo(c[3]).Within(1e-5f));
                Assert.That(colour.g, Is.EqualTo(c[4]).Within(1e-5f));
                Assert.That(colour.b, Is.EqualTo(c[5]).Within(1e-5f));
                Assert.That(colour.a, Is.EqualTo(1f));
            }
        }

        // Lerp 0x1B397B0: minss 1 / cmpltss 0 clamp t before a + (b - a) * t per channel,
        // and the result is a new instance.
        [Test]
        public void LerpClampsTAndReturnsANewColour()
        {
            var from = new LABColor(10f, -20f, 30f);
            var to = new LABColor(50f, 20f, -10f);
            LABColor mid = LABColor.Lerp(from, to, 0.25f);
            Assert.That(Raw(mid), Is.EqualTo(new[] { 20f, -10f, 20f }));
            Assert.That(mid, Is.Not.SameAs(from));
            Assert.That(Raw(LABColor.Lerp(from, to, 3f)), Is.EqualTo(Raw(to)));
            Assert.That(Raw(LABColor.Lerp(from, to, -3f)), Is.EqualTo(Raw(from)));
            Assert.That(LABColor.Lerp(from, to, 1f), Is.Not.SameAs(to));
        }

        // Lerp(Color, Color, t) 0x1B39890: FromColor both ends, LAB Lerp, then ToColor, so
        // the alpha of either input is lost and the midpoint is perceptual, not RGB.
        [Test]
        public void ColourLerpInterpolatesInLabSpaceAndDropsAlpha()
        {
            Color white = new Color(1f, 1f, 1f, 0f);
            Color black = new Color(0f, 0f, 0f, 0f);
            Color mid = LABColor.Lerp(black, white, 0.5f);
            // Black is L* 0 and white L* 100 with a* = b* = 0, so the midpoint is L* 50,
            // which the ToColor replay maps to (0.46637, 0.44612, 0.46631); RGB
            // interpolation would give 0.5 per channel.
            Assert.That(mid.r, Is.EqualTo(0.466368914f).Within(1e-4f));
            Assert.That(mid.g, Is.EqualTo(0.446116805f).Within(1e-4f));
            Assert.That(mid.b, Is.EqualTo(0.466309369f).Within(1e-4f));
            Assert.That(mid.a, Is.EqualTo(1f));
            Assert.That(mid.r, Is.Not.EqualTo(0.5f).Within(0.01f));
            Assert.That(LABColor.Lerp(black, white, 9f).a, Is.EqualTo(1f));
        }

        // Distance 0x1B39900: sqrt(dL^2 + da^2 + db^2), the CIE76 delta E.
        [Test]
        public void DistanceIsTheEuclideanDeltaE()
        {
            Assert.That(LABColor.Distance(new LABColor(1f, 2f, 3f), new LABColor(4f, -2f, 15f)), Is.EqualTo(13f));
            Assert.That(LABColor.Distance(new LABColor(1f, 2f, 3f), new LABColor(1f, 2f, 3f)), Is.EqualTo(0f));
        }

        // ToString 0x1B39BF0: string.Concat over {"L:", L, " A:", A, " B:", B} with
        // Single.ToString() in the current culture.
        [Test]
        public void ToStringJoinsTheChannelsWithTheirLabels()
        {
            CultureInfo saved = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                Assert.That(new LABColor(1.5f, -2f, 3f).ToString(), Is.EqualTo("L:1.5 A:-2 B:3"));
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                Assert.That(new LABColor(1.5f, -2f, 3f).ToString(), Is.EqualTo("L:1,5 A:-2 B:3"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = saved;
            }
        }

        // Equals 0x1B39D70: null or a different runtime type is false; otherwise the
        // inlined == compares with ucomiss, so NaN channels are never equal, even to
        // the same instance.
        [Test]
        public void EqualsComparesChannelValuesAndNaNIsNeverEqual()
        {
            var lab = new LABColor(1f, 2f, 3f);
            Assert.That(lab.Equals(new LABColor(1f, 2f, 3f)), Is.True);
            Assert.That(lab.Equals(new LABColor(1f, 2f, 4f)), Is.False);
            Assert.That(lab.Equals(new LABColor(1f, 5f, 3f)), Is.False);
            Assert.That(lab.Equals(null), Is.False);
            Assert.That(lab.Equals(new Color(1f, 2f, 3f)), Is.False);
            Assert.That(lab.Equals("L:1 A:2 B:3"), Is.False);
            // The runtime types must match exactly (Type.op_Inequality at 0x1B39DF2), so
            // equal channels in a subclass are not equal in either direction.
            Assert.That(lab.Equals(new DerivedLab()), Is.False);
            Assert.That(new DerivedLab().Equals(lab), Is.False);
            var nan = new LABColor(float.NaN, 0f, 0f);
            Assert.That(nan.Equals(nan), Is.False);
            Assert.That(new LABColor(0f, 0f, 0f).Equals(new LABColor(-0f, 0f, 0f)), Is.True);
        }

        sealed class DerivedLab : LABColor
        {
            public DerivedLab() : base(1f, 2f, 3f) { }
        }

        // op_Equality 0x1B39E80 / op_Inequality 0x1B39F40: both operands are dereferenced
        // before any comparison (NullReferenceException at 0x1B39EC0 / 0x1B39F7E), and
        // NaN makes == false and != true.
        [Test]
        public void OperatorsCompareChannelsAndThrowForANullOperand()
        {
            var lab = new LABColor(1f, 2f, 3f);
            var same = new LABColor(1f, 2f, 3f);
            Assert.That(lab == same, Is.True);
            Assert.That(lab != same, Is.False);
            Assert.That(lab == new LABColor(1f, 2f, -3f), Is.False);
            Assert.That(lab != new LABColor(1f, 2f, -3f), Is.True);
            var nan = new LABColor(1f, float.NaN, 3f);
            LABColor alias = nan;
            Assert.That(nan == alias, Is.False);
            Assert.That(nan != alias, Is.True);
            LABColor none = null;
            Assert.Throws<NullReferenceException>(() => { bool unused = lab == none; });
            Assert.Throws<NullReferenceException>(() => { bool unused = none == lab; });
            Assert.Throws<NullReferenceException>(() => { bool unused = lab != none; });
            LABColor alsoNone = none;
            Assert.Throws<NullReferenceException>(() => { bool unused = none != alsoNone; });
        }

        // GetHashCode 0x1B39ED0: Single.GetHashCode of L, A and B combined with xor, so
        // permuting the channels keeps the hash.
        [Test]
        public void HashCodeXorsTheChannelHashes()
        {
            int expected = 1.5f.GetHashCode() ^ (-2f).GetHashCode() ^ 3f.GetHashCode();
            Assert.That(new LABColor(1.5f, -2f, 3f).GetHashCode(), Is.EqualTo(expected));
            Assert.That(new LABColor(3f, 1.5f, -2f).GetHashCode(), Is.EqualTo(expected));
            Assert.That(new LABColor(4f, 4f, 0f).GetHashCode(), Is.EqualTo(0f.GetHashCode()));
        }

        static float[] Raw(LABColor lab)
        {
            return new[] { "L", "A", "B" }.Select(n => (float)typeof(LABColor).GetField(n, Declared).GetValue(lab)).ToArray();
        }

        static void Check(MethodInfo method, Type returns, int flags, Type[] parameters, params string[] names)
        {
            Assert.That(method.ReturnType, Is.EqualTo(returns), method.Name);
            Assert.That((int)method.Attributes, Is.EqualTo(flags), method.Name);
            Assert.That(method.GetParameters().Select(p => p.ParameterType).ToArray(), Is.EqualTo(parameters), method.Name);
            Assert.That(method.GetParameters().Select(p => p.Name).ToArray(), Is.EqualTo(names), method.Name);
            Assert.That(method.GetCustomAttributesData(), Is.Empty, method.Name);
        }
    }
}
