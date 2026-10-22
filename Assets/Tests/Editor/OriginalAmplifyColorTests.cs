using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Editor
{
    // The shipped Amplify Color types of Assembly-CSharp (1.10.1, Amplify Color 1.8.2). Test assemblies
    // cannot reference the predefined Assembly-CSharp, so types are reached by assembly-qualified name.
    // Every behaviour component lives on an inactive GameObject so Unity never runs OnEnable (which
    // needs the shaders and a graphics device); the methods under test are called directly.
    public sealed class OriginalAmplifyColorTests
    {
        private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        [TearDown]
        public void DestroyCreatedObjects()
        {
            foreach (UnityEngine.Object item in created)
                UnityEngine.Object.DestroyImmediate(item);
            created.Clear();
        }

        // Upstream members inside #if UNITY_EDITOR: they exist where these tests run but were compiled out
        // of the shipped player, so the shipped declaration table cannot list them.
        private static readonly Dictionary<string, string[]> EditorOnly = new Dictionary<string, string[]>
        {
            { "AmplifyColorRenderMaskBase", new[] { "OnGUI" } },
            { "AmplifyColorVolumeBase", new[] { "Window", "OnEnable", "OnDestroy" } },
        };

        private static bool Shipped(string type, MemberInfo member) =>
            !EditorOnly.TryGetValue(type, out string[] names) || !names.Contains(member.Name);

        private static Type Global(string name) => Type.GetType(name + ", Assembly-CSharp", true);

        private static Type Namespaced(string name) => Type.GetType("AmplifyColor." + name + ", Assembly-CSharp", true);

        private GameObject Inactive(string name)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            created.Add(go);
            return go;
        }

        private static object Get(object target, string field)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo info = type.GetField(field, Declared);
                if (info != null)
                    return info.GetValue(target);
            }
            throw new MissingFieldException(target.GetType().FullName, field);
        }

        private static void Set(object target, string field, object value)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo info = type.GetField(field, Declared);
                if (info != null)
                {
                    info.SetValue(target, value);
                    return;
                }
            }
            throw new MissingFieldException(target.GetType().FullName, field);
        }

        private static object Call(object target, string method, params object[] args)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                MethodInfo info = type.GetMethod(method, Declared & ~BindingFlags.Static);
                if (info != null)
                    return info.Invoke(target, args);
            }
            throw new MissingMethodException(target.GetType().FullName, method);
        }

        private static string Flags(int value) => value.ToString("x");

        [Test]
        public void ShippedTypeListMatches()
        {
            Type[] found = Global("AmplifyColorBase").Assembly.GetTypes()
                .Where(type => type.Namespace == "AmplifyColor" || (type.Namespace == null && type.Name.StartsWith("AmplifyColor", StringComparison.Ordinal)))
                .ToArray();
            Assert.That(found.Select(type => type.FullName).OrderBy(name => name, StringComparer.Ordinal),
                Is.EqualTo(OriginalAmplifyColorShippedTypes.All.Select(type => type.Name).OrderBy(name => name, StringComparer.Ordinal)));
        }

        [Test]
        public void DeclarationsMatchTheShippedAssembly()
        {
            foreach (ShippedType shipped in OriginalAmplifyColorShippedTypes.All)
            {
                Type type = Type.GetType(shipped.Name + ", Assembly-CSharp", true);
                Assert.That(Flags((int)type.Attributes), Is.EqualTo(Flags(shipped.Flags)), shipped.Name + " type flags");
                Assert.That(type.BaseType.FullName, Is.EqualTo(shipped.Base), shipped.Name + " base type");
                Assert.That(type.GetFields(Declared).Where(field => Shipped(shipped.Name, field)).OrderBy(field => field.MetadataToken)
                    .Select(field => field.Name + ":" + Flags((int)field.Attributes)),
                    Is.EqualTo(shipped.Fields), shipped.Name + " fields in metadata order");
                IEnumerable<MethodBase> methods = type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared))
                    .Where(method => Shipped(shipped.Name, method));
                Assert.That(methods.OrderBy(method => method.MetadataToken).Select(method => method.Name + ":" + Flags((int)method.Attributes)),
                    Is.EqualTo(shipped.Methods), shipped.Name + " methods in metadata order");
            }
        }

        [Test]
        public void EditorOnlyMembersExistHereAndAreAbsentFromTheShippedTable()
        {
            foreach (KeyValuePair<string, string[]> pair in EditorOnly)
            {
                ShippedType shipped = OriginalAmplifyColorShippedTypes.All.Single(type => type.Name == pair.Key);
                Type type = Global(pair.Key);
                foreach (string name in pair.Value)
                {
                    Assert.That(type.GetMember(name, Declared), Is.Not.Empty, pair.Key + "." + name + " is compiled in the Editor");
                    Assert.That(shipped.Fields.Concat(shipped.Methods).Any(entry => entry.StartsWith(name + ":", StringComparison.Ordinal)), Is.False,
                        pair.Key + "." + name + " is not in the shipped table");
                }
            }
        }

        [Test]
        public void BaseTypesAndSealingMatchTheShippedHierarchy()
        {
            Type effect = Global("AmplifyColorEffect");
            Assert.That(effect.BaseType, Is.SameAs(Global("AmplifyColorBase")));
            Assert.That(effect.IsSealed, Is.True);
            Assert.That(Global("AmplifyColorBase").BaseType, Is.SameAs(typeof(MonoBehaviour)));
            Assert.That(Global("AmplifyColorBase").IsSealed, Is.False);
            Assert.That(Global("AmplifyColorBase").IsAbstract, Is.False);
            Assert.That(Global("AmplifyColorRenderMask").BaseType, Is.SameAs(Global("AmplifyColorRenderMaskBase")));
            Assert.That(Global("AmplifyColorRenderMaskBase").BaseType, Is.SameAs(typeof(MonoBehaviour)));
            Assert.That(Global("AmplifyColorTriggerProxy").BaseType, Is.SameAs(Global("AmplifyColorTriggerProxyBase")));
            Assert.That(Global("AmplifyColorTriggerProxy2D").BaseType, Is.SameAs(Global("AmplifyColorTriggerProxyBase")));
            Assert.That(Global("AmplifyColorVolume").BaseType, Is.SameAs(Global("AmplifyColorVolumeBase")));
            Assert.That(Global("AmplifyColorVolume2D").BaseType, Is.SameAs(Global("AmplifyColorVolumeBase")));
            Assert.That(Global("AmplifyColorTriggerProxyBase").GetField("OwnerEffect").FieldType, Is.SameAs(Global("AmplifyColorBase")));
            Assert.That(Namespaced("VolumeEffect").GetField("gameObject").FieldType, Is.SameAs(Global("AmplifyColorBase")));
            Assert.That(Global("AmplifyColorRenderMaskBase").GetField("colorEffect", Declared).FieldType, Is.SameAs(Global("AmplifyColorBase")));
        }

        private static string[] Attributes(Type type) =>
            type.GetCustomAttributesData().Select(data => data.AttributeType.FullName + "(" +
                string.Join(",", data.ConstructorArguments.Select(arg => arg.Value is Type t ? t.FullName : "\"" + arg.Value + "\"")) + ")")
                .OrderBy(text => text, StringComparer.Ordinal).ToArray();

        [Test]
        public void ComponentAttributesMatchTheShippedMetadata()
        {
            Assert.That(Attributes(Global("AmplifyColorBase")), Is.EqualTo(new[] { "UnityEngine.AddComponentMenu(\"\")" }));
            Assert.That(Attributes(Global("AmplifyColorEffect")), Is.EqualTo(new[]
            {
                "UnityEngine.AddComponentMenu(\"Image Effects/Amplify Color\")",
                "UnityEngine.ExecuteInEditMode()",
                "UnityEngine.ImageEffectAllowedInSceneView()",
                "UnityEngine.ImageEffectTransformsToLDR()",
            }));
            Assert.That(Attributes(Global("AmplifyColorRenderMask")), Is.EqualTo(new[]
            {
                "UnityEngine.AddComponentMenu(\"Image Effects/Amplify Color Render Mask\")",
                "UnityEngine.ExecuteInEditMode()",
                "UnityEngine.RequireComponent(AmplifyColorEffect)",
                "UnityEngine.RequireComponent(UnityEngine.Camera)",
            }));
            Assert.That(Attributes(Global("AmplifyColorRenderMaskBase")), Is.EqualTo(new[]
            {
                "UnityEngine.AddComponentMenu(\"\")",
                "UnityEngine.ExecuteInEditMode()",
                "UnityEngine.RequireComponent(UnityEngine.Camera)",
            }));
            Assert.That(Attributes(Global("AmplifyColorTriggerProxy")), Is.EqualTo(new[]
            {
                "UnityEngine.AddComponentMenu(\"\")",
                "UnityEngine.RequireComponent(UnityEngine.Rigidbody)",
                "UnityEngine.RequireComponent(UnityEngine.SphereCollider)",
            }));
            Assert.That(Attributes(Global("AmplifyColorTriggerProxy2D")), Is.EqualTo(new[]
            {
                "UnityEngine.AddComponentMenu(\"\")",
                "UnityEngine.RequireComponent(UnityEngine.CircleCollider2D)",
                "UnityEngine.RequireComponent(UnityEngine.Rigidbody2D)",
            }));
            Assert.That(Attributes(Global("AmplifyColorTriggerProxyBase")), Is.EqualTo(new[] { "UnityEngine.AddComponentMenu(\"\")" }));
            Assert.That(Attributes(Global("AmplifyColorVolume")), Is.EqualTo(new[]
            {
                "UnityEngine.AddComponentMenu(\"Image Effects/Amplify Color Volume\")",
                "UnityEngine.RequireComponent(UnityEngine.BoxCollider)",
            }));
            Assert.That(Attributes(Global("AmplifyColorVolume2D")), Is.EqualTo(new[]
            {
                "UnityEngine.AddComponentMenu(\"Image Effects/Amplify Color Volume 2D\")",
                "UnityEngine.RequireComponent(UnityEngine.BoxCollider2D)",
            }));
            Assert.That(Attributes(Global("AmplifyColorVolumeBase")), Is.EqualTo(new[]
            {
                "UnityEngine.AddComponentMenu(\"\")",
                "UnityEngine.ExecuteInEditMode()",
            }));
        }

        [Test]
        public void SerializedFieldAttributesAndConstantsMatchTheShippedMetadata()
        {
            Type baseType = Global("AmplifyColorBase");
            Assert.That(Attributes(baseType.GetField("ApplyDithering")), Is.EqualTo(new[] { "UnityEngine.Serialization.FormerlySerializedAsAttribute(\"UseDithering\")" }));
            Assert.That(Attributes(baseType.GetField("EffectFlags")), Is.EqualTo(new[] { "UnityEngine.HideInInspector()" }));
            Assert.That(Attributes(baseType.GetField("sharedInstanceID", Declared)), Is.EqualTo(new[] { "UnityEngine.HideInInspector()", "UnityEngine.SerializeField()" }));
            Assert.That(Attributes(Global("AmplifyColorVolumeBase").GetField("EffectContainer")), Is.EqualTo(new[] { "UnityEngine.HideInInspector()" }));
            Assert.That(Attributes(Global("AmplifyColorRenderMaskBase").GetField("ClearColor")), Is.EqualTo(new[] { "UnityEngine.Serialization.FormerlySerializedAsAttribute(\"clearColor\")" }));
            Assert.That(Attributes(Global("AmplifyColorRenderMaskBase").GetField("RenderLayers")), Is.EqualTo(new[] { "UnityEngine.Serialization.FormerlySerializedAsAttribute(\"renderLayers\")" }));
            Assert.That(Attributes(Global("AmplifyColorRenderMaskBase").GetField("DebugMask")), Is.EqualTo(new[] { "UnityEngine.Serialization.FormerlySerializedAsAttribute(\"debug\")" }));

            Assert.That(baseType.GetField("LutSize").GetRawConstantValue(), Is.EqualTo(32));
            Assert.That(baseType.GetField("LutWidth").GetRawConstantValue(), Is.EqualTo(1024));
            Assert.That(baseType.GetField("LutHeight").GetRawConstantValue(), Is.EqualTo(32));
            Assert.That(baseType.GetField("DepthCurveLutRange", Declared).GetRawConstantValue(), Is.EqualTo(1024));
            Type version = Namespaced("VersionInfo");
            Assert.That(version.GetField("Major").GetRawConstantValue(), Is.EqualTo((byte)1));
            Assert.That(version.GetField("Minor").GetRawConstantValue(), Is.EqualTo((byte)8));
            Assert.That(version.GetField("Release").GetRawConstantValue(), Is.EqualTo((byte)2));
        }

        private static string[] Attributes(FieldInfo field) =>
            field.GetCustomAttributesData().Select(data => data.AttributeType.FullName + "(" +
                string.Join(",", data.ConstructorArguments.Select(arg => "\"" + arg.Value + "\"")) + ")")
                .OrderBy(text => text, StringComparer.Ordinal).ToArray();

        // Enum values are compared as integers: Enum.GetValues returns the enum type, so each element is converted.
        private static string[] NamedIntegers(Type enumType) =>
            Enum.GetValues(enumType).Cast<object>().Select(value => Enum.GetName(enumType, value) + "=" + Convert.ToInt32(value)).ToArray();

        [Test]
        public void EnumValuesMatchTheShippedLiterals()
        {
            Assert.That(Enum.GetUnderlyingType(Namespaced("Quality")), Is.SameAs(typeof(int)));
            Assert.That(NamedIntegers(Namespaced("Quality")), Is.EqualTo(new[] { "Mobile=0", "Standard=1" }));
            Assert.That(Enum.GetUnderlyingType(Namespaced("Tonemapping")), Is.SameAs(typeof(int)));
            Assert.That(NamedIntegers(Namespaced("Tonemapping")), Is.EqualTo(new[] { "Disabled=0", "Photographic=1", "FilmicHable=2", "FilmicACES=3" }));
        }

        [Test]
        public void ConstructorDefaultsMatchTheShippedConstructor()
        {
            object effect = Inactive("Lucid AmplifyColor defaults").AddComponent(Global("AmplifyColorEffect"));
            Assert.That(Convert.ToInt32(Get(effect, "Tonemapper")), Is.EqualTo(0));
            Assert.That(Get(effect, "Exposure"), Is.EqualTo(1f));
            Assert.That(Get(effect, "LinearWhitePoint"), Is.EqualTo(11.2f));
            Assert.That(Get(effect, "ApplyDithering"), Is.False);
            Assert.That(Convert.ToInt32(Get(effect, "QualityLevel")), Is.EqualTo(1));
            Assert.That(Get(effect, "BlendAmount"), Is.EqualTo(0f));
            Assert.That(Get(effect, "LutTexture"), Is.Null);
            Assert.That(Get(effect, "LutBlendTexture"), Is.Null);
            Assert.That(Get(effect, "MaskTexture"), Is.Null);
            Assert.That(Get(effect, "UseDepthMask"), Is.False);
            AnimationCurve curve = (AnimationCurve)Get(effect, "DepthMaskCurve");
            Assert.That(curve.keys.Select(key => (key.time, key.value)), Is.EqualTo(new[] { (0f, 1f), (1f, 1f) }));
            Assert.That(Get(effect, "UseVolumes"), Is.False);
            Assert.That(Get(effect, "ExitVolumeBlendTime"), Is.EqualTo(1f));
            Assert.That(((LayerMask)Get(effect, "VolumeCollisionMask")).value, Is.EqualTo(-1));
            Assert.That((int)(ColorSpace)Get(effect, "colorSpace"), Is.EqualTo(-1));
            Assert.That(Convert.ToInt32(Get(effect, "qualityLevel")), Is.EqualTo(1));
            Assert.That(((AnimationCurve)Get(effect, "prevDepthMaskCurve")).length, Is.EqualTo(0));
            Assert.That(Get(effect, "worldExposure"), Is.EqualTo(1f));
            Assert.That(Get(effect, "currentExposure"), Is.EqualTo(1f));
            Assert.That(Get(effect, "blendExposure"), Is.EqualTo(1f));
            Assert.That(Get(effect, "effectVolumesBlendAdjust"), Is.EqualTo(0f));
            Assert.That((IList)Get(effect, "enteredVolumes"), Is.Empty);
            object flags = Get(effect, "EffectFlags");
            Assert.That(flags, Is.Not.Null);
            Assert.That((IList)flags.GetType().GetField("components").GetValue(flags), Is.Empty);
            Assert.That(Get(effect, "sharedInstanceID"), Is.EqualTo(""));
            Assert.That(effect.GetType().GetProperty("SharedInstanceID").GetValue(effect), Is.EqualTo(""));
            Assert.That(Get(effect, "silentError"), Is.False);
        }

        [Test]
        public void NewSharedInstanceIdIsAFreshGuidString()
        {
            object effect = Inactive("Lucid AmplifyColor id").AddComponent(Global("AmplifyColorEffect"));
            Call(effect, "NewSharedInstanceID");
            string first = (string)Get(effect, "sharedInstanceID");
            Assert.That(Guid.TryParse(first, out _), Is.True);
            Assert.That(first, Is.EqualTo(Guid.Parse(first).ToString()));
            Call(effect, "NewSharedInstanceID");
            Assert.That((string)Get(effect, "sharedInstanceID"), Is.Not.EqualTo(first));
        }

        [Test]
        public void EnteringAVolumeInsertsItFirstAndIgnoresRepeats()
        {
            GameObject host = Inactive("Lucid AmplifyColor volumes");
            object effect = host.AddComponent(Global("AmplifyColorEffect"));
            Component first = host.AddComponent(Global("AmplifyColorVolumeBase"));
            Component second = Inactive("Lucid AmplifyColor second volume").AddComponent(Global("AmplifyColorVolumeBase"));
            IList entered = (IList)Get(effect, "enteredVolumes");

            Call(effect, "EnterVolume", first);
            Call(effect, "EnterVolume", second);
            Call(effect, "EnterVolume", first);
            Assert.That(entered.Cast<object>().ToArray(), Is.EqualTo(new object[] { second, first }));

            Call(effect, "ExitVolume", second);
            Call(effect, "ExitVolume", second);
            Assert.That(entered.Cast<object>().ToArray(), Is.EqualTo(new object[] { first }));
        }

        [Test]
        public void BlendToStartsAnAnimatedBlendTowardsTheTargetLut()
        {
            object effect = Inactive("Lucid AmplifyColor blend").AddComponent(Global("AmplifyColorEffect"));
            var lut = new Texture2D(1024, 32);
            var target = new Texture2D(1024, 32);
            created.Add(lut);
            created.Add(target);
            Set(effect, "LutTexture", lut);
            var property = effect.GetType().GetProperty("WillItBlend");
            Set(effect, "LutBlendTexture", target);
            Assert.That(property.GetValue(effect), Is.EqualTo(true));
            Set(effect, "LutBlendTexture", null);

            bool finished = false;
            Action onFinish = () => finished = true;
            effect.GetType().GetMethod("BlendTo").Invoke(effect, new object[] { target, 2.5f, onFinish });

            Assert.That(Get(effect, "LutBlendTexture"), Is.SameAs(target));
            Assert.That(Get(effect, "BlendAmount"), Is.EqualTo(0f));
            Assert.That(Get(effect, "blending"), Is.True);
            Assert.That(Get(effect, "blendingTime"), Is.EqualTo(2.5f));
            Assert.That(Get(effect, "blendingTimeCountdown"), Is.EqualTo(2.5f));
            Assert.That(Get(effect, "onFinishBlend"), Is.SameAs(onFinish));
            Assert.That(effect.GetType().GetProperty("IsBlending").GetValue(effect), Is.EqualTo(true));
            Assert.That(property.GetValue(effect), Is.EqualTo(false), "a running blend disables WillItBlend");
            Assert.That(finished, Is.False);
        }

        [Test]
        public void LutDimensionsMustBeSquareStripsAndResetAnisotropy()
        {
            MethodInfo validate = Global("AmplifyColorBase").GetMethod("ValidateLutDimensions");
            Assert.That(validate.Invoke(null, new object[] { null }), Is.EqualTo(true));

            var strip = new Texture2D(1024, 32, TextureFormat.RGB24, false, true) { anisoLevel = 4 };
            created.Add(strip);
            Assert.That(validate.Invoke(null, new object[] { strip }), Is.EqualTo(true));
            Assert.That(strip.anisoLevel, Is.EqualTo(0));

            var square = new Texture2D(32, 32, TextureFormat.RGB24, false, true) { name = "LucidSquareLut", anisoLevel = 4 };
            created.Add(square);
            LogAssert.Expect(LogType.Warning, "[AmplifyColor] Lut LucidSquareLut has invalid dimensions.");
            Assert.That(validate.Invoke(null, new object[] { square }), Is.EqualTo(false));
            Assert.That(square.anisoLevel, Is.EqualTo(4), "an invalid LUT keeps its anisotropy");
        }

        [TestCase(0, 0, false, 0, false, 18)]
        [TestCase(1, 1, false, 0, false, 1)]
        [TestCase(1, 1, true, 1, true, 15)]
        [TestCase(0, 0, true, 3, false, 23)]
        [TestCase(1, 0, true, 2, true, 8)]
        public void ShaderPassCombinesQualityColorSpaceHdrDitheringAndTonemapper(int quality, int colorSpace, bool hdr, int tonemapper, bool dithering, int expected)
        {
            GameObject host = Inactive("Lucid AmplifyColor pass");
            Camera camera = host.AddComponent<Camera>();
            camera.allowHDR = hdr;
            object effect = host.AddComponent(Global("AmplifyColorEffect"));
            Set(effect, "ownerCamera", camera);
            Set(effect, "QualityLevel", Enum.ToObject(Namespaced("Quality"), quality));
            Set(effect, "colorSpace", (ColorSpace)colorSpace);
            Set(effect, "Tonemapper", Enum.ToObject(Namespaced("Tonemapping"), tonemapper));
            Set(effect, "ApplyDithering", dithering);
            Assert.That(Call(effect, "ComputeShaderPass"), Is.EqualTo(expected));
        }

        [Test]
        public void ShaderPassNeedsTheOwnerCamera()
        {
            object effect = Inactive("Lucid AmplifyColor no camera").AddComponent(Global("AmplifyColorEffect"));
            var thrown = Assert.Throws<TargetInvocationException>(() => Call(effect, "ComputeShaderPass"));
            Assert.That(thrown.InnerException, Is.TypeOf<NullReferenceException>());
        }

        [Test]
        public void VersionInfoReportsOneEightTwo()
        {
            Type version = Namespaced("VersionInfo");
            Assert.That(version.GetMethod("StaticToString").Invoke(null, null), Is.EqualTo("1.8.2"));
            object current = version.GetMethod("Current").Invoke(null, null);
            Assert.That(current.ToString(), Is.EqualTo("1.8.2"));
            Assert.That(version.GetProperty("Number").GetValue(current), Is.EqualTo(182));
            Assert.That(version.GetMethod("Matches").Invoke(null, new[] { current }), Is.EqualTo(true));
            version.GetField("m_minor", Declared).SetValue(current, 9);
            Assert.That(version.GetMethod("Matches").Invoke(null, new[] { current }), Is.EqualTo(false));
            Assert.That(current.ToString(), Is.EqualTo("1.9.2"));
            Assert.That(version.GetProperty("FullNumber", BindingFlags.Static | BindingFlags.Public), Is.Null);
        }

        [Test]
        public void FieldTypesAreRecognisedByFullName()
        {
            Type field = Namespaced("VolumeEffectField");
            MethodInfo isValid = field.GetMethod("IsValidType");
            foreach (string valid in new[] { "System.Single", "System.Boolean", "UnityEngine.Color", "UnityEngine.Vector2", "UnityEngine.Vector3", "UnityEngine.Vector4" })
                Assert.That(isValid.Invoke(null, new object[] { valid }), Is.EqualTo(true), valid);
            foreach (string invalid in new[] { "System.Int32", "System.Double", "UnityEngine.Quaternion", "", "single" })
                Assert.That(isValid.Invoke(null, new object[] { invalid }), Is.EqualTo(false), invalid);

            object single = Activator.CreateInstance(field, "Exposure", "System.Single");
            field.GetMethod("UpdateValue").Invoke(single, new object[] { 2.5f });
            Assert.That(field.GetField("valueSingle").GetValue(single), Is.EqualTo(2.5f));
            object vector = Activator.CreateInstance(field, "Offset", "UnityEngine.Vector3");
            field.GetMethod("UpdateValue").Invoke(vector, new object[] { new Vector3(1, 2, 3) });
            Assert.That(field.GetField("valueVector3").GetValue(vector), Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(field.GetField("valueSingle").GetValue(vector), Is.EqualTo(0f), "only the field of the declared type is written");
        }

        [Test]
        public void ContainerFindsVolumeEffectsByReferenceThenBySharedInstanceId()
        {
            object effectA = Inactive("Lucid AmplifyColor A").AddComponent(Global("AmplifyColorEffect"));
            object effectB = Inactive("Lucid AmplifyColor B").AddComponent(Global("AmplifyColorEffect"));
            Call(effectA, "NewSharedInstanceID");
            Call(effectB, "NewSharedInstanceID");
            Type container = Namespaced("VolumeEffectContainer");
            object instance = Activator.CreateInstance(container);

            object volume = container.GetMethod("AddJustColorEffect").Invoke(instance, new[] { effectA });
            Assert.That(Namespaced("VolumeEffect").GetField("gameObject").GetValue(volume), Is.SameAs(effectA));
            Assert.That(container.GetMethod("FindVolumeEffect").Invoke(instance, new[] { effectA }), Is.SameAs(volume));
            Assert.That(container.GetMethod("FindVolumeEffect").Invoke(instance, new[] { effectB }), Is.Null);

            Set(effectB, "sharedInstanceID", Get(effectA, "sharedInstanceID"));
            Assert.That(container.GetMethod("FindVolumeEffect").Invoke(instance, new[] { effectB }), Is.SameAs(volume), "an instantiated copy shares the id");
            Assert.That(((Array)container.GetMethod("GetStoredEffects").Invoke(instance, null)).Cast<object>().ToArray(), Is.EqualTo(new[] { effectA }));

            container.GetMethod("RemoveVolumeEffect").Invoke(instance, new[] { volume });
            Assert.That(container.GetMethod("FindVolumeEffect").Invoke(instance, new[] { effectA }), Is.Null);
        }

        private static object NewField(string name, string type, Action<object> init)
        {
            object field = Activator.CreateInstance(Namespaced("VolumeEffectField"), name, type);
            init(field);
            return field;
        }

        [Test]
        public void BlendingVolumeEffectsLerpsNumbersAndTakesTheSecondBoolean()
        {
            Type componentType = Namespaced("VolumeEffectComponent");
            Type effectType = Namespaced("VolumeEffect");
            Type flagsType = Namespaced("VolumeEffectFlags");
            Type componentFlagsType = Namespaced("VolumeEffectComponentFlags");
            Type fieldType = Namespaced("VolumeEffectField");
            Type fieldFlagsType = Namespaced("VolumeEffectFieldFlags");
            object owner = Inactive("Lucid AmplifyColor blend owner").AddComponent(Global("AmplifyColorEffect"));

            object First(float single, bool flag, Vector3 vector, string component)
            {
                object volume = Activator.CreateInstance(effectType, owner);
                object comp = Activator.CreateInstance(componentType, component);
                IList fields = (IList)componentType.GetField("fields").GetValue(comp);
                fields.Add(NewField("amount", "System.Single", f => fieldType.GetField("valueSingle").SetValue(f, single)));
                fields.Add(NewField("enabled", "System.Boolean", f => fieldType.GetField("valueBoolean").SetValue(f, flag)));
                fields.Add(NewField("offset", "UnityEngine.Vector3", f => fieldType.GetField("valueVector3").SetValue(f, vector)));
                ((IList)effectType.GetField("components").GetValue(volume)).Add(comp);
                return volume;
            }

            object volume1 = First(0f, false, Vector3.zero, "CompA");
            object volume2 = First(8f, true, new Vector3(4, 8, 12), "CompA");

            object flags = Activator.CreateInstance(flagsType);
            IList flagComponents = (IList)flagsType.GetField("components").GetValue(flags);
            object compFlags = Activator.CreateInstance(componentFlagsType, "CompA");
            componentFlagsType.GetField("blendFlag").SetValue(compFlags, true);
            IList fieldFlags = (IList)componentFlagsType.GetField("componentFields").GetValue(compFlags);
            foreach (string name in new[] { "amount", "enabled", "offset" })
                fieldFlags.Add(Activator.CreateInstance(fieldFlagsType, NewField(name, name == "amount" ? "System.Single" : name == "enabled" ? "System.Boolean" : "UnityEngine.Vector3", f => { })));
            flagComponents.Add(compFlags);
            object skipped = Activator.CreateInstance(componentFlagsType, "CompB");
            flagComponents.Add(skipped);

            object blended = effectType.GetMethod("BlendValuesToVolumeEffect").Invoke(null, new[] { flags, volume1, volume2, 0.25f });
            IList components = (IList)effectType.GetField("components").GetValue(blended);
            Assert.That(components.Count, Is.EqualTo(1), "components without a blend flag are skipped");
            IList fields1 = (IList)componentType.GetField("fields").GetValue(components[0]);
            Assert.That(fields1.Cast<object>().Select(f => (string)fieldType.GetField("fieldName").GetValue(f)), Is.EqualTo(new[] { "amount", "enabled", "offset" }));
            Assert.That(fieldType.GetField("valueSingle").GetValue(fields1[0]), Is.EqualTo(2f));
            Assert.That(fieldType.GetField("valueBoolean").GetValue(fields1[1]), Is.EqualTo(true), "booleans are not interpolated");
            Assert.That(fieldType.GetField("valueVector3").GetValue(fields1[2]), Is.EqualTo(new Vector3(1, 2, 3)));
        }

        [Test]
        public void ComponentFlagsListPublicBlendableFieldsAndEffectsApplyThem()
        {
            Type componentFlagsType = Namespaced("VolumeEffectComponentFlags");
            Type effectType = Namespaced("VolumeEffect");
            GameObject host = Inactive("Lucid AmplifyColor apply");
            object effect = host.AddComponent(Global("AmplifyColorEffect"));
            Component volume = host.AddComponent(Global("AmplifyColorVolumeBase"));
            Type volumeType = volume.GetType();

            object compFlags = Activator.CreateInstance(componentFlagsType, new object[] { volume });
            Assert.That(componentFlagsType.GetField("componentName").GetValue(compFlags), Is.EqualTo("AmplifyColorVolumeBase"));
            IList fieldFlags = (IList)componentFlagsType.GetField("componentFields").GetValue(compFlags);
            Type fieldFlagsType = Namespaced("VolumeEffectFieldFlags");
            Assert.That(fieldFlags.Cast<object>().Select(f => (string)fieldFlagsType.GetField("fieldName").GetValue(f)),
                Is.EqualTo(new[] { "Exposure", "EnterBlendTime", "ShowInSceneView" }));
            Assert.That(fieldFlags.Cast<object>().Select(f => (bool)fieldFlagsType.GetField("blendFlag").GetValue(f)), Is.All.False);
            Assert.That(componentFlagsType.GetMethod("GetFieldNames").Invoke(compFlags, null), Is.Empty);

            fieldFlagsType.GetField("blendFlag").SetValue(fieldFlags[0], true);
            componentFlagsType.GetField("blendFlag").SetValue(compFlags, true);
            Assert.That(componentFlagsType.GetMethod("GetFieldNames").Invoke(compFlags, null), Is.EqualTo(new[] { "Exposure" }));

            volumeType.GetField("Exposure").SetValue(volume, 0.25f);
            object stored = Activator.CreateInstance(effectType, effect);
            object storedComponent = effectType.GetMethod("AddComponent").Invoke(stored, new object[] { volume, compFlags });
            IList storedFields = (IList)Namespaced("VolumeEffectComponent").GetField("fields").GetValue(storedComponent);
            Assert.That(storedFields.Count, Is.EqualTo(1));
            Assert.That(Namespaced("VolumeEffectField").GetField("valueSingle").GetValue(storedFields[0]), Is.EqualTo(0.25f));

            object other = Activator.CreateInstance(effectType, effect);
            volumeType.GetField("Exposure").SetValue(volume, 0.75f);
            effectType.GetMethod("AddComponent").Invoke(other, new object[] { volume, compFlags });

            object effectFlags = Get(effect, "EffectFlags");
            ((IList)effectFlags.GetType().GetField("components").GetValue(effectFlags)).Add(compFlags);

            effectType.GetMethod("SetValues").Invoke(stored, new[] { effect });
            Assert.That(volumeType.GetField("Exposure").GetValue(volume), Is.EqualTo(0.25f));
            effectType.GetMethod("BlendValues").Invoke(stored, new[] { effect, other, 0.5f });
            Assert.That(volumeType.GetField("Exposure").GetValue(volume), Is.EqualTo(0.5f));
            Assert.That(volumeType.GetField("EnterBlendTime").GetValue(volume), Is.EqualTo(1f), "fields without a blend flag are untouched");
        }

        [Test]
        public void AcceptableComponentsDropUnityComponentsButKeepTheDerivedEffect()
        {
            GameObject host = Inactive("Lucid AmplifyColor components");
            host.AddComponent<Camera>();
            object effect = host.AddComponent(Global("AmplifyColorEffect"));
            Component volume = host.AddComponent(Global("AmplifyColorVolumeBase"));
            Type effectType = Namespaced("VolumeEffect");
            Component[] listed = (Component[])effectType.GetMethod("ListAcceptableComponents").Invoke(null, new[] { effect });
            // The shipped predicate compares against typeof(AmplifyColorBase), so the derived effect itself is kept.
            Assert.That(listed, Is.EqualTo(new[] { (Component)effect, volume }));
            Assert.That(((Component[])effectType.GetMethod("ListAcceptableComponents").Invoke(null, new object[] { null })), Is.Empty);
        }
    }
}
