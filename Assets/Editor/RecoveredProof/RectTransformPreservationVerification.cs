using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.OriginalPreservation
{
    public static class RectTransformPreservationVerification
    {
        private static int count;
        private static void Require(bool value, string context)
        {
            ++count;
            if (!value) throw new InvalidOperationException(context);
        }
        private static void Throws<T>(Action action, string context) where T : Exception
        {
            ++count;
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException(context);
        }
        private static void Point(Vector3 actual, Vector3 expected, string context)
        {
            Require(Mathf.Abs(actual.x - expected.x) < 0.00001f &&
                    Mathf.Abs(actual.y - expected.y) < 0.00001f && actual.z == expected.z, context);
        }

        public static int ManagedDeclarationAndFailurePrefixes()
        {
            count = 0;
            Type owner = typeof(RectTransformExtensions);
            Require(owner.Assembly.GetName().Name == "HLUnityCore.Runtime", "Original assembly owner");
            Require(owner.IsPublic && owner.IsAbstract && owner.IsSealed, "Original static type shape");
            Require(owner.IsDefined(typeof(ExtensionAttribute), false), "Original extension type attribute");
            Require(owner.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 0, "Original fieldless owner");
            MethodInfo[] methods = owner.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Require(methods.Length == 3, "All original own APIs, no aliases");
            string[] names = { "GetImmediateChildren", "ResetToFullscreen", "GetPointOnRectEdge" };
            int[] arities = { 2, 1, 2 };
            for (int i = 0; i < names.Length; ++i)
            {
                MethodInfo method = owner.GetMethod(names[i], BindingFlags.Public | BindingFlags.Static);
                Require(method != null && method.IsDefined(typeof(ExtensionAttribute), false), names[i] + " genuine extension API");
                ParameterInfo[] parameters = method.GetParameters();
                Require(parameters.Length == arities[i] && parameters[0].Name == "rectTransform" && parameters[0].ParameterType == typeof(RectTransform), names[i] + " receiver identity");
                Require(!parameters[0].IsOptional && !parameters[0].HasDefaultValue, names[i] + " required receiver");
            }
            var children = new List<RectTransform> { null, null };
            var previousEnumerator = children.GetEnumerator();
            Throws<NullReferenceException>(() => RectTransformExtensions.GetImmediateChildren(null, children), "Receiver fault after Clear");
            Require(children.Count == 0, "Clear completes before null receiver access");
            Throws<InvalidOperationException>(() => previousEnumerator.MoveNext(), "Clear invalidates prior list iterator before receiver fault");
            Throws<NullReferenceException>(() => RectTransformExtensions.GetImmediateChildren(null, null), "Null output faults directly");
            Throws<NullReferenceException>(() => RectTransformExtensions.ResetToFullscreen(null), "Null receiver fullscreen fault");
            Throws<NullReferenceException>(() => RectTransformExtensions.GetPointOnRectEdge(null, Vector2.zero), "Null receiver edge fault after zero-direction arithmetic");
            Throws<NullReferenceException>(() => RectTransformExtensions.GetPointOnRectEdge(null, new Vector2(2f, -1f)), "Null receiver edge fault after max-axis division");
            return count;
        }

        public static int ImmediateChildrenRetainsSiblingOrderAndInactiveNullSlots()
        {
            count = 0;
            GameObject root = new GameObject("OriginalRectChildren", typeof(RectTransform));
            try
            {
                RectTransform parent = (RectTransform)root.transform;
                GameObject first = new GameObject("first", typeof(RectTransform));
                GameObject plain = new GameObject("plain");
                GameObject last = new GameObject("last", typeof(RectTransform));
                first.transform.SetParent(parent, false);
                plain.transform.SetParent(parent, false);
                last.transform.SetParent(parent, false);
                last.SetActive(false);
                var children = new List<RectTransform> { parent };
                parent.GetImmediateChildren(children);
                Require(children.Count == 3, "Every immediate child is retained");
                Require(ReferenceEquals(children[0], first.transform), "First sibling identity");
                Require(ReferenceEquals(children[1], null), "Plain Transform contributes null slot");
                Require(ReferenceEquals(children[2], last.transform), "Inactive RectTransform is retained");
                last.transform.SetSiblingIndex(0);
                parent.GetImmediateChildren(children);
                Require(children.Count == 3 && ReferenceEquals(children[0], last.transform) && ReferenceEquals(children[1], first.transform) && ReferenceEquals(children[2], null), "Clear then current sibling order");
                return count;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        public static int FullscreenResetPreservesPivotRotationAndScale()
        {
            count = 0;
            GameObject root = new GameObject("OriginalRectFullscreen", typeof(RectTransform));
            try
            {
                RectTransform rect = (RectTransform)root.transform;
                rect.pivot = new Vector2(0.2f, 0.8f);
                rect.localRotation = Quaternion.Euler(0f, 0f, 17f);
                rect.localScale = new Vector3(2f, 3f, 4f);
                rect.anchorMin = new Vector2(0.1f, 0.2f);
                rect.anchorMax = new Vector2(0.7f, 0.9f);
                rect.offsetMin = new Vector2(13f, 27f);
                rect.offsetMax = new Vector2(31f, 43f);
                Quaternion rotation = rect.localRotation;
                rect.ResetToFullscreen();
                Require(rect.anchorMin == Vector2.zero, "Minimum anchor zero");
                Require(rect.anchorMax == Vector2.one, "Maximum anchor one");
                Require(rect.offsetMin == Vector2.zero, "Minimum offset zero");
                Require(rect.offsetMax == Vector2.zero, "Maximum offset zero");
                Require(rect.pivot == new Vector2(0.2f, 0.8f), "Original pivot retained");
                Require(rect.localRotation == rotation, "Original rotation retained");
                Require(rect.localScale == new Vector3(2f, 3f, 4f), "Original scale retained");
                return count;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        public static int EdgePointsRetainLocalGeometryAndApproximateZeroDirection()
        {
            count = 0;
            GameObject root = new GameObject("OriginalRectEdges", typeof(RectTransform));
            try
            {
                RectTransform rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(200f, 100f);
                rect.pivot = new Vector2(0.25f, 0.75f);
                rect.position = new Vector3(300f, 400f, 500f);
                rect.localRotation = Quaternion.Euler(0f, 0f, 30f);
                rect.localScale = new Vector3(2f, 3f, 4f);
                Point(rect.GetPointOnRectEdge(Vector2.zero), new Vector3(50f, -25f, 0f), "Zero direction returns local rect center");
                Point(rect.GetPointOnRectEdge(new Vector2(2f, 1f)), new Vector3(150f, 0f, 0f), "Max-axis division retains half-height ratio");
                Point(rect.GetPointOnRectEdge(new Vector2(-1f, -2f)), new Vector3(0f, -75f, 0f), "Negative max-axis ratio");
                Point(rect.GetPointOnRectEdge(new Vector2(1f, 1f)), new Vector3(150f, 25f, 0f), "Diagonal reaches corner");
                Point(rect.GetPointOnRectEdge(new Vector2(-1f, 0f)), new Vector3(-50f, -25f, 0f), "Left edge local point");
                Point(rect.GetPointOnRectEdge(new Vector2(0f, 1f)), new Vector3(50f, 25f, 0f), "Top edge local point");
                Point(rect.GetPointOnRectEdge(new Vector2(0.000001f, 0f)), new Vector3(50.0001f, -25f, 0f), "Approximate-zero direction is preserved without normalization");
                Vector3 infinity = rect.GetPointOnRectEdge(new Vector2(float.PositiveInfinity, 0f));
                Require(float.IsNaN(infinity.x) && infinity.y == -25f && infinity.z == 0f, "Original infinity division is retained");
                Vector3 nan = rect.GetPointOnRectEdge(new Vector2(0f, float.NaN));
                Require(float.IsNaN(nan.x) && float.IsNaN(nan.y) && nan.z == 0f, "Original max second-operand NaN propagation");
                return count;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
