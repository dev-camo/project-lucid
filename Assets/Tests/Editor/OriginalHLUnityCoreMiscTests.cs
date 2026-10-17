using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using Hardlight.JSON;
using Hardlight.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // HLUnityCore.Runtime misc-batch behaviour, each case checked against the
    // shipped x86-64 body or original metadata named in its comment. Owned
    // Unity objects, temporary files and pooled JSON objects are released in
    // finally. HLOutput.LogError reaches a log only through a registered
    // HLUnityCore, so cases on an error route read the exact routed text from
    // the owned OwnedHLOutputErrorCapture; no other system is registered.
    public sealed class OriginalHLUnityCoreMiscTests
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyNested = BindingFlags.Public | BindingFlags.NonPublic;

        // Original metadata (dump.cs, DummyDll HLUnityCore.Runtime): ProcessManager
        // closures are <>c__DisplayClass32_0`1 (field systemRefs, lambda
        // (system, systemRef)), <>c__DisplayClass33_0`1 (field systems) and
        // <>c__34`1/<>c__35`1 (lambdas (system, sysRef)); the HLPropertyStore
        // iterator is <SaveAtEndOfFrame>d__71; PerformanceProfileCustom's
        // UpdateAttribute closure is <>c__DisplayClass4_0. The ordinals come
        // from member declaration order.
        [Test]
        public void ClosureAndIteratorNamesFollowTheOriginalMemberOrder()
        {
            Type refs = typeof(ProcessManager).GetNestedType("<>c__DisplayClass32_0`1", AnyNested);
            Type systems = typeof(ProcessManager).GetNestedType("<>c__DisplayClass33_0`1", AnyNested);
            Type refOfType = typeof(ProcessManager).GetNestedType("<>c__34`1", AnyNested);
            Type systemOfType = typeof(ProcessManager).GetNestedType("<>c__35`1", AnyNested);
            Assert.That(new[] { refs, systems, refOfType, systemOfType }, Has.None.Null);
            Assert.That(refs.GetField("systemRefs"), Is.Not.Null);
            Assert.That(systems.GetField("systems"), Is.Not.Null);
            Assert.That(ParameterNames(refs, "<GetSystemRefsOfType>b__0"), Is.EqualTo(new[] { "system", "systemRef" }));
            Assert.That(ParameterNames(systems, "<GetSystemsOfType>b__0"), Is.EqualTo(new[] { "system", "systemRef" }));
            Assert.That(ParameterNames(refOfType, "<GetSystemRefOfType>b__34_0"), Is.EqualTo(new[] { "system", "sysRef" }));
            Assert.That(ParameterNames(systemOfType, "<GetSystemOfType>b__35_0"), Is.EqualTo(new[] { "system", "sysRef" }));

            MethodInfo save = typeof(HLPropertyStore).GetMethod("SaveAtEndOfFrame", AnyInstance);
            Assert.That(save.GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType.Name,
                Is.EqualTo("<SaveAtEndOfFrame>d__71"));

            Type update = typeof(PerformanceProfileCustom).GetNestedType("<>c__DisplayClass4_0", AnyNested);
            Assert.That(update, Is.Not.Null);
            Assert.That(update.GetField("attribute").FieldType, Is.EqualTo(typeof(PerformanceAttribute)));
        }

        // DummyDll TypeDef flags: ProcessManager 0x100181, HLPropertyStore
        // 0x100001, GUIContentComparer 0x100001 and AndroidManifestMetadata
        // 0x102109 all carry BeforeFieldInit (0x100000), so their .cctors
        // (0x06000e4e, 0x06000d2d, 0x06000f6d, 0x06001153) are field
        // initializers; an explicit static constructor clears the flag.
        [Test]
        public void StaticInitialisersKeepTheOriginalBeforeFieldInitFlag()
        {
            foreach (Type type in new[] { typeof(ProcessManager), typeof(HLPropertyStore), typeof(GUIContentComparer), typeof(AndroidManifestMetadata) })
                Assert.That(type.Attributes & TypeAttributes.BeforeFieldInit, Is.EqualTo(TypeAttributes.BeforeFieldInit), type.FullName);
        }

        // Original metadata: HLPropertyList (TypeDefIndex 9256, token 0x020001f8)
        // flags 0x100001 and TimeUtils (9403, 0x0200028c) flags 0x100181, both
        // BeforeFieldInit. Their .cctors only store private static readonly
        // field values (dump.cs): HLPropertyList 0x06000cec (x86-64 0x1b09d60)
        // a string[] {",", "/"}, then the char[]
        // from the 2e002b002d00 initializer blob; TimeUtils 0x0600100b (x86-64
        // 0x1b20cc0) DateTime(1970, 1, 1, 0, 0, 0, Utc) into Epoch. The exact
        // flag values (stricter than the masked bit checked above) are the
        // discriminating part; the value checks guard that the field
        // initializers keep the original contents.
        [Test]
        public void PropertyListAndTimeUtilsKeepTheOriginalTypeFlagsAndStaticValues()
        {
            const BindingFlags privateStatic = BindingFlags.Static | BindingFlags.NonPublic;
            Assert.That((int)typeof(HLPropertyList).Attributes, Is.EqualTo(0x00100001));
            Assert.That((int)typeof(TimeUtils).Attributes, Is.EqualTo(0x00100181));
            Assert.That(typeof(HLPropertyList).TypeInitializer, Is.Not.Null);
            Assert.That(typeof(TimeUtils).TypeInitializer, Is.Not.Null);

            FieldInfo separators = typeof(HLPropertyList).GetField("m_invalidDecimalSeparators", privateStatic);
            FieldInfo additional = typeof(HLPropertyList).GetField("m_decimalAdditionalCharacters", privateStatic);
            Assert.That(separators, Is.Not.Null, "m_invalidDecimalSeparators");
            Assert.That(additional, Is.Not.Null, "m_decimalAdditionalCharacters");
            Assert.That(separators.IsInitOnly && additional.IsInitOnly, Is.True);
            Assert.That(separators.GetValue(null), Is.EqualTo(new[] { ",", "/" }));
            Assert.That(additional.GetValue(null), Is.EqualTo(new[] { '.', '+', '-' }));
            Assert.That(TimeUtils.Epoch.Ticks, Is.EqualTo(621355968000000000L));
            Assert.That(TimeUtils.Epoch.Kind, Is.EqualTo(DateTimeKind.Utc));
        }

        // Barycentric 0x06000a9d (x86-64 0x1b03ff0): a = v2 - v3, b = v1 - v3,
        // so u weights aV1, v weights aV2 and w (1 - u - v, 0x1b040b8) weights
        // aV3. The degenerate triangle divides 0/0 (divps 0x1b040b1) without a
        // guard. IsNormalisedRange 0x06000aa2 uses strict comparisons with
        // -0.0001f (0x29a70e8) and 1.0001f (0x29a8328).
        [Test]
        public void BarycentricWeightsVerticesInOrderWithTheNativeTolerance()
        {
            var inside = new Barycentric(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(0.25f, 0.25f));
            Assert.That(new[] { inside.u, inside.v, inside.w }, Is.EqualTo(new[] { 0.5f, 0.25f, 0.25f }));
            Assert.That(inside.IsInside, Is.True);
            Assert.That(inside.Interpolate(new Vector3(4f, 0f, 0f), new Vector3(0f, 8f, 0f), new Vector3(0f, 0f, 16f)),
                Is.EqualTo(new Vector3(2f, 2f, 4f)));

            var spatial = new Barycentric(new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 1f), new Vector3(0f, 1f, 1f), new Vector3(0.25f, 0.25f, 1f));
            Assert.That(new[] { spatial.u, spatial.v, spatial.w }, Is.EqualTo(new[] { 0.5f, 0.25f, 0.25f }));

            Assert.That(new Barycentric(-0.00005f, 0.5f, 0.50005f).IsInside, Is.True);
            Assert.That(new Barycentric(-0.0002f, 0.5f, 0.5002f).IsInside, Is.False);
            Assert.That(new Barycentric(1.0001f, 0f, 0f).IsInside, Is.False);
            Assert.That(new Barycentric(0.5f, float.NaN, 0.5f).IsInside, Is.False);

            var degenerate = new Barycentric(new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(2f, 0f), new Vector2(0.5f, 0f));
            Assert.That(float.IsNaN(degenerate.u) && float.IsNaN(degenerate.w), Is.True);
            Assert.That(degenerate.IsInside, Is.False);
        }

        // GUIContentComparer.Compare 0x06000f6c (x86-64 0x1b1e820) null-tests
        // each content, then calls String.Compare with the stored comparison;
        // .cctor 0x06000f6d stores 4 (Ordinal) in the shared Comparer. Ordinal
        // puts 'B' (66) before 'a' (97); culture ordering would not.
        [Test]
        public void ContentComparerUsesOrdinalTextAndToleratesNullContent()
        {
            var lower = new GUIContent("a");
            var upper = new GUIContent("B");
            Assert.That(GUIContentComparer.Comparer.Compare(lower, upper), Is.GreaterThan(0));
            Assert.That(GUIContentComparer.Comparer.Compare(null, lower), Is.LessThan(0));
            Assert.That(GUIContentComparer.Comparer.Compare(null, null), Is.EqualTo(0));
            Assert.That(new GUIContentComparer(StringComparison.OrdinalIgnoreCase).Compare(new GUIContent("a"), new GUIContent("A")), Is.EqualTo(0));
        }

        // AndroidManifestMetadata: the two-argument constructor 0x0600114e stores
        // type 0; GetValueTypeString 0x06001150 falls back to "value" on a
        // dictionary miss (0x1b30a11); FormatMetadataString 0x06001151 formats
        // indent, name, type string and value with no escaping.
        [Test]
        public void ManifestMetadataFormatsTheMappedAttributeVerbatim()
        {
            Assert.That(new AndroidManifestMetadata("com.example.key", "x").FormatMetadataString("    "),
                Is.EqualTo("    <meta-data android:name=\"com.example.key\" android:value=\"x\" />"));
            // Qualified: HLSplines.Runtime also declares Hardlight.MetadataValueType.
            Assert.That(new AndroidManifestMetadata("icon", "@drawable/a\"b", Hardlight.Utils.MetadataValueType.Resource).FormatMetadataString(""),
                Is.EqualTo("<meta-data android:name=\"icon\" android:resource=\"@drawable/a\"b\" />"));
            Assert.That(new AndroidManifestMetadata("n", "v", (Hardlight.Utils.MetadataValueType)7).GetValueTypeString(), Is.EqualTo("value"));
            Assert.That(default(AndroidManifestMetadata).GetValueTypeString(), Is.EqualTo(AndroidManifestMetadata.DefaultValueTypeString));
        }

        // UnityObjectWithGuid: OnBeforeSerialize 0x06000efd writes Guid.ToString();
        // OnAfterDeserialize 0x06000efe assigns Guid.NewGuid on a failed TryParse,
        // reports it through HLOutput.LogError with the format at 0x1b1b705, but
        // leaves m_serialisedGuid untouched; Equals 0x06000efc calls the
        // reference's virtual Equals with no null test (0x1b1b612..0x1b1b624).
        [Test]
        public void ObjectWithGuidRoundTripsAndRegeneratesOnlyInMemory()
        {
            ScalableFeature target = ScriptableObject.CreateInstance<ScalableFeature>();
            try
            {
                using var capture = new OwnedHLOutputErrorCapture();
                FieldInfo serialised = typeof(UnityObjectWithGuid).GetField("m_serialisedGuid", AnyInstance);
                var guid = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e");
                var value = new UnityObjectWithGuid(target, guid);
                ((ISerializationCallbackReceiver)value).OnBeforeSerialize();
                Assert.That(serialised.GetValue(value), Is.EqualTo("0f8fad5b-d9cb-469f-a165-70867728950e"));
                ((ISerializationCallbackReceiver)value).OnAfterDeserialize();
                Assert.That(value.Guid, Is.EqualTo(guid));
                Assert.That(capture.Errors, Is.Empty, "a parsable Guid is not reported");

                serialised.SetValue(value, "not a guid");
                ((ISerializationCallbackReceiver)value).OnAfterDeserialize();
                Assert.That(value.Guid, Is.Not.EqualTo(Guid.Empty).And.Not.EqualTo(guid));
                Assert.That(serialised.GetValue(value), Is.EqualTo("not a guid"));
                Assert.That(capture.Errors, Is.EqualTo(new[] { "Unable to parse Guid: not a guid A new Guid has been assigned: " + value.Guid + "|0" }));

                Assert.That(new UnityObjectWithGuid(target, guid).Equals((object)new UnityObjectWithGuid(target, guid)), Is.True);
                Assert.That(new UnityObjectWithGuid(target, guid).Equals(new UnityObjectWithGuid(target, Guid.Empty)), Is.False);
                Assert.That(new UnityObjectWithGuid(target, guid).Equals((object)target), Is.False);
                Assert.Throws<NullReferenceException>(() => new UnityObjectWithGuid(null, guid).Equals(new UnityObjectWithGuid(null, guid)));
                value.Deconstruct(out Guid outGuid, out Object outObject);
                Assert.That(outObject, Is.SameAs(target));
                Assert.That(outGuid, Is.EqualTo(value.Guid));
                Assert.That(capture.Errors.Count, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        // IGraphStorageExtensions AdjustValue Int16 0x060008c2 (x86-64 0x1aee410)
        // adds as int, narrows with movsx (0x1aee4ee) and writes SetValue<Int16>;
        // Int32 0x060008c3 adds without overflow checks (0x1aee5ee); Double
        // 0x060008c6 uses addsd (0x1aee953). FillValue 0x060008bf forwards
        // storeDefault to GetValue. TryRemoveValue 0x060008c8 (0x952630) removes
        // only after TryGetValue<T> succeeds, so a conversion fault keeps the row;
        // that fault is the only routed report (ConvertValue 0x060008cb).
        [Test]
        public void GraphStorageAdjustFillAndRemoveKeepTheNativeArithmeticAndOrder()
        {
            using (var capture = new OwnedHLOutputErrorCapture())
            {
                var storage = new FSMStorage(0);
                GraphStorageKey shortKey = 9101, intKey = 9102, doubleKey = 9103, fillKey = 9104, removeKey = 9105;

                Assert.That(storage.AdjustValue(shortKey, (short)1, short.MaxValue), Is.EqualTo(short.MinValue));
                Assert.That(storage.GetCollection()[shortKey], Is.InstanceOf<IGraphStorage.Var<short>>());
                Assert.That(storage.AdjustValue(intKey, 1, int.MaxValue), Is.EqualTo(int.MinValue));
                Assert.That(storage.AdjustValue(doubleKey, 0.2, 0.1), Is.EqualTo(0.1 + 0.2));
                Assert.That(storage.GetValueOnly(doubleKey, 0.0), Is.EqualTo(0.1 + 0.2));

                storage.FillValue(fillKey, out int filled, 7, false);
                Assert.That(filled, Is.EqualTo(7));
                Assert.That(storage.HasValue(fillKey), Is.False);
                storage.FillValue(fillKey, out filled, 8);
                Assert.That(storage.GetValueOnly(fillKey, 0), Is.EqualTo(8));

                Assert.That(storage.TryRemoveValue(removeKey, out int missing), Is.False);
                Assert.That(missing, Is.EqualTo(0));
                storage.SetValue(removeKey, 5);
                Assert.That(capture.Errors, Is.Empty, "adjust, fill and a missing remove report nothing");
                Assert.Throws<InvalidCastException>(() => storage.TryRemoveValue(removeKey, out string _));
                Assert.That(capture.Errors.Last(), Does.StartWith("Failed to retrieve value with name 'null_null_").And.EndWith("'|0"));
                Assert.That(storage.HasValue(removeKey), Is.True);
                Assert.That(storage.TryRemoveValue(removeKey, out int removed), Is.True);
                Assert.That(removed, Is.EqualTo(5));
                Assert.That(storage.HasValue(removeKey), Is.False);
            }
        }

        // PerformanceProfileCustom.UpdateAttribute 0x06000d8c (x86-64 0x1b10a20):
        // FindIndex over Enum.GetValues + 1, reset to 0 at Length (cmove at
        // 0x1b10b77). GetValues orders by unsigned value (Medium, High,
        // VeryHigh, VeryLow, Low), so an undeclared level lands on Medium, not
        // VeryLow. SetAttributes 0x06000d8a appends Medium rows; GetAttributeSetting
        // 0x06000d8b formats "{0} ({1})" with the boxed level name.
        [Test]
        public void CustomProfileCyclesLevelsInEnumValueOrderAndAppendsMediumRows()
        {
            PerformanceProfileCustom profile = ScriptableObject.CreateInstance<PerformanceProfileCustom>();
            ScalableFeature feature = ScriptableObject.CreateInstance<ScalableFeature>();
            try
            {
                feature.ID = "Shadows";
                profile.SetAttributes(new[] { feature });
                profile.SetAttributes(new[] { feature });
                Assert.That(profile.Attributes.Count, Is.EqualTo(2));
                Assert.That(profile.Attributes.All(a => a.Feature == feature && a.Level == PerformanceProfile.QualityLevel.Medium), Is.True);
                Assert.That(profile.GetAttributeSetting(profile.Attributes[0]), Is.EqualTo("Shadows (Medium)"));

                var attribute = new PerformanceAttribute { Feature = feature, Level = PerformanceProfile.QualityLevel.VeryHigh };
                profile.UpdateAttribute(attribute);
                Assert.That(attribute.Level, Is.EqualTo(PerformanceProfile.QualityLevel.VeryLow));
                attribute.Level = PerformanceProfile.QualityLevel.Low;
                profile.UpdateAttribute(attribute);
                Assert.That(attribute.Level, Is.EqualTo(PerformanceProfile.QualityLevel.Medium));
                attribute.Level = (PerformanceProfile.QualityLevel)7;
                profile.UpdateAttribute(attribute);
                Assert.That(attribute.Level, Is.EqualTo(PerformanceProfile.QualityLevel.Medium));
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(feature);
            }
        }

        // LoadOrCreate 0x06000d8e (x86-64 0x1b10c80) names a fresh instance and
        // reads only an existing file through FromJsonOverwrite (0x1b10ceb);
        // DeleteFrom 0x06000d90 deletes an existing file (0x1b10d4a) and
        // otherwise reports "No saved profile found at " + path (0x1b10d4f).
        [Test]
        public void CustomProfileLoadOrCreateAndDeleteTouchOnlyExistingFiles()
        {
            string directory = Path.Combine(Path.GetTempPath(), "lucid-profile-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "profile.json");
            PerformanceProfileCustom profile = null, source = null, loaded = null;
            ScalableFeature feature = ScriptableObject.CreateInstance<ScalableFeature>();
            try
            {
                using var capture = new OwnedHLOutputErrorCapture();
                Directory.CreateDirectory(directory);
                profile = PerformanceProfileCustom.LoadOrCreate(path, "Custom");
                Assert.That(profile.name, Is.EqualTo("Custom"));
                Assert.That(profile.Attributes.Count, Is.EqualTo(0));
                Assert.That(File.Exists(path), Is.False);

                // Existing file: the serialized attribute rows overwrite the fresh instance.
                source = ScriptableObject.CreateInstance<PerformanceProfileCustom>();
                source.SetAttributes(new[] { feature });
                source.Attributes[0].Level = PerformanceProfile.QualityLevel.High;
                File.WriteAllText(path, JsonUtility.ToJson(source));
                loaded = PerformanceProfileCustom.LoadOrCreate(path, "Custom");
                Assert.That(loaded.Attributes.Count, Is.EqualTo(1));
                Assert.That(loaded.Attributes[0].Level, Is.EqualTo(PerformanceProfile.QualityLevel.High));
                Assert.That(capture.Errors, Is.Empty, "loading reports nothing");

                PerformanceProfileCustom.DeleteFrom(path);
                Assert.That(File.Exists(path), Is.False);
                Assert.That(capture.Errors, Is.Empty, "deleting an existing file reports nothing");
                PerformanceProfileCustom.DeleteFrom(path);
                Assert.That(capture.Errors, Is.EqualTo(new[] { "No saved profile found at " + path + "|0" }));
            }
            finally
            {
                if (profile != null) Object.DestroyImmediate(profile);
                if (source != null) Object.DestroyImmediate(source);
                if (loaded != null) Object.DestroyImmediate(loaded);
                Object.DestroyImmediate(feature);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        // UnityPackageManifest 0x06000c39 (x86-64 0x1b06440) reads the five
        // strings, builds the author from GetObject("author") and copies
        // "dependencies" in key order, leaving an empty dictionary when absent.
        // UnityPackageAuthor 0x06000c37 reads "author", "name", "url" into the
        // author-first constructor 0x06000c38; a missing object faults.
        [Test]
        public void PackageManifestReadsAuthorAndOptionalDependencies()
        {
            JSONHashtable json = JSONHashtable.Create();
            JSONHashtable bare = JSONHashtable.Create();
            try
            {
                JSONHashtable author = JSONHashtable.Create();
                author.Add("name", "Hardlight");
                author.Add("url", "https://example.invalid");
                JSONHashtable dependencies = JSONHashtable.Create();
                dependencies.Add("com.b", "2.0.0");
                dependencies.Add("com.a", "1.0.0");
                json.Add("name", "com.hardlight.hlunitycore");
                json.Add("version", "1.2.3");
                json.Add("displayName", "HLUnityCore");
                json.Add("unity", "2022.3");
                json.Add("author", author);
                json.Add("dependencies", dependencies);

                var manifest = new UnityPackageManifest(json);
                Assert.That(new[] { manifest.name, manifest.version, manifest.displayName, manifest.description, manifest.unity },
                    Is.EqualTo(new[] { "com.hardlight.hlunitycore", "1.2.3", "HLUnityCore", null, "2022.3" }));
                Assert.That(manifest.author.name, Is.EqualTo("Hardlight"));
                Assert.That(manifest.author.author, Is.Null);
                Assert.That(manifest.author.url, Is.EqualTo("https://example.invalid"));
                // Key order follows the backing Dictionary enumeration of the JSON
                // table and the copy (insertion order while nothing is removed);
                // the native body (0x1b066c0..0x1b0671e) only fixes that key order is used.
                Assert.That(manifest.dependencies.Keys, Is.EqualTo(new[] { "com.b", "com.a" }));
                Assert.That(manifest.dependencies["com.a"], Is.EqualTo("1.0.0"));

                Assert.Throws<NullReferenceException>(() => new UnityPackageManifest(bare));
                bare.Add("author", JSONHashtable.Create());
                Assert.That(new UnityPackageManifest(bare).dependencies, Is.Empty);

                var ordered = new UnityPackageAuthor("A", "N", "U");
                Assert.That(new[] { ordered.author, ordered.name, ordered.url }, Is.EqualTo(new[] { "A", "N", "U" }));
            }
            finally
            {
                // JSONHashtable.Release also releases every child value, so the
                // nested author and dependency tables return to their pools here.
                json.Dispose();
                bare.Dispose();
            }
        }

        // GUIColourScope .ctor 0x06000f68 (x86-64 0x1b1e5b0) reads color,
        // contentColor and backgroundColor before testing condition (0x1b1e63c),
        // re-applies the captured colour for a null argument, and Dispose
        // 0x06000f6a restores all three unconditionally.
        [Test]
        public void ColourScopeCapturesFirstAndRestoresAllThree()
        {
            Color color = GUI.color, content = GUI.contentColor, background = GUI.backgroundColor;
            try
            {
                GUI.color = Color.red;
                GUI.contentColor = Color.yellow;
                GUI.backgroundColor = Color.cyan;

                var overriding = new GUIColourScope(Color.green, null, Color.blue);
                Assert.That(new[] { GUI.color, GUI.contentColor, GUI.backgroundColor }, Is.EqualTo(new[] { Color.green, Color.yellow, Color.blue }));
                GUI.contentColor = Color.magenta;
                overriding.Dispose();
                Assert.That(new[] { GUI.color, GUI.contentColor, GUI.backgroundColor }, Is.EqualTo(new[] { Color.red, Color.yellow, Color.cyan }));

                var skipped = new GUIColourScope(false, Color.green, Color.green, Color.green);
                Assert.That(new[] { GUI.color, GUI.contentColor, GUI.backgroundColor }, Is.EqualTo(new[] { Color.red, Color.yellow, Color.cyan }));
                GUI.color = Color.white;
                GUI.contentColor = Color.white;
                GUI.backgroundColor = Color.white;
                skipped.Dispose();
                Assert.That(new[] { GUI.color, GUI.contentColor, GUI.backgroundColor }, Is.EqualTo(new[] { Color.red, Color.yellow, Color.cyan }));
            }
            finally
            {
                GUI.color = color;
                GUI.contentColor = content;
                GUI.backgroundColor = background;
            }
        }

        // FirebaseConfigurationAsset .ctor 0x06000146 (x86-64 0x1aa01c0) sets
        // m_automaticIntegration to 1 and both paths to String.Empty before the
        // base constructor. ValidateAssetPath 0x06000145 only probes File.Exists
        // and discards the result (tail jmp at 0x1aa01b7); no report survives in
        // the retail body, so a missing file is silent on both the HLOutput and
        // the Debug route.
        [Test]
        public void FirebaseAssetDefaultsAndSilentValidation()
        {
            FirebaseConfigurationAsset asset = ScriptableObject.CreateInstance<FirebaseConfigurationAsset>();
            try
            {
                using var capture = new OwnedHLOutputErrorCapture();
                Assert.That(asset.AutomaticIntegration, Is.True);
                Assert.That(typeof(FirebaseConfigurationAsset).GetField("m_googleServicesJsonPath", AnyInstance).GetValue(asset), Is.EqualTo(string.Empty));
                Assert.That(typeof(FirebaseConfigurationAsset).GetField("m_googleServiceInfoPlist", AnyInstance).GetValue(asset), Is.EqualTo(string.Empty));
                Assert.That(typeof(FirebaseConfigurationAsset).GetField("m_injectFirebaseConfigure", AnyInstance).GetValue(asset), Is.EqualTo(false));
                typeof(FirebaseConfigurationAsset).GetField("m_googleServicesJsonPath", AnyInstance)
                    .SetValue(asset, "Missing/" + Guid.NewGuid().ToString("N") + ".json");
                Assert.DoesNotThrow(asset.Validate);
                Assert.That(capture.Errors, Is.Empty, "a missing file is not reported");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        // AndroidBillOfMaterials: the (name, version) constructors are the
        // inlined base constructor ending in virtual Validate (0x1b30558/
        // 0x1b30640), and the override 0x06001143 (0x1b306e7) reads the
        // still-null array without a null test. The single-string constructor
        // 0x06001140 with a null or empty dependency skips Validate (base
        // IsNullOrEmpty branch, 0x1b30421).
        [Test]
        public void BillOfMaterialsValidatesItsUnsetArrayDuringConstruction()
        {
            Assert.Throws<NullReferenceException>(() => new AndroidBillOfMaterials("com.google.firebase:firebase-bom", "32.0.0"));
            var empty = new AndroidBillOfMaterials((string)null);
            Assert.That(empty.VersionControlledDependencies, Is.Null);
            Assert.That(empty.Name, Is.Null);
        }

        // IManifestFileConfig default members 0x0600015b/0x0600015c return the
        // two NativeAndroid~ literals decoded from the shipped bodies.
        [Test]
        public void ManifestConfigDefaultPathsAreTheShippedLiterals()
        {
            IManifestFileConfig config = new ManifestConfig();
            Assert.That(config.DefaultManifestPath, Is.EqualTo("/NativeAndroid~/AndroidManifest.xml"));
            Assert.That(config.DefaultManifestTemplatePath, Is.EqualTo("/NativeAndroid~/TemplateAndroidManifest.xml"));
        }

        private sealed class ManifestConfig : IManifestFileConfig
        {
            public string PathToManifestTemplateFile => null;
            public string PathToManifestFile => null;
        }

        // Original DummyDll HLUnityCore.Runtime Param rows: these parameters carry
        // Optional|HasDefault (0x1010) with a null/default constant, which
        // dump.cs omits. LegalModule data and GUIColourScope condition are 0x0000.
        [Test]
        public void OptionalParametersKeepTheOriginalDefaultFlags()
        {
            const ParameterAttributes Optional = ParameterAttributes.Optional | ParameterAttributes.HasDefault;
            const ParameterAttributes Required = ParameterAttributes.None;
            const BindingFlags Declared = AnyInstance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            void Expect(MethodBase member, params (string Name, ParameterAttributes Attributes)[] expected)
            {
                Assert.That(member, Is.Not.Null);
                ParameterInfo[] actual = member.GetParameters();
                Assert.That(actual.Select(p => p.Name).ToArray(), Is.EqualTo(expected.Select(e => e.Name).ToArray()), member.Name);
                for (int i = 0; i < actual.Length; ++i)
                {
                    Assert.That(actual[i].Attributes, Is.EqualTo(expected[i].Attributes), member.Name + " " + actual[i].Name);
                    if (expected[i].Attributes == Optional)
                        Assert.That(actual[i].RawDefaultValue, Is.Null, member.Name + " " + actual[i].Name);
                }
            }

            MethodBase Single(Type owner, string name) =>
                owner.GetMethods(Declared).Single(m => m.Name == name);
            MethodBase Constructor(Type owner, int arity) =>
                owner.GetConstructors(AnyInstance).Single(c => c.GetParameters().Length == arity);

            // 0x060003ae.
            Expect(Single(typeof(FSMClassFactory), nameof(FSMClassFactory.GenerateJSONMultipleFSMsClass)), ("fsm", Optional), ("json", Optional));
            // 0x060007bc, 0x060007bd.
            Expect(Single(typeof(GameCenterSaveGame), "OnAppInitialise"), ("context", Optional));
            Expect(Single(typeof(GameCenterSaveGame), "OnAppShutdown"), ("context", Optional));
            // 0x0600094b.
            Expect(Single(typeof(LegalModule), nameof(LegalModule.CreateAndInitialise)), ("data", Required), ("onOutput", Optional));
            // 0x06000c5f.
            Expect(Constructor(typeof(Observable<>), 2), ("defaultValue", Optional), ("comparer", Optional));
            // 0x06000f05.
            Expect(Single(typeof(UpdatePublisher<>), "OnUpdate"), ("context", Optional));
            // 0x06000f68, 0x06000f69.
            Expect(Constructor(typeof(GUIColourScope), 4), ("condition", Required), ("newColour", Optional), ("newContentColour", Optional), ("newBackgroundColour", Optional));
            Expect(Constructor(typeof(GUIColourScope), 3), ("newColour", Optional), ("newContentColour", Optional), ("newBackgroundColour", Optional));
        }

        private static string[] ParameterNames(Type owner, string method) =>
            owner.GetMethod(method, AnyInstance).GetParameters().Select(p => p.Name).ToArray();
    }
}
