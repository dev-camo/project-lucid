using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of the restored Game.Runtime animation helpers (AnimLayerWeightSetter,
    // AnimParameterSetter, AnimationCustomDataCamera, AnimationCustomDataToMaterialProperty,
    // AnimationEventListener, AnimationSampler, AnimationSpeedScaler, AnimationStartDelay,
    // AnimatorBoolSetter and AnimatorCompleteAbilitySetter) against the shipped 1.10.1
    // x86-64 bodies cited per case. Each case owns its GameObjects, assets and in-memory
    // AnimatorControllers and destroys them, and owns any ProcessManager row it touches.
    // Components are added to inactive GameObjects where possible and their messages are
    // invoked directly, so no editor callback runs them a second time.
    [TestFixture]
    public sealed class OriginalAnimationHelperTests
    {
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static FieldInfo Field(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, Any | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(type.Name, name);
        }

        private static object Get(object target, string name) => Field(target.GetType(), name).GetValue(target);
        private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);

        private static object Call(object target, string name, params object[] args)
        {
            try { return target.GetType().GetMethod(name, Any).Invoke(target, args); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        // Every Unity object a case creates, destroyed newest first. AnimatorControllers
        // also lose their layer state machines and undo history.
        private sealed class Owned : IDisposable
        {
            private readonly List<Object> objects = new List<Object>();

            public T Add<T>(T value) where T : Object
            {
                objects.Add(value);
                return value;
            }

            public GameObject Inactive(string name)
            {
                var gameObject = Add(new GameObject(name) { hideFlags = HideFlags.HideAndDontSave });
                gameObject.SetActive(false);
                return gameObject;
            }

            public GameObject Active(string name) => Add(new GameObject(name) { hideFlags = HideFlags.HideAndDontSave });

            public Animator BoundAnimator(GameObject owner, Action<AnimatorController> build)
            {
                var controller = Add(new AnimatorController { name = "Lucid animation helper fixture", hideFlags = HideFlags.HideAndDontSave });
                controller.AddLayer("Base");
                build(controller);
                Undo.ClearUndo(controller);
                var animator = owner.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.Rebind();
                Assert.That(animator.isInitialized, Is.True, "fixture Animator binds the owned controller");
                return animator;
            }

            public AnimationClip LegacyClip(string name, float length, float endX)
            {
                var clip = Add(new AnimationClip { name = name, legacy = true, hideFlags = HideFlags.HideAndDontSave });
                clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, length, endX));
                return clip;
            }

            public void Dispose()
            {
                for (int i = objects.Count - 1; i >= 0; --i)
                {
                    Object value = objects[i];
                    if (value == null) continue;
                    if (value is AnimatorController controller)
                    {
                        foreach (AnimatorControllerLayer layer in controller.layers)
                            if (layer.stateMachine != null) Object.DestroyImmediate(layer.stateMachine);
                        Undo.ClearUndo(controller);
                    }
                    Object.DestroyImmediate(value);
                }
                objects.Clear();
            }
        }

        // AnimLayerWeightSetter..ctor 0x06000550 stores 1.0 (0x3f800000 at 0x52fbf4).
        // Start 0x0600054f (0x52FB60) returns on String.IsNullOrEmpty (0x52fb8c) or a
        // false Object implicit bool (0x52fbb6), otherwise passes GetLayerIndex(name)
        // (0x52fbcc) to SetLayerWeight (0x52fbe1). Writing the base layer, ignoring the
        // name guard or dereferencing a missing Animator fails.
        [Test]
        public void LayerWeightSetterWritesTheNamedLayerOnlyWithANameAndAnAnimator()
        {
            using (var owned = new Owned())
            {
                GameObject root = owned.Active("AnimLayerWeightSetter fixture");
                Animator animator = owned.BoundAnimator(root, controller =>
                {
                    controller.AddLayer("Overlay");
                    AnimatorControllerLayer[] layers = controller.layers;
                    layers[1].defaultWeight = 0.25f;
                    controller.layers = layers;
                });
                Assert.That(animator.GetLayerWeight(1), Is.EqualTo(0.25f), "fixture overlay starts at its default weight");

                var setter = root.AddComponent<AnimLayerWeightSetter>();
                Assert.That(Get(setter, "m_layerWeight"), Is.EqualTo(1f));
                Set(setter, "m_animator", animator);

                Set(setter, "m_layerName", "");
                setter.Start();
                Set(setter, "m_layerName", null);
                setter.Start();
                Assert.That(animator.GetLayerWeight(1), Is.EqualTo(0.25f), "an empty or null name writes nothing");

                Set(setter, "m_layerName", "Overlay");
                setter.Start();
                Assert.That(animator.GetLayerWeight(1), Is.EqualTo(1f));

                Set(setter, "m_layerWeight", 0.5f);
                Set(setter, "m_animator", null);
                Assert.DoesNotThrow(() => setter.Start(), "a missing Animator is skipped");
                Assert.That(animator.GetLayerWeight(1), Is.EqualTo(1f));
            }
            LogAssert.NoUnexpectedReceived();
        }

        // AnimParameterSetter.SetBool/SetFloat/SetInteger 0x06000551..53 (0x52FC10,
        // 0x52FC60, 0x52FCB0) call AnimationParameterWrapper.TryGetAnimationHash and only
        // on true pass the hash and value to Animator.SetBool/SetFloat/SetInteger. An
        // unnamed wrapper reaches no Animator, so a null Animator is never dereferenced.
        [Test]
        public void ParameterSetterWritesThroughTheWrappedHashOnlyForANamedParameter()
        {
            using (var owned = new Owned())
            {
                GameObject root = owned.Active("AnimParameterSetter fixture");
                Animator animator = owned.BoundAnimator(root, controller =>
                {
                    controller.AddParameter("Flag", AnimatorControllerParameterType.Bool);
                    controller.AddParameter("Amount", AnimatorControllerParameterType.Float);
                    controller.AddParameter("Count", AnimatorControllerParameterType.Int);
                });

                AnimParameterSetter Setter(string parameterName, Animator target)
                {
                    var wrapper = new AnimationParameterWrapper();
                    Set(wrapper, "m_parameterName", parameterName);
                    var setter = root.AddComponent<AnimParameterSetter>();
                    Set(setter, "m_animator", target);
                    Set(setter, "m_parameterWrapper", wrapper);
                    return setter;
                }

                Setter("Flag", animator).SetBool(true);
                Setter("Amount", animator).SetFloat(2.5f);
                Setter("Count", animator).SetInteger(7);
                Assert.That(animator.GetBool("Flag"), Is.True);
                Assert.That(animator.GetFloat("Amount"), Is.EqualTo(2.5f));
                Assert.That(animator.GetInteger("Count"), Is.EqualTo(7));

                AnimParameterSetter unnamed = Setter("", null);
                Assert.DoesNotThrow(() => unnamed.SetBool(true));
                Assert.DoesNotThrow(() => unnamed.SetFloat(1f));
                Assert.DoesNotThrow(() => unnamed.SetInteger(1));
            }
            LogAssert.NoUnexpectedReceived();
        }

        // AnimationCustomDataCamera..ctor 0x0600051a stores the multipliers 0.01, 1, 1, 1, 1
        // (0x52e554..0x52e570). LateUpdate 0x06000519 (0x52E250) stores, for each channel,
        // the multiplier itself when its override is set (0x52e2d1) and value * multiplier
        // otherwise (0x52e326), then returns with m_volumeProfileProvided clear (0x52e31b).
        [Test]
        public void CameraDataOutputsAreTheMultiplierWhenOverriddenAndTheScaledValueOtherwise()
        {
            using (var owned = new Owned())
            {
                var data = owned.Inactive("AnimationCustomDataCamera fixture").AddComponent<AnimationCustomDataCamera>();
                Assert.That(Get(data, "m_focusDistanceMultiplier"), Is.EqualTo(0.01f));
                Assert.That(Get(data, "m_focalLengthMultiplier"), Is.EqualTo(1f));
                Assert.That(Get(data, "m_fStopMultiplier"), Is.EqualTo(1f));
                Assert.That(Get(data, "m_nearClipMultiplier"), Is.EqualTo(1f));
                Assert.That(Get(data, "m_farClipMultiplier"), Is.EqualTo(1f));

                data.FocusDistance = 300f;
                data.FocalLength = 50f;
                data.FStop = 2.8f;
                data.NearClip = 0.3f;
                data.FarClip = 1000f;
                Set(data, "m_focalLengthMultiplier", 2f);
                Set(data, "m_fStopMultiplier", 0.5f);
                Set(data, "m_nearClipMultiplier", 3f);
                Set(data, "m_farClipMultiplier", 0.25f);

                Call(data, "LateUpdate");
                Assert.That(data.FocusDistanceOutput, Is.EqualTo(300f * 0.01f));
                Assert.That(data.FocalLengthOutput, Is.EqualTo(100f));
                Assert.That(data.FStopOutput, Is.EqualTo(2.8f * 0.5f));
                Assert.That(data.NearClipOutput, Is.EqualTo(0.3f * 3f));
                Assert.That(data.FarClipOutput, Is.EqualTo(250f));

                Set(data, "m_fStopOverride", true);
                Set(data, "m_farClipOverride", true);
                Call(data, "LateUpdate");
                Assert.That(data.FocusDistanceOutput, Is.EqualTo(300f * 0.01f));
                Assert.That(data.FocalLengthOutput, Is.EqualTo(100f));
                Assert.That(data.FStopOutput, Is.EqualTo(0.5f), "an override ignores FStop");
                Assert.That(data.NearClipOutput, Is.EqualTo(0.3f * 3f));
                Assert.That(data.FarClipOutput, Is.EqualTo(0.25f), "an override ignores FarClip");

                Set(data, "m_focusDistanceOverride", true);
                Set(data, "m_focalLengthOverride", true);
                Set(data, "m_nearClipOverride", true);
                Call(data, "LateUpdate");
                Assert.That(data.FocusDistanceOutput, Is.EqualTo(0.01f));
                Assert.That(data.FocalLengthOutput, Is.EqualTo(2f));
                Assert.That(data.NearClipOutput, Is.EqualTo(3f));
            }
            LogAssert.NoUnexpectedReceived();
        }

        // OnEnable 0x06000518 (0x52E1F0) sets m_volumeProfileProvided only when the volume
        // is not null (0x52e242) and never clears it. LateUpdate then walks
        // sharedProfile.components (Volume +0x30, VolumeProfile +0x18), skips anything that
        // is not exactly DepthOfField (0x52e3f0) without stopping, and calls the virtual
        // SetValue (slot 8, 0x52e43c) on focusDistance (+0x60), focalLength (+0x70) and
        // aperture (+0x68) with a new MinFloatParameter(output, output). SetValue copies
        // the raw value, so the ClampedFloatParameter ranges (1..300, 1..32) and the
        // override states are untouched; assigning .value or Override() would clamp.
        [Test]
        public void CameraDataPushesRawDepthOfFieldValuesIntoEveryDepthOfFieldComponent()
        {
            using (var owned = new Owned())
            {
                GameObject root = owned.Inactive("AnimationCustomDataCamera volume fixture");
                var profile = owned.Add(ScriptableObject.CreateInstance<VolumeProfile>());
                var bloom = owned.Add(profile.Add<Bloom>());
                var first = owned.Add(profile.Add<DepthOfField>());
                var second = owned.Add(ScriptableObject.CreateInstance<DepthOfField>());
                profile.components.Add(second);
                var volume = root.AddComponent<Volume>();
                volume.sharedProfile = profile;
                float gaussianStart = first.gaussianStart.value;

                var data = root.AddComponent<AnimationCustomDataCamera>();
                Set(data, "m_postProcessingVolume", volume);
                data.FocusDistance = 500f;
                data.FocalLength = 400f;
                data.FStop = 100f;

                Call(data, "LateUpdate");
                Assert.That(first.focusDistance.value, Is.EqualTo(10f), "no profile write before OnEnable");

                Call(data, "OnEnable");
                Assert.That(Get(data, "m_volumeProfileProvided"), Is.True);
                Call(data, "LateUpdate");
                foreach (DepthOfField depthOfField in new[] { first, second })
                {
                    Assert.That(depthOfField.focusDistance.value, Is.EqualTo(500f * 0.01f));
                    Assert.That(depthOfField.focalLength.value, Is.EqualTo(400f), "focal length is not clamped to 300");
                    Assert.That(depthOfField.aperture.value, Is.EqualTo(100f), "aperture is not clamped to 32");
                    Assert.That(depthOfField.focusDistance.overrideState, Is.False);
                    Assert.That(depthOfField.focalLength.overrideState, Is.False);
                    Assert.That(depthOfField.aperture.overrideState, Is.False);
                }
                Assert.That(first.gaussianStart.value, Is.EqualTo(gaussianStart));
                Assert.That(profile.components[0], Is.SameAs(bloom));

                Set(data, "m_postProcessingVolume", null);
                Call(data, "OnEnable");
                Assert.That(Get(data, "m_volumeProfileProvided"), Is.True, "a later null volume does not clear the flag");
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static readonly string[] EyeFields =
        {
            "m_leftEyeHorizontalHash", "m_leftEyeVerticalHash", "m_rightEyeHorizontalHash", "m_rightEyeVerticalHash",
            "m_leftGlintHorizontalHash", "m_leftGlintVerticalHash", "m_rightGlintHorizontalHash", "m_rightGlintVerticalHash",
        };

        // String literals of the constructor 0x06000521 (0x52EAC0..0x52EB5D), in store order.
        private static readonly string[] EyeProperties =
        {
            "_LeftEyeHorizontal", "_LeftEyeVertical", "_RightEyeHorizontal", "_RightEyeVertical",
            "_LeftGlintHorizontal", "_LeftGlintVertical", "_RightGlintHorizontal", "_RightGlintVertical",
        };

        // ..ctor 0x06000521 stores -1 in m_targetMaterialIndex (0x52eab9) and
        // Shader.PropertyToID of each literal in field order (+0x34..+0x50). Start
        // 0x0600051c and LateUpdate 0x0600051e both return on a negative index (js at
        // 0x52e5d5, 0x52e88a), so neither the renderer nor the eye data is read.
        [Test]
        public void MaterialPropertyHashesTheEyeLiteralsAndANegativeIndexDoesNothing()
        {
            using (var owned = new Owned())
            {
                var driver = owned.Inactive("AnimationCustomDataToMaterialProperty fixture").AddComponent<AnimationCustomDataToMaterialProperty>();
                Assert.That(Get(driver, "m_targetMaterialIndex"), Is.EqualTo(-1));
                for (int i = 0; i < EyeFields.Length; ++i)
                    Assert.That(Get(driver, EyeFields[i]), Is.EqualTo(Shader.PropertyToID(EyeProperties[i])), EyeFields[i]);

                Set(driver, "m_targetMaterialIndex", -2);
                Assert.DoesNotThrow(() => Call(driver, "Start"));
                Assert.DoesNotThrow(() => Call(driver, "LateUpdate"));
                Assert.That(Get(driver, "m_material"), Is.Null);
            }
            LogAssert.NoUnexpectedReceived();
        }

        // UseSharedMaterials 0x06000520 (0x52E6F0) is isEditor && !isPlaying, true here.
        // Start then uses Renderer.GetSharedMaterials (0x52e6aa) and keeps the indexed
        // material itself; LateUpdate 0x0600051e (0x52E880) writes eye_l_u, eye_l_v, eye_r_u,
        // eye_r_v, glint_l_u, glint_l_v, glint_r_u, glint_r_v (+0x20..+0x3c) to the eight
        // hashes in order; OnDestroy 0x0600051d (0x52E760) destroys only when shared
        // materials are not in use, so the shared asset survives.
        [Test]
        public void MaterialPropertyDrivesTheSharedMaterialOutsidePlayMode()
        {
            using (var owned = new Owned())
            {
                string properties = string.Join(" ", EyeProperties.Select(name => name + " (\"\", Float) = 0"));
                var shader = owned.Add(ShaderUtil.CreateShaderAsset(
                    "Shader \"Hidden/ProjectLucid/EyeProperties\" { Properties { " + properties + " } SubShader { Pass { } } }"));
                var untouched = owned.Add(new Material(shader) { hideFlags = HideFlags.HideAndDontSave });
                var driven = owned.Add(new Material(shader) { hideFlags = HideFlags.HideAndDontSave });

                GameObject root = owned.Inactive("AnimationCustomDataToMaterialProperty material fixture");
                var renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { untouched, driven };
                var eyes = root.AddComponent<AnimationCustomDataCharacter>();
                float[] values = { 0.125f, 0.25f, 0.375f, 0.5f, 0.625f, 0.75f, 0.875f, 1.125f };
                eyes.eye_l_u = values[0];
                eyes.eye_l_v = values[1];
                eyes.eye_r_u = values[2];
                eyes.eye_r_v = values[3];
                eyes.glint_l_u = values[4];
                eyes.glint_l_v = values[5];
                eyes.glint_r_u = values[6];
                eyes.glint_r_v = values[7];

                var driver = root.AddComponent<AnimationCustomDataToMaterialProperty>();
                Set(driver, "m_eyeData", eyes);
                Set(driver, "m_renderer", renderer);
                Set(driver, "m_targetMaterialIndex", 1);
                Assert.That(Call(driver, "UseSharedMaterials"), Is.True);

                Call(driver, "Start");
                Assert.That(Get(driver, "m_material"), Is.SameAs(driven), "the shared material is used, not an instance");

                Call(driver, "LateUpdate");
                for (int i = 0; i < EyeProperties.Length; ++i)
                {
                    Assert.That(driven.GetFloat(EyeProperties[i]), Is.EqualTo(values[i]), EyeProperties[i]);
                    Assert.That(untouched.GetFloat(EyeProperties[i]), Is.EqualTo(0f), EyeProperties[i]);
                }

                Call(driver, "OnDestroy");
                Assert.That(driven != null, Is.True, "the shared material is not destroyed");
            }
            LogAssert.NoUnexpectedReceived();
        }

        // OnValidate 0x0600051f (0x52E960) reads sharedMaterials once (0x52e9ea) and, only
        // when the index is at or past its length (jge 0x52e9f5), stores length - 1
        // (0x52e9fe) before tail-jumping to HLOutput.LogError 0x06000a77 (0x52ea2c) with
        // the literal (0x52ea19) and this (0x52ea23). No lower bound is applied and a
        // missing renderer skips the check. HLOutput drops the context, so the owned
        // error capture checks the message and the IL checks that this is passed.
        [Test]
        public void MaterialPropertyValidationClampsOnlyAnIndexPastTheEnd()
        {
            using (var owned = new Owned())
            {
                var material = owned.Add(new Material(Shader.Find("Hidden/InternalErrorShader")) { hideFlags = HideFlags.HideAndDontSave });
                GameObject root = owned.Inactive("AnimationCustomDataToMaterialProperty validation fixture");
                var renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { material, material };
                var driver = root.AddComponent<AnimationCustomDataToMaterialProperty>();

                using (var capture = new OwnedHLOutputErrorCapture())
                {
                    Set(driver, "m_renderer", null);
                    Set(driver, "m_targetMaterialIndex", 9);
                    Call(driver, "OnValidate");
                    Assert.That(Get(driver, "m_targetMaterialIndex"), Is.EqualTo(9), "no renderer, no clamp");

                    Set(driver, "m_renderer", renderer);
                    foreach (int kept in new[] { 1, 0, -1, -7 })
                    {
                        Set(driver, "m_targetMaterialIndex", kept);
                        Call(driver, "OnValidate");
                        Assert.That(Get(driver, "m_targetMaterialIndex"), Is.EqualTo(kept));
                    }
                    Assert.That(capture.Errors, Is.Empty, "an index in range reports nothing");

                    foreach (int past in new[] { 2, 5 })
                    {
                        Set(driver, "m_targetMaterialIndex", past);
                        Call(driver, "OnValidate");
                        Assert.That(Get(driver, "m_targetMaterialIndex"), Is.EqualTo(1));
                    }
                    Assert.That(capture.Errors, Is.EqualTo(new[] { OutOfBoundsError + "|0", OutOfBoundsError + "|0" }));
                }
            }

            MethodInfo onValidate = typeof(AnimationCustomDataToMaterialProperty).GetMethod("OnValidate", Any | BindingFlags.DeclaredOnly);
            OriginalILCallSequence.Call logError = OriginalILCallSequence.Calls(onValidate).Last();
            Assert.That(logError.Target.DeclaringType, Is.EqualTo(typeof(HLOutput)));
            Assert.That(logError.Target.Name, Is.EqualTo("LogError"));
            Assert.That(logError.Target.GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(object), typeof(Object) }));
            byte[] il = onValidate.GetMethodBody().GetILAsByteArray();
            Assert.That(il[logError.Offset - 1], Is.EqualTo((byte)0x02), "ldarg.0 passes this as the context");
            LogAssert.NoUnexpectedReceived();
        }

        private const string OutOfBoundsError = "Target material index is out of bounds for the target renderer.";

        private static AnimationEventListener.AnimationEventAction EventAction(string eventString, UnityEvent action)
        {
            var eventAction = new AnimationEventListener.AnimationEventAction();
            Set(eventAction, "m_eventString", eventString);
            Set(eventAction, "m_action", action);
            Assert.That(eventAction.EventString, Is.SameAs(eventString));
            Assert.That(eventAction.Action, Is.SameAs(action));
            return eventAction;
        }

        // UpdateCachedValues 0x06000524 (0x52EB80) clears the index (0x52ec07) before the
        // null list test (0x52ec10), then stores each EventString -> Action through the
        // indexer (0x52ec73), so a repeated string keeps the last action. Awake 0x06000522
        // and OnValidate 0x06000523 (CanValidate is true in this release) both rebuild.
        // Action 0x06000525 (0x52ED40) looks up stringParameter and invokes only a hit.
        [Test]
        public void EventListenerInvokesTheLastActionIndexedForTheEventString()
        {
            using (var owned = new Owned())
            {
                var log = new List<string>();
                UnityEvent Logged(string name)
                {
                    var unityEvent = new UnityEvent();
                    unityEvent.AddListener(() => log.Add(name));
                    return unityEvent;
                }

                var listener = owned.Inactive("AnimationEventListener fixture").AddComponent<AnimationEventListener>();
                Set(listener, "m_animatorEventActions", new List<AnimationEventListener.AnimationEventAction>
                {
                    EventAction("Step", Logged("first step")),
                    EventAction("Jump", Logged("jump")),
                    EventAction("Step", Logged("last step")),
                });
                Call(listener, "Awake");

                listener.Action(new AnimationEvent { stringParameter = "Step" });
                listener.Action(new AnimationEvent { stringParameter = "Jump" });
                listener.Action(new AnimationEvent { stringParameter = "Land" });
                Assert.That(log, Is.EqualTo(new[] { "last step", "jump" }));

                Set(listener, "m_animatorEventActions", null);
                Call(listener, "OnValidate");
                listener.Action(new AnimationEvent { stringParameter = "Jump" });
                Assert.That(log, Is.EqualTo(new[] { "last step", "jump" }), "a null list still clears the index");
            }
            LogAssert.NoUnexpectedReceived();
        }

        // SampleAnimation 0x060024f8 (0x59C9D0) returns for a null clip, otherwise samples
        // the clip on this GameObject at clip.length * m_sampleFrame (0x59ca4e). OnEnable
        // 0x060024f7 (0x59C9A0) samples, then disables the Animation and sets cullingType
        // 0 (AlwaysAnimate). Sampling at m_sampleFrame seconds would give x = 1.25.
        [Test]
        public void SamplerPosesTheObjectAtTheFractionOfTheClipLengthAndDisablesTheAnimation()
        {
            using (var owned = new Owned())
            {
                GameObject root = owned.Active("AnimationSampler fixture");
                var animation = root.AddComponent<Animation>();
                AnimationClip clip = owned.LegacyClip("LucidSamplerClip", 2f, 10f);
                animation.clip = clip;
                animation.cullingType = AnimationCullingType.BasedOnRenderers;
                var sampler = root.AddComponent<AnimationSampler>();
                Set(sampler, "m_animation", animation);
                Set(sampler, "m_sampleFrame", 0.25f);

                root.transform.localPosition = new Vector3(7f, 0f, 0f);
                Call(sampler, "OnEnable");
                Assert.That(root.transform.localPosition.x, Is.EqualTo(2.5f).Within(1e-4f));
                Assert.That(animation.enabled, Is.False);
                Assert.That(animation.cullingType, Is.EqualTo(AnimationCullingType.AlwaysAnimate));

                animation.clip = null;
                root.transform.localPosition = new Vector3(7f, 0f, 0f);
                Assert.DoesNotThrow(() => sampler.SampleAnimation());
                Assert.That(root.transform.localPosition.x, Is.EqualTo(7f), "a null clip samples nothing");
            }
            LogAssert.NoUnexpectedReceived();
        }

        // OnValidate 0x060024f9 (0x59CA90) keeps an assigned Animation, otherwise takes
        // TryGetComponent's result (0x59cb18) and only on failure AddComponent<Animation>
        // (0x59cb38), then samples. Unity may already have run OnValidate when the
        // component was added, so the case first removes any Animation and the reference.
        [Test]
        public void SamplerValidationAddsAnAnimationOnlyWhenNoneIsFound()
        {
            using (var owned = new Owned())
            {
                GameObject root = owned.Inactive("AnimationSampler validation fixture");
                var sampler = root.AddComponent<AnimationSampler>();
                foreach (Animation existing in root.GetComponents<Animation>()) Object.DestroyImmediate(existing);
                Set(sampler, "m_animation", null);

                Call(sampler, "OnValidate");
                Animation[] animations = root.GetComponents<Animation>();
                Assert.That(animations.Length, Is.EqualTo(1));
                Assert.That(Get(sampler, "m_animation"), Is.SameAs(animations[0]));

                Set(sampler, "m_animation", null);
                Call(sampler, "OnValidate");
                Assert.That(root.GetComponents<Animation>().Length, Is.EqualTo(1), "an existing Animation is found, not added");
                Assert.That(Get(sampler, "m_animation"), Is.SameAs(animations[0]));

                Call(sampler, "OnValidate");
                Assert.That(root.GetComponents<Animation>().Length, Is.EqualTo(1));

                // An assigned Animation elsewhere is kept: the lookup runs only for null
                // (op_Equality 0x59caf1, je 0x59cb4c). It has no clip, so sampling returns.
                var other = owned.Inactive("AnimationSampler other Animation").AddComponent<Animation>();
                Set(sampler, "m_animation", other);
                Call(sampler, "OnValidate");
                Assert.That(Get(sampler, "m_animation"), Is.SameAs(other));
                Assert.That(root.GetComponents<Animation>().Length, Is.EqualTo(1));
            }
            LogAssert.NoUnexpectedReceived();
        }

        // OnValidate 0x060024f9 (0x59CA90) ends by tail-calling SampleAnimation (0x59cb53),
        // so it poses at clip.length * m_sampleFrame (0x59ca4e): 2 * 0.5 = 1 s, x = 5. It
        // does not disable the Animation, unlike OnEnable (set_enabled, 0x59c9b6).
        [Test]
        public void SamplerValidationSamplesWithoutDisablingTheAnimation()
        {
            using (var owned = new Owned())
            {
                GameObject root = owned.Active("AnimationSampler validation sampling fixture");
                var animation = root.AddComponent<Animation>();
                animation.clip = owned.LegacyClip("LucidSamplerValidateClip", 2f, 10f);
                var sampler = root.AddComponent<AnimationSampler>();
                Set(sampler, "m_animation", animation);
                Set(sampler, "m_sampleFrame", 0.5f);

                root.transform.localPosition = new Vector3(7f, 0f, 0f);
                Call(sampler, "OnValidate");
                Assert.That(root.transform.localPosition.x, Is.EqualTo(5f).Within(1e-4f));
                Assert.That(animation.enabled, Is.True);
            }
            LogAssert.NoUnexpectedReceived();
        }

        // SetAnimationSpeed 0x0600052c (0x52EED0): speed = Clamp01(x) (minss 1.0 then
        // andnps on x < 0, 0x52eee6..0x52eef2). speed == 0 (ucomiss/jne/jp 0x52ef08) rewinds,
        // samples and disables only an enabled Animation; anything else, NaN included,
        // enables a disabled Animation and sets the default clip state's normalizedSpeed to
        // min + (max - min) * speed (0x52ef8c..0x52ef99). Reset 0x0600052a takes
        // GetComponent<Animation>; SetAnimationEnabled 0x0600052b writes enabled. The clip
        // is 2 s long so normalizedSpeed (set_normalizedSpeed, 0x52efaa) and speed differ.
        [Test]
        public void SpeedScalerLerpsTheClampedSpeedAndRewindsOnlyAnEnabledAnimationAtZero()
        {
            using (var owned = new Owned())
            {
                GameObject root = owned.Active("AnimationSpeedScaler fixture");
                var animation = root.AddComponent<Animation>();
                AnimationClip clip = owned.LegacyClip("LucidSpeedClip", 2f, 10f);
                animation.AddClip(clip, clip.name);
                animation.clip = clip;
                var scaler = root.AddComponent<AnimationSpeedScaler>();
                Call(scaler, "Reset");
                Assert.That(Get(scaler, "m_animation"), Is.SameAs(animation));
                Set(scaler, "m_minSpeed", 1f);
                Set(scaler, "m_maxSpeed", 3f);
                AnimationState state = animation[clip.name];

                scaler.SetAnimationEnabled(false);
                Assert.That(animation.enabled, Is.False);
                scaler.SetAnimationSpeed(0.5f);
                Assert.That(animation.enabled, Is.True);
                Assert.That(state.normalizedSpeed, Is.EqualTo(2f));
                Assert.That(state.speed, Is.Not.EqualTo(2f), "normalizedSpeed is written, not speed");
                scaler.SetAnimationSpeed(4f);
                Assert.That(state.normalizedSpeed, Is.EqualTo(3f));

                animation.enabled = false;
                scaler.SetAnimationSpeed(float.NaN);
                Assert.That(animation.enabled, Is.True, "NaN is not zero");

                animation.Play(clip.name);
                state.time = 0.5f;
                animation.enabled = false;
                scaler.SetAnimationSpeed(-1f);
                Assert.That(state.time, Is.EqualTo(0.5f), "a disabled Animation is not rewound");
                Assert.That(animation.enabled, Is.False);

                animation.enabled = true;
                root.transform.localPosition = new Vector3(7f, 0f, 0f);
                scaler.SetAnimationSpeed(0f);
                Assert.That(state.time, Is.EqualTo(0f));
                Assert.That(root.transform.localPosition.x, Is.EqualTo(0f).Within(1e-4f), "the rewound pose is sampled");
                Assert.That(animation.enabled, Is.False);
            }
            LogAssert.NoUnexpectedReceived();
        }

        // OnEnable 0x060038d5 (0x6A6780) stores GetComponent<Animation> (0x6a67bc) and plays
        // at once unless m_delay > 0 (ucomiss/jbe 0x6a67d6, so NaN plays at once too). A
        // positive delay only starts <StartDelay>d__4 (StartCoroutine, 0x6a6834); the
        // coroutine stops at its WaitForSeconds, which never resumes in EditMode.
        [Test]
        public void StartDelayPlaysAtOnceUnlessTheDelayIsPositive()
        {
            using (var owned = new Owned())
            {
                GameObject root = owned.Active("AnimationStartDelay fixture");
                var animation = root.AddComponent<Animation>();
                AnimationClip clip = owned.LegacyClip("LucidStartDelayClip", 1f, 10f);
                animation.AddClip(clip, clip.name);
                animation.clip = clip;
                var delay = root.AddComponent<AnimationStartDelay>();

                foreach (float immediate in new[] { 0f, -1f, float.NaN })
                {
                    animation.Stop();
                    Set(delay, "m_animation", null);
                    Set(delay, "m_delay", immediate);
                    Call(delay, "OnEnable");
                    Assert.That(Get(delay, "m_animation"), Is.SameAs(animation), immediate.ToString());
                    Assert.That(animation.IsPlaying(clip.name), Is.True, immediate.ToString());
                }

                animation.Stop();
                Set(delay, "m_animation", null);
                Set(delay, "m_delay", 0.5f);
                Call(delay, "OnEnable");
                Assert.That(Get(delay, "m_animation"), Is.SameAs(animation));
                Assert.That(animation.isPlaying, Is.False, "a positive delay does not play at once");
                delay.StopAllCoroutines();
                animation.Stop();
            }
            LogAssert.NoUnexpectedReceived();
        }

        // <StartDelay>d__4.MoveNext 0x060038db (0x6A6970): state 0 yields a new
        // WaitForSeconds of the current m_delay (read at 0x6a69d6, not when the iterator
        // was made); state 1 plays and finishes. Reset 0x060038dd throws
        // NotSupportedException. OnDisable 0x060038d6 (0x6A68C0) builds a fresh iterator
        // and passes it to StopCoroutine(IEnumerator) (0x6a6926); the effect of stopping a
        // never-started iterator is not observable here, so the IL calls are compared.
        [Test]
        public void StartDelayIteratorWaitsTheCurrentDelayThenPlays()
        {
            using (var owned = new Owned())
            {
                GameObject root = owned.Active("AnimationStartDelay iterator fixture");
                var animation = root.AddComponent<Animation>();
                AnimationClip clip = owned.LegacyClip("LucidStartDelayIteratorClip", 1f, 10f);
                animation.AddClip(clip, clip.name);
                animation.clip = clip;
                var delay = root.AddComponent<AnimationStartDelay>();
                Set(delay, "m_animation", animation);
                Set(delay, "m_delay", 0.75f);

                var iterator = (IEnumerator)Call(delay, "StartDelay");
                Set(delay, "m_delay", 1.5f);
                Assert.That(iterator.MoveNext(), Is.True);
                Assert.That(iterator.Current, Is.TypeOf<WaitForSeconds>());
                Assert.That(Get(iterator.Current, "m_Seconds"), Is.EqualTo(1.5f));
                Assert.That(animation.isPlaying, Is.False);
                Assert.That(iterator.MoveNext(), Is.False);
                Assert.That(animation.IsPlaying(clip.name), Is.True);
                Assert.Throws<NotSupportedException>(() => iterator.Reset());
                animation.Stop();
            }

            MethodInfo onDisable = typeof(AnimationStartDelay).GetMethod("OnDisable", Any | BindingFlags.DeclaredOnly);
            List<OriginalILCallSequence.Call> calls = OriginalILCallSequence.Calls(onDisable);
            Assert.That(OriginalILCallSequence.Names(calls), Is.EqualTo(new[] { "AnimationStartDelay.StartDelay", "MonoBehaviour.StopCoroutine" }));
            Assert.That(calls[1].Target.GetParameters().Single().ParameterType, Is.EqualTo(typeof(IEnumerator)));
            LogAssert.NoUnexpectedReceived();
        }

        // OnStateEnter 0x06000e6f (0x726680) returns for an empty name, otherwise stores
        // StringToHash(m_boolParameter) (0x7266a7) before testing m_onEnter. OnStateExit
        // 0x06000e70 (0x7266D0) tests the name and m_onExit and then writes the cached
        // hash (0x7266f3) without rehashing, so a renamed parameter still writes the old one.
        [Test]
        public void BoolSetterHashesOnEveryEntryAndExitReusesTheCachedHash()
        {
            using (var owned = new Owned())
            {
                GameObject root = owned.Active("AnimatorBoolSetter fixture");
                Animator animator = owned.BoundAnimator(root, controller =>
                {
                    controller.AddParameter("Open", AnimatorControllerParameterType.Bool);
                    controller.AddParameter("Other", AnimatorControllerParameterType.Bool);
                });
                var setter = owned.Add(ScriptableObject.CreateInstance<AnimatorBoolSetter>());
                Set(setter, "m_boolParameter", "Open");
                Set(setter, "m_boolValue", true);

                setter.OnStateEnter(animator, default, 0);
                Assert.That(Get(setter, "m_boolParameterHash"), Is.EqualTo(Animator.StringToHash("Open")), "hashed without m_onEnter");
                Assert.That(animator.GetBool("Open"), Is.False);

                Set(setter, "m_boolParameter", "Other");
                setter.OnStateExit(animator, default, 0);
                Assert.That(animator.GetBool("Open"), Is.False, "exit without m_onExit writes nothing");
                Set(setter, "m_onExit", true);
                setter.OnStateExit(animator, default, 0);
                Assert.That(animator.GetBool("Open"), Is.True, "exit writes the hash cached on entry");
                Assert.That(animator.GetBool("Other"), Is.False);

                Set(setter, "m_onEnter", true);
                Set(setter, "m_boolValue", false);
                setter.OnStateEnter(animator, default, 0);
                Assert.That(animator.GetBool("Other"), Is.False);
                Assert.That(Get(setter, "m_boolParameterHash"), Is.EqualTo(Animator.StringToHash("Other")));
                Set(setter, "m_boolValue", true);
                setter.OnStateEnter(animator, default, 0);
                Assert.That(animator.GetBool("Other"), Is.True);

                Set(setter, "m_boolParameter", "");
                Set(setter, "m_boolValue", false);
                setter.OnStateEnter(animator, default, 0);
                setter.OnStateExit(animator, default, 0);
                Assert.That(Get(setter, "m_boolParameterHash"), Is.EqualTo(Animator.StringToHash("Other")), "an empty name keeps the hash");
                Assert.That(animator.GetBool("Other"), Is.True, "an empty name writes nothing");
            }
            LogAssert.NoUnexpectedReceived();
        }

        private sealed class ProbeAbilityDefinition : AbilityDefinition
        {
            public override Type ScriptType => typeof(CompletionProbe);
        }

        private class PlainAbility : ActorAbility
        {
            public override AbilityDefinition DefinitionBase => null;
        }

        private sealed class CompletionProbe : PlainAbility, IAbilityAnimatorTriggerable
        {
            public int Triggered;
            public int Completed;
            public void SetAnimationTriggered() => ++Triggered;
            public void SetAnimationComplete() => ++Completed;
        }

        // OnStateEnter 0x06000e73 (0x726740) stores GetSystemRef<BossManager>(null, true)
        // (0x7267de), InvokeOnValid(RegisterBossManager) (0x726830; 0x06000e72 stores the
        // manager), then m_boss = GetBossForAnimator(animator) (0x72683e) whatever
        // m_triggerOnEntry is. With the flag, TryGetAbilityFromType(definition.AbilityType)
        // (0x72686e) and an IAbilityAnimatorTriggerable cast (0x726889) lead to interface
        // slot 1, SetAnimationComplete. OnStateExit 0x06000e74 (0x726910) does the same with
        // m_triggerOnExit on the boss cached by the last entry, ignoring its animator.
        [Test]
        public void CompleteAbilitySetterResolvesTheBossOnEntryAndCompletesItsAbility()
        {
            var owned = new Owned();
            // BossManager's field initialiser takes a SaveManager reference; the fixture
            // owns that row and the BossManager row it registers into.
            var saveRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<SaveManager>());
            VerificationFixtureOwnership.RegistryRow bossRow = null;
            BossManager manager = null;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                bossRow = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<BossManager>());
                manager = owned.Inactive("BossManager fixture").AddComponent<BossManager>();
                bossRow.Register(manager);

                var boss = owned.Inactive("Boss fixture").AddComponent<Boss>();
                var animator = owned.Inactive("Boss animator fixture").AddComponent<Animator>();
                var otherAnimator = owned.Inactive("Other animator fixture").AddComponent<Animator>();
                typeof(Actor).GetProperty(nameof(Actor.Animator)).GetSetMethod(true).Invoke(boss, new object[] { new ActorAnimator(animator) });
                manager.Bosses.Add(boss);

                var definition = owned.Add(ScriptableObject.CreateInstance<ProbeAbilityDefinition>());
                // Test-only fixture value, not an original constant: any value works because
                // the fixture boss owns its ability dictionary.
                definition.AbilityType = (ActorAbilityType)0x4C554344;
                var probe = new CompletionProbe();
                var abilities = (Dictionary<ActorAbilityType, ActorAbility>)Get(boss, "m_abilities");
                abilities.Add(definition.AbilityType, probe);

                var setter = owned.Add(ScriptableObject.CreateInstance<AnimatorCompleteAbilitySetter>());
                Set(setter, "m_abilityDefinition", definition);
                Set(setter, "m_triggerOnEntry", true);

                setter.OnStateEnter(animator, default, 0);
                Assert.That(Get(setter, "m_bossManager"), Is.SameAs(manager));
                Assert.That(((SystemRef<BossManager>)Get(setter, "m_bossManagerRef")).GetSafe(), Is.SameAs(manager));
                Assert.That(Get(setter, "m_boss"), Is.SameAs(boss));
                Assert.That(probe.Completed, Is.EqualTo(1));

                setter.OnStateExit(otherAnimator, default, 0);
                Assert.That(probe.Completed, Is.EqualTo(1), "exit without m_triggerOnExit completes nothing");
                Set(setter, "m_triggerOnExit", true);
                setter.OnStateExit(otherAnimator, default, 0);
                Assert.That(probe.Completed, Is.EqualTo(2), "exit completes the cached boss's ability");

                Set(setter, "m_triggerOnEntry", false);
                Set(setter, "m_boss", null);
                setter.OnStateEnter(animator, default, 0);
                Assert.That(Get(setter, "m_boss"), Is.SameAs(boss), "the boss is resolved without m_triggerOnEntry");
                Assert.That(probe.Completed, Is.EqualTo(2));

                abilities[definition.AbilityType] = new PlainAbility();
                Assert.DoesNotThrow(() => setter.OnStateExit(animator, default, 0), "an ability without the interface is skipped");

                setter.OnStateEnter(otherAnimator, default, 0);
                Assert.That(Get(setter, "m_boss"), Is.Null, "an animator no boss owns resolves to null");
                Assert.That(probe.Triggered, Is.EqualTo(0));
            },
            () => { if (manager != null) manager.Bosses.Clear(); },
            () => bossRow?.Dispose(),
            owned.Dispose,
            saveRow.Dispose);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
