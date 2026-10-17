using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Shared reflection and ownership helpers for the HLUIAnimationInOut family, AnimatableEvent
    // and FullScreenTransitionManager cases. Every Unity object a case creates is registered with
    // an Owned scope and destroyed with it; none of these helpers touch static state.
    internal static class OriginalInOutTestSupport
    {
        public const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        public const BindingFlags Declared = Any | BindingFlags.DeclaredOnly;

        public static FieldInfo Field(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, Declared);
                if (field != null) return field;
            }
            throw new MissingFieldException(type.FullName, name);
        }

        public static object Get(object target, string name) => Field(target.GetType(), name).GetValue(target);
        public static T Get<T>(object target, string name) => (T)Get(target, name);
        public static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);

        public static object Call(object target, string name, params object[] args)
        {
            MethodInfo method = null;
            for (Type current = target.GetType(); current != null && method == null; current = current.BaseType)
                method = current.GetMethod(name, Declared);
            if (method == null) throw new MissingMethodException(target.GetType().FullName, name);
            try { return method.Invoke(target, args); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        // A hoisted iterator local by its source name; the exact 5__N layout is checked once in the
        // declaration cases.
        public static FieldInfo Hoisted(object iterator, string local)
        {
            FieldInfo field = iterator.GetType().GetFields(Declared).SingleOrDefault(f => f.Name.StartsWith("<" + local + ">5__", StringComparison.Ordinal));
            Assert.That(field, Is.Not.Null, "hoisted local " + local);
            return field;
        }

        // A non-null coroutine handle started on the owner itself, so the owner's own StopCoroutine
        // accepts it. The owner's object is active only while the coroutine starts; restoring an
        // inactive object stops it but leaves the handle non-null.
        public static Coroutine StartedCoroutine(MonoBehaviour owner)
        {
            GameObject host = owner.gameObject;
            bool wasActive = host.activeSelf;
            host.SetActive(true);
            Coroutine coroutine = owner.StartCoroutine(Idle());
            host.SetActive(wasActive);
            Assert.That(coroutine, Is.Not.Null, "precondition: a coroutine handle");
            return coroutine;
        }

        private static IEnumerator Idle()
        {
            while (true)
                yield return null;
        }

        public static float Seconds(WaitForSeconds wait) => (float)typeof(WaitForSeconds).GetField("m_Seconds", Any).GetValue(wait);

        public static int RawBits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);

        public static readonly string[] EventNames =
        {
            "onAnimateIn", "onAnimateInStart", "onAnimateInComplete", "onAnimateOut", "onAnimateOutStart", "onAnimateOutComplete",
        };

        // "name:<CurrentState> <m_normalisedInOutProgress>" at the moment each event fires.
        public static string Snapshot(HLUIAnimationInOut target) =>
            target.CurrentState + " " + Get<float>(target, "m_normalisedInOutProgress").ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        public static void WireEvents(Owned owned, HLUIAnimationInOut target)
        {
            foreach (string name in EventNames)
                Set(target, "m_" + name, owned.Event(name, () => Snapshot(target)));
        }

        public sealed class Owned : IDisposable
        {
            private readonly List<Object> m_objects = new List<Object>();
            public readonly List<string> Log = new List<string>();

            public T Track<T>(T obj) where T : Object
            {
                m_objects.Add(obj);
                return obj;
            }

            // Components are added while the object is inactive, so no ExecuteAlways OnEnable runs.
            public GameObject Host(string name, bool active, params Type[] components)
            {
                var gameObject = Track(new GameObject(name) { hideFlags = HideFlags.HideAndDontSave });
                gameObject.SetActive(false);
                foreach (Type component in components)
                    if (gameObject.GetComponent(component) == null) gameObject.AddComponent(component);
                gameObject.SetActive(active);
                return gameObject;
            }

            // A curve asset whose AnimationCurve is replaced by the given keys and wrap mode.
            public HLCurve Curve(WrapMode postWrap, params Keyframe[] keys)
            {
                HLCurve curve = Track(ScriptableObject.CreateInstance<HLCurve>());
                curve.hideFlags = HideFlags.HideAndDontSave;
                var animationCurve = new AnimationCurve(keys) { postWrapMode = postWrap, preWrapMode = postWrap };
                Set(curve, "m_curve", animationCurve);
                return curve;
            }

            // Two keys with matching linear tangents, so the curve is the straight line between them.
            public HLCurve Linear(float startTime, float startValue, float endTime, float endValue, WrapMode wrap = WrapMode.ClampForever)
            {
                float slope = (endValue - startValue) / (endTime - startTime);
                return Curve(wrap, new Keyframe(startTime, startValue, slope, slope), new Keyframe(endTime, endValue, slope, slope));
            }

            public HLCurve Constant(float value, float endTime) =>
                Curve(WrapMode.ClampForever, new Keyframe(0f, value, 0f, 0f), new Keyframe(endTime, value, 0f, 0f));

            public UnityEvent Event(string name, Func<string> snapshot = null)
            {
                var unityEvent = new UnityEvent();
                unityEvent.AddListener(() => Log.Add(snapshot == null ? name : name + ":" + snapshot()));
                return unityEvent;
            }

            public void Dispose()
            {
                for (int i = m_objects.Count - 1; i >= 0; i--)
                    if (m_objects[i] != null) UnityEngine.Object.DestroyImmediate(m_objects[i]);
                m_objects.Clear();
            }
        }
    }
}
