using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of FullscreenShaderEffectTrigger (0x060029bb..c3) and
    // FullscreenShaderUIBlur (0x060035ea..ec) against the shipped x86-64
    // bodies. The manager is a raw FullscreenShaderManager carrying only the
    // collections these paths read: a real one is a TimeScaledComponent whose
    // editor Reset/OnValidate would reach SystemConfiguration. Each case owns
    // the manager's registry row and its GameObjects.
    [TestFixture]
    public sealed class OriginalFullscreenShaderComponentsTests
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const FullscreenShaderParametersType SomeType = FullscreenShaderParametersType.BoostEnter;

        static object Get(object target, string field) => target.GetType().GetField(field, Instance).GetValue(target);
        static void Set(object target, string field, object value) => FindField(target.GetType(), field).SetValue(target, value);
        static FieldInfo FindField(Type type, string field)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                FieldInfo info = t.GetField(field, Instance | BindingFlags.DeclaredOnly);
                if (info != null) return info;
            }
            throw new MissingFieldException(type.Name, field);
        }
        static void Call(object target, string method) => target.GetType().GetMethod(method, Instance).Invoke(target, null);

        sealed class Rig
        {
            public readonly FullscreenShaderManager Manager = (FullscreenShaderManager)FormatterServices.GetUninitializedObject(typeof(FullscreenShaderManager));
            public readonly List<FullscreenShaderManager.ActiveParameters> Active = new List<FullscreenShaderManager.ActiveParameters>();
            public readonly Dictionary<FullscreenShaderManager.ActiveParameters, int> Ids = new Dictionary<FullscreenShaderManager.ActiveParameters, int>();
            public readonly StackableData Requests = new StackableData();
            public readonly ObjectPool<FullscreenShaderManager.ActiveParameters> Pool =
                new ObjectPool<FullscreenShaderManager.ActiveParameters>(() => new FullscreenShaderManager.ActiveParameters());
            public readonly List<GameObject> Owned = new List<GameObject>();

            public Rig()
            {
                Set(Manager, "m_activeParameters", Active);
                Set(Manager, "m_activeParametersIDs", Ids);
                Set(Manager, "m_fullscreenEffectRequests", Requests);
                Set(Manager, "m_activeParametersPool", Pool);
                var data = (DataManager)FormatterServices.GetUninitializedObject(typeof(DataManager));
                data.FullscreenShaderParametersDefinitions = new Dictionary<FullscreenShaderParametersType, FullscreenShaderParametersDefinition>();
                Set(Manager, "m_dataManager", data);
            }

            public T Add<T>(string name) where T : Component
            {
                var owner = new GameObject(name);
                owner.SetActive(false);
                Owned.Add(owner);
                return owner.AddComponent<T>();
            }

            // An override the manager is tracking, as StartEffectOverride leaves it.
            public FullscreenShaderManager.ParametersHandle Track(int id, Action onFinished)
            {
                var parameters = new FullscreenShaderManager.ActiveParameters();
                parameters.Initialise(null, 2f, false, true, onFinished);
                Active.Add(parameters);
                Ids[parameters] = id;
                return new FullscreenShaderManager.ParametersHandle(parameters, id);
            }

            public void Destroy()
            {
                foreach (GameObject owner in Owned) Object.DestroyImmediate(owner);
            }
        }

        static void WithRegisteredManager(Action<Rig> body)
        {
            var rig = new Rig();
            VerificationFixtureOwnership.RegistryRow row = null;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                row = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<FullscreenShaderManager>());
                row.Register(rig.Manager);
                body(rig);
            }, rig.Destroy, () => row?.Dispose());
        }

        static FullscreenShaderEffectTrigger AwakeTrigger(Rig rig)
        {
            var trigger = rig.Add<FullscreenShaderEffectTrigger>("Lucid owned fullscreen trigger");
            Call(trigger, "Awake");
            return trigger;
        }

        // .ctor 0x5d3050 stores 1f to m_duration (+0x28); Awake 0x5d2e30 stores
        // GetSystem<FullscreenShaderManager>(null, true).
        [Test]
        public void AwakeTakesTheRegisteredManagerAndTheDurationDefaultsToOne()
        {
            WithRegisteredManager(rig =>
            {
                FullscreenShaderEffectTrigger trigger = AwakeTrigger(rig);
                Assert.That(Get(trigger, "m_fullscreenShaderManager"), Is.SameAs(rig.Manager));
                Assert.That(Get(trigger, "m_duration"), Is.EqualTo(1f));
                Assert.That(Get(trigger, "m_untilStopTriggered"), Is.False);
                Assert.That(Get(trigger, "m_playing"), Is.False);
            });
        }

        // SetDuration 0x5d3000: m_untilStopTriggered = ucomiss(duration, 0)
        // with ZF and not PF, the ordered == 0 test; the raw value is stored.
        // A <= 0 or a NaN-as-unlimited reading fails the -1 or NaN rows.
        [Test]
        public void SetDurationTreatsOnlyAnExactZeroAsUntilStopped()
        {
            var owner = new GameObject("Lucid owned fullscreen trigger duration");
            try
            {
                owner.SetActive(false);
                var trigger = owner.AddComponent<FullscreenShaderEffectTrigger>();
                var rows = new (float duration, bool untilStopped)[]
                {
                    (0f, true), (-0f, true), (float.NaN, false), (float.Epsilon, false), (-1f, false), (3.5f, false),
                };
                foreach ((float duration, bool untilStopped) in rows)
                {
                    Set(trigger, "m_untilStopTriggered", !untilStopped);
                    trigger.SetDuration(duration);
                    Assert.That(Get(trigger, "m_untilStopTriggered"), Is.EqualTo(untilStopped), duration.ToString("R"));
                    Assert.That(BitConverter.SingleToInt32Bits((float)Get(trigger, "m_duration")), Is.EqualTo(BitConverter.SingleToInt32Bits(duration)));
                }

                // SetFullscreenShaderParametersType 0x5d3020 is a plain store;
                // OnValidate 0x5d3030 only calls CanValidate.
                trigger.SetFullscreenShaderParametersType(SomeType);
                Assert.That(Get(trigger, "m_type"), Is.EqualTo(SomeType));
                Call(trigger, "OnValidate");
                Assert.That(Get(trigger, "m_type"), Is.EqualTo(SomeType));
                Assert.That(Get(trigger, "m_duration"), Is.EqualTo(3.5f));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        // StartEffect 0x5d2f30 passes m_type, &m_parametersHandle, m_duration,
        // m_applyCurveOverDuration and m_untilStopTriggered, ignores the bool
        // result and always sets m_playing. With no definition the override
        // fails and the handle is the default.
        [Test]
        public void StartEffectCountsAsPlayingEvenWhenTheOverrideFails()
        {
            WithRegisteredManager(rig =>
            {
                FullscreenShaderEffectTrigger trigger = AwakeTrigger(rig);
                FullscreenShaderManager.ParametersHandle stale = rig.Track(4, null);
                Set(trigger, "m_parametersHandle", stale);
                trigger.StartEffect();
                Assert.That(Get(trigger, "m_playing"), Is.True);
                var handle = (FullscreenShaderManager.ParametersHandle)Get(trigger, "m_parametersHandle");
                Assert.That(handle.ActiveParameters, Is.Null, "the out argument overwrote the stale handle");
                Assert.That(handle.InstanceID, Is.Zero);

                // A second start returns on m_playing before touching the manager.
                Set(trigger, "m_fullscreenShaderManager", null);
                Assert.DoesNotThrow(trigger.StartEffect);
                Assert.That(Get(trigger, "m_playing"), Is.True);

                // <StartEffect>b__10_0 0x5d3070, the onFinished callback, clears m_playing.
                Call(trigger, "<StartEffect>b__10_0");
                Assert.That(Get(trigger, "m_playing"), Is.False);
            });
        }

        // StartEffect 0x5d2f8c/0x5d2f91 passes +0x2c (m_applyCurveOverDuration)
        // as applyCurveOverDuration and +0x24 (m_untilStopTriggered) as
        // untilStopped, and stores m_playing = 1 at 0x5d2fe7 only after
        // StartEffectOverride returns. With a definition and m_duration 0 the
        // override's InternalUpdate(0) finishes it at once (0x688034: not
        // untilStopped and elapsed >= duration), so <StartEffect>b__10_0 clears
        // m_playing inside the call and StartEffect then sets it again.
        // Swapped flags keep the override tracked; setting m_playing before
        // the call leaves it false.
        [Test]
        public void StartEffectSetsPlayingAfterAnOverrideThatFinishesSynchronously()
        {
            WithRegisteredManager(rig =>
            {
                var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                var material = new Material(Shader.Find("UI/Default"));
                var definition = ScriptableObject.CreateInstance<FullscreenShaderParametersDefinition>();
                try
                {
                    Set(rig.Manager, "m_dreamVignetteFeature", feature);
                    Set(rig.Manager, "m_fullscreenShaderMaterial", material);
                    var pool = new ObjectPool<FullscreenShaderManager.ActiveParameters>(() =>
                    {
                        var parameters = new FullscreenShaderManager.ActiveParameters();
                        rig.Ids[parameters] = 0;
                        return parameters;
                    });
                    Set(rig.Manager, "m_activeParametersPool", pool);
                    var data = (DataManager)Get(rig.Manager, "m_dataManager");
                    data.FullscreenShaderParametersDefinitions[SomeType] = definition;

                    FullscreenShaderEffectTrigger trigger = AwakeTrigger(rig);
                    trigger.SetFullscreenShaderParametersType(SomeType);
                    Set(trigger, "m_duration", 0f);
                    Set(trigger, "m_applyCurveOverDuration", true);
                    Set(trigger, "m_untilStopTriggered", false);

                    trigger.StartEffect();
                    Assert.That(rig.Active, Is.Empty, "the override finished inside InternalUpdate(0)");
                    Assert.That(pool.CountInactive, Is.EqualTo(1));
                    Assert.That(Get(trigger, "m_playing"), Is.True, "m_playing is stored after the finishing callback");
                }
                finally
                {
                    Object.DestroyImmediate(definition);
                    Object.DestroyImmediate(material);
                    Object.DestroyImmediate(feature);
                }
            });
        }

        // StopEffect 0x5d2f00: only while m_playing, StopEffectOverride(handle)
        // then m_playing = false. The tracked override is removed, finished
        // and pooled by the manager.
        [Test]
        public void StopEffectStopsTheTrackedOverrideOnlyWhilePlaying()
        {
            WithRegisteredManager(rig =>
            {
                FullscreenShaderEffectTrigger trigger = AwakeTrigger(rig);
                int finished = 0;
                FullscreenShaderManager.ParametersHandle handle = rig.Track(3, () => finished++);
                Set(trigger, "m_parametersHandle", handle);

                trigger.StopEffect();
                Assert.That(finished, Is.Zero, "not playing: the manager is not called");
                Assert.That(rig.Active, Has.Count.EqualTo(1));

                Set(trigger, "m_playing", true);
                trigger.StopEffect();
                Assert.That(Get(trigger, "m_playing"), Is.False);
                Assert.That(finished, Is.EqualTo(1));
                Assert.That(rig.Active, Is.Empty);
                Assert.That(rig.Pool.CountInactive, Is.EqualTo(1));
            });
        }

        // OnDestroy 0x5d2eb0 tests m_playing first, then
        // m_persistAfterTriggerDestroyed; only (playing, not persisting) stops.
        // m_fullscreenShaderManager is cleared in every row.
        [Test]
        public void OnDestroyStopsAPlayingEffectUnlessItPersists()
        {
            WithRegisteredManager(rig =>
            {
                var rows = new (bool playing, bool persist, bool stops)[]
                {
                    (true, false, true), (true, true, false), (false, false, false), (false, true, false),
                };
                foreach ((bool playing, bool persist, bool stops) in rows)
                {
                    FullscreenShaderEffectTrigger trigger = AwakeTrigger(rig);
                    int finished = 0;
                    FullscreenShaderManager.ParametersHandle handle = rig.Track(9, () => finished++);
                    Set(trigger, "m_parametersHandle", handle);
                    Set(trigger, "m_playing", playing);
                    Set(trigger, "m_persistAfterTriggerDestroyed", persist);

                    Call(trigger, "OnDestroy");
                    string label = "playing " + playing + ", persist " + persist;
                    Assert.That(finished, Is.EqualTo(stops ? 1 : 0), label);
                    Assert.That(rig.Active.Contains(handle.ActiveParameters), Is.EqualTo(!stops), label);
                    Assert.That(Get(trigger, "m_playing"), Is.EqualTo(playing && !stops), label);
                    Assert.That(Get(trigger, "m_fullscreenShaderManager"), Is.Null, label);
                }
            });
        }

        // UIBlur .ctor 0x688fa0 caches GetSystemRef<FullscreenShaderManager>;
        // OnEnable 0x688e90 stores the inlined RequestUIBlurActive handle
        // (AddOverride<bool>(0, true) on +0x150); OnDisable 0x688f20 removes
        // it through RemoveOverrides and keeps the field.
        [Test]
        public void UIBlurHoldsOneBlurRequestWhileEnabled()
        {
            WithRegisteredManager(rig =>
            {
                var blur = rig.Add<FullscreenShaderUIBlur>("Lucid owned UI blur");
                Assert.That(Get(blur, "m_fullscreenShaderManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<FullscreenShaderManager>()));
                Assert.That(rig.Requests.HasData(0), Is.False);

                Call(blur, "OnEnable");
                var request = (StackableDataHandle)Get(blur, "m_uiBlurRequest");
                Assert.That(request, Is.Not.Null);
                Assert.That(rig.Requests.HasData(0), Is.True);
                Assert.That(rig.Requests.GetOverride<bool>(request, 0), Is.True);
                Assert.That(rig.Requests.HasData(1), Is.False, "UI blur is stackable id 0");

                Call(blur, "OnDisable");
                Assert.That(rig.Requests.HasData(0), Is.False);
                Assert.That(Get(blur, "m_uiBlurRequest"), Is.SameAs(request));
            });
        }

        // OnDisable returns on SystemRef.IsNull before any Get, so a missing
        // manager is not diagnosed. The leased row is the one the constructor
        // creates.
        [Test]
        public void UIBlurDisableWithoutAManagerDoesNothing()
        {
            var row = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<FullscreenShaderManager>());
            var owner = new GameObject("Lucid owned UI blur without manager");
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                owner.SetActive(false);
                var blur = owner.AddComponent<FullscreenShaderUIBlur>();
                Assert.That(((SystemRef<FullscreenShaderManager>)Get(blur, "m_fullscreenShaderManagerRef")).IsNull(), Is.True);
                Assert.DoesNotThrow(() => Call(blur, "OnDisable"));
                Assert.That(Get(blur, "m_uiBlurRequest"), Is.Null);
            }, () => Object.DestroyImmediate(owner), row.Dispose);
        }
    }
}
