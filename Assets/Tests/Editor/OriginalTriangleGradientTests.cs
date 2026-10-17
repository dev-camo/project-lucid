using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Specification of the original HLUnityUI.Runtime TriangleGradient (TypeDef
    // 0x02000003, methods 0x06000004..06) from the x86-64 bodies. Each case owns an
    // inactive GameObject and destroys it. Only the recoloured stream left in m_verts is
    // asserted after OnPopulateMesh, because the helper is not cleared before
    // AddUIVertexTriangleStream and what that engine call keeps is not pinned here.
    [TestFixture]
    public sealed class OriginalTriangleGradientTests
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // dump.cs TypeDefIndex 11827: Il2CppSetOption(NullChecks) before (ArrayBoundsChecks);
        // [SerializeField] colours, direction, offset and scale at +0xA4..+0xD0, then the
        // private m_distances/m_verts and the constants NormalizedScale = 0.5f and
        // inverse = -1.
        [Test]
        public void DeclarationKeepsTheOriginalAttributesFieldsAndConstants()
        {
            Type type = typeof(TriangleGradient);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.Namespace, Is.Null);
            Assert.That(type.BaseType, Is.EqualTo(typeof(Graphic)));
            Assert.That((int)type.Attributes, Is.EqualTo(1048577));
            Assert.That(type.GetNestedTypes(Declared), Is.Empty);
            Assert.That(type.GetProperties(Declared), Is.Empty);

            IList<CustomAttributeData> attributes = type.GetCustomAttributesData();
            Assert.That(attributes.Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(Il2CppSetOptionAttribute), typeof(Il2CppSetOptionAttribute) }));
            Assert.That(Convert.ToInt32(attributes[0].ConstructorArguments[0].Value), Is.EqualTo(1));
            Assert.That(Convert.ToInt32(attributes[1].ConstructorArguments[0].Value), Is.EqualTo(2));
            Assert.That(attributes.All(a => false.Equals(a.ConstructorArguments[1].Value)), Is.True);

            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "m_startColour", "m_endColour", "m_direction", "m_offset", "m_scale", "m_distances", "m_verts", "NormalizedScale", "inverse",
            }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[]
            {
                typeof(Color), typeof(Color), typeof(Vector2), typeof(float), typeof(float), typeof(float[]), typeof(List<UIVertex>), typeof(float), typeof(int),
            }));
            for (int i = 0; i < 7; ++i)
            {
                Assert.That(fields[i].Attributes, Is.EqualTo(FieldAttributes.Private), fields[i].Name);
                Type[] expected = i < 5 ? new[] { typeof(SerializeField) } : Type.EmptyTypes;
                Assert.That(fields[i].GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(expected), fields[i].Name);
            }
            Assert.That((int)fields[7].Attributes, Is.EqualTo(32849));
            Assert.That((int)fields[8].Attributes, Is.EqualTo(32849));
            Assert.That(fields[7].GetRawConstantValue(), Is.EqualTo(0.5f));
            Assert.That(fields[8].GetRawConstantValue(), Is.EqualTo(-1));

            MethodInfo[] methods = type.GetMethods(Declared);
            Assert.That(methods.Select(m => m.Name).ToArray(), Is.EqualTo(new[] { "OnPopulateMesh", "ApplyGradient" }));
            Assert.That((int)methods[0].Attributes, Is.EqualTo(196));
            Assert.That(methods[0].GetParameters().Single().Name, Is.EqualTo("vertexHelper"));
            Assert.That(methods[0].GetParameters().Single().ParameterType, Is.EqualTo(typeof(VertexHelper)));
            Assert.That(methods[0].GetBaseDefinition().DeclaringType, Is.EqualTo(typeof(Graphic)));
            Assert.That((int)methods[1].Attributes, Is.EqualTo(129));
            Assert.That(methods[1].GetParameters().Single().Name, Is.EqualTo("m_verts"));
            Assert.That(methods[1].GetParameters().Single().ParameterType, Is.EqualTo(typeof(List<UIVertex>)));
            Assert.That((int)type.GetConstructors(Declared).Single().Attributes, Is.EqualTo(6278));
        }

        // .ctor 0x06000006 @0x1B390C0: (1, 1, 1, 1) into both colours, Vector2.up into
        // m_direction, 1f into m_scale, a new List<UIVertex> into m_verts, then
        // Graphic..ctor. m_offset and m_distances keep their zero defaults.
        [Test]
        public void ConstructionDefaultsToWhiteUpwardsAndUnitScale()
        {
            using (var owned = new Owned(new Vector2(10f, 10f), new Vector2(0.5f, 0.5f)))
            {
                Assert.That(Get<Color>(owned.Gradient, "m_startColour"), Is.EqualTo(Color.white));
                Assert.That(Get<Color>(owned.Gradient, "m_endColour"), Is.EqualTo(Color.white));
                Assert.That(Get<Vector2>(owned.Gradient, "m_direction"), Is.EqualTo(Vector2.up));
                Assert.That(Get<float>(owned.Gradient, "m_offset"), Is.EqualTo(0f));
                Assert.That(Get<float>(owned.Gradient, "m_scale"), Is.EqualTo(1f));
                Assert.That(Get<float[]>(owned.Gradient, "m_distances"), Is.Null);
                Assert.That(Get<List<UIVertex>>(owned.Gradient, "m_verts"), Is.Empty);
                using (var serialized = new SerializedObject(owned.Gradient))
                {
                    Assert.That(serialized.FindProperty("m_scale").floatValue, Is.EqualTo(1f));
                    Assert.That(serialized.FindProperty("m_direction").vector2Value, Is.EqualTo(Vector2.up));
                    Assert.That(serialized.FindProperty("m_verts"), Is.Null);
                }
            }
        }

        // OnPopulateMesh 0x06000004: vertices (rect.x, rect.y) start/uv (0,0),
        // (rect.x, -rect.y) end/uv (0,1), (rect.x + rect.width, -rect.y) end/uv (1,1)
        // (0x1B38AD7..0x1B38C2D, the y lane negated by xorps 0x1B38B4F), triangle 0-1-2,
        // then m_verts is refilled from the helper and recoloured. A centred 40 x 20 rect
        // gives (-20, -10) (-20, 10) (20, 10); with the default up direction the first is
        // t = 0 and the other two t = 1.
        [Test]
        public void PopulateMeshBuildsTheTriangleFromTheRectOriginAndRecoloursIt()
        {
            using (var owned = new Owned(new Vector2(40f, 20f), new Vector2(0.5f, 0.5f)))
            {
                SetColours(owned.Gradient, Color.red, Color.blue);
                using (var vh = new VertexHelper())
                {
                    Populate(owned.Gradient, vh);
                    List<UIVertex> verts = Get<List<UIVertex>>(owned.Gradient, "m_verts");
                    Assert.That(verts.Count, Is.EqualTo(3));
                    Assert.That(verts.Select(v => v.position).ToArray(), Is.EqualTo(new[] { new Vector3(-20f, -10f, 0f), new Vector3(-20f, 10f, 0f), new Vector3(20f, 10f, 0f) }));
                    Assert.That(verts.Select(v => v.uv0).ToArray(), Is.EqualTo(new[] { new Vector4(0f, 0f, 0f, 0f), new Vector4(0f, 1f, 0f, 0f), new Vector4(1f, 1f, 0f, 0f) }));
                    Assert.That(verts.Select(v => v.color).ToArray(), Is.EqualTo(new[] { new Color32(255, 0, 0, 255), new Color32(0, 0, 255, 255), new Color32(0, 0, 255, 255) }));
                    Assert.That(Get<float[]>(owned.Gradient, "m_distances"), Is.EqualTo(new[] { -10f, 10f, 10f }));
                }
            }
        }

        // The triangle negates rect.y instead of using its top edge, so a bottom-left
        // pivot collapses it onto y = 0: (0, 0) (0, -0) (40, -0). With direction (1, 0)
        // the x projections are 0, 0, 40.
        [Test]
        public void PopulateMeshNegatesTheRectOriginRatherThanUsingTheTopEdge()
        {
            using (var owned = new Owned(new Vector2(40f, 20f), Vector2.zero))
            {
                SetColours(owned.Gradient, Color.red, Color.blue);
                Set(owned.Gradient, "m_direction", new Vector2(1f, 0f));
                using (var vh = new VertexHelper())
                {
                    Populate(owned.Gradient, vh);
                    List<UIVertex> verts = Get<List<UIVertex>>(owned.Gradient, "m_verts");
                    Assert.That(verts.Select(v => v.position).ToArray(), Is.EqualTo(new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(40f, 0f, 0f) }));
                    Assert.That(verts.Select(v => v.color).ToArray(), Is.EqualTo(new[] { new Color32(255, 0, 0, 255), new Color32(255, 0, 0, 255), new Color32(0, 0, 255, 255) }));
                }
            }
        }

        // ApplyGradient 0x06000005: t = ((d - min) / (max - min) + m_offset - 0.5) / m_scale
        // + 0.5 (0x1B38E14..0x1B38E3D), then Color32.Lerp of the two colours converted to
        // Color32 (integer channel deltas, clamp01 t, truncation; 0x1B38F0E..0x1B38FB3).
        // Offset 0.25, scale 2: t = 0.375 and 0.875, so red->blue gives (159, 0, 95) and
        // (31, 0, 223); a Color.Lerp then rounding would give blue 96.
        [Test]
        public void ApplyGradientOffsetsAndScalesAboutTheMiddleThenLerpsColour32()
        {
            using (var owned = new Owned(new Vector2(10f, 10f), new Vector2(0.5f, 0.5f)))
            {
                SetColours(owned.Gradient, Color.red, Color.blue);
                Set(owned.Gradient, "m_offset", 0.25f);
                Set(owned.Gradient, "m_scale", 2f);
                List<UIVertex> verts = Vertices(new Vector3(3f, -10f, 5f), new Vector3(-7f, 10f, 1f));
                Apply(owned.Gradient, verts);
                Assert.That(verts[0].color, Is.EqualTo(new Color32(159, 0, 95, 255)));
                Assert.That(verts[1].color, Is.EqualTo(new Color32(31, 0, 223, 255)));
                Assert.That(verts[0].position, Is.EqualTo(new Vector3(3f, -10f, 5f)));
                Assert.That(verts[1].uv0, Is.EqualTo(new Vector4(1f, 2f, 3f, 4f)));

                // t beyond [0, 1] clamps to the end colours.
                Set(owned.Gradient, "m_offset", 2f);
                Apply(owned.Gradient, verts);
                Assert.That(verts.Select(v => v.color).ToArray(), Is.EqualTo(new[] { new Color32(0, 0, 255, 255), new Color32(0, 0, 255, 255) }));
                Set(owned.Gradient, "m_offset", -2f);
                Apply(owned.Gradient, verts);
                Assert.That(verts.Select(v => v.color).ToArray(), Is.EqualTo(new[] { new Color32(255, 0, 0, 255), new Color32(255, 0, 0, 255) }));
            }
        }

        // 0x1B38D60..0x1B38DDE: a fresh float[Count] in m_distances holding each
        // position.xy dot m_direction (z ignored); min/max start at +/-float.MaxValue.
        [Test]
        public void ApplyGradientStoresEachProjectionInAFreshDistanceArray()
        {
            using (var owned = new Owned(new Vector2(10f, 10f), new Vector2(0.5f, 0.5f)))
            {
                Set(owned.Gradient, "m_direction", new Vector2(2f, -1f));
                List<UIVertex> verts = Vertices(new Vector3(1f, 2f, 100f), new Vector3(3f, 0f, -5f), new Vector3(-1f, 4f, 0f));
                Apply(owned.Gradient, verts);
                float[] first = Get<float[]>(owned.Gradient, "m_distances");
                Assert.That(first, Is.EqualTo(new[] { 0f, 6f, -6f }));
                // Range -6..6: t = 0.5, 1, 0 for the default offset and scale.
                SetColours(owned.Gradient, new Color(0f, 0f, 0f, 0f), Color.white);
                Apply(owned.Gradient, verts);
                Assert.That(verts.Select(v => v.color).ToArray(), Is.EqualTo(new[] { new Color32(127, 127, 127, 127), new Color32(255, 255, 255, 255), new Color32(0, 0, 0, 0) }));
                Assert.That(Get<float[]>(owned.Gradient, "m_distances"), Is.Not.SameAs(first));
            }
        }

        // The range folds minss/maxss (0x1B38DCA/0x1B38DCF) from +float.MaxValue
        // (0x29A6DF8) and -float.MaxValue (0x29A7504), so an all-positive stream keeps
        // its own minimum. Up projections 2, 4, 6: t = 0, 0.5, 1, red->blue gives
        // (255, 0, 0), (127, 0, 127) and (0, 0, 255). A range starting at zero would give
        // t = 1/3 and 2/3 for the first two.
        [Test]
        public void ApplyGradientNormalisesOverTheProjectedRangeOnly()
        {
            using (var owned = new Owned(new Vector2(10f, 10f), new Vector2(0.5f, 0.5f)))
            {
                SetColours(owned.Gradient, Color.red, Color.blue);
                List<UIVertex> verts = Vertices(new Vector3(-3f, 2f, 0f), new Vector3(5f, 4f, 0f), new Vector3(1f, 6f, 0f));
                Apply(owned.Gradient, verts);
                Assert.That(Get<float[]>(owned.Gradient, "m_distances"), Is.EqualTo(new[] { 2f, 4f, 6f }));
                Assert.That(verts.Select(v => v.color).ToArray(), Is.EqualTo(new[] { new Color32(255, 0, 0, 255), new Color32(127, 0, 127, 255), new Color32(0, 0, 255, 255) }));
            }
        }

        // 0x1B38CFA..0x1B38D00: an empty list returns before m_distances is replaced.
        [Test]
        public void ApplyGradientLeavesTheDistancesAloneForAnEmptyList()
        {
            using (var owned = new Owned(new Vector2(10f, 10f), new Vector2(0.5f, 0.5f)))
            {
                float[] previous = { 9f };
                Set(owned.Gradient, "m_distances", previous);
                var empty = new List<UIVertex>();
                Apply(owned.Gradient, empty);
                Assert.That(empty, Is.Empty);
                Assert.That(Get<float[]>(owned.Gradient, "m_distances"), Is.SameAs(previous));
            }
        }

        static T Get<T>(TriangleGradient gradient, string name)
        {
            FieldInfo field = typeof(TriangleGradient).GetField(name, Declared);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(gradient);
        }

        static void Set(TriangleGradient gradient, string name, object value)
        {
            typeof(TriangleGradient).GetField(name, Declared).SetValue(gradient, value);
        }

        static void SetColours(TriangleGradient gradient, Color start, Color end)
        {
            Set(gradient, "m_startColour", start);
            Set(gradient, "m_endColour", end);
        }

        static void Populate(TriangleGradient gradient, VertexHelper vh)
        {
            try { typeof(TriangleGradient).GetMethod("OnPopulateMesh", Declared, null, new[] { typeof(VertexHelper) }, null).Invoke(gradient, new object[] { vh }); }
            catch (TargetInvocationException e) { throw e.InnerException; }
        }

        static void Apply(TriangleGradient gradient, List<UIVertex> verts)
        {
            try { typeof(TriangleGradient).GetMethod("ApplyGradient", Declared).Invoke(gradient, new object[] { verts }); }
            catch (TargetInvocationException e) { throw e.InnerException; }
        }

        static List<UIVertex> Vertices(params Vector3[] positions)
        {
            var result = new List<UIVertex>();
            foreach (Vector3 position in positions)
            {
                UIVertex vertex = UIVertex.simpleVert;
                vertex.position = position;
                vertex.uv0 = new Vector4(1f, 2f, 3f, 4f);
                result.Add(vertex);
            }
            return result;
        }

        // An inactive RectTransform owner holding the TriangleGradient (with the
        // CanvasRenderer Graphic requires).
        sealed class Owned : IDisposable
        {
            readonly GameObject owner;
            public readonly TriangleGradient Gradient;

            public Owned(Vector2 size, Vector2 pivot)
            {
                owner = new GameObject("OriginalTriangleGradient fixture", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    owner.SetActive(false);
                    var rect = (RectTransform)owner.transform;
                    rect.pivot = pivot;
                    rect.sizeDelta = size;
                    Gradient = owner.AddComponent<TriangleGradient>();
                }
                catch { Object.DestroyImmediate(owner); throw; }
            }

            public void Dispose() { if (owner != null) Object.DestroyImmediate(owner); }
        }
    }
}
