using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Declaration contracts from the original DummyDll metadata tables (issue #295).
    // Metadata only: nothing here constructs an owner, reads a static field or runs a
    // type initializer, so shared state is left exactly as found.
    public sealed class OriginalManagedDeclarationContractsTests
    {
        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Instance |
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static Type Nested(Type owner, string name)
        {
            Type nested = owner.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(nested, owner.FullName + "+" + name);
            return nested;
        }

        private static MethodInfo Method(Type owner, string name, params Type[] parameterTypes)
        {
            MethodInfo[] matches = owner.GetMethods(Declared).Where(m => m.Name == name &&
                m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameterTypes)).ToArray();
            Assert.AreEqual(1, matches.Length, owner.FullName + "." + name);
            return matches[0];
        }

        private static void NullDefault(ParameterInfo parameter)
        {
            Assert.AreEqual(ParameterAttributes.Optional | ParameterAttributes.HasDefault, parameter.Attributes, parameter.Name);
            Assert.IsTrue(parameter.HasDefaultValue, parameter.Name);
            Assert.IsNull(parameter.DefaultValue, parameter.Name);
        }

        private static void IntDefault(ParameterInfo parameter, int value)
        {
            Assert.AreEqual(ParameterAttributes.Optional | ParameterAttributes.HasDefault, parameter.Attributes, parameter.Name);
            Assert.IsTrue(parameter.HasDefaultValue, parameter.Name);
            Assert.AreEqual(typeof(int), parameter.DefaultValue.GetType(), parameter.Name);
            Assert.AreEqual(value, (int)parameter.DefaultValue, parameter.Name);
        }

        private static void SingleStringAttribute(MemberInfo member, Type attributeType, string value)
        {
            CustomAttributeData[] matches = member.GetCustomAttributesData().Where(a => a.AttributeType == attributeType).ToArray();
            Assert.AreEqual(1, matches.Length, member.Name + " " + attributeType.Name);
            CustomAttributeData attribute = matches[0];
            CollectionAssert.AreEqual(new[] { typeof(string) }, attribute.Constructor.GetParameters().Select(p => p.ParameterType).ToArray());
            Assert.AreEqual(1, attribute.ConstructorArguments.Count);
            Assert.AreEqual(value, attribute.ConstructorArguments[0].Value);
            Assert.AreEqual(0, attribute.NamedArguments.Count);
        }

        private static void CreateAssetMenuNamedArguments(Type owner, params object[] namesAndValues)
        {
            CustomAttributeData[] matches = owner.GetCustomAttributesData().Where(a => a.AttributeType == typeof(CreateAssetMenuAttribute)).ToArray();
            Assert.AreEqual(1, matches.Length);
            CustomAttributeData attribute = matches[0];
            Assert.AreEqual(0, attribute.Constructor.GetParameters().Length);
            Assert.AreEqual(0, attribute.ConstructorArguments.Count);
            // The original blobs write the named arguments in this order, order = 0 included.
            Assert.AreEqual(namesAndValues.Length / 2, attribute.NamedArguments.Count);
            for (int i = 0; i < attribute.NamedArguments.Count; i++)
            {
                Assert.AreEqual(namesAndValues[2 * i], attribute.NamedArguments[i].MemberName);
                Assert.AreEqual(namesAndValues[2 * i + 1], attribute.NamedArguments[i].TypedValue.Value);
            }
        }

        // Negative control for the BeforeFieldInit check: an explicit static constructor
        // suppresses the flag, which is how the pre-fix SaveDataSettings compiled.
        private static class ExplicitStaticConstructorControl
        {
            internal static readonly object Value;
            static ExplicitStaticConstructorControl() { Value = new object(); }
        }

        private static class FieldInitializerControl
        {
            internal static readonly object Value = new object();
        }

        [Test]
        public void NestedTypesKeepOriginalVisibilityAndSerializableFlags()
        {
            // Original TypeDef flags: Boss 0x020001d6/0x020001d7, CameraYAxisProxyManager 0x02000267,
            // TimeScaledComponent_SDT 0x020007fd, PromptTrigger 0x0200098c, ActorState 0x0200005e.
            Assert.AreEqual(0x00102003, (int)Nested(typeof(Boss), "BossBodyComponents").Attributes);
            Assert.AreEqual(0x00102003, (int)Nested(typeof(Boss), "BossGeneralEventTriggers").Attributes);
            Assert.AreEqual(0x0010210a, (int)Nested(typeof(CameraYAxisProxyManager), "TransformStackLayerDefinition").Attributes);
            Assert.AreEqual(0x00000103, (int)Nested(typeof(TimeScaledComponent_SDT), "SynchroniseType").Attributes);
            Assert.AreEqual(0x00102002, (int)Nested(typeof(PromptTrigger), "PromptTriggerChoiceAction").Attributes);

            Type args = Nested(typeof(ActorState), "BaseJSONCtorArgs");
            Assert.AreEqual(0x00102004, (int)args.Attributes);
            // Field 0x0400025c carries GraphUnityObject(typeof(ActorAnimationDefinition)).
            FieldInfo field = args.GetField("StateAnimationDefinitionName", Declared);
            CustomAttributeData graph = field.GetCustomAttributesData().Single();
            Assert.AreEqual(typeof(GraphUnityObjectAttribute), graph.AttributeType);
            CollectionAssert.AreEqual(new[] { typeof(Type) }, graph.Constructor.GetParameters().Select(p => p.ParameterType).ToArray());
            Assert.AreEqual(typeof(ActorAnimationDefinition), graph.ConstructorArguments.Single().Value);
            Assert.AreEqual(0, graph.NamedArguments.Count);
        }

        [Test]
        public void OutroSequenceDreamOrbBeginStaysPrivate()
        {
            // Original 0x06003cb8 flags 0x0081 Private|HideBySig.
            MethodInfo begin = Method(typeof(OutroSequence), "BeginSequence", typeof(IEffectData), typeof(EffectSequence));
            Assert.AreEqual(MethodAttributes.Private | MethodAttributes.HideBySig, begin.Attributes);
        }

        [Test]
        public void SaveDataSettingsKeepsReadOnlyEditableListAndBeforeFieldInit()
        {
            Type owner = typeof(SaveDataSettings);
            // Original get_EditableUIComponents 0x06002cec returns IReadOnlyList; a List result fails here.
            PropertyInfo editable = owner.GetProperty("EditableUIComponents", Declared);
            Assert.AreEqual(typeof(IReadOnlyList<SaveDataSettingsEditableUIComponent>), editable.PropertyType);
            Assert.AreEqual(typeof(IReadOnlyList<SaveDataSettingsEditableUIComponent>), editable.GetGetMethod().ReturnType);
            Assert.IsNull(editable.GetSetMethod(true));

            // Original TypeDef 0x020007d2 flags 0x00102001 include BeforeFieldInit; the .cctor
            // (0x06002cfc) still exists because the dictionary is a static field initializer.
            Assert.AreEqual(0x00102001, (int)owner.Attributes);
            Assert.IsNotNull(owner.TypeInitializer);

            // Negative control: the check above separates the two C# spellings.
            Assert.AreEqual((TypeAttributes)0, typeof(ExplicitStaticConstructorControl).Attributes & TypeAttributes.BeforeFieldInit);
            Assert.AreEqual(TypeAttributes.BeforeFieldInit, typeof(FieldInitializerControl).Attributes & TypeAttributes.BeforeFieldInit);
        }

        [Test]
        public void DevelopmentOnlyMethodsKeepConditionalAttribute()
        {
            // Original 0x06002c22, 0x060035d0 and 0x060035d1: ConditionalAttribute("BUILD_DEVELOPMENT").
            SingleStringAttribute(Method(typeof(SaveDataGame), "RemoveUnlockedOrnamentGuid", typeof(string)), typeof(ConditionalAttribute), "BUILD_DEVELOPMENT");
            SingleStringAttribute(Method(typeof(FullscreenShaderManager), "SetupDebugMenu"), typeof(ConditionalAttribute), "BUILD_DEVELOPMENT");
            SingleStringAttribute(Method(typeof(FullscreenShaderManager), "RemoveDebugMenu"), typeof(ConditionalAttribute), "BUILD_DEVELOPMENT");
        }

        [Test]
        public void GameRuntimeOptionalParametersKeepOriginalDefaults()
        {
            // Original 0x06000387: audioSources = null, slotIndex = -1, slotCount = 0.
            ParameterInfo[] playOneShot = Method(typeof(ActorAudio), "PlayOneShot",
                typeof(AudioClip), typeof(AudioSource), typeof(List<AudioSource>), typeof(int), typeof(int)).GetParameters();
            Assert.AreEqual(ParameterAttributes.None, playOneShot[0].Attributes);
            Assert.AreEqual(ParameterAttributes.None, playOneShot[1].Attributes);
            NullDefault(playOneShot[2]);
            IntDefault(playOneShot[3], -1);
            IntDefault(playOneShot[4], 0);

            // Original 0x060008f6 and 0x060008f7: onComplete = null.
            Type loaded = typeof(Action<HLAudioClipHandler.LoadedAudioParameters>);
            NullDefault(Method(typeof(HLAudioClipHandler), "LoadAudioClip", typeof(HLMusicClipDefinition), loaded).GetParameters()[1]);
            NullDefault(Method(typeof(HLAudioClipHandler), "LoadAudioClip", typeof(HLSfxClipDefinition), loaded).GetParameters()[1]);

            // Original 0x0600208a and 0x0600208b: `in` data keeps In + IsReadOnly; onComplete = null.
            Type inData = typeof(IEffectData).MakeByRefType();
            foreach (string name in new[] { "ValidateAndBegin", "BeginSequence" })
            {
                ParameterInfo[] parameters = Method(typeof(EffectSequence), name, inData, typeof(Action)).GetParameters();
                Assert.AreEqual(ParameterAttributes.In, parameters[0].Attributes, name);
                // Stored row: IsReadOnlyAttribute (blob 01000000). Mono also projects the In
                // flag (0x0001) as an InAttribute pseudoattribute, so compare as a set.
                CollectionAssert.AreEquivalent(
                    new[] { "System.Runtime.CompilerServices.IsReadOnlyAttribute", "System.Runtime.InteropServices.InAttribute" },
                    parameters[0].GetCustomAttributesData().Select(a => a.AttributeType.FullName).ToArray(), name);
                NullDefault(parameters[1]);
            }

            // Original 0x06003e34 and 0x06003e38: homingAttackAbility = null.
            NullDefault(Method(typeof(HomingTarget), "DoOnCharacterHit", typeof(IHomingAttack)).GetParameters()[0]);
            ParameterInfo[] onHit = Method(typeof(HomingTarget), "OnCharacterHit", typeof(Character), typeof(bool), typeof(IHomingAttack)).GetParameters();
            Assert.AreEqual(ParameterAttributes.None, onHit[0].Attributes);
            Assert.AreEqual(ParameterAttributes.None, onHit[1].Attributes);
            NullDefault(onHit[2]);
        }

        [Test]
        public void UnityUiOptionalParametersKeepOriginalNullDefaults()
        {
            // Original 0x0600039b/0x0600039c/0x0600039d and 0x060002b2: raw default dataIndex -1 (null).
            NullDefault(Method(typeof(UISelectable), "Add", typeof(UISelectable), typeof(string)).GetParameters()[1]);
            NullDefault(Method(typeof(UISelectable), "Remove", typeof(UISelectable), typeof(string)).GetParameters()[1]);
            NullDefault(Method(typeof(UISelectable), "GetSelectables", typeof(string)).GetParameters()[0]);
            NullDefault(Method(typeof(ViewBase), "Setup", typeof(ViewParameters)).GetParameters()[0]);
            // Negative control: the abstract OnSetup 0x060002b3 keeps a required parameter (no param flags, no default).
            ParameterInfo onSetup = Method(typeof(ViewBase), "OnSetup", typeof(ViewParameters)).GetParameters().Single();
            Assert.AreEqual(ParameterAttributes.None, onSetup.Attributes);
            Assert.IsFalse(onSetup.HasDefaultValue);
        }

        [Test]
        public void ProfilerAndLocalisationMenusKeepOriginalAttributeArguments()
        {
            // Original 0x060001b5/0x060001b6 and 0x060001f0/0x060001f1: ContextMenu(string).
            SingleStringAttribute(Method(typeof(HLProfilerConsole), "Action_ProfilerShow"), typeof(ContextMenu), "Show Profiler");
            SingleStringAttribute(Method(typeof(HLProfilerConsole), "Action_ProfilerHide"), typeof(ContextMenu), "Hide Profiler");
            SingleStringAttribute(Method(typeof(HLProfilerDataManager), "Action_ProfilerLogActiveDataSnapshot"), typeof(ContextMenu), "Log Profiler Active Data Snapshot");
            SingleStringAttribute(Method(typeof(HLProfilerDataManager), "Action_ProfilerLogDataSnapshot"), typeof(ContextMenu), "Log Profiler All Data Snapshot");

            // Original CreateAssetMenu blobs; a bare [CreateAssetMenu] has no named arguments and fails.
            CreateAssetMenuNamedArguments(typeof(HLProfilerGroupDefinition),
                "fileName", "ProfilerGroup_",
                "menuName", "Hardlight/HLUnityCore/Profiler/Group Definition");
            CreateAssetMenuNamedArguments(typeof(FontAssetLanguageFallbackData),
                "fileName", "FontAssetLanguageFallbackData",
                "menuName", "Hardlight/Localisation/FontAsset Language Fallback Data",
                "order", 0);
        }
    }
}
