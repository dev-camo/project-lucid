using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Hardlight;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Editor
{
    public sealed class OriginalUISoundEffectManagerTests
    {
        private const float OriginalBasePitch = 1.05946f;
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
                                               BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        [Test]
        public void OriginalGlobalSoundManagerRetainsCompleteDeclarations()
        {
            Type type = typeof(UISoundEffectManager);
            Assert.That(type.FullName, Is.EqualTo("UISoundEffectManager"));
            Assert.That(type.Namespace, Is.Null);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.Attributes, Is.EqualTo((TypeAttributes)1048577));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoSingleton<UISoundEffectManager>)));
            Assert.That(type.IsGenericType, Is.False);
            Assert.That(type.GetNestedTypes(Declared), Is.Empty);
            Assert.That(type.GetProperties(Declared), Is.Empty);
            Assert.That(type.GetEvents(Declared), Is.Empty);
            Assert.That(type.GetInterfaces(), Is.EqualTo(new[] { typeof(ISystem) }));

            var attributes = type.GetCustomAttributesData();
            Assert.That(attributes.Count, Is.EqualTo(2));
            RequireOption(attributes[0], Option.ArrayBoundsChecks);
            RequireOption(attributes[1], Option.NullChecks);

            FieldInfo[] fields = type.GetFields(Declared).OrderBy(field => field.MetadataToken).ToArray();
            Assert.That(fields.Select(field => field.Name), Is.EqualTo(new[] { "basePitch", "m_audioSource" }));
            Assert.That(fields[0].FieldType, Is.EqualTo(typeof(float)));
            Assert.That(fields[0].Attributes, Is.EqualTo((FieldAttributes)32849));
            Assert.That(fields[0].GetRawConstantValue(), Is.EqualTo(OriginalBasePitch));
            Assert.That(fields[0].GetCustomAttributesData(), Is.Empty);
            Assert.That(fields[1].FieldType, Is.EqualTo(typeof(AudioSource)));
            Assert.That(fields[1].Attributes, Is.EqualTo(FieldAttributes.Private));
            var serialized = fields[1].GetCustomAttributesData();
            Assert.That(serialized.Count, Is.EqualTo(1));
            RequireParameterlessAttribute(serialized[0], typeof(SerializeField));

            ConstructorInfo[] constructors = type.GetConstructors(Declared);
            Assert.That(constructors.Length, Is.EqualTo(1));
            Assert.That(constructors[0].Attributes, Is.EqualTo((MethodAttributes)6278));
            Assert.That(constructors[0].GetParameters(), Is.Empty);
            Assert.That(constructors[0].GetCustomAttributesData(), Is.Empty);
            Assert.That(constructors[0].GetMethodImplementationFlags(), Is.EqualTo(MethodImplAttributes.IL));

            MethodInfo[] methods = type.GetMethods(Declared).OrderBy(method => method.MetadataToken).ToArray();
            Assert.That(methods.Select(method => method.Name), Is.EqualTo(new[] { "PlayAudioClip", "PlayAudioClipWithSemitoneOffset" }));
            RequireMethod(methods[0], new[] { typeof(AudioClip), typeof(float) }, new[] { "clip", "volume" });
            RequireMethod(methods[1], new[] { typeof(AudioClip), typeof(int), typeof(float) }, new[] { "clip", "semitoneChange", "volume" });
        }

        [Test]
        public void OriginalInactiveSoundManagerWithNullSourceSuppressesPlaybackRoutes()
        {
            UISoundEffectManager originalInstance = UISoundEffectManager.Instance;
            GameObject managerObject = null;
            try
            {
                managerObject = new GameObject("Owned original null-source UI sound manager");
                managerObject.SetActive(false);
                UISoundEffectManager manager = managerObject.AddComponent<UISoundEffectManager>();
                Assert.That(managerObject.activeInHierarchy, Is.False);
                Assert.That(ReferenceEquals(UISoundEffectManager.Instance, originalInstance), Is.True);
                using (var serialized = new SerializedObject(manager))
                {
                    SerializedProperty source = serialized.FindProperty("m_audioSource");
                    Assert.That(source, Is.Not.Null);
                    Assert.That(source.propertyType, Is.EqualTo(SerializedPropertyType.ObjectReference));
                    Assert.That(source.objectReferenceValue, Is.Null);

                    // Null-source suppression is exercised through public methods on a real inactive component.
                    // It does not establish native fault, audio rendering, or active singleton lifecycle parity.
                    float[] volumes = { float.NegativeInfinity, -2f, 0f, float.NaN, 2f, float.PositiveInfinity };
                    foreach (float volume in volumes)
                    {
                        manager.PlayAudioClip(null, volume);
                        manager.PlayAudioClipWithSemitoneOffset(null, int.MinValue, volume);
                        manager.PlayAudioClipWithSemitoneOffset(null, int.MaxValue, volume);
                    }
                    manager.PlayAudioClip(null);
                    manager.PlayAudioClipWithSemitoneOffset(null, 0);
                    Assert.That(source.objectReferenceValue, Is.Null);
                    Assert.That(ReferenceEquals(UISoundEffectManager.Instance, originalInstance), Is.True);
                    LogAssert.NoUnexpectedReceived();
                }
            }
            finally
            {
                try
                {
                    if (managerObject != null) Object.DestroyImmediate(managerObject);
                }
                finally
                {
                    Assert.That(ReferenceEquals(UISoundEffectManager.Instance, originalInstance), Is.True);
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OriginalSoundManagerForwardsPitchVolumeToOwnedMutedAudioSource()
        {
            UISoundEffectManager originalInstance = UISoundEffectManager.Instance;
            GameObject managerObject = null;
            GameObject audioObject = null;
            AudioSource audio = null;
            AudioClip clip = null;
            try
            {
                managerObject = new GameObject("Owned inactive original UI sound manager");
                managerObject.SetActive(false);
                UISoundEffectManager manager = managerObject.AddComponent<UISoundEffectManager>();
                Assert.That(managerObject.activeInHierarchy, Is.False);
                Assert.That(ReferenceEquals(UISoundEffectManager.Instance, originalInstance), Is.True);

                audioObject = new GameObject("Owned muted UI sound source");
                audio = audioObject.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.mute = true;
                Assert.That(audioObject.activeInHierarchy, Is.True);
                Assert.That(audio.enabled, Is.True);
                Assert.That(audio.mute, Is.True);
                clip = AudioClip.Create("Owned silent UI sound clip", 64, 1, 8000, false);
                Assert.That(clip, Is.Not.Null);
                Assert.That(clip.SetData(new float[64], 0), Is.True);

                using (var serialized = new SerializedObject(manager))
                {
                    SerializedProperty source = serialized.FindProperty("m_audioSource");
                    Assert.That(source, Is.Not.Null);
                    Assert.That(source.propertyType, Is.EqualTo(SerializedPropertyType.ObjectReference));
                    Assert.That(source.objectReferenceValue, Is.Null);
                    source.objectReferenceValue = audio;
                    Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
                    serialized.Update();
                    Assert.That(source.objectReferenceValue, Is.SameAs(audio));
                }

                // These observations cover rebuilt provider properties and public calls only.
                // The source is muted, and no audible rendering or original libm result is asserted.
                manager.PlayAudioClip(clip, 0.375f);
                Assert.That(audio.pitch, Is.EqualTo(OriginalBasePitch).Within(0.00001f));
                Assert.That(audio.volume, Is.EqualTo(0.375f).Within(0.000001f));
                manager.PlayAudioClip(clip);
                Assert.That(audio.pitch, Is.EqualTo(OriginalBasePitch).Within(0.00001f));
                Assert.That(audio.volume, Is.EqualTo(1f).Within(0.000001f));

                int[] semitones = { 12, 0, -12 };
                float[] volumes = { 0.25f, 0.5f, 0.75f };
                for (int index = 0; index < semitones.Length; index++)
                {
                    manager.PlayAudioClipWithSemitoneOffset(clip, semitones[index], volumes[index]);
                    float expected = Mathf.Pow(OriginalBasePitch, (float)semitones[index]);
                    Assert.That(audio.pitch, Is.EqualTo(expected).Within(0.00001f));
                    Assert.That(audio.volume, Is.EqualTo(volumes[index]).Within(0.000001f));
                    if (semitones[index] > 0) Assert.That(audio.pitch, Is.GreaterThan(OriginalBasePitch));
                    if (semitones[index] < 0) Assert.That(audio.pitch, Is.LessThan(OriginalBasePitch));
                }
                manager.PlayAudioClipWithSemitoneOffset(clip, 0);
                Assert.That(audio.pitch, Is.EqualTo(1f).Within(0.00001f));
                Assert.That(audio.volume, Is.EqualTo(1f).Within(0.000001f));
                Assert.That(audio.mute, Is.True);
                Assert.That(ReferenceEquals(UISoundEffectManager.Instance, originalInstance), Is.True);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                try
                {
                    if (audio != null) audio.Stop();
                }
                finally
                {
                    try
                    {
                        if (audioObject != null) Object.DestroyImmediate(audioObject);
                    }
                    finally
                    {
                        try
                        {
                            if (clip != null) Object.DestroyImmediate(clip);
                        }
                        finally
                        {
                            try
                            {
                                if (managerObject != null) Object.DestroyImmediate(managerObject);
                            }
                            finally
                            {
                                Assert.That(ReferenceEquals(UISoundEffectManager.Instance, originalInstance), Is.True);
                            }
                        }
                    }
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static void RequireMethod(MethodInfo method, Type[] types, string[] names)
        {
            Assert.That(method.Attributes, Is.EqualTo((MethodAttributes)134));
            Assert.That(method.GetMethodImplementationFlags(), Is.EqualTo(MethodImplAttributes.IL));
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(method.ReturnParameter.GetCustomAttributesData(), Is.Empty);
            Assert.That(method.GetCustomAttributesData(), Is.Empty);
            Assert.That(method.IsGenericMethod, Is.False);
            ParameterInfo[] parameters = method.GetParameters();
            Assert.That(parameters.Select(parameter => parameter.ParameterType), Is.EqualTo(types));
            Assert.That(parameters.Select(parameter => parameter.Name), Is.EqualTo(names));
            for (int index = 0; index < parameters.Length; index++)
            {
                ParameterInfo parameter = parameters[index];
                bool optional = index == parameters.Length - 1;
                Assert.That(parameter.Attributes, Is.EqualTo(optional ? ParameterAttributes.Optional | ParameterAttributes.HasDefault : ParameterAttributes.None));
                Assert.That(parameter.HasDefaultValue, Is.EqualTo(optional));
                var attributes = parameter.GetCustomAttributesData();
                if (optional)
                {
                    Assert.That(parameter.RawDefaultValue, Is.EqualTo(1f));
                    // Mono exposes the precise flag-derived OptionalAttribute separately from stored source annotations.
                    Assert.That(attributes.Count, Is.EqualTo(1));
                    RequireParameterlessAttribute(attributes[0], typeof(OptionalAttribute));
                }
                else Assert.That(attributes, Is.Empty);
            }
        }

        private static void RequireOption(CustomAttributeData attribute, Option option)
        {
            Assert.That(attribute.AttributeType, Is.EqualTo(typeof(Il2CppSetOptionAttribute)));
            Assert.That(attribute.Constructor.GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(Option), typeof(object) }));
            Assert.That(attribute.ConstructorArguments.Count, Is.EqualTo(2));
            Assert.That(attribute.ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(Option)));
            Assert.That(attribute.ConstructorArguments[0].Value, Is.EqualTo((int)option));
            Assert.That(attribute.ConstructorArguments[1].ArgumentType, Is.EqualTo(typeof(bool)));
            Assert.That(attribute.ConstructorArguments[1].Value, Is.EqualTo(false));
            Assert.That(attribute.NamedArguments, Is.Empty);
        }

        private static void RequireParameterlessAttribute(CustomAttributeData attribute, Type type)
        {
            Assert.That(attribute.AttributeType, Is.EqualTo(type));
            Assert.That(attribute.Constructor.GetParameters(), Is.Empty);
            Assert.That(attribute.ConstructorArguments, Is.Empty);
            Assert.That(attribute.NamedArguments, Is.Empty);
        }
    }
}
