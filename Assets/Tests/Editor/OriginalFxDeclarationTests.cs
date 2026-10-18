using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Declarations of the restored fx types against the dumper's DummyDll
    // Game.Runtime metadata: TypeDef flags, Il2CppSetOption order, field and
    // MethodDef order with their flags, and the compiler-generated member
    // names that the original member order fixes. Reflection only.
    [TestFixture]
    public sealed class OriginalFxDeclarationTests
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        const int Arrays = 2, Nulls = 1;

        static void CheckDeclaration(Type type, int flags, Type baseType, int[] options, string[] fields, int[] fieldFlags,
            string[] methods, int[] methodFlags)
        {
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("Game.Runtime"), type.Name);
            Assert.That((int)type.Attributes, Is.EqualTo(flags), type.Name + " TypeDef flags");
            Assert.That(type.BaseType, Is.EqualTo(baseType), type.Name);
            int[] declaredOptions = type.GetCustomAttributesData()
                .Where(a => a.AttributeType == typeof(Il2CppSetOptionAttribute))
                .Select(a => Convert.ToInt32(a.ConstructorArguments[0].Value)).ToArray();
            Assert.That(declaredOptions, Is.EqualTo(options), type.Name + " Il2CppSetOption order");
            foreach (CustomAttributeData option in type.GetCustomAttributesData().Where(a => a.AttributeType == typeof(Il2CppSetOptionAttribute)))
                Assert.That(option.ConstructorArguments[1].Value, Is.EqualTo(false), type.Name);

            FieldInfo[] declaredFields = type.GetFields(Declared).OrderBy(f => f.MetadataToken).ToArray();
            Assert.That(declaredFields.Select(f => f.Name).ToArray(), Is.EqualTo(fields), type.Name + " fields");
            Assert.That(declaredFields.Select(f => (int)f.Attributes).ToArray(), Is.EqualTo(fieldFlags), type.Name + " field flags");

            MethodBase[] members = type.GetMethods(Declared).Cast<MethodBase>()
                .Concat(type.GetConstructors(Declared)).OrderBy(m => m.MetadataToken).ToArray();
            Assert.That(members.Select(m => m.Name).ToArray(), Is.EqualTo(methods), type.Name + " methods");
            Assert.That(members.Select(m => (int)m.Attributes).ToArray(), Is.EqualTo(methodFlags), type.Name + " method flags");
        }

        static void CheckSerialized(Type type, params string[] names)
        {
            foreach (FieldInfo field in type.GetFields(Declared))
                Assert.That(field.IsDefined(typeof(SerializeField), false), Is.EqualTo(names.Contains(field.Name)), type.Name + "." + field.Name);
        }

        static string[] FieldAttributes(Type type, string field) =>
            type.GetField(field, Declared).GetCustomAttributesData().Select(a => a.AttributeType.Name).ToArray();

        // TypeDef 0x02000088 (flags 0x100001, no attributes) and 0x060004c1..c7.
        [Test]
        public void AddressableHostRedirectorKeepsItsDeclaration()
        {
            CheckDeclaration(typeof(AddressableHostRedirector), 0x100001, typeof(object), new int[0],
                new[] { "m_targetHost", "m_defaultMatchHostRegEx", "m_customMatchHostRegEx" }, new[] { 1, 33, 1 },
                new[] { "get_MatchHostRegEx", "set_MatchHostRegEx", ".ctor", "Finalize", "SetHostURL", "EditResourceRequest", "ReplaceHost" },
                new[] { 2182, 2182, 6278, 196, 134, 129, 129 });
            Assert.That(typeof(AddressableHostRedirector).GetCustomAttributesData(), Is.Empty);
            Assert.That(typeof(AddressableHostRedirector).GetMethod("Finalize", Declared).GetBaseDefinition().DeclaringType, Is.EqualTo(typeof(object)));
        }

        // TypeDefs 0x020009e5, 0x020009e6, 0x020006c6 and 0x020006ce.
        [Test]
        public void TextureShadowAndCanvasHelpersKeepTheirDeclarations()
        {
            CheckDeclaration(typeof(AssignAddressableTexture), 0x100001, typeof(MonoBehaviour), new[] { Nulls, Arrays },
                new[] { "m_camera", "m_renderTexture" }, new[] { 1, 1 },
                new[] { "OnEnable", ".ctor", "<OnEnable>b__2_0" }, new[] { 129, 6278, 129 });
            CheckSerialized(typeof(AssignAddressableTexture), "m_camera", "m_renderTexture");
            Assert.That(typeof(AssignAddressableTexture).GetField("m_renderTexture", Declared).FieldType,
                Is.EqualTo(typeof(UnityEngine.AddressableAssets.AssetReferenceT<RenderTexture>)));

            CheckDeclaration(typeof(BlobShadowModifier), 0x100101, typeof(MonoBehaviour), new[] { Nulls, Arrays },
                new[] { "m_characterManagerRef" }, new[] { 33 },
                new[] { "Action_ShowBlobShadow", "Action_HideBlobShadow", ".ctor" }, new[] { 134, 134, 6278 });

            CheckDeclaration(typeof(CanvasShaderModifier), 0x100101, typeof(object), new[] { Nulls, Arrays },
                new[] { "m_renderers" }, new[] { 33 },
                new[] { ".ctor", "SetDreamNoiseStandardOffset" }, new[] { 6278, 134 });
            Assert.That(typeof(CanvasShaderModifier).GetField("m_renderers", Declared).FieldType, Is.EqualTo(typeof(IEnumerable<CanvasRenderer>)));

            // Abstract|Sealed|BeforeFieldInit: a static class with an initializer, no explicit .cctor.
            CheckDeclaration(typeof(ShaderProperties), 0x100181, typeof(object), new[] { Arrays, Nulls },
                new[] { "DreamNoiseST" }, new[] { 54 }, new[] { ".cctor" }, new[] { 6289 });
            Assert.That(typeof(ShaderProperties).GetField("DreamNoiseST").FieldType, Is.EqualTo(typeof(string)));
        }

        // TypeDefs 0x02000a05, 0x02000a11, 0x02000a0e and 0x02000a10.
        [Test]
        public void RendererAndColourHelpersKeepTheirDeclarations()
        {
            CheckDeclaration(typeof(FlashRenderers), 0x100001, typeof(TimeScaledComponent_SDT), new[] { Arrays, Nulls },
                new[] { "m_renderers", "m_initialDelay", "m_flashInterval", "m_timerSeconds", "m_nextFlashIntervalSeconds" },
                new[] { 1, 1, 1, 1, 1 },
                new[] { "OnEnable", "InternalUpdate", "Action_EnableAllRenderers", ".ctor" }, new[] { 196, 196, 134, 6278 });
            CheckSerialized(typeof(FlashRenderers), "m_renderers", "m_initialDelay", "m_flashInterval");

            CheckDeclaration(typeof(MaterialPropertyBlockSetter), 0x100001, typeof(MonoBehaviour), new[] { Arrays, Nulls },
                new[] { "m_renderer", "m_colourPropertyName", "m_colorToSet" }, new[] { 1, 1, 1 },
                new[] { "Awake", ".ctor" }, new[] { 129, 6278 });
            CheckSerialized(typeof(MaterialPropertyBlockSetter), "m_renderer", "m_colourPropertyName", "m_colorToSet");

            CheckDeclaration(typeof(LerpColour), 0x100001, typeof(MonoBehaviour), new[] { Arrays, Nulls },
                new[] { "m_lerpSpeedMultiplier", "m_setFirstValue", "Colour", "m_lerpRoutine", "m_valueAssigned" },
                new[] { 1, 1, 38, 1, 1 },
                new[] { "SetTargetColour", "LerpStartColour", ".ctor" }, new[] { 134, 129, 6278 });
            CheckSerialized(typeof(LerpColour), "m_lerpSpeedMultiplier", "m_setFirstValue");
            Assert.That(FieldAttributes(typeof(LerpColour), "m_setFirstValue"), Is.EqualTo(new[] { "SerializeField", "TooltipAttribute" }));
            Assert.That(typeof(LerpColour).GetField("m_setFirstValue", Declared).GetCustomAttribute<TooltipAttribute>().tooltip,
                Is.EqualTo("First non-empty target colour assigned will be directly set rather than lerped to."));

            // Serializable is the TypeDef flag 0x2000, not a custom attribute row.
            CheckDeclaration(typeof(LineRendererContainer), 0x102001, typeof(object), new[] { Nulls, Arrays },
                new[] { "m_positionsCountMax", "m_lineRenderer", "m_positionsCount", "m_positions" }, new[] { 1, 1, 1, 1 },
                new[] { ".ctor", "SetActive", "AddPosition", "Finalise" }, new[] { 6278, 134, 134, 134 });
            CheckSerialized(typeof(LineRendererContainer), "m_positionsCountMax", "m_lineRenderer");
        }

        // TypeDefs 0x0200076c and 0x02000961; the trigger's field attributes
        // keep their metadata order, Tooltip before SerializeField on three.
        [Test]
        public void FullscreenShaderComponentsKeepTheirDeclarations()
        {
            Type trigger = typeof(FullscreenShaderEffectTrigger);
            CheckDeclaration(trigger, 0x100001, typeof(MonoBehaviour), new[] { Nulls, Arrays },
                new[] { "m_type", "m_untilStopTriggered", "m_duration", "m_applyCurveOverDuration", "m_persistAfterTriggerDestroyed",
                        "m_playing", "m_fullscreenShaderManager", "m_parametersHandle" },
                new[] { 1, 1, 1, 1, 1, 1, 1, 1 },
                new[] { "Awake", "OnDestroy", "StartEffect", "StopEffect", "SetDuration", "SetFullscreenShaderParametersType",
                        "OnValidate", ".ctor", "<StartEffect>b__10_0" },
                new[] { 129, 129, 134, 134, 134, 134, 129, 6278, 129 });
            Assert.That(FieldAttributes(trigger, "m_type"), Is.EqualTo(new[] { "SerializeField" }));
            Assert.That(FieldAttributes(trigger, "m_untilStopTriggered"), Is.EqualTo(new[] { "SerializeField", "TooltipAttribute" }));
            Assert.That(FieldAttributes(trigger, "m_duration"), Is.EqualTo(new[] { "TooltipAttribute", "SerializeField", "MinAttribute" }));
            Assert.That(FieldAttributes(trigger, "m_applyCurveOverDuration"), Is.EqualTo(new[] { "TooltipAttribute", "SerializeField" }));
            Assert.That(FieldAttributes(trigger, "m_persistAfterTriggerDestroyed"), Is.EqualTo(new[] { "TooltipAttribute", "SerializeField" }));
            Assert.That(trigger.GetField("m_duration", Declared).GetCustomAttribute<MinAttribute>().min, Is.EqualTo(0f));
            Assert.That(trigger.GetField("m_untilStopTriggered", Declared).GetCustomAttribute<TooltipAttribute>().tooltip,
                Is.EqualTo("If ticked, effect will continue for an unlimited time until the StopEffect function is called."));
            Assert.That(trigger.GetField("m_duration", Declared).GetCustomAttribute<TooltipAttribute>().tooltip,
                Is.EqualTo("Duration of effect if not unlimited."));
            Assert.That(trigger.GetField("m_applyCurveOverDuration", Declared).GetCustomAttribute<TooltipAttribute>().tooltip,
                Is.EqualTo("If this shader parameter definition has normalised anim curves, apply them over the given duration."));
            Assert.That(trigger.GetField("m_persistAfterTriggerDestroyed", Declared).GetCustomAttribute<TooltipAttribute>().tooltip,
                Is.EqualTo("If ticked, effect will continue to its natural end even if this object that triggered it is destroyed."));

            CheckDeclaration(typeof(FullscreenShaderUIBlur), 0x100001, typeof(MonoBehaviour), new[] { Nulls, Arrays },
                new[] { "m_fullscreenShaderManagerRef", "m_uiBlurRequest" }, new[] { 33, 1 },
                new[] { "OnEnable", "OnDisable", ".ctor" }, new[] { 129, 129, 6278 });
            Assert.That(typeof(FullscreenShaderUIBlur).GetInterfaces(), Does.Contain(typeof(Hardlight.ISystem)));
        }

        // TypeDefs 0x02000a0c, 0x02000a19 and 0x02000084 (RequireComponent
        // is that type's first attribute row).
        [Test]
        public void SpawnerQualityAndBinderTargetsKeepTheirDeclarations()
        {
            CheckDeclaration(typeof(InstancedObjectSpawner), 0x100001, typeof(MonoBehaviour), new[] { Nulls, Arrays },
                new[] { "m_prefab", "m_maxRow", "m_maxColumn", "m_toggleableComponents", "m_rendererFeatures",
                        "m_currentComponentIndex", "m_currentFeatureIndex" },
                new[] { 1, 1, 1, 1, 1, 1, 1 },
                new[] { "CreateInstances", "ResetComponents", "ToggleNextComponent", "HasComponentsToToggle", "ResetRenderingFeatures",
                        "ToggleNextRenderFeature", "HasFeaturesToToggle", "CleanUp", ".ctor" },
                new[] { 134, 134, 134, 134, 134, 134, 134, 134, 6278 });
            CheckSerialized(typeof(InstancedObjectSpawner), "m_prefab", "m_maxRow", "m_maxColumn", "m_toggleableComponents", "m_rendererFeatures");
            Assert.That(typeof(InstancedObjectSpawner).GetMethod("CreateInstances").GetCustomAttribute<ContextMenu>().menuItem, Is.EqualTo("CreateInstances"));
            Assert.That(typeof(InstancedObjectSpawner).GetMethod("CleanUp").GetCustomAttribute<ContextMenu>().menuItem, Is.EqualTo("CleanUp"));

            CheckDeclaration(typeof(PFXQualitySwitcher), 0x100001, typeof(MonoBehaviour), new[] { Nulls, Arrays },
                new[] { "m_default", "m_veryLow" }, new[] { 1, 1 },
                new[] { "Awake", "OnEnable", "OnDisable", ".ctor" }, new[] { 129, 129, 129, 6278 });

            Type binder = typeof(ParticleEffectBinderTarget);
            CheckDeclaration(binder, 0x100001, typeof(MonoBehaviour), new[] { Arrays, Nulls },
                new[] { "m_particleSystem" }, new[] { 1 },
                new[] { "Reset", "SetTransformScale", "SetTransformScale", "SetStartSpeed", "SetDuration", "SetMaxParticles",
                        "SetStartLifetime", "SetStartDelay", "SetEmitterVelocity", "SetEmitterVelocity", "SetStartSizeMultiplier",
                        "SetEmissionRateOverTimeMultiplier", "SetStartColour", "SetStartColourValueIndex", "SetStartColourRedValue",
                        "SetStartColourGreenValue", "SetStartColourBlueValue", "SetStartColourAlphaValue", "SetEmission", ".ctor" },
                new[] { 129, 134, 134, 134, 134, 134, 134, 134, 134, 134, 134, 134, 134, 129, 134, 134, 134, 134, 134, 6278 });
            CustomAttributeData first = binder.GetCustomAttributesData()[0];
            Assert.That(first.AttributeType, Is.EqualTo(typeof(RequireComponent)));
            Assert.That(first.ConstructorArguments.Single().Value, Is.EqualTo(typeof(ParticleSystem)));
            MethodInfo[] overloads = binder.GetMethods(Declared).Where(m => m.Name == "SetTransformScale" || m.Name == "SetEmitterVelocity")
                .OrderBy(m => m.MetadataToken).ToArray();
            Assert.That(overloads.Select(m => m.GetParameters().Single().ParameterType).ToArray(),
                Is.EqualTo(new[] { typeof(float), typeof(Vector3), typeof(float), typeof(Vector3) }), "float overload first (0x06000498, 0x0600049f)");
        }

        // Compiler-generated names from the original nested TypeDefs and
        // MethodDefs. Their ordinals count every earlier member, nested types
        // included, so they pin the member order of each owner.
        [Test]
        public void CompilerGeneratedMembersKeepTheOriginalNames()
        {
            Assert.That(typeof(FullscreenShaderManager).GetMethods(Declared).Where(m => m.Name.StartsWith("<")).Select(m => m.Name).ToArray(),
                Is.EqualTo(new[] { "<Awake>b__51_0", "<Awake>b__51_1", "<Awake>b__51_2" }), "0x060035d3..d5");
            Assert.That(typeof(FullscreenShaderManager).GetMethod("<Awake>b__51_1", Declared).GetParameters().Single().Name, Is.EqualTo("activeParameters"));
            Assert.That(typeof(FullscreenShaderManager).GetMethod("<Awake>b__51_2", Declared).GetParameters().Single().Name, Is.EqualTo("activeParameters"));

            Type manager = typeof(ParticleEffectManager);
            Assert.That(manager.GetNestedTypes(Declared).OrderBy(t => t.MetadataToken).Select(t => t.Name).ToArray(), Is.EqualTo(new[]
            {
                "PoolInterest", "<>c__DisplayClass20_0", "<>c__DisplayClass29_0", "<>c__DisplayClass30_0", "<AfterLevelExit>d__18",
            }), "0x02000a8f..93");
            Assert.That(manager.GetMethods(Declared).Where(m => m.Name.StartsWith("<")).Select(m => m.Name).ToArray(),
                Is.EqualTo(new[] { "<AfterLevelExit>b__18_0" }), "0x06003cda");
            Type startClosure = manager.GetNestedType("<>c__DisplayClass30_0", Declared);
            Assert.That(startClosure.GetFields(Declared).OrderBy(f => f.MetadataToken).Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "pfxType", "<>4__this", "pfx", "onComplete", "particleEffectDefinition", "particleBindingParameters", "onStart", "startEffect",
            }), "0x04002c2e..35");
            Assert.That(startClosure.GetMethods(Declared).OrderBy(m => m.MetadataToken).Select(m => m.Name).ToArray(), Is.EqualTo(new[]
            {
                "<AcquirePFX>b__0", "<AcquirePFX>g__OnStartCallback|1", "<AcquirePFX>g__OnCompleteCallback|2",
            }));
            Assert.That(manager.GetNestedType("<>c__DisplayClass29_0", Declared).GetFields(Declared).OrderBy(f => f.MetadataToken)
                .Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "<>4__this", "onComplete" }));
            Assert.That(manager.GetNestedType("<>c__DisplayClass20_0", Declared).GetFields(Declared).OrderBy(f => f.MetadataToken)
                .Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "<>4__this", "particleEffectDefinition" }));

            Type iterator = typeof(LerpColour).GetNestedType("<LerpStartColour>d__6", Declared);
            Assert.That(iterator, Is.Not.Null, "0x02000a0f");
            Assert.That(iterator.GetFields(Declared).OrderBy(f => f.MetadataToken).Select(f => f.Name).ToArray(), Is.EqualTo(new[]
            {
                "<>1__state", "<>2__current", "<>4__this", "startColour", "targetColour", "<lerpValue>5__2",
            }), "0x04002a38..3d");
        }
    }
}
