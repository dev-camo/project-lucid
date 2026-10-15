// Controlled rebuilt Unity obligations for preserved HLUIEffectManager; original native/provider/FP parity remains held.
// Inactive owned objects never deliberately run Awake; active Core logging and list-to-dictionary initialization are separate gates.
using System;
using System.Collections.Generic;
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
    public sealed class OriginalHLUIEffectManagerTests
    {
        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        [Test]
        public void OriginalEffectManagerRetainsWholeTwoOwnerDeclarations()
        {
            Type manager = typeof(HLUIEffectManager);
            Type cue = RequireAudioCueType();
            Assert.That(manager.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(manager.FullName, Is.EqualTo("Hardlight.HLUIEffectManager"));
            Assert.That((int)manager.Attributes, Is.EqualTo(1048577));
            Assert.That(manager.BaseType, Is.EqualTo(typeof(MonoSingleton<HLUIEffectManager>)));
            Assert.That(manager.GetNestedTypes(Declared), Is.EqualTo(new[] { cue }));
            Assert.That(cue.FullName, Is.EqualTo("Hardlight.HLUIEffectManager+HLAudioCueKVP"));
            Assert.That((int)cue.Attributes, Is.EqualTo(1056771));
            Assert.That(cue.BaseType, Is.EqualTo(typeof(object)));
            Assert.That(cue.GetNestedTypes(Declared), Is.Empty);
            Assert.That(manager.GetEvents(Declared), Is.Empty);
            Assert.That(cue.GetEvents(Declared), Is.Empty);
            Assert.That(manager.GetProperties(Declared).Select(p => p.Name), Is.EqualTo(new[] { "DisableWobbleScaler", "ReferencePixelsPerUnit" }));
            Assert.That(cue.GetProperties(Declared).Select(p => p.Name), Is.EqualTo(new[] { "AudioClip", "AudioType" }));
            var options = manager.GetCustomAttributesData();
            Assert.That(options.Count, Is.EqualTo(2));
            RequireOption(options[0], Option.ArrayBoundsChecks);
            RequireOption(options[1], Option.NullChecks);
            // Serializable is a TypeDef bit; Mono projects its one flag-derived attribute separately.
            var serializable = cue.GetCustomAttributesData();
            Assert.That(serializable.Count, Is.EqualTo(1));
            Assert.That(serializable[0].AttributeType, Is.EqualTo(typeof(SerializableAttribute)));
            Assert.That(serializable[0].ConstructorArguments, Is.Empty);
            Assert.That(serializable[0].NamedArguments, Is.Empty);
            RequireOwner(manager, ManagerMethods, ManagerFields, ManagerFieldAttributes);
            RequireOwner(cue, CueMethods, CueFields, CueFieldAttributes);
            Assert.That(manager.GetField("SubPageLeftRightEdgeBufferPixelsPortrait", Declared).GetRawConstantValue(), Is.EqualTo(300f));
            Assert.That(manager.GetField("SubPageLeftRightEdgeBufferPixelsLandscape", Declared).GetRawConstantValue(), Is.EqualTo(25f));
            Assert.That(manager.GetField("FadeInAlpha", Declared).GetRawConstantValue(), Is.EqualTo(1f));
            Assert.That(manager.GetField("FadeOutAlpha", Declared).GetRawConstantValue(), Is.EqualTo(0f));
        }

        [Test]
        public void OriginalInactiveEffectManagerRetainsDefaultsAndEmptyReferenceSetter()
        {
            using (var owned = new InactiveManager())
            {
                HLUIEffectManager manager = owned.Manager;
                Assert.That(manager.DisableWobbleScaler, Is.EqualTo(new Vector2(10f, 0f)));
                Assert.That(manager.ReferencePixelsPerUnit, Is.EqualTo(100f));
                foreach (float value in new[] { -125f, 0f, 37.5f, 2000f })
                {
                    manager.ReferencePixelsPerUnit = value;
                    Assert.That(manager.ReferencePixelsPerUnit, Is.EqualTo(100f));
                }
                Assert.That(manager.GetDisabledButtonFaceColour(), Is.EqualTo(Color.white));
                Assert.That(manager.GetDisabledTextColour(), Is.EqualTo(new Color(0.647058845f, 0.807843149f, 1f, 1f)));
                Assert.That(manager.GetDisabledTextOutlineColour(), Is.EqualTo(new Color(0.0627450943f, 0.258823544f, 0.643137276f, 1f)));
                Assert.That(manager.GetDisabledGradientStartColour(), Is.EqualTo(Color.white));
                Assert.That(manager.GetDisabledGradientEndColour(), Is.EqualTo(Color.white));
                using (var serialized = new SerializedObject(manager))
                {
                    serialized.Update();
                    foreach (CurveRoute route in Curves(manager))
                    {
                        SerializedProperty field = RequireProperty(serialized, route.Field, SerializedPropertyType.AnimationCurve);
                        Assert.That(field.animationCurveValue.length, Is.Zero);
                        Assert.That(route.End(), Is.Zero);
                    }
                    Assert.That(RequireProperty(serialized, "m_audioCueList", SerializedPropertyType.Generic).arraySize, Is.Zero);
                    foreach (string name in new[] { "m_disabledButtonSprite", "m_disabledSecondaryButtonSprite", "m_disabledPrimaryTextMeshProMaterialPreset", "m_disabledSecondaryTextMeshProMaterialPreset" })
                        Assert.That(RequireProperty(serialized, name, SerializedPropertyType.ObjectReference).objectReferenceValue, Is.Null);
                }
                Assert.That(HLUIEffectManager.GetOrientationSpecificPageBleedSize(Hardlight.UIOrientation.Landscape), Is.EqualTo(25f));
                foreach (int orientation in new[] { int.MinValue, -1, 0, 1, 3, int.MaxValue })
                    Assert.That(HLUIEffectManager.GetOrientationSpecificPageBleedSize((Hardlight.UIOrientation)orientation), Is.EqualTo(300f));
                owned.RequireUnchanged();
            }
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OriginalEffectManagerSamplesEighteenGenuineSerializedFiniteCurves()
        {
            using (var owned = new InactiveManager())
            using (var serialized = new SerializedObject(owned.Manager))
            {
                CurveRoute[] routes = Curves(owned.Manager);
                Assert.That(routes.Length, Is.EqualTo(18));
                serialized.Update();
                for (int index = 0; index < routes.Length; index++)
                {
                    float start = -2f - index * 0.125f;
                    float end = 3f + index * 0.25f;
                    var curve = AnimationCurve.Linear(start, -4f - index, end, 6f + index * 0.5f);
                    curve.preWrapMode = WrapMode.ClampForever;
                    curve.postWrapMode = WrapMode.ClampForever;
                    RequireProperty(serialized, routes[index].Field, SerializedPropertyType.AnimationCurve).animationCurveValue = curve;
                }
                Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
                serialized.Update();
                for (int index = 0; index < routes.Length; index++)
                {
                    CurveRoute route = routes[index];
                    AnimationCurve curve = RequireProperty(serialized, route.Field, SerializedPropertyType.AnimationCurve).animationCurveValue;
                    float start = -2f - index * 0.125f;
                    float end = 3f + index * 0.25f;
                    Assert.That(curve.length, Is.EqualTo(2));
                    Assert.That(route.End(), Is.EqualTo(end));
                    Assert.That(HLUIEffectManager.GetCurveEndTime(curve), Is.EqualTo(end));
                    foreach (float time in new[] { start - 1f, start, (start + end) * 0.5f, end, end + 1f })
                        Assert.That(route.Sample(time), Is.EqualTo(curve.Evaluate(time)).Within(0.00001f));
                    Assert.That(route.Sample(start), Is.EqualTo(-4f - index).Within(0.00001f));
                    Assert.That(route.Sample(end), Is.EqualTo(6f + index * 0.5f).Within(0.00001f));
                    if (route.Curve != null)
                    {
                        AnimationCurve reference = route.Curve();
                        Assert.That(reference.length, Is.EqualTo(2));
                        Assert.That(reference.keys.Select(key => key.time), Is.EqualTo(new[] { start, end }));
                        Assert.That(reference.Evaluate((start + end) * 0.5f), Is.EqualTo(route.Sample((start + end) * 0.5f)).Within(0.00001f));
                    }
                }
                Assert.That(HLUIEffectManager.GetCurveEndTime(new AnimationCurve()), Is.Zero);
                owned.RequireUnchanged();
            }
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OriginalEffectManagerReturnsGenuineOwnedSerializedVisualValues()
        {
            using (var owned = new InactiveManager())
            {
                Texture2D texture = null;
                Sprite sprite = null;
                Material material = null;
                try
                {
                    Shader shader = Shader.Find("UI/Default");
                    Assert.That(shader, Is.Not.Null, "A genuine available Unity UI shader is required; no test shader substitute is supplied.");
                    material = new Material(shader);
                    texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    texture.SetPixels(Enumerable.Repeat(new Color(0.2f, 0.4f, 0.6f, 1f), 16).ToArray());
                    texture.Apply(false, false);
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.25f, 0.75f), 16f);
                    Assert.That(sprite, Is.Not.Null);
                    using (var serialized = new SerializedObject(owned.Manager))
                    {
                        serialized.Update();
                        foreach (string name in new[] { "m_disabledButtonSprite", "m_disabledSecondaryButtonSprite" })
                            RequireProperty(serialized, name, SerializedPropertyType.ObjectReference).objectReferenceValue = sprite;
                        foreach (string name in new[] { "m_disabledPrimaryTextMeshProMaterialPreset", "m_disabledSecondaryTextMeshProMaterialPreset" })
                            RequireProperty(serialized, name, SerializedPropertyType.ObjectReference).objectReferenceValue = material;
                        string[] fields = { "m_disabledButtonFaceColour", "m_disabledButtonTextColour", "m_disabledButtonTextOutlineColour", "m_disabledGradientStartColour", "m_disabledGradientEndColour" };
                        Color[] values = { new Color(0.1f, 0.2f, 0.3f, 0.4f), new Color(0.5f, 0.6f, 0.7f, 0.8f), new Color(0.9f, 0.8f, 0.7f, 0.6f), new Color(0.6f, 0.4f, 0.2f, 1f), new Color(0.3f, 0.5f, 0.7f, 0.9f) };
                        for (int index = 0; index < fields.Length; index++)
                            RequireProperty(serialized, fields[index], SerializedPropertyType.Color).colorValue = values[index];
                        RequireProperty(serialized, "m_disableWobbleScaler", SerializedPropertyType.Vector2).vector2Value = new Vector2(-7.5f, 12.25f);
                        RequireProperty(serialized, "m_referencePixelsPerUnit", SerializedPropertyType.Float).floatValue = 37.5f;
                        Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
                        serialized.Update();
                        Assert.That(owned.Manager.GetDisabledButtonBackground(), Is.SameAs(sprite));
                        Assert.That(owned.Manager.GetDisabledSecondaryButtonBackground(), Is.SameAs(sprite));
                        Assert.That(owned.Manager.GetDisabledPrimaryTextMeshProMaterialPreset(), Is.SameAs(material));
                        Assert.That(owned.Manager.GetDisabledSecondaryTextMeshProMaterialPreset(), Is.SameAs(material));
                        Assert.That(new[] { owned.Manager.GetDisabledButtonFaceColour(), owned.Manager.GetDisabledTextColour(), owned.Manager.GetDisabledTextOutlineColour(), owned.Manager.GetDisabledGradientStartColour(), owned.Manager.GetDisabledGradientEndColour() }, Is.EqualTo(values));
                        Assert.That(owned.Manager.DisableWobbleScaler, Is.EqualTo(new Vector2(-7.5f, 12.25f)));
                        owned.Manager.ReferencePixelsPerUnit = -900f;
                        Assert.That(owned.Manager.ReferencePixelsPerUnit, Is.EqualTo(37.5f));
                        owned.RequireUnchanged();
                    }
                }
                finally
                {
                    try { if (sprite != null) Object.DestroyImmediate(sprite); }
                    finally
                    {
                        try { if (texture != null) Object.DestroyImmediate(texture); }
                        finally { if (material != null) Object.DestroyImmediate(material); }
                    }
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OriginalAudioCueRoundTripsRawKeysAndEmptyManagerSuppressesDispatch()
        {
            object first = Activator.CreateInstance(RequireAudioCueType());
            object second = Activator.CreateInstance(RequireAudioCueType());
            Assert.That(ReadAudioCueClip(first), Is.Null);
            Assert.That((int)ReadAudioCueType(first), Is.Zero);
            foreach (int key in new[] { -1225125244, -19, 0, 71 })
            {
                JsonUtility.FromJsonOverwrite("{\"m_audioType\":" + key.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}", first);
                Assert.That((int)ReadAudioCueType(first), Is.EqualTo(key));
                Assert.That(ReadAudioCueClip(first), Is.Null);
                Assert.That((int)ReadAudioCueType(second), Is.Zero);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(first), second);
                Assert.That(ReadAudioCueType(second), Is.EqualTo(ReadAudioCueType(first)));
                JsonUtility.FromJsonOverwrite("{\"m_audioType\":0}", second);
            }
            using (var owned = new InactiveManager())
            {
                var dictionary = (Dictionary<HLAudioTypes, AudioClip>)typeof(HLUIEffectManager).GetField("m_audioCues", Declared).GetValue(owned.Manager);
                Assert.That(dictionary, Is.Not.Null);
                Assert.That(dictionary.Count, Is.Zero);
                Assert.That(dictionary.Comparer.GetType(), Is.EqualTo(typeof(HLAudioTypeEqualityComparer)));
                UISoundEffectManager soundBefore = UISoundEffectManager.Instance;
                foreach (HLAudioTypes key in new[] { HLAudioTypes.None, (HLAudioTypes)(-19), (HLAudioTypes)0, (HLAudioTypes)71 })
                {
                    owned.Manager.PlayAudioClip(key, -0.25f);
                    owned.Manager.PlayAudioClipWithSemitoneOffset(key, 12, 0.75f);
                }
                Assert.That(dictionary.Count, Is.Zero);
                Assert.That(ReferenceEquals(UISoundEffectManager.Instance, soundBefore), Is.True);
                // No Awake means no audio-cue insertion/duplicate-key or audible playback acceptance.
                owned.RequireUnchanged();
            }
            LogAssert.NoUnexpectedReceived();
        }

        // PlayAudioClip (x86-64 0x1b49c90) returns on HLAudioTypes.None (0xb6fa1284) before the lookup,
        // then requires ContainsKey and a Unity-non-null clip before reading UISoundEffectManager.Instance.
        // With no sound manager instance, only a cue that passes every guard reaches that read and faults.
        [Test]
        public void PopulatedCuesStopAtNoneAndNullClipsBeforeTheSoundManager()
        {
            Assume.That(UISoundEffectManager.Instance, Is.Null, "requires no live UI sound manager");
            AudioClip clip = AudioClip.Create("Lucid owned UI cue", 1, 1, 44100, false);
            try
            {
                using (var owned = new InactiveManager())
                {
                    var cues = (Dictionary<HLAudioTypes, AudioClip>)typeof(HLUIEffectManager).GetField("m_audioCues", Declared).GetValue(owned.Manager);
                    cues[HLAudioTypes.None] = clip;
                    cues[(HLAudioTypes)71] = null;
                    cues[(HLAudioTypes)(-19)] = clip;
                    try
                    {
                        foreach (HLAudioTypes stopped in new[] { HLAudioTypes.None, (HLAudioTypes)71, (HLAudioTypes)5 })
                        {
                            owned.Manager.PlayAudioClip(stopped, 0.5f);
                            owned.Manager.PlayAudioClipWithSemitoneOffset(stopped, 3, 0.5f);
                        }
                        Assert.Throws<NullReferenceException>(() => owned.Manager.PlayAudioClip((HLAudioTypes)(-19), 0.5f));
                        Assert.Throws<NullReferenceException>(() => owned.Manager.PlayAudioClipWithSemitoneOffset((HLAudioTypes)(-19), 3, 0.5f));
                    }
                    finally { cues.Clear(); }
                    owned.RequireUnchanged();
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(clip); }
            LogAssert.NoUnexpectedReceived();
        }

        private static Type RequireAudioCueType()
        {
            Type cue = typeof(HLUIEffectManager).GetNestedType("HLAudioCueKVP", Declared);
            Assert.That(cue, Is.Not.Null);
            Assert.That(cue.IsNestedPrivate, Is.True);
            return cue;
        }

        private static HLAudioTypes ReadAudioCueType(object cue) =>
            (HLAudioTypes)RequireAudioCueType().GetProperty("AudioType", Declared).GetValue(cue);

        private static AudioClip ReadAudioCueClip(object cue) =>
            (AudioClip)RequireAudioCueType().GetProperty("AudioClip", Declared).GetValue(cue);

        private sealed class InactiveManager : IDisposable
        {
            private GameObject gameObject;
            private readonly HLUIEffectManager previous = HLUIEffectManager.Instance;
            private readonly KeyValuePair<ISystem, SystemRef>[] systems = SnapshotSystems();
            public HLUIEffectManager Manager { get; private set; }

            public InactiveManager()
            {
                // Pair snapshots do not observe per-row typed caches. Refuse every
                // preexisting default Core row and live Core before any Unity object.
                RequireSafeCoreBaseline(systems);
                try
                {
                    gameObject = new GameObject("Owned original UI effect fixture");
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
        }

        private static void RequireSafeCoreBaseline(KeyValuePair<ISystem, SystemRef>[] rows)
        {
            Assert.That(ReferenceEquals(HLUnityCore.Instance, null), Is.True,
                "A foreign Core singleton may invoke real logging callbacks during destruction.");
            string coreName = ProcessManager.GetDefaultName<HLUnityCore>();
            foreach (KeyValuePair<ISystem, SystemRef> row in rows)
            {
                Assert.That(row.Value, Is.Not.Null);
                Assert.That(string.Equals(row.Value.SystemName(), coreName, StringComparison.Ordinal), Is.False,
                    "An existing default Core row can acquire an unseen typed cache; no typed getter is used to probe it.");
                Assert.That(row.Key is HLUnityCore, Is.False,
                    "A foreign Core system under any alias is outside owned inactive fixture setup.");
            }
        }

        private static KeyValuePair<ISystem, SystemRef>[] SnapshotSystems()
        {
            var rows = new List<KeyValuePair<ISystem, SystemRef>>();
            ProcessManager.ForEachSystem((system, reference) => rows.Add(new KeyValuePair<ISystem, SystemRef>(system, reference)));
            return rows.ToArray();
        }

        private static SerializedProperty RequireProperty(SerializedObject serialized, string name, SerializedPropertyType kind)
        {
            SerializedProperty property = serialized.FindProperty(name);
            Assert.That(property, Is.Not.Null, name);
            Assert.That(property.propertyType, Is.EqualTo(kind), name);
            return property;
        }

        private sealed class CurveRoute
        {
            public readonly string Field;
            public readonly Func<float> End;
            public readonly Func<float, float> Sample;
            public readonly Func<AnimationCurve> Curve;
            public CurveRoute(string field, Func<float> end, Func<float, float> sample, Func<AnimationCurve> curve = null)
            { Field = field; End = end; Sample = sample; Curve = curve; }
        }

        private static CurveRoute[] Curves(HLUIEffectManager manager) => new[]
        {
            new CurveRoute("m_onPressBounceCurve", manager.GetOnPressCurveEndTime, manager.SampleOnPressCurve),
            new CurveRoute("m_onReleaseBounceCurve", manager.GetOnReleaseCurveEndTime, manager.SampleOnReleaseCurve),
            new CurveRoute("m_onDisabledPressShakeCurve", manager.GetOnDisabledPressCurveEndTime, manager.SampleOnDisablePressCurve),
            new CurveRoute("m_onEnableBounceCurve", manager.GetOnEnableBounceEndTime, manager.SampleOnEnableBounceCurve),
            new CurveRoute("m_uiUnclampedSlideAnimationCurve", manager.GetUIUnclampedSlideAnimationCurveEndTime, manager.SampleUIUnclampedSlideAnimationCurve),
            new CurveRoute("m_uiClampedSlideAnimationCurve", manager.GetUIClampedSlideAnimationCurveEndTime, manager.SampleUIClampedSlideAnimationCurve),
            new CurveRoute("m_bubblePowerupPopCurve", manager.GetBubblePowerupPopCurveEndTime, manager.SampleBubblePowerupPopCurve),
            new CurveRoute("m_menuThrobberColourLerpCurve", manager.GetMenuThrobberColourLerpCurveEndTime, manager.SampleMenuThrobberColourLerpCurve),
            new CurveRoute("m_FTUEHandBounceCurve", manager.GetFTUEHandBounceCurveEndTime, manager.SampleFTUEHandBounceCurve),
            new CurveRoute("m_FTUEHandUrgencyOverTime", manager.GetFTUEHandUrgencyCurveEndTime, manager.SampleFTUEHandUrgencyCurve),
            new CurveRoute("m_UIScaleChangerIncreaseCurve", manager.GetUIScaleChangerIncreaseCurveEndTime, manager.SampleUIScaleChangerIncreaseCurve),
            new CurveRoute("m_UIScaleChangerDecreaseCurve", manager.GetUIScaleChangerDecreaseCurveEndTime, manager.SampleUIScaleChangerDecreaseCurve),
            new CurveRoute("m_effectCurveSmooth", manager.GetEffectCurveSmoothEndTime, manager.SampleEffectCurveSmooth, manager.EffectCurveSmooth),
            new CurveRoute("m_effectCurvePulse", manager.GetEffectCurvePulseEndTime, manager.SampleEffectCurvePulse, manager.EffectCurvePulse),
            new CurveRoute("m_panelShineLength", manager.GetPanelShineLengthCurveEndTime, manager.SamplePanelShineLengthCurve, manager.PanelShineLengthOverTime),
            new CurveRoute("m_panelShineThickness", manager.GetPanelShineThicknessCurveEndTime, manager.SamplePanelShineThicknessCurve, manager.PanelShineThicknessOverTime),
            new CurveRoute("m_panelShineAlpha", manager.GetPanelShineAlphaCurveEndTime, manager.SamplePanelShineAlphaCurve, manager.PanelShineAlphaOverTime),
            new CurveRoute("m_attackFeedbackAlphaCurve", manager.GetAttackFeedbackAlphaCurveEndTime, manager.SampleAttackFeedbackAlphaCurve),
        };

        private static string Canonical(Type type)
        {
            if (!type.IsGenericType) return type.FullName;
            return type.GetGenericTypeDefinition().FullName + "<" + string.Join(",", type.GetGenericArguments().Select(Canonical)) + ">";
        }

        private static void RequireOwner(Type type, string[] expectedMethods, string[] expectedFields, string[][] expectedAttributes)
        {
            MethodBase[] methods = type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared)).OrderBy(m => m.MetadataToken).ToArray();
            Assert.That(methods.Select(MethodRow), Is.EqualTo(expectedMethods));
            foreach (MethodBase method in methods)
            {
                Assert.That(method.GetMethodImplementationFlags(), Is.EqualTo(MethodImplAttributes.IL));
                Assert.That(method.IsGenericMethod, Is.False);
                Assert.That(method.GetCustomAttributesData(), Is.Empty);
                if (method is MethodInfo ordinary) Assert.That(ordinary.ReturnParameter.GetCustomAttributesData(), Is.Empty);
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    bool optional = parameter.Name == "volume";
                    Assert.That(parameter.Attributes, Is.EqualTo(optional ? ParameterAttributes.Optional | ParameterAttributes.HasDefault : ParameterAttributes.None));
                    Assert.That(parameter.HasDefaultValue, Is.EqualTo(optional));
                    if (optional)
                    {
                        Assert.That(parameter.RawDefaultValue, Is.EqualTo(1f));
                        var attributes = parameter.GetCustomAttributesData();
                        Assert.That(attributes.Count, Is.EqualTo(1));
                        Assert.That(attributes[0].AttributeType, Is.EqualTo(typeof(OptionalAttribute)));
                        Assert.That(attributes[0].ConstructorArguments, Is.Empty);
                        Assert.That(attributes[0].NamedArguments, Is.Empty);
                    }
                    else Assert.That(parameter.GetCustomAttributesData(), Is.Empty);
                }
            }
            FieldInfo[] fields = type.GetFields(Declared).OrderBy(f => f.MetadataToken).ToArray();
            Assert.That(fields.Select(f => f.Name + "|" + (int)f.Attributes + "|" + Canonical(f.FieldType)), Is.EqualTo(expectedFields));
            for (int index = 0; index < fields.Length; index++)
                Assert.That(fields[index].GetCustomAttributesData().Select(AttributeRow), Is.EqualTo(expectedAttributes[index]));
        }

        private static string MethodRow(MethodBase method) => method.Name + "|" + (int)method.Attributes + "|" + (method is MethodInfo info ? Canonical(info.ReturnType) : "System.Void") + "|" + string.Join(",", method.GetParameters().Select(p => Canonical(p.ParameterType) + " " + p.Name));

        private static string AttributeRow(CustomAttributeData attribute)
        {
            Assert.That(attribute.NamedArguments, Is.Empty);
            if (attribute.AttributeType == typeof(Hardlight.Utils.HashEnumAttribute))
            {
                Assert.That(attribute.ConstructorArguments.Count, Is.EqualTo(1));
                Assert.That(attribute.ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(Type)));
                Assert.That(attribute.ConstructorArguments[0].Value, Is.EqualTo(typeof(HLAudioTypes)));
                return attribute.AttributeType.FullName + "|Hardlight.HLAudioTypes";
            }
            Assert.That(attribute.ConstructorArguments.All(a => a.ArgumentType == typeof(string)), Is.True);
            return attribute.AttributeType.FullName + "|" + string.Join("|", attribute.ConstructorArguments.Select(a => (string)a.Value));
        }

        private static void RequireOption(CustomAttributeData attribute, Option option)
        {
            Assert.That(attribute.AttributeType, Is.EqualTo(typeof(Il2CppSetOptionAttribute)));
            Assert.That(attribute.Constructor.GetParameters().Select(p => p.ParameterType), Is.EqualTo(new[] { typeof(Option), typeof(object) }));
            Assert.That(attribute.ConstructorArguments.Count, Is.EqualTo(2));
            Assert.That(attribute.ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(Option)));
            Assert.That(attribute.ConstructorArguments[0].Value, Is.EqualTo((int)option));
            Assert.That(attribute.ConstructorArguments[1].ArgumentType, Is.EqualTo(typeof(bool)));
            Assert.That(attribute.ConstructorArguments[1].Value, Is.EqualTo(false));
            Assert.That(attribute.NamedArguments, Is.Empty);
        }

        private static readonly string[] CueMethods =
        {
            "get_AudioClip|2182|UnityEngine.AudioClip|",
            "get_AudioType|2182|Hardlight.HLAudioTypes|",
            ".ctor|6278|System.Void|",
        };

        private static readonly string[] CueFields =
        {
            "m_audioClip|1|UnityEngine.AudioClip",
            "m_audioType|1|Hardlight.HLAudioTypes",
        };

        private static readonly string[][] CueFieldAttributes =
        {
            new string[] { "UnityEngine.SerializeField|" },
            new string[] { "Hardlight.Utils.HashEnumAttribute|Hardlight.HLAudioTypes", "UnityEngine.SerializeField|" },
        };

        private static readonly string[] ManagerMethods =
        {
            "get_DisableWobbleScaler|2182|UnityEngine.Vector2|",
            "Awake|196|System.Void|",
            "OnDestroy|196|System.Void|",
            "GetOrientationSpecificPageBleedSize|150|System.Single|Hardlight.UIOrientation orientation",
            "PlayAudioClip|134|System.Void|Hardlight.HLAudioTypes audioType,System.Single volume",
            "PlayAudioClipWithSemitoneOffset|134|System.Void|Hardlight.HLAudioTypes audioType,System.Int32 semitoneChange,System.Single volume",
            "GetDisabledButtonBackground|134|UnityEngine.Sprite|",
            "GetDisabledSecondaryButtonBackground|134|UnityEngine.Sprite|",
            "GetDisabledPrimaryTextMeshProMaterialPreset|134|UnityEngine.Material|",
            "GetDisabledSecondaryTextMeshProMaterialPreset|134|UnityEngine.Material|",
            "GetDisabledButtonFaceColour|134|UnityEngine.Color|",
            "GetDisabledTextColour|134|UnityEngine.Color|",
            "GetDisabledTextOutlineColour|134|UnityEngine.Color|",
            "GetDisabledGradientStartColour|134|UnityEngine.Color|",
            "GetDisabledGradientEndColour|134|UnityEngine.Color|",
            "GetCurveEndTime|150|System.Single|UnityEngine.AnimationCurve curve",
            "GetOnPressCurveEndTime|134|System.Single|",
            "GetOnReleaseCurveEndTime|134|System.Single|",
            "GetOnDisabledPressCurveEndTime|134|System.Single|",
            "GetOnEnableBounceEndTime|134|System.Single|",
            "GetUIUnclampedSlideAnimationCurveEndTime|134|System.Single|",
            "GetUIClampedSlideAnimationCurveEndTime|134|System.Single|",
            "GetBubblePowerupPopCurveEndTime|134|System.Single|",
            "GetMenuThrobberColourLerpCurveEndTime|134|System.Single|",
            "GetFTUEHandBounceCurveEndTime|134|System.Single|",
            "GetFTUEHandUrgencyCurveEndTime|134|System.Single|",
            "EffectCurveSmooth|134|UnityEngine.AnimationCurve|",
            "EffectCurvePulse|134|UnityEngine.AnimationCurve|",
            "PanelShineLengthOverTime|134|UnityEngine.AnimationCurve|",
            "PanelShineThicknessOverTime|134|UnityEngine.AnimationCurve|",
            "PanelShineAlphaOverTime|134|UnityEngine.AnimationCurve|",
            "GetUIScaleChangerIncreaseCurveEndTime|134|System.Single|",
            "GetUIScaleChangerDecreaseCurveEndTime|134|System.Single|",
            "GetEffectCurveSmoothEndTime|134|System.Single|",
            "GetEffectCurvePulseEndTime|134|System.Single|",
            "GetPanelShineLengthCurveEndTime|134|System.Single|",
            "GetPanelShineThicknessCurveEndTime|134|System.Single|",
            "GetPanelShineAlphaCurveEndTime|134|System.Single|",
            "GetAttackFeedbackAlphaCurveEndTime|134|System.Single|",
            "SampleOnPressCurve|134|System.Single|System.Single time",
            "SampleOnReleaseCurve|134|System.Single|System.Single time",
            "SampleOnDisablePressCurve|134|System.Single|System.Single time",
            "SampleOnEnableBounceCurve|134|System.Single|System.Single time",
            "SampleUIUnclampedSlideAnimationCurve|134|System.Single|System.Single time",
            "SampleUIClampedSlideAnimationCurve|134|System.Single|System.Single time",
            "SampleBubblePowerupPopCurve|134|System.Single|System.Single time",
            "SampleMenuThrobberColourLerpCurve|134|System.Single|System.Single time",
            "SampleFTUEHandBounceCurve|134|System.Single|System.Single time",
            "SampleFTUEHandUrgencyCurve|134|System.Single|System.Single time",
            "SampleEffectCurveSmooth|134|System.Single|System.Single time",
            "SampleEffectCurvePulse|134|System.Single|System.Single time",
            "SamplePanelShineLengthCurve|134|System.Single|System.Single time",
            "SamplePanelShineThicknessCurve|134|System.Single|System.Single time",
            "SamplePanelShineAlphaCurve|134|System.Single|System.Single time",
            "SampleUIScaleChangerIncreaseCurve|134|System.Single|System.Single time",
            "SampleUIScaleChangerDecreaseCurve|134|System.Single|System.Single time",
            "SampleAttackFeedbackAlphaCurve|134|System.Single|System.Single time",
            "get_ReferencePixelsPerUnit|2182|System.Single|",
            "set_ReferencePixelsPerUnit|2182|System.Void|System.Single value",
            ".ctor|6278|System.Void|",
        };

        private static readonly string[] ManagerFields =
        {
            "SubPageLeftRightEdgeBufferPixelsPortrait|32854|System.Single",
            "SubPageLeftRightEdgeBufferPixelsLandscape|32854|System.Single",
            "FadeInAlpha|32854|System.Single",
            "FadeOutAlpha|32854|System.Single",
            "m_onPressBounceCurve|1|UnityEngine.AnimationCurve",
            "m_onReleaseBounceCurve|1|UnityEngine.AnimationCurve",
            "m_onDisabledPressShakeCurve|1|UnityEngine.AnimationCurve",
            "m_disableWobbleScaler|1|UnityEngine.Vector2",
            "m_onEnableBounceCurve|1|UnityEngine.AnimationCurve",
            "m_uiUnclampedSlideAnimationCurve|1|UnityEngine.AnimationCurve",
            "m_uiClampedSlideAnimationCurve|1|UnityEngine.AnimationCurve",
            "m_bubblePowerupPopCurve|1|UnityEngine.AnimationCurve",
            "m_menuThrobberColourLerpCurve|1|UnityEngine.AnimationCurve",
            "m_FTUEHandBounceCurve|1|UnityEngine.AnimationCurve",
            "m_FTUEHandUrgencyOverTime|1|UnityEngine.AnimationCurve",
            "m_UIScaleChangerIncreaseCurve|1|UnityEngine.AnimationCurve",
            "m_UIScaleChangerDecreaseCurve|1|UnityEngine.AnimationCurve",
            "m_effectCurveSmooth|1|UnityEngine.AnimationCurve",
            "m_effectCurvePulse|1|UnityEngine.AnimationCurve",
            "m_panelShineLength|1|UnityEngine.AnimationCurve",
            "m_panelShineThickness|1|UnityEngine.AnimationCurve",
            "m_panelShineAlpha|1|UnityEngine.AnimationCurve",
            "m_attackFeedbackAlphaCurve|1|UnityEngine.AnimationCurve",
            "m_disabledButtonSprite|1|UnityEngine.Sprite",
            "m_disabledSecondaryButtonSprite|1|UnityEngine.Sprite",
            "m_disabledPrimaryTextMeshProMaterialPreset|1|UnityEngine.Material",
            "m_disabledSecondaryTextMeshProMaterialPreset|1|UnityEngine.Material",
            "m_disabledButtonFaceColour|1|UnityEngine.Color",
            "m_disabledButtonTextColour|1|UnityEngine.Color",
            "m_disabledButtonTextOutlineColour|1|UnityEngine.Color",
            "m_disabledGradientStartColour|1|UnityEngine.Color",
            "m_disabledGradientEndColour|1|UnityEngine.Color",
            "m_referencePixelsPerUnit|1|System.Single",
            "m_audioCueList|1|System.Collections.Generic.List`1<Hardlight.HLUIEffectManager+HLAudioCueKVP>",
            "m_audioCues|1|System.Collections.Generic.Dictionary`2<Hardlight.HLAudioTypes,UnityEngine.AudioClip>",
        };

        private static readonly string[][] ManagerFieldAttributes =
        {
            new string[] {  },
            new string[] {  },
            new string[] {  },
            new string[] {  },
            new string[] { "UnityEngine.TooltipAttribute|Curve to sample from to shrink UI elements on press down", "UnityEngine.SerializeField|", "UnityEngine.HeaderAttribute|Sample curves" },
            new string[] { "UnityEngine.TooltipAttribute|Curve to sample from to bounce UI elements on press complete", "UnityEngine.SerializeField|" },
            new string[] { "UnityEngine.TooltipAttribute|Curve to sample from to shake UI elements on press when disabled", "UnityEngine.SerializeField|" },
            new string[] { "UnityEngine.TooltipAttribute|Value to scale disabled curve value along X and y", "UnityEngine.SerializeField|" },
            new string[] { "UnityEngine.TooltipAttribute|Curve to sample from to bounce UI elements on enable", "UnityEngine.SerializeField|" },
            new string[] { "UnityEngine.SerializeField|", "UnityEngine.TooltipAttribute|Curve to sample from for the slide on/off animation stretch or bounce" },
            new string[] { "UnityEngine.TooltipAttribute|Curve to sample from for the slide on/off animation when clamped", "UnityEngine.SerializeField|" },
            new string[] { "UnityEngine.SerializeField|", "UnityEngine.TooltipAttribute|Curve to sample from for bubble popping effect" },
            new string[] { "UnityEngine.SerializeField|", "UnityEngine.TooltipAttribute|Curve to sample from for menu throbber colour lerp" },
            new string[] { "UnityEngine.SerializeField|", "UnityEngine.TooltipAttribute|Curve to control the FTUE hand bounce" },
            new string[] { "UnityEngine.TooltipAttribute|Curve to control the speed of the FTUE hand bounce over time", "UnityEngine.SerializeField|" },
            new string[] { "UnityEngine.TooltipAttribute|Curve to control the way the UI scale changer acts when scaling up", "UnityEngine.SerializeField|" },
            new string[] { "UnityEngine.TooltipAttribute|Curve to control the way the UI scale changer acts when scaling down", "UnityEngine.SerializeField|" },
            new string[] { "UnityEngine.HeaderAttribute|Common Effect Curves", "UnityEngine.SerializeField|" },
            new string[] { "UnityEngine.SerializeField|" },
            new string[] { "UnityEngine.HeaderAttribute|Panel Shine Effect Curves", "UnityEngine.SerializeField|" },
            new string[] { "UnityEngine.SerializeField|" },
            new string[] { "UnityEngine.SerializeField|" },
            new string[] { "UnityEngine.SerializeField|", "UnityEngine.HeaderAttribute|Attack Feedback Curves" },
            new string[] { "UnityEngine.TooltipAttribute|Disabled primary button background image", "UnityEngine.SerializeField|", "UnityEngine.HeaderAttribute|Disabled Effects" },
            new string[] { "UnityEngine.SerializeField|", "UnityEngine.TooltipAttribute|Disabled secondary button background image" },
            new string[] { "UnityEngine.SerializeField|", "UnityEngine.TooltipAttribute|Disabled TextMeshPro Material Preset" },
            new string[] { "UnityEngine.SerializeField|", "UnityEngine.TooltipAttribute|Disabled TextMeshPro Material Preset" },
            new string[] { "UnityEngine.TooltipAttribute|Disabled button face colour", "UnityEngine.SerializeField|" },
            new string[] { "UnityEngine.TooltipAttribute|Disabled button text colour", "UnityEngine.SerializeField|" },
            new string[] { "UnityEngine.TooltipAttribute|Disabled button text outline colour", "UnityEngine.SerializeField|" },
            new string[] { "UnityEngine.TooltipAttribute|This is the start colour for any disabled gradient", "UnityEngine.SerializeField|" },
            new string[] { "UnityEngine.TooltipAttribute|This is the end colour for any disabled gradient", "UnityEngine.SerializeField|" },
            new string[] { "UnityEngine.TooltipAttribute|This value should match the Reference Pixels Per Unit used on the Canvas Scaler components", "UnityEngine.SerializeField|", "UnityEngine.HeaderAttribute|Global Values" },
            new string[] { "UnityEngine.TooltipAttribute|This List Will Become A Dictionary of KeyVal Pairs for look up during runtime", "UnityEngine.SerializeField|", "UnityEngine.HeaderAttribute|Button Audio" },
            new string[] {  },
        };
    }
}
