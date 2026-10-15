using System;
using System.Collections.Generic;
using Hardlight;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Editor
{
    // Owned inactive HLUIEffectManager only; Awake, registration and Core logging never run.
    public sealed class OriginalHLUIEffectManagerWeakRemediationTests
    {
        // Every Sample* body (e.g. SampleOnPressCurve 0x1b4a750, SampleEffectCurveSmooth
        // 0x1b4a7f0, SampleAttackFeedbackAlphaCurve 0x1b4a8c0, x86-64) loads its curve field
        // and tail-jumps to AnimationCurve.Evaluate with the caller's time. Looping wrap
        // modes make out-of-range times differ from clamped ones, so clamping or remapping
        // the time inside a Sample* method fails here.
        [Test]
        public void OriginalEffectManagerSamplesPassRawTimeToLoopingCurves()
        {
            using (var owned = new InactiveManager())
            using (var serialized = new SerializedObject(owned.Manager))
            {
                KeyValuePair<string, Func<float, float>>[] routes = Routes(owned.Manager);
                Assert.That(routes.Length, Is.EqualTo(18));
                serialized.Update();
                for (int index = 0; index < routes.Length; index++)
                {
                    AnimationCurve curve = AnimationCurve.Linear(0f, index, 4f, index + 10f);
                    curve.preWrapMode = WrapMode.Loop;
                    curve.postWrapMode = WrapMode.Loop;
                    SerializedProperty property = serialized.FindProperty(routes[index].Key);
                    Assert.That(property, Is.Not.Null, routes[index].Key);
                    Assert.That(property.propertyType, Is.EqualTo(SerializedPropertyType.AnimationCurve), routes[index].Key);
                    property.animationCurveValue = curve;
                }
                Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
                for (int index = 0; index < routes.Length; index++)
                {
                    Func<float, float> sample = routes[index].Value;
                    Assert.That(sample(2f), Is.EqualTo(index + 5f).Within(0.0001f), routes[index].Key);
                    // One quarter past the end loops to t=1; one quarter before the start loops to t=3.
                    Assert.That(sample(5f), Is.EqualTo(index + 2.5f).Within(0.0001f), routes[index].Key);
                    Assert.That(sample(-1f), Is.EqualTo(index + 7.5f).Within(0.0001f), routes[index].Key);
                }
                owned.RequireUnchanged();
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static KeyValuePair<string, Func<float, float>>[] Routes(HLUIEffectManager manager) => new[]
        {
            Route("m_onPressBounceCurve", manager.SampleOnPressCurve),
            Route("m_onReleaseBounceCurve", manager.SampleOnReleaseCurve),
            Route("m_onDisabledPressShakeCurve", manager.SampleOnDisablePressCurve),
            Route("m_onEnableBounceCurve", manager.SampleOnEnableBounceCurve),
            Route("m_uiUnclampedSlideAnimationCurve", manager.SampleUIUnclampedSlideAnimationCurve),
            Route("m_uiClampedSlideAnimationCurve", manager.SampleUIClampedSlideAnimationCurve),
            Route("m_bubblePowerupPopCurve", manager.SampleBubblePowerupPopCurve),
            Route("m_menuThrobberColourLerpCurve", manager.SampleMenuThrobberColourLerpCurve),
            Route("m_FTUEHandBounceCurve", manager.SampleFTUEHandBounceCurve),
            Route("m_FTUEHandUrgencyOverTime", manager.SampleFTUEHandUrgencyCurve),
            Route("m_UIScaleChangerIncreaseCurve", manager.SampleUIScaleChangerIncreaseCurve),
            Route("m_UIScaleChangerDecreaseCurve", manager.SampleUIScaleChangerDecreaseCurve),
            Route("m_effectCurveSmooth", manager.SampleEffectCurveSmooth),
            Route("m_effectCurvePulse", manager.SampleEffectCurvePulse),
            Route("m_panelShineLength", manager.SamplePanelShineLengthCurve),
            Route("m_panelShineThickness", manager.SamplePanelShineThicknessCurve),
            Route("m_panelShineAlpha", manager.SamplePanelShineAlphaCurve),
            Route("m_attackFeedbackAlphaCurve", manager.SampleAttackFeedbackAlphaCurve),
        };

        private static KeyValuePair<string, Func<float, float>> Route(string field, Func<float, float> sample)
        {
            return new KeyValuePair<string, Func<float, float>>(field, sample);
        }

        // Same ownership rules as OriginalHLUIEffectManagerTests: refuse a live Core or Core
        // row, keep the singleton and the system registry unchanged, and destroy the object.
        private sealed class InactiveManager : IDisposable
        {
            private GameObject gameObject;
            private readonly HLUIEffectManager previous = HLUIEffectManager.Instance;
            private readonly KeyValuePair<ISystem, SystemRef>[] systems = SnapshotSystems();
            public HLUIEffectManager Manager { get; private set; }

            public InactiveManager()
            {
                Assert.That(ReferenceEquals(HLUnityCore.Instance, null), Is.True,
                    "A foreign Core singleton may invoke real logging callbacks during destruction.");
                string coreName = ProcessManager.GetDefaultName<HLUnityCore>();
                foreach (KeyValuePair<ISystem, SystemRef> row in systems)
                {
                    Assert.That(row.Value, Is.Not.Null);
                    Assert.That(string.Equals(row.Value.SystemName(), coreName, StringComparison.Ordinal), Is.False);
                    Assert.That(row.Key is HLUnityCore, Is.False);
                }
                try
                {
                    gameObject = new GameObject("Owned original UI effect wrap fixture");
                    gameObject.SetActive(false);
                    Manager = gameObject.AddComponent<HLUIEffectManager>();
                    Assert.That(Manager, Is.Not.Null);
                    RequireUnchanged();
                }
                catch (Exception primary)
                {
                    try { Dispose(); }
                    catch (Exception cleanup) { throw new AggregateException("Owned construction and cleanup failed", primary, cleanup); }
                    throw;
                }
            }

            public void RequireUnchanged()
            {
                Assert.That(ReferenceEquals(HLUIEffectManager.Instance, previous), Is.True);
                KeyValuePair<ISystem, SystemRef>[] current = SnapshotSystems();
                Assert.That(current.Length, Is.EqualTo(systems.Length));
                for (int index = 0; index < current.Length; index++)
                {
                    Assert.That(ReferenceEquals(current[index].Key, systems[index].Key), Is.True);
                    Assert.That(ReferenceEquals(current[index].Value, systems[index].Value), Is.True);
                }
            }

            public void Dispose()
            {
                try
                {
                    if (gameObject != null) Object.DestroyImmediate(gameObject);
                }
                finally { RequireUnchanged(); }
            }

            private static KeyValuePair<ISystem, SystemRef>[] SnapshotSystems()
            {
                var rows = new List<KeyValuePair<ISystem, SystemRef>>();
                ProcessManager.ForEachSystem((system, reference) => rows.Add(new KeyValuePair<ISystem, SystemRef>(system, reference)));
                return rows.ToArray();
            }
        }
    }
}
