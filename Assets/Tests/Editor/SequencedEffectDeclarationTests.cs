using System;
using System.Reflection;
using NUnit.Framework;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Declarations of the restored Game.Runtime sequenced effects (TypeDefs 0x0200061d..0x0200062c) against the original DummyDll
    // metadata: type and member flags, member order, field attribute order, Il2Cpp options and the iterator state machines.
    public sealed class SequencedEffectDeclarationTests
    {
        private static void AssertType(Type type, int typeFlags, Type baseType, int[] options,
            (string name, int flags)[] fields, (string name, int flags)[] methods)
        {
            AssertMembers(type, typeFlags, fields, methods);
            AssertIl2CppOptions(type, options);
            Assert.That(type.BaseType, Is.EqualTo(baseType), type.FullName);
        }

        // SequencedMagnetism 0x0200061d and its iterator 0x0200061e.
        [Test]
        public void MagnetismKeepsTheOriginalDeclaration()
        {
            AssertType(typeof(HardlightProject.SequencedMagnetism), 0x00100001, typeof(HardlightProject.SequencedEffect), new[] { 1, 2 },
                new[]
                {
                    ("m_speed", 0x0001), ("m_minDistance", 0x0001),
                    ("m_timeoutSeconds", 0x0001), ("m_timeManager", 0x0001)
                },
                new[]
                {
                    ("OnEffectTriggered", 0x00c4), ("MoveToTransform", 0x0081),
                    (".ctor", 0x1886)
                });
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedMagnetism), "m_speed")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedMagnetism), "m_minDistance")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedMagnetism), "m_timeoutSeconds")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
            AssertType(typeof(HardlightProject.SequencedMagnetism).GetNestedType("<MoveToTransform>d__5", BindingFlags.NonPublic), 0x00100103, typeof(object), new int[0],
                new[]
                {
                    ("<>1__state", 0x0001), ("<>2__current", 0x0001),
                    ("<>4__this", 0x0006), ("data", 0x0006),
                    ("<isComplete>5__2", 0x0001), ("<timeoutSeconds>5__3", 0x0001),
                    ("<speed>5__4", 0x0001), ("<waitForFixedUpdate>5__5", 0x0001)
                },
                new[]
                {
                    (".ctor", 0x1886), ("System.IDisposable.Dispose", 0x01e1),
                    ("MoveNext", 0x01e1), ("System.Collections.Generic.IEnumerator<System.Object>.get_Current", 0x09e1),
                    ("System.Collections.IEnumerator.Reset", 0x01e1), ("System.Collections.IEnumerator.get_Current", 0x09e1)
                });
        }

        // SequencedMaterial 0x0200061f and its iterator 0x02000620.
        [Test]
        public void MaterialKeepsTheOriginalDeclaration()
        {
            AssertType(typeof(HardlightProject.SequencedMaterial), 0x00100081, typeof(HardlightProject.SequencedEffect), new[] { 2, 1 },
                new[]
                {
                    ("m_material", 0x0004), ("m_blendTime", 0x0001),
                    ("m_blendValueStart", 0x0001), ("m_blendValueEnd", 0x0001),
                    ("m_timeManagerRef", 0x0021), ("m_blendValueReset", 0x0004)
                },
                new[]
                {
                    ("SetBlendValue", 0x05c4), ("OnEffectTriggered", 0x00c4),
                    ("WaitForAnimation", 0x0081), ("ResetProperties", 0x0086),
                    (".ctor", 0x1884)
                });
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedMaterial), "m_material")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedMaterial), "m_blendTime")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedMaterial), "m_blendValueStart")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedMaterial), "m_blendValueEnd")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
            AssertType(typeof(HardlightProject.SequencedMaterial).GetNestedType("<WaitForAnimation>d__8", BindingFlags.NonPublic), 0x00100103, typeof(object), new int[0],
                new[]
                {
                    ("<>1__state", 0x0001), ("<>2__current", 0x0001),
                    ("<>4__this", 0x0006), ("<timeManager>5__2", 0x0001),
                    ("<waitForFixedUpdate>5__3", 0x0001), ("<time>5__4", 0x0001)
                },
                new[]
                {
                    (".ctor", 0x1886), ("System.IDisposable.Dispose", 0x01e1),
                    ("MoveNext", 0x01e1), ("System.Collections.Generic.IEnumerator<System.Object>.get_Current", 0x09e1),
                    ("System.Collections.IEnumerator.Reset", 0x01e1), ("System.Collections.IEnumerator.get_Current", 0x09e1)
                });
        }

        // SequencedMaterialAlpha 0x02000621.
        [Test]
        public void MaterialAlphaKeepsTheOriginalDeclaration()
        {
            AssertType(typeof(HardlightProject.SequencedMaterialAlpha), 0x00100001, typeof(HardlightProject.SequencedMaterial), new[] { 1, 2 },
                new (string, int)[0],
                new[]
                {
                    ("Awake", 0x0081), ("SetBlendValue", 0x00c4),
                    (".ctor", 0x1886)
                });
        }

        // SequencedMaterialProperty 0x02000622.
        [Test]
        public void MaterialPropertyKeepsTheOriginalDeclaration()
        {
            AssertType(typeof(HardlightProject.SequencedMaterialProperty), 0x00100001, typeof(HardlightProject.SequencedMaterial), new[] { 2, 1 },
                new[]
                {
                    ("m_propertyName", 0x0001), ("m_propertyHash", 0x0001)
                },
                new[]
                {
                    ("Awake", 0x0081), ("SetBlendValue", 0x00c4),
                    (".ctor", 0x1886)
                });
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedMaterialProperty), "m_propertyName")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
        }

        // SequencedParticleEffectSpawner 0x02000623.
        [Test]
        public void ParticleEffectSpawnerKeepsTheOriginalDeclaration()
        {
            AssertType(typeof(HardlightProject.SequencedParticleEffectSpawner), 0x00100001, typeof(HardlightProject.SequencedEffect), new[] { 2, 1 },
                new[]
                {
                    ("m_effectSpawner", 0x0001), ("m_overrideSpawnWithTargetedEffect", 0x0001),
                    ("m_waitForParticleEffectToFinish", 0x0001), ("m_offsetMultiplier", 0x0001)
                },
                new[]
                {
                    ("OnEffectTriggered", 0x00c4), ("Reset", 0x0081),
                    (".ctor", 0x1886)
                });
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedParticleEffectSpawner), "m_effectSpawner")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedParticleEffectSpawner), "m_overrideSpawnWithTargetedEffect")), Is.EqualTo(new[] { typeof(UnityEngine.TooltipAttribute), typeof(UnityEngine.SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedParticleEffectSpawner), "m_waitForParticleEffectToFinish")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField), typeof(UnityEngine.TooltipAttribute) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedParticleEffectSpawner), "m_offsetMultiplier")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField), typeof(UnityEngine.TooltipAttribute) }));
        }

        // SequencedParticles 0x02000624 and its iterator 0x02000625.
        [Test]
        public void ParticlesKeepsTheOriginalDeclaration()
        {
            AssertType(typeof(HardlightProject.SequencedParticles), 0x00100001, typeof(HardlightProject.SequencedEffect), new[] { 1, 2 },
                new[]
                {
                    ("m_useParticleEffectWrapper", 0x0001), ("m_particleSystem", 0x0001),
                    ("m_playOnTarget", 0x0001), ("m_particleEffectWrapper", 0x0001),
                    ("m_waitForParticleCompletion", 0x0001)
                },
                new[]
                {
                    ("OnEffectTriggered", 0x00c4), ("WaitForParticles", 0x0081),
                    ("OnValidate", 0x0081), (".ctor", 0x1886),
                    ("<OnEffectTriggered>b__5_0", 0x0081)
                });
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedParticles), "m_useParticleEffectWrapper")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedParticles), "m_particleSystem")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField), typeof(Hardlight.HideIfAttribute) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedParticles), "m_playOnTarget")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField), typeof(Hardlight.HideIfAttribute) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedParticles), "m_particleEffectWrapper")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField), typeof(Hardlight.ShowIfAttribute) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedParticles), "m_waitForParticleCompletion")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
            AssertType(typeof(HardlightProject.SequencedParticles).GetNestedType("<WaitForParticles>d__6", BindingFlags.NonPublic), 0x00100103, typeof(object), new int[0],
                new[]
                {
                    ("<>1__state", 0x0001), ("<>2__current", 0x0001),
                    ("<>4__this", 0x0006), ("data", 0x0006)
                },
                new[]
                {
                    (".ctor", 0x1886), ("System.IDisposable.Dispose", 0x01e1),
                    ("MoveNext", 0x01e1), ("System.Collections.Generic.IEnumerator<System.Object>.get_Current", 0x09e1),
                    ("System.Collections.IEnumerator.Reset", 0x01e1), ("System.Collections.IEnumerator.get_Current", 0x09e1)
                });
        }

        // SequencedPostProcessing 0x02000626 and its iterator 0x02000627.
        [Test]
        public void PostProcessingKeepsTheOriginalDeclaration()
        {
            AssertType(typeof(HardlightProject.SequencedPostProcessing), 0x00100001, typeof(HardlightProject.SequencedEffect), new[] { 1, 2 },
                new[]
                {
                    ("m_curve", 0x0001), ("m_postProcessVolume", 0x0001),
                    ("m_timeManager", 0x0001)
                },
                new[]
                {
                    ("OnEffectTriggered", 0x00c4), ("ScalePostProcessWeight", 0x0081),
                    (".ctor", 0x1886)
                });
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedPostProcessing), "m_curve")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedPostProcessing), "m_postProcessVolume")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
            AssertType(typeof(HardlightProject.SequencedPostProcessing).GetNestedType("<ScalePostProcessWeight>d__4", BindingFlags.NonPublic), 0x00100103, typeof(object), new int[0],
                new[]
                {
                    ("<>1__state", 0x0001), ("<>2__current", 0x0001),
                    ("<>4__this", 0x0006), ("<elapsedTime>5__2", 0x0001),
                    ("<finalFrame>5__3", 0x0001), ("<endTime>5__4", 0x0001)
                },
                new[]
                {
                    (".ctor", 0x1886), ("System.IDisposable.Dispose", 0x01e1),
                    ("MoveNext", 0x01e1), ("System.Collections.Generic.IEnumerator<System.Object>.get_Current", 0x09e1),
                    ("System.Collections.IEnumerator.Reset", 0x01e1), ("System.Collections.IEnumerator.get_Current", 0x09e1)
                });
        }

        // SequencedTransformSurfaceFollower 0x0200062a.
        [Test]
        public void TransformSurfaceFollowerKeepsTheOriginalDeclaration()
        {
            AssertType(typeof(HardlightProject.SequencedTransformSurfaceFollower), 0x00100001, typeof(HardlightProject.SequencedEffect), new[] { 1, 2 },
                new[]
                {
                    ("m_transformSurfaceFollower", 0x0001)
                },
                new[]
                {
                    ("OnEffectTriggered", 0x00c4), ("OnComplete", 0x0081),
                    (".ctor", 0x1886)
                });
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedTransformSurfaceFollower), "m_transformSurfaceFollower")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
        }

        // SequencedUnityEvent 0x0200062b.
        [Test]
        public void UnityEventKeepsTheOriginalDeclaration()
        {
            AssertType(typeof(HardlightProject.SequencedUnityEvent), 0x00100001, typeof(HardlightProject.SequencedEffect), new[] { 1, 2 },
                new[]
                {
                    ("m_unityEvent", 0x0001), ("m_lengthOverrideSeconds", 0x0001),
                    ("m_sequenceWaitCount", 0x0001)
                },
                new[]
                {
                    ("OnEffectTriggered", 0x00c4), ("OnEffectComplete", 0x0081),
                    ("OnSequenceStart", 0x01e6), ("OnSequenceEnd", 0x01e6),
                    ("BeginSequence", 0x0086), (".ctor", 0x1886)
                });
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedUnityEvent), "m_unityEvent")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedUnityEvent), "m_lengthOverrideSeconds")), Is.EqualTo(new[] { typeof(UnityEngine.SerializeField) }));
        }

        // SequencedWaitForContinue 0x0200062c.
        [Test]
        public void WaitForContinueKeepsTheOriginalDeclaration()
        {
            AssertType(typeof(HardlightProject.SequencedWaitForContinue), 0x00100001, typeof(HardlightProject.SequencedEffect), new[] { 1, 2 },
                new[]
                {
                    ("m_allowContinueBeforeTriggered", 0x0001), ("m_onlyTriggerOnce", 0x0001),
                    ("m_triggered", 0x0001), ("m_continued", 0x0001)
                },
                new[]
                {
                    ("OnEffectTriggered", 0x00c4), ("DoContinue", 0x0086),
                    (".ctor", 0x1886)
                });
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedWaitForContinue), "m_allowContinueBeforeTriggered")), Is.EqualTo(new[] { typeof(UnityEngine.TooltipAttribute), typeof(UnityEngine.SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(HardlightProject.SequencedWaitForContinue), "m_onlyTriggerOnce")), Is.EqualTo(new[] { typeof(UnityEngine.TooltipAttribute), typeof(UnityEngine.SerializeField) }));
        }

    }
}
