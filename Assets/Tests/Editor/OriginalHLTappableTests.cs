using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace ProjectLucid.Tests
{
    // Hardlight.HLTappable (HLUnityUI.Runtime, TypeDef 0x020000f8) and TappedEvent (0x020000f9),
    // x86-64 bodies 0x1B73980..0x1B73A90. Every case runs on an inactive owned component, so the
    // HLSelectable lifecycle never runs.
    public sealed class OriginalHLTappableTests
    {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // dump.cs TypeDefIndex 12073/12072 and the DummyDll flag and attribute rows.
        [Test]
        public void DeclarationsKeepOriginalFlagsFieldsAndAttributeRows()
        {
            Type type = typeof(HLTappable);
            Assert.That((int)type.Attributes, Is.EqualTo(1048577));
            Assert.That(type.BaseType, Is.EqualTo(typeof(HLSelectable)));
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            CustomAttributeData[] options = type.GetCustomAttributesData().ToArray();
            Assert.That(options.Select(a => a.AttributeType), Is.EqualTo(new[] { typeof(Il2CppSetOptionAttribute), typeof(Il2CppSetOptionAttribute) }));
            Assert.That(options.Select(a => Convert.ToInt32(a.ConstructorArguments[0].Value)), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(options.Select(a => a.ConstructorArguments[1].Value), Is.EqualTo(new object[] { false, false }));

            FieldInfo[] fields = type.GetFields(Declared).OrderBy(f => f.MetadataToken).ToArray();
            Assert.That(fields.Select(f => f.Name), Is.EqualTo(new[] { "m_onTap", "m_tapTimeThreshold", "m_tapStartTime" }));
            Assert.That(fields.Select(f => f.FieldType), Is.EqualTo(new[] { typeof(HLTappable.TappedEvent), typeof(float), typeof(float) }));
            Assert.That(fields.Select(f => (int)f.Attributes), Is.EqualTo(new[] { 4, 1, 1 }));
            Assert.That(fields[0].GetCustomAttributesData().Select(a => a.AttributeType), Is.EqualTo(new[] { typeof(SerializeField) }));
            Assert.That(fields[1].GetCustomAttributesData().Select(a => a.AttributeType), Is.EqualTo(new[] { typeof(SerializeField) }));
            Assert.That(fields[2].GetCustomAttributesData(), Is.Empty);

            Assert.That(type.GetProperties(Declared).Select(p => p.Name), Is.EqualTo(new[] { "OnTap" }));
            Assert.That(type.GetMethods(Declared).Length + type.GetConstructors(Declared).Length, Is.EqualTo(5));
            Method(type, "get_OnTap", 2182, typeof(HLTappable.TappedEvent), Type.EmptyTypes, new string[0]);
            Method(type, "set_OnTap", 2182, typeof(void), new[] { typeof(HLTappable.TappedEvent) }, new[] { "value" });
            Method(type, "OnPointerDown", 198, typeof(void), new[] { typeof(PointerEventData) }, new[] { "eventData" });
            Method(type, "OnPointerUp", 198, typeof(void), new[] { typeof(PointerEventData) }, new[] { "eventData" });
            Method(type, ".ctor", 6278, null, Type.EmptyTypes, new string[0]);

            Type tapped = typeof(HLTappable.TappedEvent);
            Assert.That(type.GetNestedTypes(Declared), Is.EqualTo(new[] { tapped }));
            Assert.That((int)tapped.Attributes, Is.EqualTo(1056770));
            Assert.That(tapped.BaseType, Is.EqualTo(typeof(UnityEvent)));
            Assert.That(tapped.IsSerializable, Is.True);
            Assert.That(tapped.GetFields(Declared), Is.Empty);
            Assert.That(tapped.GetMethods(Declared), Is.Empty);
            Method(tapped, ".ctor", 6278, null, Type.EmptyTypes, new string[0]);
        }

        // .ctor 0x1B73A10 allocates the TappedEvent and stores 0.1f (0x3DCCCCCD) before the HLSelectable
        // base; the accessors 0x1B73980/0x1B73990 read and write m_onTap directly.
        [Test]
        public void ConstructorSeedsTheEventAndThresholdAndTheAccessorsUseTheField()
        {
            using (var owned = new Owned())
            {
                HLTappable tappable = owned.Tappable;
                Assert.That(Read(tappable, "m_onTap"), Is.InstanceOf<HLTappable.TappedEvent>());
                Assert.That(tappable.OnTap, Is.SameAs(Read(tappable, "m_onTap")));
                float threshold = (float)Read(tappable, "m_tapTimeThreshold");
                Assert.That(BitConverter.ToInt32(BitConverter.GetBytes(threshold), 0), Is.EqualTo(0x3DCCCCCD));
                Assert.That(Read(tappable, "m_tapStartTime"), Is.EqualTo(0f));

                var replacement = new HLTappable.TappedEvent();
                tappable.OnTap = replacement;
                Assert.That(Read(tappable, "m_onTap"), Is.SameAs(replacement));
                tappable.OnTap = null;
                Assert.That(tappable.OnTap, Is.Null);
                owned.AssertNoLogs();
            }
        }

        // OnPointerDown 0x1B739B0 only stores Time.realtimeSinceStartup: no base call and no read of the
        // event data, so a null event or any button records the start and the HLSelectable pointer
        // state is untouched.
        [Test]
        public void PointerDownRecordsTheStartTimeOnly()
        {
            using (var owned = new Owned())
            {
                HLTappable tappable = owned.Tappable;
                float before = Time.realtimeSinceStartup;
                tappable.OnPointerDown(null);
                float after = Time.realtimeSinceStartup;
                float start = (float)Read(tappable, "m_tapStartTime");
                Assert.That(start, Is.InRange(before, after));
                Assert.That(owned.IsPointerDown, Is.False);

                tappable.OnPointerDown(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
                Assert.That((float)Read(tappable, "m_tapStartTime"), Is.GreaterThanOrEqualTo(start));
                Assert.That(owned.IsPointerDown, Is.False);
                Assert.That(owned.Taps, Is.Zero);
                owned.AssertNoLogs();
            }
        }

        // OnPointerUp 0x1B739D0 taps only when threshold > (now - start) (ucomiss/jbe at 0x1B739F0):
        // strictly shorter than the threshold, so a zero, negative or NaN threshold never taps a real press.
        [Test]
        public void PointerUpTapsOnlyWhenStrictlyShorterThanTheThreshold()
        {
            using (var owned = new Owned())
            {
                HLTappable tappable = owned.Tappable;
                tappable.OnPointerDown(null);
                Write(tappable, "m_tapTimeThreshold", 1000f);
                tappable.OnPointerUp(null);
                Assert.That(owned.Taps, Is.EqualTo(1));
                tappable.OnPointerUp(new PointerEventData(null) { button = PointerEventData.InputButton.Middle });
                Assert.That(owned.Taps, Is.EqualTo(2), "the start is not consumed and the button is not read");

                Write(tappable, "m_tapStartTime", Time.realtimeSinceStartup - 5000f);
                tappable.OnPointerUp(null);
                Assert.That(owned.Taps, Is.EqualTo(2), "held longer than the threshold");

                foreach (float threshold in new[] { 0f, -1f, float.NaN })
                {
                    Write(tappable, "m_tapTimeThreshold", threshold);
                    tappable.OnPointerDown(null);
                    tappable.OnPointerUp(null);
                }
                Assert.That(owned.Taps, Is.EqualTo(2), "an elapsed time of zero or more is never below these thresholds");

                // The elapsed time is not clamped: a start in the future taps even at a zero threshold.
                Write(tappable, "m_tapTimeThreshold", 0f);
                Write(tappable, "m_tapStartTime", Time.realtimeSinceStartup + 10f);
                tappable.OnPointerUp(null);
                Assert.That(owned.Taps, Is.EqualTo(3));

                Write(tappable, "m_tapTimeThreshold", 1000f);
                Write(tappable, "m_tapStartTime", Time.realtimeSinceStartup);
                tappable.OnTap = null;
                Assert.Throws<NullReferenceException>(() => tappable.OnPointerUp(null));
                Assert.That(owned.IsPointerDown, Is.False);
                owned.AssertNoLogs();
            }
        }

        private sealed class Owned : IDisposable
        {
            private readonly List<string> logs = new List<string>();
            private GameObject root;

            public HLTappable Tappable { get; }
            public int Taps { get; private set; }

            public bool IsPointerDown => (bool)typeof(HLSelectable).GetProperty("IsPointerDown", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .GetValue(Tappable);

            public Owned()
            {
                Application.logMessageReceived += Log;
                try
                {
                    root = new GameObject("owned-original-hl-tappable", typeof(RectTransform));
                    root.SetActive(false);
                    Tappable = root.AddComponent<HLTappable>();
                    Tappable.OnTap.AddListener(() => Taps++);
                    Assert.That(root.activeInHierarchy, Is.False);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public void AssertNoLogs()
            {
                Assert.That(logs, Is.Empty);
            }

            public void Dispose()
            {
                try
                {
                    if (!ReferenceEquals(root, null)) UnityEngine.Object.DestroyImmediate(root);
                }
                finally
                {
                    root = null;
                    Application.logMessageReceived -= Log;
                }
            }

            private void Log(string message, string stackTrace, LogType type)
            {
                logs.Add(type + ": " + message);
            }
        }

        private static object Read(HLTappable target, string name)
        {
            FieldInfo field = typeof(HLTappable).GetField(name, Declared);
            Assert.That(field, Is.Not.Null, name);
            return field.GetValue(target);
        }

        private static void Write(HLTappable target, string name, object value)
        {
            FieldInfo field = typeof(HLTappable).GetField(name, Declared);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static void Method(Type type, string name, int flags, Type result, Type[] arguments, string[] parameterNames)
        {
            MethodBase method = name == ".ctor" ? (MethodBase)type.GetConstructor(Declared, null, arguments, null) :
                type.GetMethod(name, Declared, null, arguments, null);
            Assert.That(method, Is.Not.Null, type.Name + "." + name);
            Assert.That((int)method.Attributes, Is.EqualTo(flags), name);
            if (method is MethodInfo info) Assert.That(info.ReturnType, Is.EqualTo(result), name);
            Assert.That(method.GetParameters().Select(p => p.Name), Is.EqualTo(parameterNames), name);
        }
    }
}
