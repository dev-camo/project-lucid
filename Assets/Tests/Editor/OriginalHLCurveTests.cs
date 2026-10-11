using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

public sealed class OriginalHLCurveTests
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Test]
    public void OriginalCurveAndCollectionRetainCompleteDeclarationContracts()
    {
        Type curve = typeof(HLCurve);
        Type collection = typeof(HLCurveAnimatedGraphicCurveCollection);
        Assert.That(curve.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
        Assert.That(collection.Assembly, Is.SameAs(curve.Assembly));
        CheckType(curve, 1048833, "NewCurveAsset", "Hardlight/Curve Asset", 40, new[] { 2, 1 });
        CheckType(collection, 1048577, "NewCurveAnimatedGraphicCurveCollectionAsset", "Hardlight/CurveAnimatedGraphic Curve Collection Asset", 41, new[] { 1, 2 });
        CheckFields(curve, new[] { "m_curve" }, new[] { typeof(AnimationCurve) });
        CheckFields(collection,
            new[] { "m_onPressedXCurve", "m_onPressedYCurve", "m_onReleasedXCurve", "m_onReleasedYCurve", "m_onDisabledXCurve", "m_onDisabledYCurve", "m_onDisabledAnimDirection" },
            new[] { curve, curve, curve, curve, curve, curve, typeof(Vector2) });
        Assert.That(curve.GetMethods(Declared).Length, Is.EqualTo(10));
        Assert.That(collection.GetMethods(Declared).Length, Is.EqualTo(7));
        CheckMethod(curve, "get_Curve", typeof(AnimationCurve), 2182, Type.EmptyTypes, new string[0]);
        CheckMethod(curve, "EndTime", typeof(float), 134, Type.EmptyTypes, new string[0]);
        CheckMethod(curve, "SampleAtTime", typeof(float), 134, new[] { typeof(float) }, new[] { "time" });
        CheckMethod(curve, "SampleNormalised", typeof(float), 134, new[] { typeof(float) }, new[] { "normalisedPercent" });
        foreach (Type input in new[] { typeof(AnimationCurve), curve })
        {
            CheckMethod(curve, "GetCurveEndTime", typeof(float), 150, new[] { input }, new[] { "curve" });
            CheckMethod(curve, "SampleCurveAtTime", typeof(float), 150, new[] { input, typeof(float) }, new[] { "curve", "time" });
            CheckMethod(curve, "SampleCurveNormalised", typeof(float), 150, new[] { input, typeof(float) }, new[] { "curve", "normalisedPercent" });
        }
        string[] properties = { "OnPressedXCurve", "OnPressedYCurve", "OnReleasedXCurve", "OnReleasedYCurve", "OnDisabledXCurve", "OnDisabledYCurve", "OnDisabledAnimDirection" };
        for (int i = 0; i < properties.Length; ++i)
            CheckMethod(collection, "get_" + properties[i], i == 6 ? typeof(Vector2) : curve, 2182, Type.EmptyTypes, new string[0]);
        CheckProperties(curve, new[] { "Curve" }, new[] { typeof(AnimationCurve) });
        CheckProperties(collection, properties, new[] { curve, curve, curve, curve, curve, curve, typeof(Vector2) });
    }

    [Test]
    public void OriginalFreshCurveOwnsEmptyAnimationCurveAndPositiveZeroEnd()
    {
        HLCurve curve = ScriptableObject.CreateInstance<HLCurve>();
        try
        {
            Assert.That(curve.Curve, Is.Not.Null);
            AnimationCurve owned = curve.Curve;
            Assert.That(curve.Curve, Is.SameAs(owned));
            Assert.That(owned.length, Is.Zero);
            foreach (float value in new[] { curve.EndTime(), HLCurve.GetCurveEndTime(owned), HLCurve.GetCurveEndTime(curve) })
                Assert.That(BitConverter.ToInt32(BitConverter.GetBytes(value), 0), Is.Zero, "Empty end time retains positive zero bits.");
            foreach (float time in new[] { -3f, 0f, 5f })
                CheckRawSamples(curve, time, owned.Evaluate(time));
            foreach (float fraction in new[] { -1f, 0f, 0.5f, 1f, 2f })
                CheckNormalisedSamples(curve, fraction, owned.Evaluate(0f));
            Assert.That(curve.Curve, Is.SameAs(owned));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(curve);
        }
    }

    [Test]
    public void OriginalNormalisedSamplesUseZeroToLastKeyAndAllThreeRoutes()
    {
        HLCurve curve = ScriptableObject.CreateInstance<HLCurve>();
        try
        {
            SetCurve(curve, AnimationCurve.Linear(2f, 10f, 4f, 20f));
            AnimationCurve owned = curve.Curve;
            Assert.That(owned.length, Is.EqualTo(2));
            Assert.That(owned[0].time, Is.EqualTo(2f));
            Assert.That(curve.EndTime(), Is.EqualTo(4f));
            Assert.That(HLCurve.GetCurveEndTime(owned), Is.EqualTo(4f));
            Assert.That(HLCurve.GetCurveEndTime(curve), Is.EqualTo(4f));
            foreach (float fraction in new[] { -1f, 0f, 0.25f, 0.5f, 0.75f, 1f, 2f })
            {
                float time = fraction < 0f ? 0f : fraction > 1f ? 4f : fraction * 4f;
                CheckNormalisedSamples(curve, fraction, owned.Evaluate(time));
            }
            Assert.That(curve.SampleNormalised(0.5f), Is.EqualTo(10f).Within(0.00001f), "Half maps to time two, rather than the midpoint of the first/last key interval.");
            Assert.That(curve.SampleNormalised(0.75f), Is.EqualTo(15f).Within(0.00001f));
            Assert.That(curve.Curve, Is.SameAs(owned));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(curve);
        }
    }

    [Test]
    public void OriginalRawTimeSamplesPreserveOwnedLoopWrappingWithoutClamp()
    {
        HLCurve curve = ScriptableObject.CreateInstance<HLCurve>();
        try
        {
            AnimationCurve serialized = AnimationCurve.Linear(2f, 10f, 4f, 20f);
            serialized.preWrapMode = WrapMode.Loop;
            serialized.postWrapMode = WrapMode.Loop;
            SetCurve(curve, serialized);
            AnimationCurve owned = curve.Curve;
            Assert.That(owned.preWrapMode, Is.EqualTo(WrapMode.Loop));
            Assert.That(owned.postWrapMode, Is.EqualTo(WrapMode.Loop));
            foreach (float time in new[] { 1f, 3f, 5f })
                CheckRawSamples(curve, time, owned.Evaluate(time));
            Assert.That(owned.Evaluate(1f), Is.Not.EqualTo(owned.Evaluate(2f)), "The pre-wrap sample distinguishes forwarding from endpoint clamping.");
            Assert.That(owned.Evaluate(5f), Is.Not.EqualTo(owned.Evaluate(4f)), "The post-wrap sample distinguishes forwarding from endpoint clamping.");
            Assert.That(curve.Curve, Is.SameAs(owned));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(curve);
        }
    }

    [Test]
    public void OriginalCollectionSerializesSixOwnedCurvesAndDirectionThroughGetters()
    {
        HLCurveAnimatedGraphicCurveCollection collection = null;
        HLCurve[] curves = new HLCurve[6];
        try
        {
            collection = ScriptableObject.CreateInstance<HLCurveAnimatedGraphicCurveCollection>();
            Assert.That(collection.OnPressedXCurve, Is.Null);
            Assert.That(collection.OnPressedYCurve, Is.Null);
            Assert.That(collection.OnReleasedXCurve, Is.Null);
            Assert.That(collection.OnReleasedYCurve, Is.Null);
            Assert.That(collection.OnDisabledXCurve, Is.Null);
            Assert.That(collection.OnDisabledYCurve, Is.Null);
            Assert.That(collection.OnDisabledAnimDirection, Is.EqualTo(Vector2.zero));
            for (int i = 0; i < curves.Length; ++i)
                curves[i] = ScriptableObject.CreateInstance<HLCurve>();
            string[] fields = { "m_onPressedXCurve", "m_onPressedYCurve", "m_onReleasedXCurve", "m_onReleasedYCurve", "m_onDisabledXCurve", "m_onDisabledYCurve" };
            using (SerializedObject serialized = new SerializedObject(collection))
            {
                serialized.Update();
                for (int i = 0; i < fields.Length; ++i)
                {
                    SerializedProperty property = serialized.FindProperty(fields[i]);
                    Assert.That(property, Is.Not.Null);
                    Assert.That(property.propertyType, Is.EqualTo(SerializedPropertyType.ObjectReference));
                    property.objectReferenceValue = curves[i];
                }
                SerializedProperty direction = serialized.FindProperty("m_onDisabledAnimDirection");
                Assert.That(direction, Is.Not.Null);
                Assert.That(direction.propertyType, Is.EqualTo(SerializedPropertyType.Vector2));
                direction.vector2Value = new Vector2(-2.5f, 7.25f);
                Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
            }
            HLCurve[] returned = { collection.OnPressedXCurve, collection.OnPressedYCurve, collection.OnReleasedXCurve, collection.OnReleasedYCurve, collection.OnDisabledXCurve, collection.OnDisabledYCurve };
            for (int i = 0; i < curves.Length; ++i)
                Assert.That(returned[i], Is.SameAs(curves[i]));
            Assert.That(collection.OnDisabledAnimDirection, Is.EqualTo(new Vector2(-2.5f, 7.25f)));
        }
        finally
        {
            try
            {
                if (collection != null)
                    UnityEngine.Object.DestroyImmediate(collection);
            }
            finally
            {
                // Each owned destruction gets an independent attempt even if another cleanup throws.
                DestroyCurves(curves, curves.Length - 1);
            }
        }
    }

    private static void DestroyCurves(HLCurve[] curves, int index)
    {
        if (index < 0) return;
        try
        {
            if (curves[index] != null) UnityEngine.Object.DestroyImmediate(curves[index]);
        }
        finally
        {
            DestroyCurves(curves, index - 1);
        }
    }

    private static void SetCurve(HLCurve curve, AnimationCurve value)
    {
        using (SerializedObject serialized = new SerializedObject(curve))
        {
            serialized.Update();
            SerializedProperty property = serialized.FindProperty("m_curve");
            Assert.That(property, Is.Not.Null);
            Assert.That(property.propertyType, Is.EqualTo(SerializedPropertyType.AnimationCurve));
            property.animationCurveValue = value;
            Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
        }
        Assert.That(curve.Curve, Is.Not.Null);
    }

    private static void CheckRawSamples(HLCurve curve, float time, float expected)
    {
        Assert.That(curve.SampleAtTime(time), Is.EqualTo(expected));
        Assert.That(HLCurve.SampleCurveAtTime(curve.Curve, time), Is.EqualTo(expected));
        Assert.That(HLCurve.SampleCurveAtTime(curve, time), Is.EqualTo(expected));
    }

    private static void CheckNormalisedSamples(HLCurve curve, float fraction, float expected)
    {
        Assert.That(curve.SampleNormalised(fraction), Is.EqualTo(expected));
        Assert.That(HLCurve.SampleCurveNormalised(curve.Curve, fraction), Is.EqualTo(expected));
        Assert.That(HLCurve.SampleCurveNormalised(curve, fraction), Is.EqualTo(expected));
    }

    private static void CheckType(Type type, int attributes, string fileName, string menuName, int order, int[] optionOrder)
    {
        Assert.That((int)type.Attributes, Is.EqualTo(attributes));
        Assert.That(type.BaseType, Is.EqualTo(typeof(ScriptableObject)));
        Assert.That(type.IsGenericType, Is.False);
        Assert.That(type.GetNestedTypes(Declared), Is.Empty);
        Assert.That(type.GetEvents(Declared), Is.Empty);
        Assert.That(type.GetInterfaces(), Is.Empty);
        ConstructorInfo constructor = type.GetConstructors(Declared).Single();
        Assert.That((int)constructor.Attributes, Is.EqualTo(6278));
        Assert.That(constructor.GetParameters(), Is.Empty);
        Assert.That(constructor.GetCustomAttributesData(), Is.Empty);
        var attributesData = type.GetCustomAttributesData();
        Assert.That(attributesData.Count, Is.EqualTo(3));
        for (int i = 0; i < 2; ++i)
        {
            var attribute = attributesData[i];
            Assert.That(attribute.AttributeType, Is.EqualTo(typeof(Il2CppSetOptionAttribute)));
            Assert.That(attribute.Constructor.DeclaringType.Assembly.GetName().Name, Is.EqualTo("HLUnityCore.Runtime"));
            Assert.That(attribute.Constructor.GetParameters().Select(p => p.ParameterType).ToArray(), Is.EqualTo(new[] { typeof(Option), typeof(object) }));
            Assert.That(attribute.ConstructorArguments.Count, Is.EqualTo(2));
            Assert.That(attribute.ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(Option)));
            Assert.That(Convert.ToInt32(attribute.ConstructorArguments[0].Value), Is.EqualTo(optionOrder[i]));
            Assert.That(attribute.ConstructorArguments[1].ArgumentType, Is.EqualTo(typeof(bool)));
            Assert.That(attribute.ConstructorArguments[1].Value, Is.EqualTo(false));
            Assert.That(attribute.NamedArguments, Is.Empty);
        }
        var menu = attributesData[2];
        Assert.That(menu.AttributeType, Is.EqualTo(typeof(CreateAssetMenuAttribute)));
        Assert.That(menu.Constructor.GetParameters(), Is.Empty);
        Assert.That(menu.ConstructorArguments, Is.Empty);
        Assert.That(menu.NamedArguments.Select(a => a.MemberName).ToArray(), Is.EqualTo(new[] { "fileName", "menuName", "order" }));
        Assert.That(menu.NamedArguments.Select(a => a.IsField).ToArray(), Is.EqualTo(new[] { false, false, false }));
        Assert.That(menu.NamedArguments.Select(a => a.TypedValue.ArgumentType).ToArray(), Is.EqualTo(new[] { typeof(string), typeof(string), typeof(int) }));
        Assert.That(menu.NamedArguments.Select(a => a.TypedValue.Value).ToArray(), Is.EqualTo(new object[] { fileName, menuName, order }));
    }

    private static void CheckFields(Type type, string[] names, Type[] types)
    {
        FieldInfo[] fields = type.GetFields(Declared);
        Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(names));
        Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(types));
        foreach (FieldInfo field in fields)
        {
            Assert.That((int)field.Attributes, Is.EqualTo(1));
            var attributes = field.GetCustomAttributesData();
            Assert.That(attributes.Count, Is.EqualTo(1));
            Assert.That(attributes[0].AttributeType, Is.EqualTo(typeof(SerializeField)));
            Assert.That(attributes[0].Constructor.GetParameters(), Is.Empty);
            Assert.That(attributes[0].ConstructorArguments, Is.Empty);
            Assert.That(attributes[0].NamedArguments, Is.Empty);
        }
    }

    private static void CheckMethod(Type type, string name, Type result, int attributes, Type[] parameters, string[] names)
    {
        MethodInfo method = type.GetMethods(Declared).Single(m => m.Name == name && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters));
        Assert.That(method.ReturnType, Is.EqualTo(result));
        Assert.That((int)method.Attributes, Is.EqualTo(attributes));
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

    private static void CheckProperties(Type type, string[] names, Type[] results)
    {
        PropertyInfo[] properties = type.GetProperties(Declared);
        Assert.That(properties.Select(p => p.Name).ToArray(), Is.EqualTo(names));
        Assert.That(properties.Select(p => p.PropertyType).ToArray(), Is.EqualTo(results));
        foreach (PropertyInfo property in properties)
        {
            Assert.That(property.Attributes, Is.EqualTo(PropertyAttributes.None));
            Assert.That(property.GetIndexParameters(), Is.Empty);
            Assert.That(property.GetSetMethod(true), Is.Null);
            Assert.That(property.GetGetMethod(true), Is.Not.Null);
            Assert.That(property.GetCustomAttributesData(), Is.Empty);
        }
    }
}
