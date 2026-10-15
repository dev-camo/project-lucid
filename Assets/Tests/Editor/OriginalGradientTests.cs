using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Unity.IL2CPP.CompilerServices;
using OriginalGradient = Hardlight.Gradient;

public sealed class OriginalGradientTests
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Test]
    public void OriginalGradientRetainsWholeDeclarationAndOrderedAnnotations()
    {
        Type type = typeof(OriginalGradient);
        Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
        Assert.That(type.BaseType, Is.EqualTo(typeof(BaseMeshEffect)));
        Assert.That((int)type.Attributes, Is.EqualTo(1048577));
        Assert.That(type.IsGenericType, Is.False);
        Assert.That(type.GetNestedTypes(Declared), Is.Empty);
        Assert.That(type.GetEvents(Declared), Is.Empty);
        Assert.That(type.GetInterfaces(), Is.EqualTo(typeof(BaseMeshEffect).GetInterfaces()));
        var attributes = type.GetCustomAttributesData();
        Assert.That(attributes.Count, Is.EqualTo(3));
        for (int i = 0; i < 2; ++i)
        {
            var option = attributes[i];
            Assert.That(option.AttributeType, Is.EqualTo(typeof(Il2CppSetOptionAttribute)));
            Assert.That(option.Constructor.DeclaringType.Assembly.GetName().Name, Is.EqualTo("HLUnityCore.Runtime"));
            Assert.That(option.Constructor.GetParameters().Select(p => p.ParameterType).ToArray(), Is.EqualTo(new[] { typeof(Option), typeof(object) }));
            Assert.That(option.ConstructorArguments.Count, Is.EqualTo(2));
            Assert.That(option.ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(Option)));
            Assert.That(Convert.ToInt32(option.ConstructorArguments[0].Value), Is.EqualTo(i + 1));
            Assert.That(option.ConstructorArguments[1].ArgumentType, Is.EqualTo(typeof(bool)));
            Assert.That(option.ConstructorArguments[1].Value, Is.EqualTo(false));
            Assert.That(option.NamedArguments, Is.Empty);
        }
        Assert.That(attributes[2].AttributeType, Is.EqualTo(typeof(AddComponentMenu)));
        Assert.That(attributes[2].Constructor.GetParameters().Select(p => p.ParameterType).ToArray(), Is.EqualTo(new[] { typeof(string), typeof(int) }));
        Assert.That(attributes[2].ConstructorArguments.Select(p => p.Value).ToArray(), Is.EqualTo(new object[] { "Hardlight UI/Effects/Gradient", 3 }));
        Assert.That(attributes[2].NamedArguments, Is.Empty);

        string[] fields = { "m_startColor", "m_endColor", "m_direction", "m_offset", "m_scale", "m_greyscaleFactor", "m_brightness" };
        Type[] fieldTypes = { typeof(Color), typeof(Color), typeof(Vector2), typeof(float), typeof(float), typeof(float), typeof(float) };
        FieldInfo[] actual = type.GetFields(Declared);
        Assert.That(actual.Select(f => f.Name).ToArray(), Is.EqualTo(fields));
        Assert.That(actual.Select(f => f.FieldType).ToArray(), Is.EqualTo(fieldTypes));
        for (int i = 0; i < actual.Length; ++i)
        {
            Assert.That((int)actual[i].Attributes, Is.EqualTo(1));
            var data = actual[i].GetCustomAttributesData();
            Type[] expected = i < 2 ? new[] { typeof(SerializeField), typeof(FormerlySerializedAsAttribute) }
                : i == 5 ? new[] { typeof(SerializeField), typeof(UnityEngine.RangeAttribute) }
                : i == 6 ? new[] { typeof(UnityEngine.RangeAttribute), typeof(SerializeField) } : new[] { typeof(SerializeField) };
            Assert.That(data.Select(a => a.AttributeType).ToArray(), Is.EqualTo(expected));
            foreach (var attribute in data)
            {
                Assert.That(attribute.NamedArguments, Is.Empty);
                if (attribute.AttributeType == typeof(FormerlySerializedAsAttribute))
                {
                    Assert.That(attribute.Constructor.GetParameters().Select(p => p.ParameterType).ToArray(), Is.EqualTo(new[] { typeof(string) }));
                    Assert.That(attribute.ConstructorArguments.Single().Value, Is.EqualTo(i == 0 ? "m_end" : "m_start"));
                }
                else if (attribute.AttributeType == typeof(UnityEngine.RangeAttribute))
                {
                    Assert.That(attribute.Constructor.GetParameters().Select(p => p.ParameterType).ToArray(), Is.EqualTo(new[] { typeof(float), typeof(float) }));
                    Assert.That(attribute.ConstructorArguments.Select(p => p.Value).ToArray(), Is.EqualTo(new object[] { 0f, 1f }));
                }
                else
                {
                    Assert.That(attribute.Constructor.GetParameters(), Is.Empty);
                    Assert.That(attribute.ConstructorArguments, Is.Empty);
                }
            }
        }

        ConstructorInfo constructor = type.GetConstructors(Declared).Single();
        Assert.That((int)constructor.Attributes, Is.EqualTo(6278));
        Assert.That(constructor.GetParameters(), Is.Empty);
        Assert.That(constructor.GetCustomAttributesData(), Is.Empty);
        Assert.That(constructor.GetMethodImplementationFlags(), Is.EqualTo(MethodImplAttributes.IL));
        Assert.That(type.GetMethods(Declared).Length, Is.EqualTo(18));
        Assert.That(type.GetMethods(Declared).Sum(m => m.GetParameters().Length), Is.EqualTo(11));
        CheckMethod("ModifyMesh", typeof(void), 198, new[] { typeof(VertexHelper) }, new[] { "vh" });
        string[] properties = { "StartColour", "EndColour", "Offset", "Scale", "Direction", "GreyscaleFactor", "Brightness" };
        Type[] results = { typeof(Color), typeof(Color), typeof(float), typeof(float), typeof(Vector2), typeof(float), typeof(float) };
        PropertyInfo[] declaredProperties = type.GetProperties(Declared);
        Assert.That(declaredProperties.Select(p => p.Name).ToArray(), Is.EqualTo(properties));
        Assert.That(declaredProperties.Select(p => p.PropertyType).ToArray(), Is.EqualTo(results));
        for (int i = 0; i < properties.Length; ++i)
        {
            CheckMethod("get_" + properties[i], results[i], 2182, Type.EmptyTypes, new string[0]);
            CheckMethod("set_" + properties[i], typeof(void), 2182, new[] { results[i] }, new[] { "value" });
            Assert.That(declaredProperties[i].Attributes, Is.EqualTo(PropertyAttributes.None));
            Assert.That(declaredProperties[i].GetIndexParameters(), Is.Empty);
            Assert.That(declaredProperties[i].GetCustomAttributesData(), Is.Empty);
        }
        CheckMethod("SetGreyscaleFactorAndBrightness", typeof(void), 134, new[] { typeof(float), typeof(float) }, new[] { "greyscaleFactor", "brightness" });
        CheckMethod("Awake", typeof(void), 132, Type.EmptyTypes, new string[0]);
        CheckMethod("ApplyGradient", typeof(void), 129, new[] { typeof(List<UIVertex>) }, new[] { "verts" });
        MethodInfo awake = type.GetMethod("Awake", Declared);
        Assert.That(awake.IsVirtual, Is.False);
        Assert.That(awake.GetBaseDefinition(), Is.EqualTo(awake));
    }

    [Test]
    public void OriginalGradientNormalOwnedConstructionRetainsDefaults()
    {
        GameObject owner = null;
        try
        {
            owner = new GameObject("OriginalGradient-owned-defaults", typeof(RectTransform));
            owner.SetActive(false);
            Image image = owner.AddComponent<Image>();
            OriginalGradient gradient = owner.AddComponent<OriginalGradient>();
            Assert.That(image, Is.Not.Null);
            Assert.That(image.color, Is.EqualTo(Color.white));
            Assert.That(gradient.StartColour, Is.EqualTo(Color.white));
            Assert.That(gradient.EndColour, Is.EqualTo(Color.white));
            Assert.That(gradient.Direction, Is.EqualTo(Vector2.up));
            Assert.That(gradient.Scale, Is.EqualTo(1f));
            Assert.That(gradient.Offset, Is.Zero);
            Assert.That(gradient.GreyscaleFactor, Is.Zero);
            Assert.That(gradient.Brightness, Is.Zero);
            using (SerializedObject serialized = new SerializedObject(gradient))
            {
                serialized.Update();
                Assert.That(serialized.FindProperty("m_startColor").colorValue, Is.EqualTo(Color.white));
                Assert.That(serialized.FindProperty("m_endColor").colorValue, Is.EqualTo(Color.white));
                Assert.That(serialized.FindProperty("m_direction").vector2Value, Is.EqualTo(Vector2.up));
                Assert.That(serialized.FindProperty("m_scale").floatValue, Is.EqualTo(1f));
                Assert.That(serialized.FindProperty("m_brightness").floatValue, Is.Zero);
            }
        }
        finally { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); }
    }

    [Test]
    public void OriginalGradientPublicSettersRetainUnboundedFiniteValues()
    {
        GameObject owner = null;
        try
        {
            owner = new GameObject("OriginalGradient-owned-setters", typeof(RectTransform));
            owner.SetActive(false);
            owner.AddComponent<Image>();
            OriginalGradient gradient = owner.AddComponent<OriginalGradient>();
            Color start = new Color(-0.5f, 2f, 0.25f, 1.5f);
            Color end = new Color(3f, -1f, 0.75f, -0.25f);
            gradient.StartColour = start;
            gradient.EndColour = end;
            gradient.Offset = -2.25f;
            gradient.Scale = -3f;
            gradient.Direction = new Vector2(3f, 4f);
            gradient.GreyscaleFactor = -1.25f;
            gradient.Brightness = 2.5f;
            Assert.That(gradient.StartColour, Is.EqualTo(start));
            Assert.That(gradient.EndColour, Is.EqualTo(end));
            Assert.That(gradient.Offset, Is.EqualTo(-2.25f));
            Assert.That(gradient.Scale, Is.EqualTo(-3f));
            Assert.That(gradient.Direction, Is.EqualTo(new Vector2(3f, 4f)));
            Assert.That(gradient.GreyscaleFactor, Is.EqualTo(-1.25f));
            Assert.That(gradient.Brightness, Is.EqualTo(2.5f));
            gradient.SetGreyscaleFactorAndBrightness(4f, -2f);
            Assert.That(gradient.GreyscaleFactor, Is.EqualTo(4f));
            Assert.That(gradient.Brightness, Is.EqualTo(-2f));
            Assert.That(gradient.Scale, Is.EqualTo(-3f));
        }
        finally { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); }
    }

    [Test]
    public void OriginalGradientInactiveModifyMeshKeepsWholeTriangleStream()
    {
        GameObject owner = null;
        try
        {
            owner = new GameObject("OriginalGradient-owned-inactive", typeof(RectTransform));
            owner.SetActive(false);
            owner.AddComponent<Image>();
            OriginalGradient gradient = owner.AddComponent<OriginalGradient>();
            Assert.That(gradient.IsActive(), Is.False);
            using (VertexHelper vertices = Triangle())
            {
                List<UIVertex> before = Stream(vertices);
                gradient.ModifyMesh(vertices);
                List<UIVertex> after = Stream(vertices);
                Assert.That(vertices.currentVertCount, Is.EqualTo(3));
                Assert.That(vertices.currentIndexCount, Is.EqualTo(3));
                Assert.That(after.Count, Is.EqualTo(before.Count));
                for (int i = 0; i < after.Count; ++i) CheckVertexChannels(before[i], after[i], true);
            }
        }
        finally { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); }
    }

    [Test]
    public void OriginalGradientActiveModifyMeshReplacesColorAndUV1Only()
    {
        GameObject owner = null;
        try
        {
            owner = new GameObject("OriginalGradient-owned-active", typeof(RectTransform));
            owner.SetActive(false);
            owner.AddComponent<Image>();
            OriginalGradient gradient = owner.AddComponent<OriginalGradient>();
            owner.SetActive(true);
            Assert.That(gradient.IsActive(), Is.True);
            Color32 start = new Color32(240, 40, 60, 255);
            Color32 end = new Color32(20, 80, 220, 128);
            gradient.StartColour = start;
            gradient.EndColour = end;
            gradient.Direction = Vector2.up;
            gradient.Offset = 0f;
            gradient.Scale = 1f;
            gradient.SetGreyscaleFactorAndBrightness(-1.25f, 2.5f);
            using (VertexHelper vertices = Triangle())
            {
                List<UIVertex> before = Stream(vertices);
                gradient.ModifyMesh(vertices);
                List<UIVertex> after = Stream(vertices);
                Assert.That(vertices.currentVertCount, Is.EqualTo(3));
                Assert.That(vertices.currentIndexCount, Is.EqualTo(3));
                Assert.That(after.Count, Is.EqualTo(3));
                Color32[] expected = { end, Color32.Lerp(end, start, 0.5f), start };
                for (int i = 0; i < after.Count; ++i)
                {
                    CheckVertexChannels(before[i], after[i], false);
                    Assert.That(after[i].color, Is.EqualTo(expected[i]));
                    Assert.That(after[i].color, Is.Not.EqualTo(before[i].color), "Original colors are replaced rather than multiplied.");
                    Assert.That(after[i].uv1, Is.EqualTo(new Vector4(-1.25f, 2.5f, 0f, 0f)));
                }
            }
        }
        finally { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); }
    }

    // 0x1b4e090 x86-64 ApplyGradient: projection = x*dir.x + y*dir.y (direction at
    // +0x48), t = ((p - min) / span + offset(+0x50) - 0.5) / scale(+0x54) + 0.5,
    // then Color32.Lerp(end(+0x38), start(+0x28), t) with t clamped to [0, 1] and
    // each channel truncated from a + (b - a) * t; uv1 = (greyscale, brightness, 0, 0).
    // Direction (0.75, 0.5) projects the triangle (0,0), (2,1), (0,2) to 0, 2, 1, so
    // every t below is dyadic and each byte is exact; expectations are hand-derived.
    // Mutations caught: dropping the x term, applying offset or scale in another
    // order, an unclamped lerp, swapped endpoints, and a zero-span guard (the
    // zero-direction stream must still rewrite uv1 rather than return early).
    [Test]
    public void OriginalGradientActiveModifyMeshProjectsOffsetsScalesAndClamps()
    {
        GameObject owner = null;
        try
        {
            owner = new GameObject("OriginalGradient-owned-projection", typeof(RectTransform));
            owner.SetActive(false);
            owner.AddComponent<Image>();
            OriginalGradient gradient = owner.AddComponent<OriginalGradient>();
            owner.SetActive(true);
            Assert.That(gradient.IsActive(), Is.True);
            gradient.StartColour = new Color32(240, 40, 60, 255);
            gradient.EndColour = new Color32(20, 80, 220, 128);
            gradient.Direction = new Vector2(0.75f, 0.5f);
            gradient.SetGreyscaleFactorAndBrightness(0.25f, 0.75f);

            // Interior: t = 0.375, 0.875, 0.625.
            gradient.Offset = 0.25f;
            gradient.Scale = 2f;
            ModifyAndCheck(gradient, new Vector4(0.25f, 0.75f, 0f, 0f),
                new Color32(102, 65, 160, 175), new Color32(212, 45, 80, 239), new Color32(157, 55, 120, 207));

            // Clamped: t = -0.25 -> 0 (end), 1.75 -> 1 (start), 0.75 interior.
            gradient.Offset = 0.125f;
            gradient.Scale = 0.5f;
            ModifyAndCheck(gradient, new Vector4(0.25f, 0.75f, 0f, 0f),
                new Color32(20, 80, 220, 128), new Color32(240, 40, 60, 255), new Color32(185, 50, 100, 223));

            // Zero span: a zero direction projects every vertex to 0, so t is NaN.
            // The NaN-to-byte colour stays unqualified; only the unguarded uv1 write is pinned.
            gradient.Direction = Vector2.zero;
            gradient.SetGreyscaleFactorAndBrightness(-0.5f, 1.5f);
            using (VertexHelper vertices = Triangle())
            {
                List<UIVertex> before = Stream(vertices);
                gradient.ModifyMesh(vertices);
                List<UIVertex> after = Stream(vertices);
                Assert.That(after.Count, Is.EqualTo(3));
                for (int i = 0; i < after.Count; ++i)
                {
                    CheckVertexChannels(before[i], after[i], false);
                    Assert.That(after[i].uv1, Is.EqualTo(new Vector4(-0.5f, 1.5f, 0f, 0f)));
                }
            }
        }
        finally { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); }
    }

    private static void ModifyAndCheck(OriginalGradient gradient, Vector4 uv1, params Color32[] expected)
    {
        using (VertexHelper vertices = Triangle())
        {
            List<UIVertex> before = Stream(vertices);
            gradient.ModifyMesh(vertices);
            List<UIVertex> after = Stream(vertices);
            Assert.That(vertices.currentVertCount, Is.EqualTo(3));
            Assert.That(after.Count, Is.EqualTo(expected.Length));
            for (int i = 0; i < after.Count; ++i)
            {
                CheckVertexChannels(before[i], after[i], false);
                Assert.That(after[i].color, Is.EqualTo(expected[i]), "vertex " + i);
                Assert.That(after[i].uv1, Is.EqualTo(uv1));
            }
        }
    }

    // 0x060002de Awake x86-64 0x1b4ea10: base UIBehaviour.Awake, then a zero
    // direction (sqrMagnitude < 1e-10) becomes Vector2.up, any other direction
    // is normalized in place, and maxss clamps scale to >= 0. Fails if Awake
    // keeps the authored (3,4), normalizes zero to zero, or leaves -2 scale.
    [Test]
    public void OriginalGradientAwakeNormalizesAuthoredDirectionAndClampsScale()
    {
        var owners = new List<GameObject>();
        try
        {
            OriginalGradient skewed = AuthoredInactive(owners, new Vector2(3f, 4f), -2f);
            OriginalGradient zero = AuthoredInactive(owners, Vector2.zero, 0.5f);
            // Authored values survive until the genuine engine Awake runs.
            Assert.That(skewed.Direction, Is.EqualTo(new Vector2(3f, 4f)));
            Assert.That(skewed.Scale, Is.EqualTo(-2f));
            Assert.That(zero.Direction, Is.EqualTo(Vector2.zero));
            foreach (GameObject owner in owners) owner.SetActive(true);
            Assert.That(skewed.IsActive() && zero.IsActive(), Is.True);
            Assert.That(skewed.Direction.x, Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(skewed.Direction.y, Is.EqualTo(0.8f).Within(1e-6f));
            Assert.That(skewed.Scale, Is.EqualTo(0f));
            Assert.That(zero.Direction, Is.EqualTo(Vector2.up));
            Assert.That(zero.Scale, Is.EqualTo(0.5f));
            // Setters after Awake stay unclamped and unnormalized.
            skewed.Direction = new Vector2(0f, -7f);
            skewed.Scale = -1f;
            Assert.That(skewed.Direction, Is.EqualTo(new Vector2(0f, -7f)));
            Assert.That(skewed.Scale, Is.EqualTo(-1f));
        }
        finally
        {
            for (int i = owners.Count - 1; i >= 0; --i)
                if (owners[i] != null) UnityEngine.Object.DestroyImmediate(owners[i]);
        }
    }

    private static OriginalGradient AuthoredInactive(List<GameObject> owners, Vector2 direction, float scale)
    {
        var owner = new GameObject("OriginalGradient-owned-awake", typeof(RectTransform));
        owners.Add(owner);
        owner.SetActive(false);
        owner.AddComponent<Image>();
        OriginalGradient gradient = owner.AddComponent<OriginalGradient>();
        using (SerializedObject serialized = new SerializedObject(gradient))
        {
            serialized.Update();
            serialized.FindProperty("m_direction").vector2Value = direction;
            serialized.FindProperty("m_scale").floatValue = scale;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        Assert.That(owner.activeInHierarchy, Is.False);
        return gradient;
    }

    private static VertexHelper Triangle()
    {
        VertexHelper vertices = new VertexHelper();
        try
        {
            for (int i = 0; i < 3; ++i)
            {
                UIVertex vertex = UIVertex.simpleVert;
                vertex.position = new Vector3(i == 1 ? 2f : 0f, i, i + 5f);
                vertex.color = new Color32(7, 11, 13, 17);
                vertex.normal = new Vector3(0f, 0f, -1f);
                vertex.tangent = new Vector4(1f, 0f, 0f, -1f);
                vertex.uv0 = new Vector4(i, 2f, 3f, 4f);
                vertex.uv1 = new Vector4(5f, 6f, 7f, 8f);
                vertex.uv2 = new Vector4(9f, i, 11f, 12f);
                vertex.uv3 = new Vector4(13f, 14f, i, 16f);
                vertices.AddVert(vertex);
            }
            vertices.AddTriangle(0, 1, 2);
            return vertices;
        }
        catch { vertices.Dispose(); throw; }
    }

    private static List<UIVertex> Stream(VertexHelper vertices)
    {
        List<UIVertex> result = new List<UIVertex>();
        vertices.GetUIVertexStream(result);
        return result;
    }

    private static void CheckVertexChannels(UIVertex before, UIVertex after, bool includeColorAndUV1)
    {
        Assert.That(after.position, Is.EqualTo(before.position));
        Assert.That(after.normal, Is.EqualTo(before.normal));
        Assert.That(after.tangent, Is.EqualTo(before.tangent));
        Assert.That(after.uv0, Is.EqualTo(before.uv0));
        Assert.That(after.uv2, Is.EqualTo(before.uv2));
        Assert.That(after.uv3, Is.EqualTo(before.uv3));
        if (includeColorAndUV1)
        {
            Assert.That(after.color, Is.EqualTo(before.color));
            Assert.That(after.uv1, Is.EqualTo(before.uv1));
        }
    }

    private static void CheckMethod(string name, Type result, int flags, Type[] parameters, string[] names)
    {
        MethodInfo method = typeof(OriginalGradient).GetMethods(Declared).Single(m => m.Name == name && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters));
        Assert.That(method.ReturnType, Is.EqualTo(result));
        Assert.That((int)method.Attributes, Is.EqualTo(flags));
        Assert.That(method.GetMethodImplementationFlags(), Is.EqualTo(MethodImplAttributes.IL));
        Assert.That(method.IsGenericMethod, Is.False);
        Assert.That(method.GetCustomAttributesData(), Is.Empty);
        Assert.That(method.ReturnParameter.GetCustomAttributesData(), Is.Empty);
        ParameterInfo[] actual = method.GetParameters();
        Assert.That(actual.Select(p => p.Name).ToArray(), Is.EqualTo(names));
        foreach (ParameterInfo parameter in actual)
        {
            Assert.That(parameter.Attributes, Is.EqualTo(ParameterAttributes.None));
            Assert.That(parameter.HasDefaultValue, Is.False);
            Assert.That(parameter.GetCustomAttributesData(), Is.Empty);
        }
    }
}
