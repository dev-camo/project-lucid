using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectLucid.Verification
{
    // Real original types only. Uninitialized allocation below exercises managed
    // field logic without calling MonoBehaviour/ScrollRect constructors or engine APIs.
    internal static class ScrollRectPreservationVerification
    {
        private const BindingFlags Own = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static void Check(bool value, string witness, ref int count)
        {
            count++;
            if (!value) throw new InvalidOperationException(witness);
        }
        private static HLScrollRect ManagedReceiver() => (HLScrollRect)FormatterServices.GetUninitializedObject(typeof(HLScrollRect));
        private static void Field(object receiver, string name, object value)
        {
            Type type = receiver.GetType();
            FieldInfo field;
            while ((field = type.GetField(name, Own)) == null) type = type.BaseType;
            field.SetValue(receiver, value);
        }
        private static void Small(HLScrollRect receiver, bool width, bool height)
        {
            Field(receiver, "<ContentWidthIsTooSmall>k__BackingField", width);
            Field(receiver, "<ContentHeightIsTooSmall>k__BackingField", height);
        }
        public static int OriginalDeclarations()
        {
            int count = 0;
            Type scroll = typeof(HLScrollRect), visual = typeof(HLScrollableVisualInterface);
            Check(scroll.Assembly.GetName().Name == "HLUnityUI.Runtime", "original UI assembly", ref count);
            Check(scroll.BaseType == typeof(ScrollRect) && !scroll.IsSealed, "original scroll base/flags", ref count);
            Check(visual.IsSealed && visual.BaseType == typeof(MonoBehaviour), "original sealed visual base", ref count);
            Check(visual.GetFields(Own).Length == 0, "fieldless visual", ref count);
            Check(visual.GetConstructors(Own).Length == 1 && visual.GetMethods(Own).Length == 0, "visual one ctor", ref count);
            Check(typeof(IScrollFlexible).Assembly.GetName().Name == "HLEventSystems.Runtime", "original marker assembly", ref count);
            Check(typeof(IScrollFlexible).IsInterface && typeof(IScrollFlexible).GetMethods().Length == 0, "zero-own-API marker", ref count);
            Check(typeof(IEventSystemHandler).IsAssignableFrom(typeof(IScrollFlexible)), "marker inherited real interface", ref count);
            Check(typeof(IScrollFlexible).IsAssignableFrom(visual), "visual marker", ref count);
            Check(scroll.GetFields(Own).Length == 18, "all original outer fields", ref count);
            Check((float)scroll.GetField("minSqrVelocityMagnitude", Own).GetRawConstantValue() == 100f, "100 original constant", ref count);
            Check(scroll.GetField("m_rectTransform", Own).IsFamily && scroll.GetField("m_routeToParentsInternal", Own).IsFamily, "protected original fields", ref count);
            MethodInfo position = scroll.GetMethod("SetContentAnchoredPosition", Own);
            Check(position.IsPublic && !position.IsVirtual, "original public nonvirtual hide", ref count);
            Check(scroll.GetProperty("ContentWidthIsTooSmall", Own).GetSetMethod(true).IsFamily, "width protected setter", ref count);
            Check(scroll.GetProperty("ContentHeightIsTooSmall", Own).GetSetMethod(true).IsFamily, "height protected setter", ref count);
            MethodInfo parent = scroll.GetMethod("DoForFirstFoundParent", Own);
            Type generic = parent.GetGenericArguments()[0];
            Check(generic.GenericParameterAttributes == GenericParameterAttributes.None && generic.GetGenericParameterConstraints().SequenceEqual(new[] { typeof(IEventSystemHandler) }), "exact generic constraint", ref count);
            Check(scroll.GetNestedTypes(BindingFlags.NonPublic).Length == 5, "four closures and iterator", ref count);
            Check(scroll.GetMethod("ResetToTop", Own).IsPrivate, "original private iterator factory", ref count);
            return count;
        }
        public static int RubberDeltaFixedVectors()
        {
            int count = 0;
            MethodInfo method = typeof(HLScrollRect).GetMethod("CalculateRubberDelta", Own);
            // Fixed binary32 outcomes independently derived from the retained native
            // float-product/double-reciprocal/float-product operation schedule.
            float[] stretch = { 0f, -0f, 1f, -1f, 20f, -20f, 100f, -100f, 1f, 100f, 20f, -20f, 1e-20f, 100f };
            float[] size = { 100f, 100f, 100f, 100f, 100f, 100f, 100f, 100f, 1f, 1f, -100f, -100f, 100f, 1e-20f };
            uint[] bits = { 0u, 0u, 0x3f0c07a3u, 0xbf0c07a3u, 0x411e8efeu, 0xc11e8efeu, 0x420def7cu, 0xc20def7cu, 0x3eb5ad6cu, 0x3f7b6db7u, 0x4145c0b8u, 0xc145c0b8u, 0u, 0x1e3ce508u };
            for (int i = 0; i < stretch.Length; i++)
            {
                float result = (float)method.Invoke(null, new object[] { stretch[i], size[i] });
                Check(BitConverter.ToUInt32(BitConverter.GetBytes(result), 0) == bits[i], "fixed rubber vector " + i, ref count);
            }
            foreach (float[] vector in new[] { new[] { 0f, 0f }, new[] { float.NaN, 100f }, new[] { 1f, float.NaN }, new[] { float.PositiveInfinity, float.PositiveInfinity } })
                Check(float.IsNaN((float)method.Invoke(null, new object[] { vector[0], vector[1] })), "unguarded nonfinite rubber vector", ref count);
            return count;
        }
        public static int PublishedSmallContentPredicate()
        {
            int count = 0;
            HLScrollRect receiver = ManagedReceiver();
            // Truth table covers both disabled axes and retained small flags.
            for (int mask = 0; mask < 32; mask++)
            {
                bool enabled = (mask & 1) != 0, horizontal = (mask & 2) != 0, vertical = (mask & 4) != 0, width = (mask & 8) != 0, height = (mask & 16) != 0;
                Field(receiver, "m_doNotScrollIfContentIsTooSmall", enabled);
                Field(receiver, "m_Horizontal", horizontal);
                Field(receiver, "m_Vertical", vertical);
                Small(receiver, width, height);
                bool expected = !enabled || (!horizontal || !width) && (!vertical || !height);
                Check(receiver.ContentNeedsScrolling() == expected, "small flag table " + mask, ref count);
            }
            return count;
        }
        public static int PointerClampAndFailurePrefixes()
        {
            int count = 0;
            HLScrollRect receiver = ManagedReceiver();
            MethodInfo clamp = typeof(HLScrollRect).GetMethod("ClampEventData", Own);
            Field(receiver, "m_cachedPointerDragStartPosition", new Vector2(11f, -13f));
            for (int mask = 0; mask < 32; mask++)
            {
                bool enabled = (mask & 1) != 0, horizontal = (mask & 2) != 0, vertical = (mask & 4) != 0, width = (mask & 8) != 0, height = (mask & 16) != 0;
                Field(receiver, "m_doNotScrollIfContentIsTooSmall", enabled);
                Field(receiver, "m_Horizontal", horizontal);
                Field(receiver, "m_Vertical", vertical);
                Small(receiver, width, height);
                PointerEventData data = new PointerEventData(null) { position = new Vector2(19f, 23f), delta = new Vector2(5f, -7f) };
                object[] args = { data };
                clamp.Invoke(receiver, args);
                bool x = enabled && horizontal && width, y = enabled && vertical && height;
                Check(ReferenceEquals(args[0], data), "original ref receiver retained " + mask, ref count);
                Check(data.position.x == (x ? 11f : 19f), "clamp position x " + mask, ref count);
                Check(data.position.y == (y ? -13f : 23f), "clamp position y " + mask, ref count);
                Check(data.delta.x == (x ? 0f : 5f), "clamp delta x " + mask, ref count);
                Check(data.delta.y == (y ? 0f : -7f), "clamp delta y " + mask, ref count);
            }
            Field(receiver, "m_doNotScrollIfContentIsTooSmall", false);
            object[] nullArgs = { null };
            clamp.Invoke(receiver, nullArgs);
            Check(nullArgs[0] == null, "disabled clamp returns before null parameter read", ref count);
            Field(receiver, "m_doNotScrollIfContentIsTooSmall", true);
            bool fault = false;
            try { clamp.Invoke(receiver, nullArgs); }
            catch (TargetInvocationException exception) { fault = exception.InnerException is NullReferenceException; }
            Check(fault && nullArgs[0] == null, "enabled clamp retains null and faults before writes", ref count);
            return count;
        }
    }
}
