using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of the restored HLSplines.Runtime metadata value types, MetadataFixedGroup,
    // MetadataTestMonoBehaviour, SplineEditorDefinition, StandaloneAutoProfiler and the two
    // edit-mode mesh visualisations, against the shipped 1.10.1 x86-64 bodies. Each case owns
    // every object it creates and restores the two editor-definition default caches it sets.
    [TestFixture]
    public sealed class OriginalSplineMetadataTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const string Unhandled = "Unhandled interpolation type.";
        const float Tolerance = 1e-5f;

        static FieldInfo Field(Type type, string name) =>
            type.GetField(name, Any | BindingFlags.DeclaredOnly) ?? throw new MissingFieldException(type.FullName, name);
        static object Get(object target, Type owner, string name) => Field(owner, name).GetValue(target);
        static void Set(object target, Type owner, string name, object value) => Field(owner, name).SetValue(target, value);

        static object Call(object target, string name, params object[] args) => CallAs(target.GetType(), target, name, args);

        // Private members of a base class are only visible through that class.
        static object CallAs(Type owner, object target, string name, params object[] args)
        {
            MethodInfo method = owner.GetMethod(name, Any) ?? throw new MissingMethodException(owner.FullName, name);
            try { return method.Invoke(target, args); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        static string Label(MetadataValueType valueType) => (string)Get(valueType, typeof(MetadataValueType), "m_label");
        static string HelpText(MetadataValueType valueType) => (string)Get(valueType, typeof(MetadataValueType), "m_helpText");

        // Owns Unity objects and both editor-definition caches for one case.
        sealed class Owned : IDisposable
        {
            readonly List<Object> objects = new List<Object>();
            readonly FieldInfo splineCache = Field(typeof(SplineEditorDefinition), "s_default");
            readonly FieldInfo ribbonCache = Field(typeof(RibbonEditorDefinition), "s_default");
            readonly object splineDefault, ribbonDefault;
            bool cachesTaken;

            public Owned()
            {
                splineDefault = splineCache.GetValue(null);
                ribbonDefault = ribbonCache.GetValue(null);
            }

            public T Add<T>(T value) where T : Object { objects.Add(value); return value; }
            public T Create<T>() where T : ScriptableObject => Add(ScriptableObject.CreateInstance<T>());

            public GameObject Inactive(string name, Transform parent = null)
            {
                var gameObject = Add(new GameObject(name));
                gameObject.SetActive(false);
                if (parent != null) gameObject.transform.SetParent(parent, false);
                return gameObject;
            }

            public Material Material()
            {
                Shader shader = Shader.Find("Hidden/InternalErrorShader");
                Assert.That(shader, Is.Not.Null, "installed internal shader");
                return Add(new Material(shader));
            }

            // Installs owned definitions as Default without touching Resources.
            public void Definitions(SplineEditorDefinition spline, RibbonEditorDefinition ribbon)
            {
                cachesTaken = true;
                splineCache.SetValue(null, spline);
                ribbonCache.SetValue(null, ribbon);
            }

            public void Dispose()
            {
                if (cachesTaken)
                {
                    splineCache.SetValue(null, splineDefault);
                    ribbonCache.SetValue(null, ribbonDefault);
                }
                for (int i = objects.Count - 1; i >= 0; --i)
                    if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            }
        }

        static Metadata Row(MetadataKeyType key, string value) => new Metadata(key, value);

        // Constructors share MetadataValueType's field initialisers ("Value", ""); each Reset
        // (0x1a55040 Bool, 0x1a55330 Float, 0x1a558b0 String, 0x1a55ab0 StringList, 0x1a55760
        // Null, 0x17ca400 TEnum) calls the base Reset then stores its own literal. TObject Reset
        // (0x17ca7a0) stores typeof(TObject).Name through RuntimeType vtable slot 8 (get_Name).
        [Test]
        public void ConstructorsKeepTheBaseLabelAndResetStoresEachShippedLabel()
        {
            using (var owned = new Owned())
            {
                var expected = new (MetadataValueType type, string label)[]
                {
                    (owned.Create<MetadataValueTypeBool>(), "Bool"),
                    (owned.Create<MetadataValueTypeFloat>(), "Float"),
                    (owned.Create<MetadataValueTypeString>(), "Text"),
                    (owned.Create<MetadataValueTypeStringList>(), "Text"),
                    (owned.Create<MetadataValueTypeNull>(), "Null"),
                    (owned.Create<MetadataValueTypeTestEnum>(), "Enum"),
                    (owned.Create<MetadataValueTypeObject>(), "Object"),
                    (owned.Create<MetadataValueTypeScriptableObject>(), "ScriptableObject"),
                };
                foreach (var row in expected)
                {
                    string name = row.type.GetType().Name;
                    Assert.That(Label(row.type), Is.EqualTo("Value"), name + " constructor label");
                    Assert.That(HelpText(row.type), Is.EqualTo(""), name + " constructor help text");
                    Assert.That(row.type.GetGUID(), Is.EqualTo(""), name + " Guid initialiser");
                    Call(row.type, "Reset");
                    Assert.That(Label(row.type), Is.EqualTo(row.label), name + " Reset label");
                    Assert.That(HelpText(row.type), Is.EqualTo(""), name + " Reset leaves help text");
                }

                // 0x1a55a10: m_regEx is "" and m_default stays null; 0x1a55c10 allocates m_strings.
                var text = owned.Create<MetadataValueTypeString>();
                Assert.That(Get(text, typeof(MetadataValueTypeString), "m_regEx"), Is.EqualTo(""));
                var list = owned.Create<MetadataValueTypeStringList>();
                var strings = (List<string>)Get(list, typeof(MetadataValueTypeStringList), "m_strings");
                Assert.That(strings, Is.Not.Null.And.Empty);
                Assert.That(strings, Is.Not.SameAs(Get(owned.Create<MetadataValueTypeStringList>(), typeof(MetadataValueTypeStringList), "m_strings")));
            }
        }

        // Bool 0x1a55090 boxes the private BoolValue enum and calls Enum.ToString; Float
        // 0x1a55380 tail-calls Single.ToString on m_default; String/StringList 0x1a55900/
        // 0x1a55b00 return the m_default reference; TEnum 0x17ca460 calls Enum.ToString on the
        // field; TObject 0x17ca830 returns String.Empty; Null has no override (base "").
        [Test]
        public void DefaultsAreFormattedFromEachOwnDefaultField()
        {
            using (var owned = new Owned())
            {
                var boolean = owned.Create<MetadataValueTypeBool>();
                Assert.That(boolean.GetDefaultAsString(), Is.EqualTo("False"));
                FieldInfo boolDefault = Field(typeof(MetadataValueTypeBool), "m_default");
                boolDefault.SetValue(boolean, Enum.ToObject(boolDefault.FieldType, 1));
                Assert.That(boolean.GetDefaultAsString(), Is.EqualTo("True"), "enum member name, not bool or int text");
                boolDefault.SetValue(boolean, Enum.ToObject(boolDefault.FieldType, 7));
                Assert.That(boolean.GetDefaultAsString(), Is.EqualTo("7"), "an unnamed enum value prints its number");

                var single = owned.Create<MetadataValueTypeFloat>();
                Set(single, typeof(MetadataValueTypeFloat), "m_default", 2.5f);
                Set(single, typeof(MetadataValueTypeFloat), "m_minimum", 9f);
                Set(single, typeof(MetadataValueTypeFloat), "m_hasMinimum", true);
                Assert.That(single.GetDefaultAsString(), Is.EqualTo(2.5f.ToString()), "minimum does not clamp the default");

                var text = owned.Create<MetadataValueTypeString>();
                Assert.That(text.GetDefaultAsString(), Is.Null, "unset default is not replaced by \"\"");
                string authored = new string('x', 3);
                Set(text, typeof(MetadataValueTypeString), "m_default", authored);
                Set(text, typeof(MetadataValueTypeString), "m_regEx", "^y$");
                Assert.That(text.GetDefaultAsString(), Is.SameAs(authored), "regex is not applied");

                var list = owned.Create<MetadataValueTypeStringList>();
                Set(list, typeof(MetadataValueTypeStringList), "m_default", authored);
                Assert.That(list.GetDefaultAsString(), Is.SameAs(authored), "default need not be in m_strings");

                var testEnum = owned.Create<MetadataValueTypeTestEnum>();
                Assert.That(testEnum.GetDefaultAsString(), Is.EqualTo("ValueA"));
                Set(testEnum, typeof(MetadataValueTypeTEnum<TestEnum>), "m_default", TestEnum.ValueC);
                Assert.That(testEnum.GetDefaultAsString(), Is.EqualTo("ValueC"));
                Assert.That(CallAs(typeof(MetadataValueTypeTEnum<TestEnum>), testEnum, "GetDefaultValue"), Is.EqualTo(TestEnum.ValueC), "0x17ca450 returns the field");

                Assert.That(owned.Create<MetadataValueTypeObject>().GetDefaultAsString(), Is.SameAs(string.Empty));
                Assert.That(owned.Create<MetadataValueTypeScriptableObject>().GetDefaultAsString(), Is.SameAs(string.Empty));
                Assert.That(owned.Create<MetadataValueTypeNull>().GetDefaultAsString(), Is.EqualTo(""));
            }
        }

        // Constant interpolation is `ucomiss 1f, t` + cmova (Bool 0x1a551e3, String 0x1a5595e,
        // StringList 0x1a55b5e) or `cmpltss t, 1f` (Float 0x1a5541d): t < 1 keeps a, and both
        // t >= 1 and NaN take b. Linear is only implemented for Float (clamped Lerp, 0x1a55431);
        // every other type, and Float's unknown types, throw System.Exception(Unhandled).
        [Test]
        public void ConstantSelectsAUnlessTReachesOneOrIsNaN()
        {
            using (var owned = new Owned())
            {
                var key = owned.Create<MetadataKeyType>();
                var other = owned.Create<MetadataKeyType>();
                float[] keepsA = { 0f, 0.999f, -1f, float.NegativeInfinity };
                float[] takesB = { 1f, 1.001f, float.PositiveInfinity, float.NaN };

                var text = owned.Create<MetadataValueTypeString>();
                var list = owned.Create<MetadataValueTypeStringList>();
                var testEnum = owned.Create<MetadataValueTypeTestEnum>();
                string a = new string('a', 2), b = new string('b', 2);
                foreach (MetadataValueType type in new MetadataValueType[] { text, list, testEnum })
                {
                    foreach (float t in keepsA)
                    {
                        type.Interpolate(Row(key, a), Row(other, b), t, MetadataInterpolationType.Constant, out Metadata result);
                        Assert.That(result.Value, Is.SameAs(a), type.GetType().Name + " t=" + t);
                        Assert.That(result.Key, Is.SameAs(key), "key comes from a");
                    }
                    foreach (float t in takesB)
                    {
                        type.Interpolate(Row(key, a), Row(other, b), t, MetadataInterpolationType.Constant, out Metadata result);
                        Assert.That(result.Value, Is.SameAs(b), type.GetType().Name + " t=" + t);
                        Assert.That(result.Key, Is.SameAs(key), "key comes from a even when b's value is used");
                    }
                    Assert.That(() => type.Interpolate(Row(key, a), Row(other, b), 0f, MetadataInterpolationType.Linear, out _),
                        Throws.Exception.TypeOf<Exception>().With.Message.EqualTo(Unhandled), type.GetType().Name + " Linear");
                }

                // TEnum selects the stored text verbatim; it is never parsed into TEnum.
                testEnum.Interpolate(Row(key, "NotATestEnum"), Row(key, "ValueB"), 0f, MetadataInterpolationType.Constant, out Metadata raw);
                Assert.That(raw.Value, Is.EqualTo("NotATestEnum"));

                var boolean = owned.Create<MetadataValueTypeBool>();
                foreach (float t in keepsA)
                {
                    boolean.Interpolate(Row(key, "TRUE"), Row(other, "false"), t, MetadataInterpolationType.Constant, out Metadata result);
                    Assert.That(result.Value, Is.EqualTo("True"), "Boolean.TryParse then Boolean.ToString, t=" + t);
                    Assert.That(result.Key, Is.SameAs(key));
                }
                foreach (float t in takesB)
                {
                    boolean.Interpolate(Row(key, "false"), Row(other, " true "), t, MetadataInterpolationType.Constant, out Metadata result);
                    Assert.That(result.Value, Is.EqualTo("True"), "b is parsed with whitespace trimmed, t=" + t);
                }
                boolean.Interpolate(Row(key, "yes"), Row(key, "true"), 0f, MetadataInterpolationType.Constant, out Metadata malformed);
                Assert.That(malformed.Value, Is.EqualTo("False"), "a failed parse keeps the false out value");
                Assert.That(() => boolean.Interpolate(Row(key, "true"), Row(key, "true"), 0f, MetadataInterpolationType.Linear, out _),
                    Throws.Exception.TypeOf<Exception>().With.Message.EqualTo(Unhandled));

                var single = owned.Create<MetadataValueTypeFloat>();
                Set(single, typeof(MetadataValueTypeFloat), "m_hasMaximum", true);
                Set(single, typeof(MetadataValueTypeFloat), "m_maximum", 1f);
                foreach (float t in keepsA)
                {
                    single.Interpolate(Row(key, "2"), Row(other, "6"), t, MetadataInterpolationType.Constant, out Metadata result);
                    Assert.That(result.Value, Is.EqualTo(2f.ToString()), "t=" + t);
                    Assert.That(result.Key, Is.SameAs(key));
                }
                foreach (float t in takesB)
                {
                    single.Interpolate(Row(key, "2"), Row(other, "6"), t, MetadataInterpolationType.Constant, out Metadata result);
                    Assert.That(result.Value, Is.EqualTo(6f.ToString()), "maximum does not clamp, t=" + t);
                }
            }
        }

        // Float Linear (0x1a55431): min(1, t), then t < 0 becomes 0, then a + (b - a) * t.
        [Test]
        public void FloatLinearIsAClampedLerpOfTheParsedValues()
        {
            using (var owned = new Owned())
            {
                var key = owned.Create<MetadataKeyType>();
                var single = owned.Create<MetadataValueTypeFloat>();
                var rows = new (string a, string b, float t, float expected)[]
                {
                    ("2", "6", 0.25f, 3f),
                    ("2", "6", 0.75f, 5f),
                    ("2", "6", 2f, 6f),
                    ("2", "6", -3f, 2f),
                    ("6", "2", 0.5f, 4f),
                    ("bad", "8", 0.5f, 4f),
                };
                foreach (var row in rows)
                {
                    single.Interpolate(Row(key, row.a), Row(key, row.b), row.t, MetadataInterpolationType.Linear, out Metadata result);
                    Assert.That(result.Value, Is.EqualTo(row.expected.ToString()), row.a + ".." + row.b + " t=" + row.t);
                }
                Assert.That(() => single.Interpolate(Row(key, "1"), Row(key, "2"), 0f, (MetadataInterpolationType)2, out _),
                    Throws.Exception.TypeOf<Exception>().With.Message.EqualTo(Unhandled));
            }
        }

        // TObject Interpolate (0x17ca870) builds Metadata(a.m_key, a.m_value) through the string
        // constructor 0x0600000e: b, t and the type are never read and a's object is dropped.
        // Null Interpolate (0x1a557b0) stores a itself.
        [Test]
        public void ObjectTypesCopyOnlyAsStringAndNullReturnsA()
        {
            using (var owned = new Owned())
            {
                var key = owned.Create<MetadataKeyType>();
                var target = owned.Create<MetadataKeyType>();
                foreach (MetadataValueType type in new MetadataValueType[] { owned.Create<MetadataValueTypeObject>(), owned.Create<MetadataValueTypeScriptableObject>() })
                {
                    var withObject = new Metadata(key, target);
                    type.Interpolate(withObject, null, float.NaN, (MetadataInterpolationType)9, out Metadata result);
                    Assert.That(result, Is.Not.SameAs(withObject));
                    Assert.That(result.Key, Is.SameAs(key));
                    Assert.That(result.ValueObject, Is.Null, type.GetType().Name + " drops the object reference");
                    Assert.That(result.Value, Is.Null);
                    string text = new string('t', 1);
                    type.Interpolate(Row(key, text), Row(target, "b"), 1f, MetadataInterpolationType.Constant, out result);
                    Assert.That(result.Value, Is.SameAs(text), "a's value even when t selects b elsewhere");
                }

                var none = owned.Create<MetadataValueTypeNull>();
                Metadata a = Row(key, "a");
                none.Interpolate(a, null, 1f, (MetadataInterpolationType)9, out Metadata same);
                Assert.That(same, Is.SameAs(a));
            }
        }

        // MetadataFixedGroup .ctor (0x1a53960) allocates its MetadataFixedGroupData (and that
        // data's list) before ScriptableObjectWithGuid's constructor; get_Metadata (0x1a53950)
        // returns m_data.m_metadata (+0x20 then +0x18).
        [Test]
        public void FixedGroupExposesItsOwnDataListToTheGroupQueries()
        {
            using (var owned = new Owned())
            {
                var group = owned.Create<MetadataFixedGroup>();
                var data = (MetadataFixedGroupData)Get(group, typeof(MetadataFixedGroup), "m_data");
                Assert.That(data, Is.Not.Null);
                var rows = (List<MetadataFixed>)Get(data, typeof(MetadataFixedGroupData), "m_metadata");
                Assert.That(group.Metadata, Is.SameAs(rows));
                Assert.That(Get(owned.Create<MetadataFixedGroup>(), typeof(MetadataFixedGroup), "m_data"), Is.Not.SameAs(data));

                var key = owned.Create<MetadataKeyType>();
                Assert.That(group.KeyTypeExists(key), Is.False);
                rows.Add(new MetadataFixed(key, "12"));
                Assert.That(group.TryGetValue(key, out int value), Is.True);
                Assert.That(value, Is.EqualTo(12));
            }
        }

        // 0x1a54880: every serialized container is allocated in field order and the individual
        // row is a MetadataFlexi with a null key and "" value; m_metadataGroup stays null.
        [Test]
        public void MetadataTestBehaviourAllocatesEveryContainer()
        {
            using (var owned = new Owned())
            {
                var probe = owned.Inactive("Lucid owned metadata test behaviour").AddComponent<MetadataTestMonoBehaviour>();
                Type type = typeof(MetadataTestMonoBehaviour);
                Assert.That((Object)Get(probe, type, "m_metadataGroup") == null, Is.True);
                Assert.That((List<MetadataGroup>)Get(probe, type, "m_metadataGroupArray"), Is.Not.Null.And.Empty);
                Assert.That(((MetadataFlexiGroupData)Get(probe, type, "m_metadataFlexi")).Metadata, Is.Not.Null.And.Empty);
                Assert.That((List<MetadataFlexiGroupData>)Get(probe, type, "m_metadataFlexiArray"), Is.Not.Null.And.Empty);
                Assert.That(((MetadataFixedGroupData)Get(probe, type, "m_metadataFixed")).Metadata, Is.Not.Null.And.Empty);
                Assert.That((List<MetadataFixedGroupData>)Get(probe, type, "m_metadataFixedArray"), Is.Not.Null.And.Empty);
                var individual = (MetadataFlexi)Get(probe, type, "m_metadataIndividual");
                Assert.That(individual, Is.Not.Null);
                Assert.That(individual.Key == null, Is.True);
                Assert.That(individual.Value, Is.EqualTo(""));
                Assert.That(individual.ValueObject == null, Is.True);
            }
        }

        static SplineRenderQualitySetting Quality(int distance, bool selected = false)
        {
            var setting = new SplineRenderQualitySetting();
            Set(setting, typeof(SplineRenderQualitySetting), "m_splineDrawDistance", distance);
            Set(setting, typeof(SplineRenderQualitySetting), "m_isSelected", selected);
            return setting;
        }

        // .ctor 0x1a723a0 stores 0.5f, quality 1, width 0x3dcccccd (0.1f), delay 1f, snap true
        // and snap distance 5f; getters 0x1a71f10..0x1a71f90 read their fields. The quality pick
        // (0x1a71fa0) inserts one default when empty, squares the int distance (imul) and keeps a
        // strictly covering, strictly smaller square. A disabled Default (0x1a721cf/0x1a72268/
        // 0x1a72306) clears the out setting before any camera or list access.
        [Test]
        public void SplineDefinitionDefaultsQualityPickAndDisabledRendering()
        {
            using (var owned = new Owned())
            {
                var definition = owned.Create<SplineEditorDefinition>();
                Assert.That(definition.AutoLengthUpdateInterval, Is.EqualTo(0.5f));
                Assert.That(definition.GeneratedMeshQuality, Is.EqualTo(1));
                Assert.That(definition.GeneratedMeshWidth, Is.EqualTo(0.1f));
                Assert.That(definition.GeneratedMeshesSaveDelaySeconds, Is.EqualTo(1f));
                Assert.That(definition.SnapToMesh, Is.True);
                Assert.That(definition.MeshSnapDistance, Is.EqualTo(5f));
                Assert.That(definition.RenderWithDepthTest, Is.False);
                Assert.That(definition.VisualisationMaterial == null, Is.True);
                Assert.That(definition.GeneratedMeshesLocation, Is.Null);
                Assert.That(Get(definition, typeof(SplineEditorDefinition), "m_disableRendering"), Is.EqualTo(false));
                Set(definition, typeof(SplineEditorDefinition), "m_meshSnapDistance", -2f);
                Set(definition, typeof(SplineEditorDefinition), "m_generatedMeshWidth", 4f);
                Assert.That(definition.MeshSnapDistance, Is.EqualTo(-2f));
                Assert.That(definition.GeneratedMeshWidth, Is.EqualTo(4f), "Range attribute has no runtime clamp");

                SplineRenderQualitySetting Pick(bool selected, float distance) =>
                    (SplineRenderQualitySetting)Call(definition, "GetRenderQualitySetting", selected, distance);
                Assert.That(() => Pick(false, 0f), Throws.TypeOf<NullReferenceException>(), "a null list is not repaired");
                var list = new List<SplineRenderQualitySetting>();
                Set(definition, typeof(SplineEditorDefinition), "m_renderQualitySettings", list);
                SplineRenderQualitySetting inserted = Pick(false, 0f);
                Assert.That(list, Has.Count.EqualTo(1));
                Assert.That(inserted, Is.SameAs(list[0]), "the inserted 1000-unit default covers distance 0");
                SplineRenderQualitySetting near = Quality(4), far = Quality(10), equal = Quality(4), selectedNear = Quality(2, true);
                list.Clear(); list.Add(far); list.Add(near); list.Add(equal); list.Add(selectedNear);
                Assert.That(Pick(false, 15f), Is.SameAs(near), "smallest covering square; the equal later one is not smaller");
                Assert.That(Pick(false, 16f), Is.SameAs(far), "16 is not strictly below 4*4");
                Assert.That(Pick(false, 100f), Is.Null);
                Assert.That(Pick(false, float.NaN), Is.Null);
                Assert.That(Pick(true, 3f), Is.SameAs(selectedNear));
                Assert.That(Pick(true, 4f), Is.Null);
                list.Clear(); list.Add(Quality(50000));
                Assert.That(Pick(false, 0f), Is.Null, "50000*50000 wraps negative before the float compare");

                owned.Definitions(definition, null);
                Assert.That(SplineEditorDefinition.Default, Is.SameAs(definition), "the cache is returned without loading");
                Set(definition, typeof(SplineEditorDefinition), "m_disableRendering", true);
                list.Clear();
                SplineRenderQualitySetting setting = Quality(1);
                Assert.That(SplineEditorDefinition.ShouldRender(Vector3.zero, false, out setting), Is.False);
                Assert.That(setting, Is.Null);
                setting = Quality(1);
                Assert.That(SplineEditorDefinition.ShouldRender(new Bounds(), false, out setting), Is.False);
                Assert.That(setting, Is.Null);
                setting = Quality(1);
                Assert.That(SplineEditorDefinition.ShouldRender(new Bounds(), true, out setting, Matrix4x4.identity), Is.False);
                Assert.That(setting, Is.Null);
                Assert.That(list, Is.Empty, "disabled rendering never reaches the quality pick");
            }
        }

        // Enabled ShouldRender (0x1a721b0 point, 0x1a722f0 bounds): a negative camera distance
        // clears the setting, then Default is read again (0x1a72210/0x1a72352) for the quality
        // pick and the result is `setting != null && CentreSpline.m_renderQuality != 0`
        // (cmp dword [rax+0x10], 0; setne at 0x1a72240/0x1a72382). The matrix overload
        // (0x1a72250) moves only the centre with MultiplyPoint3x4 and copies the extents
        // (0x1a72299..0x1a722b3). Without Camera.current the distance is float.MaxValue.
        [Test]
        public void EnabledShouldRenderPicksByCameraDistanceAndRenderQuality()
        {
            using (var owned = new Owned())
            {
                var definition = owned.Create<SplineEditorDefinition>();
                var list = new List<SplineRenderQualitySetting>();
                Set(definition, typeof(SplineEditorDefinition), "m_renderQualitySettings", list);
                owned.Definitions(definition, null);
                Assert.That(Get(definition, typeof(SplineEditorDefinition), "m_disableRendering"), Is.EqualTo(false));

                var cameraObject = owned.Add(new GameObject("Lucid owned render camera"));
                cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
                Camera camera = cameraObject.AddComponent<Camera>();
                Camera previous = Camera.current;
                try
                {
                    // 11 * 11 = 121 covers the squared camera distance of 100 to the origin.
                    SplineRenderQualitySetting covering = Quality(11);
                    list.Add(covering);
                    void RenderQuality(int value) =>
                        Set(covering.CentreSpline, typeof(SingleSplineRenderQualitySettings), "m_renderQuality", value);

                    Camera.SetupCurrent(camera);
                    Assert.That(Camera.current, Is.SameAs(camera), "precondition: owned current camera");
                    Assert.That(covering.CentreSpline.RenderQuality, Is.EqualTo(8), "shipped field initialiser");
                    Assert.That(SplineEditorDefinition.ShouldRender(Vector3.zero, false, out SplineRenderQualitySetting setting), Is.True);
                    Assert.That(setting, Is.SameAs(covering));
                    RenderQuality(0);
                    Assert.That(SplineEditorDefinition.ShouldRender(Vector3.zero, false, out setting), Is.False, "quality 0 does not render");
                    Assert.That(setting, Is.SameAs(covering), "the picked setting is still published");
                    RenderQuality(-1);
                    Assert.That(SplineEditorDefinition.ShouldRender(Vector3.zero, false, out setting), Is.True, "any nonzero quality renders, not only > 0");
                    Assert.That(setting, Is.SameAs(covering));
                    Assert.That(SplineEditorDefinition.ShouldRender(Vector3.zero, true, out setting), Is.False, "no selected setting");
                    Assert.That(setting, Is.Null);
                    RenderQuality(8);

                    var unit = new Bounds(Vector3.zero, Vector3.one);
                    Assert.That(SplineEditorDefinition.ShouldRender(unit, false, out setting), Is.True);
                    Assert.That(setting, Is.SameAs(covering));
                    Assert.That(SplineEditorDefinition.ShouldRender(unit, false, out setting, Matrix4x4.identity), Is.True);
                    Assert.That(setting, Is.SameAs(covering));
                    Assert.That(SplineEditorDefinition.ShouldRender(unit, false, out setting, Matrix4x4.Translate(new Vector3(0f, 0f, 20f))), Is.False,
                        "the transformed centre at z = 20 is 900 squared units away");
                    Assert.That(setting, Is.Null);
                    var far = new Bounds(new Vector3(0f, 0f, 50f), Vector3.one);
                    Assert.That(SplineEditorDefinition.ShouldRender(far, false, out setting), Is.False, "precondition: 3600 is not covered");
                    Assert.That(SplineEditorDefinition.ShouldRender(far, false, out setting, Matrix4x4.Translate(new Vector3(0f, 0f, -50f))), Is.True,
                        "the matrix brings the centre back to the origin");
                    Assert.That(setting, Is.SameAs(covering));

                    Camera.SetupCurrent(null);
                    Assert.That(Camera.current == null, Is.True, "precondition: no current camera");
                    list.Clear();
                    Assert.That(SplineEditorDefinition.ShouldRender(Vector3.zero, false, out setting), Is.False,
                        "float.MaxValue is beyond the inserted 1000-unit default");
                    Assert.That(setting, Is.Null);
                    Assert.That(list, Has.Count.EqualTo(1), "the enabled path reaches the quality pick through Default");
                }
                finally
                {
                    Camera.SetupCurrent(previous);
                }
            }
        }

        // get_Default (0x1a71ca0) on a null cache: List(LoadAll("")), Find(name.Contains("Default"))
        // at 0x1a71e91, Remove of that match at 0x1a71ea9, then list[0] replaces the match
        // whenever anything remains (0x1a71eae..0x1a71ec8). So the result is LoadAll[1] when
        // LoadAll[0] is 'Default'-named and more remain, otherwise LoadAll[0]. The prepared
        // project already ships Assets/Resources/DefaultSplineEditorDefinition.asset and
        // SDTSplineEditorDefinition.asset (baseline asset map), so the owned assets join a set
        // whose load order this case does not control.
        [Test]
        public void DefaultTakesTheFirstAssetLeftAfterRemovingTheDefaultNamedMatch()
        {
            string folderName = "LucidOwnedSplineDefinitions_" + Guid.NewGuid().ToString("N");
            string folder = "Assets/" + folderName;
            using (var owned = new Owned())
            {
                try
                {
                    Assert.That(UnityEditor.AssetDatabase.IsValidFolder(folder), Is.False);
                    Assert.That(UnityEditor.AssetDatabase.CreateFolder("Assets", folderName), Is.Not.Empty);
                    Assert.That(UnityEditor.AssetDatabase.CreateFolder(folder, "Resources"), Is.Not.Empty);
                    string named = folder + "/Resources/Lucid Owned Default Spline Definition.asset";
                    string other = folder + "/Resources/Lucid Owned Other Spline Definition.asset";
                    UnityEditor.AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<SplineEditorDefinition>(), named);
                    UnityEditor.AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<SplineEditorDefinition>(), other);
                    var namedAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<SplineEditorDefinition>(named);
                    var otherAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<SplineEditorDefinition>(other);

                    SplineEditorDefinition Resolve(out SplineEditorDefinition[] loaded)
                    {
                        owned.Definitions(null, null);
                        loaded = Resources.LoadAll<SplineEditorDefinition>("");
                        return SplineEditorDefinition.Default;
                    }

                    SplineEditorDefinition first = Resolve(out SplineEditorDefinition[] all);
                    Assert.That(all, Has.Member(namedAsset).And.Member(otherAsset),"precondition: both owned assets load");
                    SplineEditorDefinition expected = all[0].name.Contains("Default") ? all[1] : all[0];
                    Assert.That(first, Is.SameAs(expected));
                    SplineEditorDefinition match = Array.Find(all, asset => asset.name.Contains("Default"));
                    Assert.That(first, Is.Not.SameAs(match), "the 'Default'-named match loses to whatever remains");

                    Assert.That(UnityEditor.AssetDatabase.DeleteAsset(other), Is.True);
                    SplineEditorDefinition second = Resolve(out all);
                    Assert.That(all, Has.Member(namedAsset), "precondition: the named asset still loads");
                    Assert.That(Array.IndexOf(all, otherAsset), Is.EqualTo(-1), "precondition: the other asset is gone");
                    expected = all.Length > 1 && all[0].name.Contains("Default") ? all[1] : all[0];
                    Assert.That(second, Is.SameAs(expected), all.Length == 1 ? "a lone match is kept" : "first remaining asset");
                }
                finally
                {
                    if (UnityEditor.AssetDatabase.IsValidFolder(folder))
                        Assert.That(UnityEditor.AssetDatabase.DeleteAsset(folder), Is.True, "delete only this case's owned folder");
                }
            }
        }

        // Start (0x1a767a0) runs only when Debug.isDebugBuild (always in the Editor) and passes
        // FindObjectsOfTypeInActiveScene<ISurface>(true) to CheckForHiddenAndInactiveSurfaces with
        // logSurfaceName = false (edx = 0): totals are logged, names never are. The constructor
        // (0x1a76850) allocates the profiler.
        [Test]
        public void StandaloneProfilerReportsTotalsIncludingInactiveSurfaces()
        {
            using (var owned = new Owned())
            {
                var profilerObject = owned.Inactive("Lucid owned standalone profiler");
                var profiler = profilerObject.AddComponent<StandaloneAutoProfiler>();
                var helper = Get(profiler, typeof(StandaloneAutoProfiler), "m_surfaceSetupProfiler");
                Assert.That(helper, Is.InstanceOf<SurfaceSetupProfiler>());
                Assert.That(Get(owned.Inactive("Lucid owned second profiler").AddComponent<StandaloneAutoProfiler>(),
                    typeof(StandaloneAutoProfiler), "m_surfaceSetupProfiler"), Is.Not.SameAs(helper));

                owned.Inactive("Lucid owned inactive surface").AddComponent<EditableSpline>();
                var hidden = owned.Inactive("Lucid owned hidden surface");
                hidden.AddComponent<EditableSpline>();
                hidden.hideFlags = HideFlags.HideInHierarchy;
                hidden.SetActive(true);

                LogAssert.Expect(LogType.Warning, new Regex(@"^\[SurfaceSetupProfiler\] Total hidden ISurface: [1-9][0-9]*\.$"));
                LogAssert.Expect(LogType.Warning, new Regex(@"^\[SurfaceSetupProfiler\] Total inactive ISurface: [1-9][0-9]*\.$"));
                Call(profiler, "Start");
                LogAssert.NoUnexpectedReceived();
            }
        }

        // Builds an inactive editable spline with straight knots along a local direction.
        static EditableSpline StraightSpline(Owned owned, Transform parent, Vector3 direction, params float[] distances)
        {
            var splineObject = owned.Inactive("Lucid owned editable spline", parent);
            var spline = splineObject.AddComponent<EditableSpline>();
            foreach (float distance in distances)
            {
                var knot = new GameObject("Lucid owned spline knot");
                knot.transform.SetParent(splineObject.transform, false);
                knot.transform.localPosition = direction * distance;
                spline.Knots.Add(knot.AddComponent<SplineKnot>());
            }
            return spline;
        }

        static EditableRibbon StraightRibbon(Owned owned, Transform parent, params float[] z)
        {
            var ribbonObject = owned.Inactive("Lucid owned editable ribbon", parent);
            var ribbon = ribbonObject.AddComponent<EditableRibbon>();
            foreach (float knotZ in z)
            {
                var knot = new GameObject("Lucid owned ribbon knot");
                knot.transform.SetParent(ribbonObject.transform, false);
                knot.transform.localPosition = new Vector3(0f, 0f, knotZ);
                ribbon.Knots.Add(knot.AddComponent<RibbonKnot>());
            }
            ribbon.Initialise();
            return ribbon;
        }

        static void Near(Vector3 actual, Vector3 expected, string message) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + ": " + actual + " vs " + expected);
        static void Near(Vector2 actual, Vector2 expected, string message) =>
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(Tolerance), message + ": " + actual + " vs " + expected);

        static (SplineEditorDefinition spline, RibbonEditorDefinition ribbon, Material ribbonMaterial) Definitions(Owned owned, int splineQuality, float width, int ribbonQuality)
        {
            var spline = owned.Create<SplineEditorDefinition>();
            var ribbon = owned.Create<RibbonEditorDefinition>();
            Material ribbonMaterial = owned.Material();
            Set(spline, typeof(SplineEditorDefinition), "m_generatedMeshQuality", splineQuality);
            Set(spline, typeof(SplineEditorDefinition), "m_generatedMeshWidth", width);
            Set(spline, typeof(SplineEditorDefinition), "m_visualisationMaterial", owned.Material());
            Set(spline, typeof(SplineEditorDefinition), "m_generatedMeshesSaveDelaySeconds", 100f);
            Set(ribbon, typeof(RibbonEditorDefinition), "m_generatedMeshQuality", ribbonQuality);
            Set(ribbon, typeof(RibbonEditorDefinition), "m_visualisationMaterial", ribbonMaterial);
            Set(ribbon, typeof(RibbonEditorDefinition), "m_generatedMeshesSaveDelaySeconds", 200f);
            owned.Definitions(spline, ribbon);
            return (spline, ribbon, ribbonMaterial);
        }

        // SplineVisualisation.GenerateMesh (0x1a97630). Per step of 1/quality on each knot pair:
        // p0, p1 from GetLocalPosFromKnotT; vertices p0+left*w, p1+right*w, p0+right*w, p1+left*w
        // (Vector3.left/right statics at +0x30/+0x3c, so local x whatever the spline direction);
        // triangles b,b+1,b+2,b,b+3,b+1; uvs (0,v) (1,v') (1,v) (0,v') with v' = v + 2|p1-p0|
        // (sqrtss then addss x,x at 0x1a97ebd). The renderer of a new object takes the RIBBON
        // definition's material (RibbonEditorDefinition+0x20 at 0x1a977f0); the mesh is named
        // after the visualisation transform, uploaded non-readable, and the transform's
        // gameObject is returned. An existing object is reused without touching its renderer.
        [Test]
        public void SplineMeshIsAFixedWidthStripWithChordLengthV()
        {
            using (var owned = new Owned())
            {
                var defs = Definitions(owned, 2, 0.25f, 3);
                var owner = owned.Inactive("Lucid owned spline visualisation");
                var visualisation = owner.AddComponent<SplineVisualisation>();
                Assert.That(owner.GetComponent<Spline>(), Is.Not.Null, "RequireComponent(Spline)");
                // A diagonal line: the side offsets stay on local x rather than following the path.
                EditableSpline spline = StraightSpline(owned, owner.transform, new Vector3(0.6f, 0f, 0.8f), 0f, 5f, 10f);
                var verts = (List<Vector3>)Get(visualisation, typeof(SplineVisualisation), "m_meshVerts");
                var tris = (List<int>)Get(visualisation, typeof(SplineVisualisation), "m_meshTriangles");
                var uvs = (List<Vector2>)Get(visualisation, typeof(SplineVisualisation), "m_meshUVs");
                verts.Add(Vector3.one); tris.Add(99); uvs.Add(Vector2.one);

                var generated = (GameObject)Call(visualisation, "GenerateMesh", null, spline);
                owned.Add(generated);
                Assert.That(generated.name, Is.EqualTo("Lucid owned spline visualisation[Visualisation]"), "named after this object, not the spline");
                Assert.That(generated.transform.parent, Is.SameAs(spline.transform));
                Assert.That(generated.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(generated.transform.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(generated.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(defs.ribbonMaterial), "ribbon definition material");

                const float width = 0.25f;
                int quads = 2 * 2;
                Assert.That(verts, Has.Count.EqualTo(4 * quads), "lists are cleared before filling");
                Assert.That(tris, Has.Count.EqualTo(6 * quads));
                Assert.That(uvs, Has.Count.EqualTo(4 * quads));
                float v = 0f;
                int quad = 0;
                for (int knot = 0; knot < 2; ++knot)
                {
                    for (int i = 0; i < 2; ++i, ++quad)
                    {
                        Vector3 p0 = spline.GetLocalPosFromKnotT(new KnotT(knot, i * 0.5f));
                        Vector3 p1 = spline.GetLocalPosFromKnotT(new KnotT(knot, (i + 1) * 0.5f));
                        float next = v + 2f * Vector3.Distance(p0, p1);
                        int b = 4 * quad;
                        string at = "knot " + knot + " step " + i;
                        Near(verts[b], p0 + new Vector3(-width, 0f, 0f), at + " left0");
                        Near(verts[b + 1], p1 + new Vector3(width, 0f, 0f), at + " right1");
                        Near(verts[b + 2], p0 + new Vector3(width, 0f, 0f), at + " right0");
                        Near(verts[b + 3], p1 + new Vector3(-width, 0f, 0f), at + " left1");
                        Assert.That(tris.GetRange(6 * quad, 6), Is.EqualTo(new[] { b, b + 1, b + 2, b, b + 3, b + 1 }), at);
                        Near(uvs[b], new Vector2(0f, v), at + " uv0");
                        Near(uvs[b + 1], new Vector2(1f, next), at + " uv1");
                        Near(uvs[b + 2], new Vector2(1f, v), at + " uv2");
                        Near(uvs[b + 3], new Vector2(0f, next), at + " uv3");
                        v = next;
                    }
                }
                Assert.That(v, Is.EqualTo(20f).Within(1e-3f), "v accumulates across knots: twice the 10-unit straight length");

                Mesh mesh = owned.Add(generated.GetComponent<MeshFilter>().sharedMesh);
                Assert.That(mesh.name, Is.EqualTo(generated.name));
                Assert.That(mesh.hideFlags, Is.EqualTo(HideFlags.None));
                Assert.That(mesh.vertexCount, Is.EqualTo(4 * quads));
                Assert.That(mesh.isReadable, Is.False, "UploadMeshData(true)");

                var renderer = generated.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = null;
                generated.name = "Lucid renamed visualisation";
                generated.transform.SetParent(owner.transform, false);
                generated.transform.localPosition = new Vector3(1f, 2f, 3f);
                var again = (GameObject)Call(visualisation, "GenerateMesh", generated, spline);
                Assert.That(again, Is.SameAs(generated));
                Assert.That(again.name, Is.EqualTo("Lucid renamed visualisation"));
                Assert.That(again.transform.parent, Is.SameAs(spline.transform));
                Assert.That(again.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(renderer.sharedMaterial == null, Is.True, "an existing renderer is not reassigned");
                Mesh second = owned.Add(again.GetComponent<MeshFilter>().sharedMesh);
                Assert.That(second, Is.Not.SameAs(mesh));
                Assert.That(second.name, Is.EqualTo("Lucid renamed visualisation"));
                Assert.That(verts, Has.Count.EqualTo(4 * quads));
            }
        }

        // RibbonVisualisation.GenerateMesh (0x1a95490). Per step: centre t0, centre t1, left t0,
        // right t1, right t0, left t1; u = |centre - side| / -6 (left, 0x29a827c) or / 6 (right,
        // 0x29a72d4); v' = v + |c1 - c0| / 6. Vertices c0 r1 r0 c1 l0 c1 c0 l1, triangles
        // b..b+3 as the spline quad, twice; the visualisation argument itself is returned.
        [Test]
        public void RibbonMeshHangsSideQuadsOffTheCentreLine()
        {
            using (var owned = new Owned())
            {
                var defs = Definitions(owned, 5, 0.5f, 2);
                var owner = owned.Inactive("Lucid owned ribbon visualisation");
                var visualisation = owner.AddComponent<RibbonVisualisation>();
                Assert.That(owner.GetComponent<Ribbon>(), Is.Not.Null, "RequireComponent(Ribbon)");
                EditableRibbon ribbon = StraightRibbon(owned, owner.transform, 0f, 6f, 12f);
                var verts = (List<Vector3>)Get(visualisation, typeof(RibbonVisualisation), "m_meshVerts");
                var tris = (List<int>)Get(visualisation, typeof(RibbonVisualisation), "m_meshTriangles");
                var uvs = (List<Vector2>)Get(visualisation, typeof(RibbonVisualisation), "m_meshUVs");
                tris.Add(-1);

                var generated = (GameObject)Call(visualisation, "GenerateMesh", null, ribbon);
                owned.Add(generated);
                Assert.That(generated.name, Is.EqualTo("Lucid owned ribbon visualisation[Visualisation]"));
                Assert.That(generated.transform.parent, Is.SameAs(ribbon.transform));
                Assert.That(generated.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(defs.ribbonMaterial));

                int steps = 2 * 2;
                Assert.That(verts, Has.Count.EqualTo(8 * steps), "ribbon quality, not spline quality");
                Assert.That(tris, Has.Count.EqualTo(12 * steps));
                Assert.That(uvs, Has.Count.EqualTo(8 * steps));
                float v = 0f;
                int step = 0;
                for (int knot = 0; knot < 2; ++knot)
                {
                    for (int i = 0; i < 2; ++i, ++step)
                    {
                        Vector3 Pos(float t, RibbonAlignment alignment) => ribbon.GetLocalPosFromKnotT(new KnotT(knot, t), alignment);
                        float t0 = i * 0.5f, t1 = (i + 1) * 0.5f;
                        Vector3 c0 = Pos(t0, RibbonAlignment.Center), c1 = Pos(t1, RibbonAlignment.Center);
                        Vector3 l0 = Pos(t0, RibbonAlignment.Left), l1 = Pos(t1, RibbonAlignment.Left);
                        Vector3 r0 = Pos(t0, RibbonAlignment.Right), r1 = Pos(t1, RibbonAlignment.Right);
                        float next = v + Vector3.Distance(c0, c1) / 6f;
                        int b = 8 * step;
                        string at = "knot " + knot + " step " + i;
                        Vector3[] expectedVerts = { c0, r1, r0, c1, l0, c1, c0, l1 };
                        for (int k = 0; k < 8; ++k) Near(verts[b + k], expectedVerts[k], at + " vertex " + k);
                        Assert.That(tris.GetRange(12 * step, 12), Is.EqualTo(new[] { b, b + 1, b + 2, b, b + 3, b + 1, b + 4, b + 5, b + 6, b + 4, b + 7, b + 5 }), at);
                        // Unit side widths: each side sub-knot sits one unit from the centre.
                        Vector2[] expectedUVs =
                        {
                            new Vector2(0f, v), new Vector2(1f / 6f, next), new Vector2(1f / 6f, v), new Vector2(0f, next),
                            new Vector2(-1f / 6f, v), new Vector2(0f, next), new Vector2(0f, v), new Vector2(-1f / 6f, next),
                        };
                        for (int k = 0; k < 8; ++k) Near(uvs[b + k], expectedUVs[k], at + " uv " + k);
                        v = next;
                    }
                }
                Assert.That(v, Is.EqualTo(2f).Within(1e-3f), "12 straight units over 6");
                Mesh mesh = owned.Add(generated.GetComponent<MeshFilter>().sharedMesh);
                Assert.That(mesh.name, Is.EqualTo(generated.name));
                Assert.That(mesh.isReadable, Is.False);

                generated.transform.SetParent(owner.transform, false);
                generated.name = "Lucid renamed ribbon visualisation";
                var reused = (GameObject)Call(visualisation, "GenerateMesh", generated, ribbon);
                Assert.That(reused, Is.SameAs(generated), "returns the visualisation argument");
                Assert.That(reused.transform.parent, Is.SameAs(ribbon.transform));
                Assert.That(verts, Has.Count.EqualTo(8 * steps), "refilled, not appended");
                Mesh second = owned.Add(reused.GetComponent<MeshFilter>().sharedMesh);
                Assert.That(second, Is.Not.SameAs(mesh));
                Assert.That(second.name, Is.EqualTo("Lucid renamed ribbon visualisation"));
            }
        }

        // Each u comes from its own centre/side pair (0x1a95f04..0x1a96063): |c0 - l0| / -6,
        // |c0 - r0| / 6, |c1 - l1| / -6 and |c1 - r1| / 6, emitted for r1 (0x1a96344), r0
        // (0x1a96492), l0 (0x1a967c5) and l1 (0x1a96b93). Unequal side widths (EditableRibbon
        // +0x70/+0x74) and an overridden last-knot offset make every distance in a step differ.
        [Test]
        public void RibbonMeshUTakesEachSideDistanceFromItsOwnPair()
        {
            using (var owned = new Owned())
            {
                Definitions(owned, 1, 0.1f, 2);
                var owner = owned.Inactive("Lucid owned ribbon u visualisation");
                var visualisation = owner.AddComponent<RibbonVisualisation>();
                var ribbonObject = owned.Inactive("Lucid owned editable ribbon", owner.transform);
                var ribbon = ribbonObject.AddComponent<EditableRibbon>();
                Set(ribbon, typeof(EditableRibbon), "m_leftSplineWidth", 1f);
                Set(ribbon, typeof(EditableRibbon), "m_rightSplineWidth", 2f);
                foreach (float knotZ in new[] { 0f, 6f, 12f })
                {
                    var knot = new GameObject("Lucid owned ribbon knot");
                    knot.transform.SetParent(ribbonObject.transform, false);
                    knot.transform.localPosition = new Vector3(0f, 0f, knotZ);
                    ribbon.Knots.Add(knot.AddComponent<RibbonKnot>());
                }
                ribbon.Initialise();
                RibbonKnot last = ribbon.Knots[2];
                Set(last, typeof(RibbonKnot), "m_overrideSubKnotOffset", true);
                last.GetSubKnot(RibbonAlignment.Left).LocalOffset = new Vector3(-5.5f, 0f, 0f);
                last.GetSubKnot(RibbonAlignment.Right).LocalOffset = new Vector3(3.5f, 0f, 0f);
                var uvs = (List<Vector2>)Get(visualisation, typeof(RibbonVisualisation), "m_meshUVs");

                var generated = owned.Add((GameObject)Call(visualisation, "GenerateMesh", null, ribbon));
                owned.Add(generated.GetComponent<MeshFilter>().sharedMesh);
                Assert.That(uvs, Has.Count.EqualTo(8 * 2 * 2));

                // Knot endpoints sit on the sub-knot offsets: widths 1 and 2 at knot 0, 5.5 and 3.5 at knot 2.
                Assert.That(uvs[4].x, Is.EqualTo(-1f / 6f).Within(Tolerance), "knot 0 l0 uses the left width");
                Assert.That(uvs[2].x, Is.EqualTo(2f / 6f).Within(Tolerance), "knot 0 r0 uses the right width");
                int lastStep = 8 * 3;
                Assert.That(uvs[lastStep + 7].x, Is.EqualTo(-5.5f / 6f).Within(Tolerance), "knot 2 l1 uses the overridden left offset");
                Assert.That(uvs[lastStep + 1].x, Is.EqualTo(3.5f / 6f).Within(Tolerance), "knot 2 r1 uses the overridden right offset");

                int step = 0;
                for (int knot = 0; knot < 2; ++knot)
                {
                    for (int i = 0; i < 2; ++i, ++step)
                    {
                        Vector3 Pos(float t, RibbonAlignment alignment) => ribbon.GetLocalPosFromKnotT(new KnotT(knot, t), alignment);
                        float t0 = i * 0.5f, t1 = (i + 1) * 0.5f;
                        Vector3 c0 = Pos(t0, RibbonAlignment.Center), c1 = Pos(t1, RibbonAlignment.Center);
                        float left0 = Vector3.Distance(c0, Pos(t0, RibbonAlignment.Left));
                        float right0 = Vector3.Distance(c0, Pos(t0, RibbonAlignment.Right));
                        float left1 = Vector3.Distance(c1, Pos(t1, RibbonAlignment.Left));
                        float right1 = Vector3.Distance(c1, Pos(t1, RibbonAlignment.Right));
                        int b = 8 * step;
                        string at = "knot " + knot + " step " + i;
                        if (knot == 1)
                        {
                            float[] distances = { left0, right0, left1, right1 };
                            for (int p = 0; p < 4; ++p)
                                for (int q = p + 1; q < 4; ++q)
                                    Assert.That(Mathf.Abs(distances[p] - distances[q]), Is.GreaterThan(1e-3f), at + " precondition: distinct side distances");
                        }
                        Assert.That(uvs[b + 1].x, Is.EqualTo(right1 / 6f).Within(Tolerance), at + " r1");
                        Assert.That(uvs[b + 2].x, Is.EqualTo(right0 / 6f).Within(Tolerance), at + " r0");
                        Assert.That(uvs[b + 4].x, Is.EqualTo(-left0 / 6f).Within(Tolerance), at + " l0");
                        Assert.That(uvs[b + 7].x, Is.EqualTo(-left1 / 6f).Within(Tolerance), at + " l1");
                    }
                }
            }
        }

        // Both constructors (0x1a98550/0x1a97030) allocate the three readonly lists; Reset
        // (0x1a97140/0x1a94ed0) stores GetComponent of the required type; GetMeshName
        // (0x1a984f0/0x1a96fd0) uses this gameObject's name and ignores the argument.
        [Test]
        public void VisualisationsAllocateListsAndResetToTheRequiredComponent()
        {
            using (var owned = new Owned())
            {
                var splineOwner = owned.Inactive("Lucid owned spline reset");
                var spline = splineOwner.AddComponent<SplineVisualisation>();
                var ribbonOwner = owned.Inactive("Lucid owned ribbon reset");
                var ribbon = ribbonOwner.AddComponent<RibbonVisualisation>();
                foreach ((MonoBehaviour behaviour, Type type) in new (MonoBehaviour, Type)[] { (spline, typeof(SplineVisualisation)), (ribbon, typeof(RibbonVisualisation)) })
                {
                    object verts = Get(behaviour, type, "m_meshVerts");
                    Assert.That((List<Vector3>)verts, Is.Not.Null.And.Empty);
                    Assert.That((List<int>)Get(behaviour, type, "m_meshTriangles"), Is.Not.Null.And.Empty);
                    Assert.That((List<Vector2>)Get(behaviour, type, "m_meshUVs"), Is.Not.Null.And.Empty);
                    Assert.That(Get(behaviour, type, "m_storeGeneratedMeshTime"), Is.Null);
                    var other = owned.Inactive("Lucid owned other");
                    object[] args = { other };
                    Assert.That(Call(behaviour, "GetMeshName", args), Is.EqualTo(behaviour.gameObject.name + "[Visualisation]"));
                    Assert.That(args[0], Is.SameAs(other));
                }
                Set(spline, typeof(SplineVisualisation), "m_spline", null);
                Call(spline, "Reset");
                Assert.That(Get(spline, typeof(SplineVisualisation), "m_spline"), Is.SameAs(splineOwner.GetComponent<Spline>()));
                Set(ribbon, typeof(RibbonVisualisation), "m_ribbon", null);
                Call(ribbon, "Reset");
                Assert.That(Get(ribbon, typeof(RibbonVisualisation), "m_ribbon"), Is.SameAs(ribbonOwner.GetComponent<Ribbon>()));
            }
        }

        static Delegate[] Handlers(object source, Type owner, string eventName) =>
            ((Delegate)Get(source, owner, eventName))?.GetInvocationList() ?? new Delegate[0];

        // OnSplineTypeChanged (0x1a97190) removes itself from the current spline with no null
        // guard (interface slot 27), stores the argument, then on an EditableSpline (hierarchy
        // `is` test at 0x1a972d6) stores it and StartGeneratingMesh (0x1a97450) subscribes
        // UpdateGeneratedMesh and generates when there is no visualisation; on any other spline
        // it only unsubscribes UpdateGeneratedMesh from the remembered editable spline. It always
        // re-adds itself to the new current spline (slot 26). UpdateGeneratedMesh (0x1a97520)
        // schedules Now + SplineEditorDefinition delay.
        [Test]
        public void SplineTypeChangeMovesSubscriptionsAndStartsGeneration()
        {
            using (var owned = new Owned())
            {
                var defs = Definitions(owned, 1, 0.1f, 1);
                var owner = owned.Inactive("Lucid owned spline type change");
                var visualisation = owner.AddComponent<SplineVisualisation>();
                EditableSpline editable = StraightSpline(owned, owner.transform, Vector3.forward, 0f, 1f);
                var segmented = owned.Inactive("Lucid owned segmented spline", owner.transform).AddComponent<SegmentedSpline>();
                var handler = (Action<ISpline>)Delegate.CreateDelegate(typeof(Action<ISpline>), visualisation, "OnSplineTypeChanged");
                Type type = typeof(SplineVisualisation);

                Assert.That(() => handler(editable), Throws.TypeOf<NullReferenceException>(), "no guard on the first current spline");

                Set(visualisation, type, "m_currentSpline", editable);
                editable.OnSplineTypeChanged += handler;
                DateTime before = DateTime.Now;
                handler(editable);
                DateTime after = DateTime.Now;
                Assert.That(Get(visualisation, type, "m_editableSpline"), Is.SameAs(editable));
                Assert.That(Handlers(editable, typeof(EditableSpline), "OnSplineTypeChanged"), Has.Length.EqualTo(1), "removed then re-added");
                Delegate[] knots = Handlers(editable, typeof(EditableSpline), "OnSplineKnotsUpdated");
                Assert.That(knots, Has.Length.EqualTo(1));
                Assert.That(knots[0].Method.Name, Is.EqualTo("UpdateGeneratedMesh"));
                var generated = (GameObject)Get(visualisation, type, "m_meshVisualisation");
                Assert.That(generated, Is.Not.Null, "no visualisation yet, so one is generated");
                owned.Add(generated);
                owned.Add(generated.GetComponent<MeshFilter>().sharedMesh);
                var due = (DateTime?)Get(visualisation, type, "m_storeGeneratedMeshTime");
                Assert.That(due.HasValue, Is.True);
                Assert.That(due.Value, Is.InRange(before.AddSeconds(100), after.AddSeconds(100)), "spline definition delay, not the ribbon's 200 s");

                handler(segmented);
                Assert.That(Get(visualisation, type, "m_currentSpline"), Is.SameAs(segmented));
                Assert.That(Handlers(editable, typeof(EditableSpline), "OnSplineTypeChanged"), Is.Empty);
                Assert.That(Handlers(segmented, typeof(SegmentedSpline), "OnSplineTypeChanged"), Has.Length.EqualTo(1));
                Assert.That(Handlers(editable, typeof(EditableSpline), "OnSplineKnotsUpdated"), Is.Empty);
                Assert.That(Get(visualisation, type, "m_editableSpline"), Is.SameAs(editable), "the remembered editable spline is kept");
                Assert.That(Get(visualisation, type, "m_meshVisualisation"), Is.SameAs(generated));
                segmented.OnSplineTypeChanged -= handler;
            }
        }

        // OnRibbonTypeChanged (0x1a94f20) and StartGeneratingMesh (0x1a95240) mirror the spline
        // bodies with the EditableRibbon events inlined (+0x98); UpdateGeneratedMesh (0x1a95380)
        // uses RibbonEditorDefinition's delay (+0x38).
        [Test]
        public void RibbonTypeChangeMovesSubscriptionsAndStartsGeneration()
        {
            using (var owned = new Owned())
            {
                Definitions(owned, 1, 0.1f, 1);
                var owner = owned.Inactive("Lucid owned ribbon type change");
                var visualisation = owner.AddComponent<RibbonVisualisation>();
                EditableRibbon editable = StraightRibbon(owned, owner.transform, 0f, 1f);
                var segmented = owned.Inactive("Lucid owned segmented ribbon", owner.transform).AddComponent<SegmentedRibbon>();
                var handler = (Action<IRibbon>)Delegate.CreateDelegate(typeof(Action<IRibbon>), visualisation, "OnRibbonTypeChanged");
                Type type = typeof(RibbonVisualisation);

                Assert.That(() => handler(editable), Throws.TypeOf<NullReferenceException>());

                var existing = owned.Inactive("Lucid owned existing ribbon mesh");
                Set(visualisation, type, "m_meshVisualisation", existing);
                Set(visualisation, type, "m_currentRibbon", editable);
                handler(editable);
                Assert.That(Get(visualisation, type, "m_editableRibbon"), Is.SameAs(editable));
                Assert.That(Get(visualisation, type, "m_meshVisualisation"), Is.SameAs(existing), "an existing visualisation is not regenerated");
                Assert.That(existing.GetComponent<MeshFilter>() == null, Is.True);
                Assert.That(Get(visualisation, type, "m_storeGeneratedMeshTime"), Is.Null);
                Delegate[] knots = Handlers(editable, typeof(EditableRibbon), "OnRibbonKnotsUpdated");
                Assert.That(knots, Has.Length.EqualTo(1));
                Assert.That(knots[0].Method.Name, Is.EqualTo("UpdateGeneratedMesh"));
                Assert.That(Handlers(editable, typeof(EditableRibbon), "OnRibbonTypeChanged"), Has.Length.EqualTo(1));

                Set(visualisation, type, "m_meshVisualisation", null);
                DateTime before = DateTime.Now;
                Call(visualisation, "UpdateGeneratedMesh");
                DateTime after = DateTime.Now;
                var generated = owned.Add((GameObject)Get(visualisation, type, "m_meshVisualisation"));
                owned.Add(generated.GetComponent<MeshFilter>().sharedMesh);
                var due = (DateTime?)Get(visualisation, type, "m_storeGeneratedMeshTime");
                Assert.That(due.Value, Is.InRange(before.AddSeconds(200), after.AddSeconds(200)), "ribbon definition delay");

                handler(segmented);
                Assert.That(Get(visualisation, type, "m_currentRibbon"), Is.SameAs(segmented));
                Assert.That(Handlers(editable, typeof(EditableRibbon), "OnRibbonTypeChanged"), Is.Empty);
                Assert.That(Handlers(segmented, typeof(SegmentedRibbon), "OnRibbonTypeChanged"), Has.Length.EqualTo(1));
                Assert.That(Handlers(editable, typeof(EditableRibbon), "OnRibbonKnotsUpdated"), Is.Empty);
                Assert.That(Get(visualisation, type, "m_editableRibbon"), Is.SameAs(editable));
                segmented.OnRibbonTypeChanged -= handler;
            }
        }

        // The editable branch (0x1a972f1 -> 0x1a97364) stores the new spline and calls
        // StartGeneratingMesh without unsubscribing the remembered one, and StartGeneratingMesh
        // (0x1a974cb) adds UpdateGeneratedMesh with no prior remove. The other branch is guarded
        // by Object.op_Inequality (0x1a97316), so no remembered spline is not a fault.
        [Test]
        public void SplineTypeChangeLeaksTheOldEditableAndStacksSubscriptions()
        {
            using (var owned = new Owned())
            {
                Definitions(owned, 1, 0.1f, 1);
                Type type = typeof(SplineVisualisation);
                SplineVisualisation Visualisation(string name, out Action<ISpline> handler)
                {
                    var owner = owned.Inactive(name);
                    var created = owner.AddComponent<SplineVisualisation>();
                    Set(created, type, "m_meshVisualisation", owned.Inactive(name + " existing mesh"));
                    handler = (Action<ISpline>)Delegate.CreateDelegate(typeof(Action<ISpline>), created, "OnSplineTypeChanged");
                    return created;
                }
                Delegate[] Knots(EditableSpline spline) => Handlers(spline, typeof(EditableSpline), "OnSplineKnotsUpdated");
                Delegate[] TypeChanged(object spline, Type owner) => Handlers(spline, owner, "OnSplineTypeChanged");

                var switching = Visualisation("Lucid owned spline switch", out Action<ISpline> switchHandler);
                EditableSpline a = StraightSpline(owned, switching.transform, Vector3.forward, 0f, 1f);
                EditableSpline b = StraightSpline(owned, switching.transform, Vector3.forward, 0f, 2f);
                Set(switching, type, "m_currentSpline", a);
                switchHandler(a);
                switchHandler(b);
                Assert.That(Get(switching, type, "m_editableSpline"), Is.SameAs(b));
                Assert.That(Knots(b), Has.Length.EqualTo(1));
                Assert.That(Knots(a), Has.Length.EqualTo(1), "the previous editable spline keeps its UpdateGeneratedMesh handler");
                Assert.That(Knots(a)[0].Target, Is.SameAs(switching));
                Assert.That(TypeChanged(a, typeof(EditableSpline)), Is.Empty);
                Assert.That(TypeChanged(b, typeof(EditableSpline)), Has.Length.EqualTo(1));
                b.OnSplineTypeChanged -= switchHandler;

                var repeated = Visualisation("Lucid owned spline repeat", out Action<ISpline> repeatHandler);
                EditableSpline c = StraightSpline(owned, repeated.transform, Vector3.forward, 0f, 1f);
                Set(repeated, type, "m_currentSpline", c);
                repeatHandler(c);
                repeatHandler(c);
                Assert.That(Knots(c), Has.Length.EqualTo(2), "each call adds another UpdateGeneratedMesh");
                Assert.That(TypeChanged(c, typeof(EditableSpline)), Has.Length.EqualTo(1));
                c.OnSplineTypeChanged -= repeatHandler;

                var plain = Visualisation("Lucid owned spline no editable", out Action<ISpline> plainHandler);
                var segmented = owned.Inactive("Lucid owned segmented spline", plain.transform).AddComponent<SegmentedSpline>();
                Set(plain, type, "m_currentSpline", segmented);
                Assert.That(Get(plain, type, "m_editableSpline"), Is.Null);
                Assert.That(() => plainHandler(segmented), Throws.Nothing);
                Assert.That(Get(plain, type, "m_editableSpline"), Is.Null);
                Assert.That(TypeChanged(segmented, typeof(SegmentedSpline)), Has.Length.EqualTo(1));
                segmented.OnSplineTypeChanged -= plainHandler;
            }
        }

        // Ribbon counterparts: editable branch 0x1a95081 -> 0x1a95159, Combine-only subscription
        // at 0x1a9530e, and the op_Inequality guard at 0x1a950aa.
        [Test]
        public void RibbonTypeChangeLeaksTheOldEditableAndStacksSubscriptions()
        {
            using (var owned = new Owned())
            {
                Definitions(owned, 1, 0.1f, 1);
                Type type = typeof(RibbonVisualisation);
                RibbonVisualisation Visualisation(string name, out Action<IRibbon> handler)
                {
                    var owner = owned.Inactive(name);
                    var created = owner.AddComponent<RibbonVisualisation>();
                    Set(created, type, "m_meshVisualisation", owned.Inactive(name + " existing mesh"));
                    handler = (Action<IRibbon>)Delegate.CreateDelegate(typeof(Action<IRibbon>), created, "OnRibbonTypeChanged");
                    return created;
                }
                Delegate[] Knots(EditableRibbon ribbon) => Handlers(ribbon, typeof(EditableRibbon), "OnRibbonKnotsUpdated");
                Delegate[] TypeChanged(object ribbon, Type owner) => Handlers(ribbon, owner, "OnRibbonTypeChanged");

                var switching = Visualisation("Lucid owned ribbon switch", out Action<IRibbon> switchHandler);
                EditableRibbon a = StraightRibbon(owned, switching.transform, 0f, 1f);
                EditableRibbon b = StraightRibbon(owned, switching.transform, 0f, 2f);
                Set(switching, type, "m_currentRibbon", a);
                switchHandler(a);
                switchHandler(b);
                Assert.That(Get(switching, type, "m_editableRibbon"), Is.SameAs(b));
                Assert.That(Knots(b), Has.Length.EqualTo(1));
                Assert.That(Knots(a), Has.Length.EqualTo(1), "the previous editable ribbon keeps its UpdateGeneratedMesh handler");
                Assert.That(Knots(a)[0].Target, Is.SameAs(switching));
                Assert.That(TypeChanged(a, typeof(EditableRibbon)), Is.Empty);
                Assert.That(TypeChanged(b, typeof(EditableRibbon)), Has.Length.EqualTo(1));
                b.OnRibbonTypeChanged -= switchHandler;

                var repeated = Visualisation("Lucid owned ribbon repeat", out Action<IRibbon> repeatHandler);
                EditableRibbon c = StraightRibbon(owned, repeated.transform, 0f, 1f);
                Set(repeated, type, "m_currentRibbon", c);
                repeatHandler(c);
                repeatHandler(c);
                Assert.That(Knots(c), Has.Length.EqualTo(2), "each call adds another UpdateGeneratedMesh");
                Assert.That(TypeChanged(c, typeof(EditableRibbon)), Has.Length.EqualTo(1));
                c.OnRibbonTypeChanged -= repeatHandler;

                var plain = Visualisation("Lucid owned ribbon no editable", out Action<IRibbon> plainHandler);
                var segmented = owned.Inactive("Lucid owned segmented ribbon", plain.transform).AddComponent<SegmentedRibbon>();
                Set(plain, type, "m_currentRibbon", segmented);
                Assert.That(Get(plain, type, "m_editableRibbon"), Is.Null);
                Assert.That(() => plainHandler(segmented), Throws.Nothing);
                Assert.That(Get(plain, type, "m_editableRibbon"), Is.Null);
                Assert.That(TypeChanged(segmented, typeof(SegmentedRibbon)), Has.Length.EqualTo(1));
                segmented.OnRibbonTypeChanged -= plainHandler;
            }
        }

        // StoreMesh (0x1a983c0/0x1a96ea0): returns unless HasValue, !(Now < due) and the editable
        // surface is not Unity-null; then reads m_meshVisualisation's MeshFilter.sharedMesh (a
        // missing visualisation faults) for the empty retail StoreGeneratedMesh and clears due.
        [Test]
        public void StoreMeshClearsAPassedScheduleOnlyWithAnEditableSurface()
        {
            using (var owned = new Owned())
            {
                var splineOwner = owned.Inactive("Lucid owned spline store");
                var spline = splineOwner.AddComponent<SplineVisualisation>();
                var ribbonOwner = owned.Inactive("Lucid owned ribbon store");
                var ribbon = ribbonOwner.AddComponent<RibbonVisualisation>();
                EditableSpline editableSpline = StraightSpline(owned, splineOwner.transform, Vector3.forward, 0f, 1f);
                EditableRibbon editableRibbon = StraightRibbon(owned, ribbonOwner.transform, 0f, 1f);
                var cases = new (MonoBehaviour behaviour, Type type, string editableField, Object editable)[]
                {
                    (spline, typeof(SplineVisualisation), "m_editableSpline", editableSpline),
                    (ribbon, typeof(RibbonVisualisation), "m_editableRibbon", editableRibbon),
                };
                foreach (var row in cases)
                {
                    string name = row.type.Name;
                    var meshObject = owned.Inactive("Lucid owned stored mesh");
                    meshObject.AddComponent<MeshFilter>();
                    DateTime past = DateTime.Now.AddMinutes(-1), future = DateTime.Now.AddMinutes(10);
                    Set(row.behaviour, row.type, "m_meshVisualisation", meshObject);

                    Set(row.behaviour, row.type, "m_storeGeneratedMeshTime", (DateTime?)future);
                    Set(row.behaviour, row.type, row.editableField, row.editable);
                    Call(row.behaviour, "StoreMesh");
                    Assert.That(Get(row.behaviour, row.type, "m_storeGeneratedMeshTime"), Is.EqualTo(future), name + " future time kept");

                    Set(row.behaviour, row.type, "m_storeGeneratedMeshTime", (DateTime?)past);
                    Set(row.behaviour, row.type, row.editableField, null);
                    Call(row.behaviour, "StoreMesh");
                    Assert.That(Get(row.behaviour, row.type, "m_storeGeneratedMeshTime"), Is.EqualTo(past), name + " no editable surface");

                    Set(row.behaviour, row.type, row.editableField, row.editable);
                    Set(row.behaviour, row.type, "m_meshVisualisation", null);
                    Assert.That(() => Call(row.behaviour, "StoreMesh"), Throws.TypeOf<NullReferenceException>(), name + " reads the visualisation");
                    Assert.That(Get(row.behaviour, row.type, "m_storeGeneratedMeshTime"), Is.EqualTo(past));

                    Set(row.behaviour, row.type, "m_meshVisualisation", meshObject);
                    Call(row.behaviour, "StoreMesh");
                    Assert.That(Get(row.behaviour, row.type, "m_storeGeneratedMeshTime"), Is.Null, name + " passed time cleared");

                    Set(row.behaviour, row.type, "m_meshVisualisation", null);
                    Call(row.behaviour, "StoreMesh");
                    Assert.That(Get(row.behaviour, row.type, "m_storeGeneratedMeshTime"), Is.Null, name + " nothing scheduled returns first");
                }
            }
        }
    }
}
