using System;
using System.Text.RegularExpressions;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Ornament (Game.Runtime 020006dd, bodies 0x59CE60..0x59D1A0), OrnamentAdvanced (020006de, 0x59D1F0..0x59D2D0) and
    // OrnamentSimple (020006e3, 0x5A0080..0x5A0160). Edit Mode sends no Awake or OnEnable to these components, so the
    // lifecycle bodies are called by name; the engine's own OnValidate on AddComponent may already have filled the
    // references, so every OnValidate case first clears them.
    public sealed class OrnamentComponentTests
    {
        private static readonly Regex EditModeMaterialInstance =
            new Regex("^Instantiating material due to calling renderer\\.material during edit mode");

        // AddComponent sends the editor's OnValidate, whose nested AddComponent calls (a rotator, a sampler and the
        // sampler's own Animator or Animation) draw engine messages that are not what these cases examine.
        // The test framework logs each change of the setting, so a case that checks for unexpected logs expects both.
        private static void IgnoringEngineMessages(Action body, bool expectSendMessageWarning = false)
        {
            bool before = LogAssert.ignoreFailingMessages;
            if (!before) LogAssert.Expect(LogType.Log, new Regex("IgnoreFailingMessages:true"));
            LogAssert.ignoreFailingMessages = true;
            // Ignoring covers errors only; the warning of the rotator that OnValidate adds is not an error.
            if (expectSendMessageWarning) LogAssert.Expect(LogType.Warning, new Regex("SendMessage cannot be called during Awake, CheckConsistency, or OnValidate"));
            try
            {
                body();
            }
            finally
            {
                if (!before) LogAssert.Expect(LogType.Log, new Regex("IgnoreFailingMessages:false"));
                LogAssert.ignoreFailingMessages = before;
            }
        }

        private static T NewOrnament<T>(Owned owned, bool active = true, bool expectRotatorWarning = false) where T : Ornament
        {
            T ornament = null;
            IgnoringEngineMessages(() => ornament = owned.Host("Lucid ornament", active).AddComponent<T>(), expectRotatorWarning);
            return ornament;
        }

        private static void ClearReferences(Ornament ornament, params string[] fields)
        {
            foreach (SmoothedTransformRotator existing in ornament.GetComponents<SmoothedTransformRotator>())
                UnityEngine.Object.DestroyImmediate(existing);
            Set(ornament, "m_thumbnailCamera", null);
            Set(ornament, "m_transformRotator", null);
            foreach (string field in fields)
                Set(ornament, field, null);
        }

        // Constructor 0x59D1A0 (and the subclass constructors) store Shader.PropertyToID("_CastShadows") at +0x3C.
        [Test]
        public void EveryOrnamentResolvesTheCastShadowsPropertyIdWhenConstructed()
        {
            Owned.Run(owned =>
            {
                int expected = Shader.PropertyToID("_CastShadows");
                Assert.That(Get<int>(NewOrnament<Ornament>(owned), "m_castShadows"), Is.EqualTo(expected));
                Assert.That(Get<int>(NewOrnament<OrnamentAdvanced>(owned), "m_castShadows"), Is.EqualTo(expected));
                Assert.That(Get<int>(NewOrnament<OrnamentSimple>(owned), "m_castShadows"), Is.EqualTo(expected));
            });
        }

        // 0x59CE60, 0x59CE70, 0x59CE80, 0x59D1F0, 0x5A0080: the accessors return the serialized fields.
        [Test]
        public void TheAccessorsReturnTheSerializedFields()
        {
            Owned.Run(owned =>
            {
                var advanced = NewOrnament<OrnamentAdvanced>(owned);
                var simple = NewOrnament<OrnamentSimple>(owned);
                var camera = owned.Host("Lucid thumbnail camera", false).AddComponent<Camera>();
                var rotator = owned.Host("Lucid rotator", false).AddComponent<SmoothedTransformRotator>();
                var animator = owned.Host("Lucid animator sampler", false).AddComponent<AnimatorSampler>();
                var animation = owned.Host("Lucid animation sampler", false).AddComponent<AnimationSampler>();
                Set(advanced, "m_thumbnailCamera", camera); // Reflection: authored references
                Set(advanced, "m_transformRotator", rotator);
                Set(advanced, "m_yRotationOffset", 12.5f);
                Set(advanced, "m_animatorSampler", animator);
                Set(simple, "m_animationSampler", animation);

                Assert.That(advanced.ThumbnailCamera, Is.SameAs(camera));
                Assert.That(advanced.TransformRotator, Is.SameAs(rotator));
                Assert.That(advanced.YRotationOffset, Is.EqualTo(12.5f));
                Assert.That(advanced.AnimatorSampler, Is.SameAs(animator));
                Assert.That(simple.AnimationSampler, Is.SameAs(animation));
            });
        }

        // Awake 0x59CE90: a thumbnail camera's object is deactivated (SetActive(false), 0x59CEF9); none means no work.
        [Test]
        public void AwakeSwitchesTheThumbnailCameraObjectOff()
        {
            Owned.Run(owned =>
            {
                var ornament = NewOrnament<Ornament>(owned);
                Assert.DoesNotThrow(() => Call(ornament, "Awake"), "no thumbnail camera");

                var camera = owned.Host("Lucid thumbnail camera").AddComponent<Camera>();
                Set(ornament, "m_thumbnailCamera", camera); // Reflection: authored reference
                Call(ornament, "Awake");
                Assert.That(camera.gameObject.activeSelf, Is.False);
            });
        }

        // OnEnable 0x59CF10: shadow casting On (1, 0x59CF77), updateWhenOffscreen true, the keyword enabled on the
        // renderer's own material instance and the cast-shadows float set to the constant 1.0 at 0x29A6BE0.
        // OnDisable 0x59CFE0 stores that same 1.0 again (not 0) and then disables the keyword.
        [Test]
        public void EnableAndDisableDriveTheCastShadowsKeywordAndStoreOneInTheFloatBothTimes()
        {
            Owned.Run(owned =>
            {
                var shader = owned.Track(ShaderUtil.CreateShaderAsset(
                    "Shader \"Hidden/ProjectLucid/OrnamentCastShadows\" { Properties { _CastShadows (\"\", Float) = 0 } " +
                    // The keyword has to be declared by the shader for a material to hold it.
                    "SubShader { Pass { HLSLPROGRAM\n#pragma vertex Vertex\n#pragma fragment Fragment\n#pragma shader_feature_local _CastShadows\n" +
                    "float4 Vertex(float4 position : POSITION) : SV_POSITION { return position; } float4 Fragment() : SV_Target { return 0; }\nENDHLSL } } }"));
                var shared = owned.Track(new Material(shader) { hideFlags = HideFlags.HideAndDontSave });
                var renderer = owned.Host("Lucid shadow renderer", false).AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMaterial = shared;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.updateWhenOffscreen = false;
                var ornament = NewOrnament<Ornament>(owned, expectRotatorWarning: true);
                Set(ornament, "m_rendererToEnableShadowsOn", renderer); // Reflection: authored reference

                // The first .material read instantiates; the second finds the instance.
                LogAssert.Expect(LogType.Error, EditModeMaterialInstance);
                Call(ornament, "OnEnable");
                Material instance = owned.Track(renderer.sharedMaterial);
                Assert.That(instance, Is.Not.SameAs(shared));
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
                Assert.That(renderer.updateWhenOffscreen, Is.True);
                Assert.That(instance.IsKeywordEnabled("_CastShadows"), Is.True);
                Assert.That(instance.GetFloat("_CastShadows"), Is.EqualTo(1f));
                Assert.That(shared.IsKeywordEnabled("_CastShadows"), Is.False, "the shared material is untouched");

                instance.SetFloat("_CastShadows", 0f);
                Call(ornament, "OnDisable");
                Assert.That(instance.IsKeywordEnabled("_CastShadows"), Is.False);
                Assert.That(instance.GetFloat("_CastShadows"), Is.EqualTo(1f), "OnDisable stores 1.0, not 0");
            });
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void EnableAndDisableWithoutARendererDoNothing()
        {
            Owned.Run(owned =>
            {
                var ornament = NewOrnament<Ornament>(owned, expectRotatorWarning: true);
                Assert.DoesNotThrow(() => Call(ornament, "OnEnable"));
                Assert.DoesNotThrow(() => Call(ornament, "OnDisable"));
            });
            LogAssert.NoUnexpectedReceived();
        }

        // OnValidate 0x59D090: an unset camera becomes the first active child camera, and an unset rotator the one
        // on this object or, failing that, a new one.
        [Test]
        public void ValidationFillsAnAbsentCameraAndAddsARotator()
        {
            Owned.Run(owned =>
            {
                var ornament = NewOrnament<Ornament>(owned);
                var camera = owned.Host("Lucid child camera", true, ornament.transform).AddComponent<Camera>();
                ClearReferences(ornament);

                IgnoringEngineMessages(() => Call(ornament, "OnValidate"));
                Assert.That(ornament.ThumbnailCamera, Is.SameAs(camera));
                Assert.That(ornament.TransformRotator, Is.Not.Null);
                Assert.That(ornament.TransformRotator.gameObject, Is.SameAs(ornament.gameObject));
                Assert.That(ornament.GetComponents<SmoothedTransformRotator>().Length, Is.EqualTo(1));
            });
        }

        [Test]
        public void ValidationReusesARotatorAlreadyOnTheObject()
        {
            Owned.Run(owned =>
            {
                var ornament = NewOrnament<Ornament>(owned);
                ClearReferences(ornament);
                var existing = ornament.gameObject.AddComponent<SmoothedTransformRotator>();

                IgnoringEngineMessages(() => Call(ornament, "OnValidate"));
                Assert.That(ornament.TransformRotator, Is.SameAs(existing));
                Assert.That(ornament.GetComponents<SmoothedTransformRotator>().Length, Is.EqualTo(1), "none is added");
            });
        }

        // The camera lookup runs only while the field is a null reference, and the rotator lookup only while it is
        // Unity-null: assigned references elsewhere are kept and nothing is added to this object.
        [Test]
        public void ValidationKeepsAssignedReferencesAndAddsNothingThen()
        {
            Owned.Run(owned =>
            {
                var ornament = NewOrnament<Ornament>(owned);
                owned.Host("Lucid child camera", true, ornament.transform).AddComponent<Camera>();
                ClearReferences(ornament);
                var assignedCamera = owned.Host("Lucid assigned camera", false).AddComponent<Camera>();
                var assignedRotator = owned.Host("Lucid assigned rotator", false).AddComponent<SmoothedTransformRotator>();
                Set(ornament, "m_thumbnailCamera", assignedCamera); // Reflection: authored references
                Set(ornament, "m_transformRotator", assignedRotator);

                IgnoringEngineMessages(() => Call(ornament, "OnValidate"));
                Assert.That(ornament.ThumbnailCamera, Is.SameAs(assignedCamera));
                Assert.That(ornament.TransformRotator, Is.SameAs(assignedRotator));
                Assert.That(ornament.GetComponents<SmoothedTransformRotator>(), Is.Empty);
            });
        }

        // 0x59D0DA tests the camera field with a plain reference compare (cmp [rbx+0x20], 0) and 0x59D133 tests the
        // rotator with Object::op_Inequality: a destroyed camera still counts as assigned (the child camera is not
        // looked up), a destroyed rotator counts as absent (a new one is added).
        [Test]
        public void ValidationTreatsADestroyedCameraAsAssignedAndADestroyedRotatorAsAbsent()
        {
            Owned.Run(owned =>
            {
                var ornament = NewOrnament<Ornament>(owned);
                var childCamera = owned.Host("Lucid child camera", true, ornament.transform).AddComponent<Camera>();
                ClearReferences(ornament);
                var deadCamera = owned.Host("Lucid dead camera", false).AddComponent<Camera>();
                var deadRotator = owned.Host("Lucid dead rotator", false).AddComponent<SmoothedTransformRotator>();
                Set(ornament, "m_thumbnailCamera", deadCamera); // Reflection: authored references
                Set(ornament, "m_transformRotator", deadRotator);
                UnityEngine.Object.DestroyImmediate(deadCamera);
                UnityEngine.Object.DestroyImmediate(deadRotator);

                IgnoringEngineMessages(() => Call(ornament, "OnValidate"));
                Assert.That(ReferenceEquals(Get<Camera>(ornament, "m_thumbnailCamera"), deadCamera), Is.True);
                Assert.That(Get<Camera>(ornament, "m_thumbnailCamera"), Is.Not.SameAs(childCamera));
                Assert.That(ornament.GetComponents<SmoothedTransformRotator>().Length, Is.EqualTo(1));
                Assert.That(ornament.TransformRotator != null, Is.True);
                Assert.That(ornament.TransformRotator.gameObject, Is.SameAs(ornament.gameObject));
            });
        }

        // OrnamentAdvanced.OnValidate 0x59D200: the base runs first, then the animator sampler is taken from the object
        // or added; an assigned sampler is kept.
        [Test]
        public void AdvancedValidationRunsTheBaseThenFindsOrAddsTheAnimatorSampler()
        {
            Owned.Run(owned =>
            {
                var ornament = NewOrnament<OrnamentAdvanced>(owned, false);
                ClearReferences(ornament, "m_animatorSampler");
                foreach (AnimatorSampler leftover in ornament.GetComponents<AnimatorSampler>())
                    UnityEngine.Object.DestroyImmediate(leftover);

                IgnoringEngineMessages(() => Call(ornament, "OnValidate"));
                var added = ornament.GetComponent<AnimatorSampler>();
                Assert.That(added, Is.Not.Null);
                Assert.That(ornament.AnimatorSampler, Is.SameAs(added));
                Assert.That(ornament.TransformRotator, Is.Not.Null, "the base ran");

                Set(ornament, "m_animatorSampler", null);
                IgnoringEngineMessages(() => Call(ornament, "OnValidate"));
                Assert.That(ornament.AnimatorSampler, Is.SameAs(added));
                Assert.That(ornament.GetComponents<AnimatorSampler>().Length, Is.EqualTo(1), "the one on the object is found, not added again");

                var assigned = owned.Host("Lucid assigned animator sampler", false).AddComponent<AnimatorSampler>();
                Set(ornament, "m_animatorSampler", assigned); // Reflection: authored reference
                IgnoringEngineMessages(() => Call(ornament, "OnValidate"));
                Assert.That(ornament.AnimatorSampler, Is.SameAs(assigned));
            });
        }

        // OrnamentSimple.OnValidate 0x5A0090 is the same shape over AnimationSampler.
        [Test]
        public void SimpleValidationRunsTheBaseThenFindsOrAddsTheAnimationSampler()
        {
            Owned.Run(owned =>
            {
                var ornament = NewOrnament<OrnamentSimple>(owned, false);
                ClearReferences(ornament, "m_animationSampler");
                foreach (AnimationSampler leftover in ornament.GetComponents<AnimationSampler>())
                    UnityEngine.Object.DestroyImmediate(leftover);

                IgnoringEngineMessages(() => Call(ornament, "OnValidate"));
                var added = ornament.GetComponent<AnimationSampler>();
                Assert.That(added, Is.Not.Null);
                Assert.That(ornament.AnimationSampler, Is.SameAs(added));
                Assert.That(ornament.TransformRotator, Is.Not.Null, "the base ran");

                Set(ornament, "m_animationSampler", null);
                IgnoringEngineMessages(() => Call(ornament, "OnValidate"));
                Assert.That(ornament.AnimationSampler, Is.SameAs(added));
                Assert.That(ornament.GetComponents<AnimationSampler>().Length, Is.EqualTo(1), "the one on the object is found, not added again");

                var assigned = owned.Host("Lucid assigned animation sampler", false).AddComponent<AnimationSampler>();
                Set(ornament, "m_animationSampler", assigned); // Reflection: authored reference
                IgnoringEngineMessages(() => Call(ornament, "OnValidate"));
                Assert.That(ornament.AnimationSampler, Is.SameAs(assigned));
            });
        }

        // Declarations from the DummyDll metadata: type flags 0x00100001, field and method flags and metadata order,
        // the IL2CPP options and the constructor placed last.
        [Test]
        public void DeclarationsKeepTheOriginalMembersFlagsAndOrder()
        {
            AssertMembers(typeof(Ornament), 0x00100001,
                new[]
                {
                    ("m_thumbnailCamera", 0x0001), ("m_transformRotator", 0x0001), ("m_rendererToEnableShadowsOn", 0x0001),
                    ("m_yRotationOffset", 0x0001), ("m_castShadows", 0x0021), ("CastShadowsKeyword", 0x8051),
                },
                new[]
                {
                    ("get_TransformRotator", 0x0886), ("get_ThumbnailCamera", 0x0886), ("get_YRotationOffset", 0x0886),
                    ("Awake", 0x0081), ("OnEnable", 0x0081), ("OnDisable", 0x0081), ("OnValidate", 0x01c4), (".ctor", 0x1886),
                });
            AssertMembers(typeof(OrnamentAdvanced), 0x00100001,
                new[] { ("m_animatorSampler", 0x0001) },
                new[] { ("get_AnimatorSampler", 0x0886), ("OnValidate", 0x00c4), (".ctor", 0x1886) });
            AssertMembers(typeof(OrnamentSimple), 0x00100001,
                new[] { ("m_animationSampler", 0x0001) },
                new[] { ("get_AnimationSampler", 0x0886), ("OnValidate", 0x00c4), (".ctor", 0x1886) });
            foreach (Type type in new[] { typeof(Ornament), typeof(OrnamentAdvanced), typeof(OrnamentSimple) })
                AssertIl2CppOptions(type, 1, 2); // metadata order: NullChecks, then ArrayBoundsChecks
            Assert.That(typeof(Ornament).GetField("CastShadowsKeyword", Declared).GetRawConstantValue(), Is.EqualTo("_CastShadows"));
            Assert.That(typeof(OrnamentAdvanced).BaseType, Is.EqualTo(typeof(Ornament)));
            Assert.That(typeof(OrnamentSimple).BaseType, Is.EqualTo(typeof(Ornament)));
            foreach (string field in new[] { "m_thumbnailCamera", "m_transformRotator", "m_rendererToEnableShadowsOn", "m_yRotationOffset" })
                Assert.That(AttributeTypes(Field(typeof(Ornament), field)), Is.EqualTo(new[] { typeof(SerializeField) }), field);
            Assert.That(typeof(Ornament).GetMethod("OnValidate", Declared).IsFamily, Is.True);
        }
    }
}
