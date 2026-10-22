using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HardlightProject;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests.Editor
{
    // Root pointer serialization only. Group payload layouts, Get/GetGroup,
    // FSM acquisition and application/service entry remain outside these cases.
    public sealed class OriginalDataDefinitionsAssetImportTests
    {
        private const string SourcePath = "Assets/Scripts/Definitions/DataDefinitions.cs";
        private const string ExportedScriptGuid = "24332ee03b3b859457af1233e8363cce";
        private const string MaintainedScriptGuid = "f0947ea3ca8a4c33b0d54f95391994fb";
        private const string RootAssetPath = "Assets/Data/Definitions/DataDefinitions.asset";
        private const string RootAssetGuid = "97124c4e8a752d7489e33dd26346a55c";
        private const string OriginalRootHash = "5ffc3dadc7b6b168fd26cd55ae98f7f24ebc1215be7c8e30a47f1d878c3cade5";
        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly string[] ExpectedOwners = {
            "Game.Runtime::HardlightProject.LevelStartPositionDefinition",
            "Game.Runtime::HardlightProject.LevelDefinition",
            "UnityEngine.UI::UnityEngine.UI.LayoutElement",
            "UnityEngine.UI::UnityEngine.UI.ContentSizeFitter",
            "UnityEngine.UI::UnityEngine.UI.AspectRatioFitter",
            "HLUnityCore.Runtime::Hardlight.ActiveRuntimeConfiguration",
            "HLUnityCore.Runtime::Hardlight.RuntimeConfiguration",
            "Game.Runtime::HardlightProject.GameLevels",
            "Game.Runtime::HardlightProject.CoreGameConfiguration",
            "HLLocalisation.Runtime::Hardlight.HLLocalisationConfigurationAsset",
            "Game.Runtime::HardlightProject.HashedGroupConfiguration",
            "Game.Runtime::HardlightProject.DataDefinitions",
            "HLUnityCore.Runtime::Hardlight.FiniteStateMachineBehaviour",
            "HLUnityCore.Runtime::Hardlight.FSMUserBehaviour",
            "HLUnityCore.Runtime::Hardlight.FiniteStateMachineScriptableObject",
            "Unity.TextMeshPro::TMPro.TMP_StyleSheet",
            "Unity.TextMeshPro::TMPro.TMP_SpriteAsset" };

        private sealed class Reference
        {
            public readonly string Path, Guid;
            public Reference(string path, string guid) { Path = path; Guid = guid; }
        }
        // Immutable supplied root order; these pointers do not admit group layouts.
        private static readonly Reference[] References = {
            new Reference("Assets/Data/Definitions/Characters/CharacterDefinitions.asset", "7552f04205a70d447899f21c0aa941db"),
            new Reference("Assets/Data/Definitions/Collectables/CollectableDefinitionGroup.asset", "4a511fc19f555bb4886eb9a025bb2a50"),
            new Reference("Assets/Data/Definitions/Rails/RailDefinitionGroup.asset", "00036a9f972990a4bb9cab91d0c37acd"),
            new Reference("Assets/Data/Definitions/Rails/LightspeedDashDefinitionGroup.asset", "d98db9388f441c046890cfc067241393"),
            new Reference("Assets/Data/Definitions/Rails/TransporterDefinitionGroup.asset", "c24e965b77d1f1642ba4f384556cfaaf"),
            new Reference("Assets/Data/Definitions/Terrain/TerrainHalfPipeDefinitionGroup.asset", "b557d0bcb913e0c4883d99d243bfca0b"),
            new Reference("Assets/Data/Definitions/Animations/CharacterAnimationDefinitionGroup.asset", "36af254d0c983e84295ceb6f8702f11a"),
            new Reference("Assets/Data/Definitions/Abilities/AbilityDefinitionGroup.asset", "98ad11e606434dd45aada0c8e95cc050"),
            new Reference("Assets/Data/Definitions/UI/Events/UIModernEventDefinitionGroup.asset", "ddc46d1f1805a1646adf55f14720dc8f"),
            new Reference("Assets/Data/Definitions/DeviceMatches/DevicePerformanceGroup.asset", "a59b9efefda101c44821776559f3911f"),
            new Reference("Assets/Data/Definitions/Terrain/TerrainDefinitionGroup.asset", "6e375ef36b74a4747bce5b27c737e16a"),
            new Reference("Assets/Data/Definitions/Surfaces/GravitySurfaceDefinitionGroup.asset", "87dac697ec2a2564abc98a53852c5ffc"),
            new Reference("Assets/Data/Definitions/Enemies/EnemyDefinitionGroup.asset", "a2c10c930fd26e845b9c3787f646c9b4"),
            new Reference("Assets/Data/Definitions/StartingPoint/LevelStartPositionDefinitionGroup.asset", "6ab6dd2c0cdb9074aaa8ab48cdbf3f74"),
            new Reference("Assets/Data/Definitions/Levels/LevelSetupDefinitionGroup.asset", "814de37a2ac3167478e877f88154cbd7"),
            new Reference("Assets/Data/Definitions/Hazards/HazardDefinitionGroup.asset", "64256f4e71c8956468ac270e0cbc5c84"),
            new Reference("Assets/Data/Definitions/CascadeObjects/CascadeObjectDefinitionGroup.asset", "761724eba8265c840b54677e6e57ab69"),
            new Reference("Assets/Data/Definitions/ActorTerrainEffects/ActorTerrainEffectsDefinitionGroup.asset", "fb0d208ec0e043d40b9b8ca4126d9c55"),
            new Reference("Assets/Data/Definitions/Animations/BossAnimationDefinitionGroup.asset", "6063e8f967bfa5341ba397e617d7092f"),
            new Reference("Assets/Data/Definitions/GlobalConstants/GlobalConstantDefinitionGroup.asset", "3798a5af65aa5dc44a56a20c70920439"),
            new Reference("Assets/Data/Definitions/Terrain/TerrainTrackerGroup.asset", "6830dcb3db92cbf4cbac6954d74b6323"),
            new Reference("Assets/Data/Definitions/ApplicationMessages/ApplicationStateEventDefinitionGroup.asset", "02e56081233c4b547a7f8ec3a25bdaec"),
            new Reference("Assets/Data/Definitions/Audio/Clips/HLAudioClipGroup.asset", "2a33c88c472e40a4dacdde8209e30326"),
            new Reference("Assets/Data/Definitions/Audio/Mixers/HLAudioMixerGroup.asset", "9147905e4bc869846abe86d85766908e"),
            new Reference("Assets/Data/Definitions/UI/Containers/uicontaineridentifier_group.asset", "d7bf78b6d34d7c84cb60668d4282e4f1"),
            new Reference("Assets/Data/Definitions/ParticleEffects/ParticleEffectDefinitionGroup.asset", "b4932ef3dfe0ad44b86f3aa9e413dddb"),
            new Reference("Assets/Data/Definitions/TimeScale/TimeScaleDefinitionGroup.asset", "f1b04d9dc8c07bd448af93c0edfcf6cd"),
            new Reference("Assets/Data/Definitions/UI/FullscreenShaders/FullscreenShaderParametersDefinitionGroup.asset", "09f94508b470b5244a2b2c6412e8c38e"),
            new Reference("Assets/Data/Definitions/Cutscenes/CutsceneDefinitionGroup.asset", "0dfc30a8f29c6a74ebb6d4f23b38e0a2"),
            new Reference("Assets/Data/Definitions/GameplayIslands/GameplayIslandDefinitionGroup.asset", "b4fe56f90d61419439411d4684c2b511"),
            new Reference("Assets/Data/Definitions/Animations/EnemyAnimationDefinitionGroup.asset", "abadc98e3c24165479a9cf1455b98c99"),
            new Reference("Assets/Data/Definitions/Projectiles/ProjectileDefinitionGroup.asset", "cd53c5fc7b609cf418bba6802179de95"),
            new Reference("Assets/Data/Definitions/Waypoints/WaypointDefinitionGroup.asset", "a84acf85c974d3b44bb12982074afd90"),
            new Reference("Assets/Data/Definitions/Targeting/TargetingTypePriorityDefinitionGroup.asset", "b551ed687430e794cb962547efaff0dd"),
            new Reference("Assets/Data/Definitions/Ornaments/OrnamentDefinitionGroup.asset", "ba88afedc383db449bc1eae4413dedc4"),
            new Reference("Assets/Data/Definitions/CharacterArchetypes/CharacterArchetypeDefinitionGroup.asset", "e6cdbfcaa13798e46a1acbd19bbe4813"),
            new Reference("Assets/Data/Definitions/IntroSequences/IntroSequenceDefinitionGroup.asset", "24635248bccaa1146bcee145a0e64d66"),
            new Reference("Assets/Data/Definitions/OutroSequences/OutroSequenceDefinitionGroup.asset", "11690ad6f105db04bba4fca65b5cf4db"),
            new Reference("Assets/Data/Definitions/MetaGameUnlocks/MetaGameUnlockDefinitionGroup.asset", "7bcebb6f4e03aac4ca269c2bad018f07"),
            new Reference("Assets/Data/Definitions/Camera/CameraProxyTargetSettings/CameraProxyTargetSettingsDataGroup.asset", "455c78e6f3a360244819ea7310c2bc98"),
            new Reference("Assets/Data/Definitions/CharacterAbilityUI/CharacterAbilityUIDefinitionGroup.asset", "0c54d4387bc2210439a3efca14b737b4"),
            new Reference("Assets/Data/Definitions/Transitions/Fade/FadeTransitionDefinitionGroup.asset", "f68aa9c3dd640ff49875bb00b5371831"),
            new Reference("Assets/Data/Definitions/AppRequestReview/AppRatingRequesterDefinitionGroup.asset", "c58de52da656a634f9c97aafd07af43f"),
            new Reference("Assets/Data/Definitions/Subtitles/SubtitleCharacterDefinitionGroup.asset", "77215581dc05a4b448bda950774475f4"),
            new Reference("Assets/Data/Definitions/CharacterStickyControls/StickyControlsDefinitionGroup.asset", "0cef3dc623fc65b499ab62220e1a9482"),
            new Reference("Assets/Data/Definitions/Versions/ApplicationVersionDefinitionGroup.asset", "b3c3ebbefde9e8d448fd89f79d73d433"),
            new Reference("Assets/Data/Definitions/Ranks/RankDefinitionGroup.asset", "fa0c9f8b35331c94ea799efee76c2d2f"),
            new Reference("Assets/Data/Definitions/MusicTrack/MusicTrackDefinitionGroup.asset", "77f756b250de8ca419d427ef583e65f5"),
            new Reference("Assets/MonoBehaviour/CharacterActionGlyphMapGroup.asset", "4dcdaf5e93065054cbef4875ea18f7d3"),
            new Reference("Assets/Data/Definitions/DreamPowers/DreamPowerDefinitionGroup.asset", "b9b8c1b85d14668489983eb04dbe5499"),
            new Reference("Assets/Data/Definitions/DreamPowerStoreBands/DreamPowerStoreBandDefinitionGroup.asset", "58c2094fd8c00224b9baade834fd3f6b"),
            new Reference("Assets/Data/Definitions/TouchLayout/TouchLayoutConfigurationGroup.asset", "1b85a48a4adc81b429670072ed691c96"),
            new Reference("Assets/Data/Definitions/ChallengeZoneMaterials/ChallengeZoneMaterialDefinitionGroup.asset", "aa6b78005fb8ebd4ba6110a2bdad6851"),
            new Reference("Assets/Data/Definitions/Achievements/AchievementDefinitionGroup.asset", "2ff5eeffa103db645a9e5ba08b2dc699"),
            new Reference("Assets/Data/Definitions/Missions/MissionScorerStreakDefinitionGroup.asset", "4d5e23031418ace4881cb1a0cfd3a3b0"),
            new Reference("Assets/Data/Definitions/Analytics/AnalyticsConfigurationGroup.asset", "845e086aaf07a2348aff4bde7bb63b9d"),
            new Reference("Assets/Data/Definitions/RewardTrack/RewardTrackDefinitionGroup.asset", "9f07815c46aeab441a370cb50edb3f97")
        };

        [Test]
        public void DataDefinitionsRetainsOriginalFieldsAndTypedAuthoringDeclarations()
        {
            Type owner = typeof(DataDefinitions);
            Assert.That(owner.Assembly.GetName().Name, Is.EqualTo("Game.Runtime"));
            Assert.That(owner.IsPublic && !owner.IsAbstract && !owner.IsSealed, Is.True);
            Assert.That(owner.BaseType, Is.EqualTo(typeof(ScriptableObject)));
            FieldInfo[] fields = owner.GetFields(Declared).OrderBy(f => f.MetadataToken).ToArray();
            CollectionAssert.AreEqual(new[] { "AssetMenu", "DefinitionsMenu", "GroupsMenu", "DataDefinitionsArray" }, fields.Select(f => f.Name));
            string[] constants = { "HardlightProject/DefinitionData/", "HardlightProject/DefinitionData/Definitions/", "HardlightProject/DefinitionData/Groups/" };
            for (int i = 0; i < constants.Length; i++)
            {
                Assert.That(fields[i].IsPublic && fields[i].IsStatic && fields[i].IsLiteral, Is.True);
                Assert.That(fields[i].FieldType, Is.EqualTo(typeof(string)));
                Assert.That(fields[i].GetRawConstantValue(), Is.EqualTo(constants[i]));
            }
            Assert.That(fields[3].IsPublic && !fields[3].IsStatic && !fields[3].IsNotSerialized, Is.True);
            Assert.That(fields[3].FieldType, Is.EqualTo(typeof(DefinitionDataType[])));
            Assert.That(fields[3].IsDefined(typeof(SerializeField), false), Is.False, "The original public array needs no fabricated attribute.");
            Assert.That(owner.GetProperties(Declared), Is.Empty); Assert.That(owner.GetEvents(Declared), Is.Empty);
            MethodInfo[] methods = owner.GetMethods(Declared).OrderBy(m => m.MetadataToken).ToArray();
            CollectionAssert.AreEqual(new[] { "Get", "GetGroup" }, methods.Select(m => m.Name));
            Assert.That(methods.All(m => m.IsPublic && !m.IsStatic && m.IsGenericMethodDefinition && m.GetParameters().Length == 0), Is.True);
            Type[] get = methods[0].GetGenericArguments();
            CollectionAssert.AreEqual(new[] { "TKey", "TData" }, get.Select(t => t.Name));
            Assert.That(get[0].GetGenericParameterConstraints(), Is.Empty);
            CollectionAssert.AreEqual(new[] { typeof(ScriptableObject) }, get[1].GetGenericParameterConstraints());
            Assert.That(methods[0].ReturnType.GetGenericTypeDefinition(), Is.EqualTo(typeof(Dictionary<,>)));
            CollectionAssert.AreEqual(get, methods[0].ReturnType.GetGenericArguments());
            Type group = methods[1].GetGenericArguments().Single();
            Assert.That(group.Name, Is.EqualTo("T")); Assert.That(methods[1].ReturnType, Is.EqualTo(group));
            CollectionAssert.AreEqual(new[] { typeof(ScriptableObject) }, group.GetGenericParameterConstraints());
            ConstructorInfo constructor = owner.GetConstructors(Declared).Single();
            Assert.That(constructor.IsPublic && !constructor.IsStatic, Is.True); Assert.That(constructor.GetParameters(), Is.Empty);

            CustomAttributeData[] attributes = owner.GetCustomAttributesData().ToArray();
            CollectionAssert.AreEqual(new[] { typeof(Il2CppSetOptionAttribute), typeof(Il2CppSetOptionAttribute), typeof(CreateAssetMenuAttribute) }, attributes.Select(a => a.AttributeType));
            Option[] options = { Option.ArrayBoundsChecks, Option.NullChecks };
            for (int i = 0; i < options.Length; i++)
            {
                CustomAttributeData option = attributes[i];
                CollectionAssert.AreEqual(new[] { typeof(Option), typeof(object) }, option.Constructor.GetParameters().Select(p => p.ParameterType));
                Assert.That(option.ConstructorArguments.Count, Is.EqualTo(2));
                Assert.That(option.ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(Option)));
                Assert.That(Convert.ToInt32(option.ConstructorArguments[0].Value), Is.EqualTo((int)options[i]));
                Assert.That(option.ConstructorArguments[1].ArgumentType, Is.EqualTo(typeof(bool)));
                Assert.That(option.ConstructorArguments[1].Value, Is.EqualTo(false)); Assert.That(option.NamedArguments, Is.Empty);
            }
            CustomAttributeData menu = attributes[2]; Assert.That(menu.ConstructorArguments, Is.Empty);
            Assert.That(menu.NamedArguments.Count, Is.EqualTo(2));
            Assert.That(menu.NamedArguments.Single(a => a.MemberName == "fileName").TypedValue.Value, Is.EqualTo("DataDefinitions"));
            Assert.That(menu.NamedArguments.Single(a => a.MemberName == "menuName").TypedValue.Value, Is.EqualTo("HardlightProject/DefinitionData/Groups/All Definitions"));
        }

        [Test]
        public void GenuineOwnedDataDefinitionsRetainsFiftySevenNullSlotsAndMonoScriptAfterReimport()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string folder = "Assets/__LucidOwnedDataDefinitions_" + Guid.NewGuid().ToString("N");
            string physical = CheckedPath(root, Path.Combine(root, folder));
            string path = folder + "/DataDefinitions.asset";
            var owned = new List<DataDefinitions>(); var serialized = new List<SerializedObject>();
            var secondary = new List<Exception>(); ExceptionDispatchInfo primary = null; bool created = false;
            try
            {
                Assert.That(Directory.Exists(physical) || File.Exists(physical) || File.Exists(physical + ".meta"), Is.False);
                Assert.That(AssetDatabase.IsValidFolder(folder), Is.False);
                string folderGuid = AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
                created = !String.IsNullOrEmpty(folderGuid); Assert.That(created, Is.True);
                DataDefinitions value = ScriptableObject.CreateInstance<DataDefinitions>(); owned.Add(value);
                Assert.That(value.DataDefinitionsArray, Is.Null, "Genuine original base-only construction retains the source null array.");
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(SourcePath);
                AssertScript(script, MaintainedScriptGuid, null);
                Assert.That(MonoScript.FromScriptableObject(value), Is.EqualTo(script));
                // Slot count of the immutable supplied root (the pinned 57-entry
                // References order, checked against raw pointers in the actual test).
                Assert.That(References.Length, Is.EqualTo(57));
                value.DataDefinitionsArray = new DefinitionDataType[References.Length];
                AssetDatabase.CreateAsset(value, path); EditorUtility.SetDirty(value); AssetDatabase.SaveAssetIfDirty(value);
                string assetGuid; long assetId;
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out assetGuid, out assetId), Is.True);
                Assert.That(assetId, Is.EqualTo(11400000));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                DataDefinitions loaded = AssetDatabase.LoadAssetAtPath<DataDefinitions>(path);
                Assert.That(loaded, Is.Not.Null); if (!ReferenceEquals(value, loaded)) owned.Add(loaded);
                Assert.That(loaded.GetType(), Is.EqualTo(typeof(DataDefinitions)));
                Assert.That(loaded.DataDefinitionsArray, Is.Not.Null); Assert.That(loaded.DataDefinitionsArray.Length, Is.EqualTo(References.Length));
                Assert.That(loaded.DataDefinitionsArray.All(x => ReferenceEquals(x, null)), Is.True);
                AssertIdentity(loaded, assetGuid, assetId); Assert.That(MonoScript.FromScriptableObject(loaded), Is.EqualTo(script));
                var data = new SerializedObject(loaded); serialized.Add(data); data.Update();
                Assert.That(Require(data, "m_Script", SerializedPropertyType.ObjectReference).objectReferenceValue, Is.EqualTo(script));
                SerializedProperty array = Require(data, "DataDefinitionsArray", SerializedPropertyType.Generic);
                Assert.That(array.isArray, Is.True); Assert.That(array.arraySize, Is.EqualTo(References.Length));
                for (int i = 0; i < References.Length; i++)
                {
                    SerializedProperty slot = array.GetArrayElementAtIndex(i);
                    Assert.That(slot.propertyType, Is.EqualTo(SerializedPropertyType.ObjectReference));
                    Assert.That(slot.objectReferenceValue, Is.Null, "Owned null slot " + i);
                }
            }
            catch (Exception failure) { primary = ExceptionDispatchInfo.Capture(failure); }
            finally
            {
                foreach (SerializedObject data in serialized) try { data.Dispose(); } catch (Exception failure) { secondary.Add(failure); }
                if (created)
                {
                    try { Assert.That(AssetDatabase.DeleteAsset(folder), Is.True, "Delete only this case's owned scratch."); }
                    catch (Exception failure) { secondary.Add(failure); }
                }
                foreach (DataDefinitions value in owned)
                    try { if (value != null) Object.DestroyImmediate(value); } catch (Exception failure) { secondary.Add(failure); }
                if (created)
                    try
                    {
                        Assert.That(AssetDatabase.IsValidFolder(folder), Is.False);
                        Assert.That(Directory.Exists(physical) || File.Exists(physical) || File.Exists(physical + ".meta"), Is.False, "Owned scratch and its metadata are physically absent.");
                    }
                    catch (Exception failure) { secondary.Add(failure); }
            }
            Rethrow(primary, secondary, "OwnedDataDefinitionsSecondaryFailures");
        }

        [Test]
        public void ActualSuppliedDataDefinitionsPreservesScriptAndFiftySevenOrderedRawReferences()
        {
            var observed = new Observation(); ExceptionDispatchInfo primary = null;
            try
            {
                string root = Directory.GetParent(Application.dataPath).FullName;
                string work = Environment.GetEnvironmentVariable("LUCID_RECOVERY_WORK_DIR");
                if (String.IsNullOrEmpty(work)) work = Path.Combine(root, ".cache", "project-lucid");
                work = Path.GetFullPath(work);
                ExportReceipt export = JsonUtility.FromJson<ExportReceipt>(observed.Text(work, Path.Combine(work, "assets/latest-assets.json")));
                PreparationReceipt prepared = JsonUtility.FromJson<PreparationReceipt>(observed.Text(work, Path.Combine(work, "assets/latest-prepare.json")));
                Assert.That(export, Is.Not.Null); Assert.That(prepared, Is.Not.Null);
                Assert.That(export.status, Is.EqualTo("exported")); Assert.That(prepared.status, Is.EqualTo("prepared"));
                Assert.That(export.input_fingerprint, Is.Not.Null); Assert.That(prepared.input_fingerprint, Is.Not.Null);
                Assert.That(IsHash(export.input_fingerprint.sha256), Is.True);
                Assert.That(prepared.input_fingerprint.sha256, Is.EqualTo(export.input_fingerprint.sha256));
                Bindings proof = prepared.script_bindings;
                Assert.That(proof, Is.Not.Null); Assert.That(proof.status, Is.EqualTo("verified_layout")); Assert.That(proof.references_modified, Is.True);
                AssertOwners(proof); Assert.That(proof.assets, Is.Not.Null); Assert.That(proof.assets.Length, Is.EqualTo(656));
                Assert.That(proof.source_fingerprint, Is.EqualTo(ProjectLucid.Editor.LucidArtifactIdentity.Fingerprint(root, false)), "Preparation must belong to this complete current source.");
                observed.SourceRoot = root; observed.SourceFingerprint = proof.source_fingerprint;
                Assert.That(IsHash(proof.comparison_sha256), Is.True);
                Assert.That(Hash(observed.Read(work, proof.comparison_path)), Is.EqualTo(proof.comparison_sha256));
                Assert.That(Path.GetFullPath(prepared.recovered_path), Is.EqualTo(Path.GetFullPath(Path.Combine(root, "Assets/Recovered"))));
                AssetMap map = JsonUtility.FromJson<AssetMap>(observed.Text(work, export.asset_map_path));
                Assert.That(map, Is.Not.Null); Assert.That(map.assets, Is.Not.Null);
                Binding binding = proof.bindings.Single(b => b.assembly == "Game.Runtime" && b.full_name == typeof(DataDefinitions).FullName);
                Assert.That(binding.maintained_path, Is.EqualTo(SourcePath));
                Assert.That(binding.exported_guid, Is.EqualTo(ExportedScriptGuid)); Assert.That(binding.exported_file_id, Is.EqualTo(11500000));
                Assert.That(binding.target_guid, Is.EqualTo(MaintainedScriptGuid)); Assert.That(binding.target_file_id, Is.GreaterThan(0));
                Assert.That(IsHash(binding.source_sha256), Is.True);
                Assert.That(Hash(observed.Read(root, Path.Combine(root, SourcePath))), Is.EqualTo(binding.source_sha256));
                Assert.That(MetaGuid(observed.Read(root, Path.Combine(root, SourcePath + ".meta"))), Is.EqualTo(MaintainedScriptGuid));
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(SourcePath);
                AssertScript(script, MaintainedScriptGuid, binding.target_file_id);
                ExportAsset original = SelectAsset(map, RootAssetGuid, RootAssetPath);
                CollectionAssert.AreEqual(new[] { ExportedScriptGuid }, original.script_references);
                BoundAsset bound = proof.assets.Single(a => a.guid == RootAssetGuid);
                Assert.That(bound.path, Is.EqualTo(RootAssetPath)); Assert.That(bound.pointers_rewritten, Is.EqualTo(1));
                string importedPath = "Assets/Recovered/" + RootAssetPath.Substring("Assets/".Length);
                byte[] raw = observed.Read(work, Path.Combine(export.project_path, RootAssetPath));
                byte[] converted = observed.Read(prepared.recovered_path, Path.Combine(root, importedPath));
                Assert.That(raw.Length, Is.EqualTo(4531)); Assert.That(Hash(raw), Is.EqualTo(OriginalRootHash));
                Assert.That(bound.exported_sha256, Is.EqualTo(OriginalRootHash)); Assert.That(IsHash(bound.prepared_sha256), Is.True);
                Assert.That(Hash(converted), Is.EqualTo(bound.prepared_sha256));
                ObserveMeta(observed, work, prepared.recovered_path, Path.Combine(export.project_path, RootAssetPath), Path.Combine(root, importedPath), RootAssetGuid);
                string originalText = Utf8.GetString(raw);
                string from = ScriptPointer(binding.exported_file_id, ExportedScriptGuid);
                string to = ScriptPointer(binding.target_file_id, MaintainedScriptGuid);
                Assert.That(Regex.Matches(originalText, "^" + Regex.Escape(from) + "\\r?$", RegexOptions.Multiline).Count, Is.EqualTo(1));
                CollectionAssert.AreEqual(Utf8.GetBytes(originalText.Replace(from, to)), converted, "Only the qualified root m_Script changes.");
                Assert.That(References.Length, Is.EqualTo(57));
                CollectionAssert.AreEqual(References.Select(r => "{fileID: 11400000, guid: " + r.Guid + ", type: 2}"), RootPointers(originalText));
                DataDefinitions loaded = AssetDatabase.LoadAssetAtPath<DataDefinitions>(importedPath);
                Assert.That(loaded, Is.Not.Null); Assert.That(loaded.GetType(), Is.EqualTo(typeof(DataDefinitions)));
                Assert.That(AssetDatabase.GetAssetPath(loaded), Is.EqualTo(importedPath)); AssertIdentity(loaded, RootAssetGuid, 11400000);
                Assert.That(MonoScript.FromScriptableObject(loaded), Is.EqualTo(script));
                var data = new SerializedObject(loaded); observed.Serialized.Add(data); data.Update();
                Assert.That(Require(data, "m_Script", SerializedPropertyType.ObjectReference).objectReferenceValue, Is.EqualTo(script));
                SerializedProperty array = Require(data, "DataDefinitionsArray", SerializedPropertyType.Generic);
                Assert.That(array.isArray, Is.True); Assert.That(array.arraySize, Is.EqualTo(57));
                Assert.That(loaded.DataDefinitionsArray, Is.Not.Null); Assert.That(loaded.DataDefinitionsArray.Length, Is.EqualTo(57));
                int typedSlots = 0, unqualifiedSlots = 0;
                for (int i = 0; i < References.Length; i++)
                {
                    Reference reference = References[i]; ExportAsset row = SelectAsset(map, reference.Guid, reference.Path);
                    Assert.That(row.script_references, Is.Not.Null); Assert.That(row.script_references.Length, Is.EqualTo(1));
                    string groupScriptGuid = row.script_references[0];
                    Assert.That(Regex.IsMatch(groupScriptGuid, "^[0-9a-f]{32}$"), Is.True);
                    Assert.That(proof.bindings.Any(b => b.exported_guid == groupScriptGuid), Is.False, "No referenced group script is admitted by this root-only feature.");
                    Assert.That(proof.assets.Any(a => a.guid == reference.Guid), Is.False, "The group remains outside the rewritten asset roster.");
                    string groupPath = "Assets/Recovered/" + reference.Path.Substring("Assets/".Length);
                    string rawPath = Path.Combine(export.project_path, reference.Path), currentPath = Path.Combine(root, groupPath);
                    CollectionAssert.AreEqual(observed.Read(work, rawPath), observed.Read(prepared.recovered_path, currentPath), "Unqualified referenced payload bytes stay exact: " + reference.Path);
                    ObserveMeta(observed, work, prepared.recovered_path, rawPath, currentPath, reference.Guid);
                    SerializedProperty slot = array.GetArrayElementAtIndex(i);
                    Assert.That(slot.propertyType, Is.EqualTo(SerializedPropertyType.ObjectReference));
                    Object actual = slot.objectReferenceValue; DefinitionDataType typed = loaded.DataDefinitionsArray[i];
                    if (typed != null)
                    {
                        Assert.That(actual, Is.EqualTo(typed)); AssertIdentity(typed, reference.Guid, 11400000);
                        Assert.That(AssetDatabase.GetAssetPath(typed), Is.EqualTo(groupPath));
                        MonoScript actualScript = MonoScript.FromScriptableObject(typed);
                        Assert.That(actualScript, Is.Not.Null); Assert.That(actualScript.GetClass(), Is.EqualTo(typed.GetType()));
                        AssertIdentity(actualScript, groupScriptGuid, 11500000);
                        string actualSource = AssetDatabase.GetAssetPath(actualScript);
                        Assert.That(actualSource.EndsWith(".cs", StringComparison.Ordinal), Is.True);
                        observed.Read(root, Path.Combine(root, actualSource));
                        Assert.That(MetaGuid(observed.Read(root, Path.Combine(root, actualSource + ".meta"))), Is.EqualTo(groupScriptGuid));
                        typedSlots++;
                    }
                    else
                    {
                        // An absent or non-typed imported slot is retained as unknown.
                        // Matching source metadata alone cannot qualify its payload.
                        if (actual != null) AssertIdentity(actual, reference.Guid, 11400000);
                        string eligibleSource = AssetDatabase.GUIDToAssetPath(groupScriptGuid);
                        string reason = String.IsNullOrEmpty(eligibleSource) ? "original group script GUID has no current AssetDatabase path" : "script GUID resolves to " + eligibleSource + "; typed payload/layout still unqualified";
                        TestContext.WriteLine("Unqualified supplied slot " + i + " (" + reference.Guid + "): " + reason);
                        unqualifiedSlots++;
                    }
                }
                Assert.That(typedSlots + unqualifiedSlots, Is.EqualTo(57));
                TestContext.WriteLine("Root pointers retained: 57; observed typed GUID identities: " + typedSlots + "; unqualified slots: " + unqualifiedSlots + ". No group layout, Get/GetGroup, FSM or startup qualification.");
            }
            catch (Exception failure) { primary = ExceptionDispatchInfo.Capture(failure); }
            finally { Rethrow(primary, observed.Finish(), "ReadonlyDataDefinitionsSecondaryFailures"); }
        }

        [Serializable] private sealed class Fingerprint { public string sha256 = null; }
        [Serializable] private sealed class ExportReceipt
        {
            public string status = null, project_path = null, asset_map_path = null;
            public Fingerprint input_fingerprint = null;
        }
        [Serializable] private sealed class PreparationReceipt
        {
            public string status = null, recovered_path = null;
            public Fingerprint input_fingerprint = null; public Bindings script_bindings = null;
        }
        [Serializable] private sealed class AssetMap { public ExportAsset[] assets = null; }
        [Serializable] private sealed class ExportAsset
        {
            public string path = null, guid = null; public string[] script_references = null;
        }
        [Serializable] private sealed class Bindings
        {
            public string status = null, source_fingerprint = null, comparison_path = null, comparison_sha256 = null;
            public bool references_modified; public Binding[] bindings = null; public BoundAsset[] assets = null;
        }
        [Serializable] private sealed class Binding
        {
            public string assembly = null, full_name = null, maintained_path = null;
            public string exported_guid = null, target_guid = null, source_sha256 = null;
            public long exported_file_id, target_file_id;
        }
        [Serializable] private sealed class BoundAsset
        {
            public string path = null, guid = null, exported_sha256 = null, prepared_sha256 = null; public int pointers_rewritten;
        }
        private sealed class FileObservation { public string Root, Path; public byte[] Bytes; }
        private sealed class Observation
        {
            public string SourceRoot, SourceFingerprint;
            public readonly List<SerializedObject> Serialized = new List<SerializedObject>();
            private readonly Dictionary<string, FileObservation> files = new Dictionary<string, FileObservation>(StringComparer.Ordinal);
            public byte[] Read(string root, string candidate)
            {
                string path = CheckedPath(root, candidate); byte[] bytes = File.ReadAllBytes(path);
                if (files.TryGetValue(path, out FileObservation prior)) CollectionAssert.AreEqual(prior.Bytes, bytes, "Previously observed bytes stay unchanged: " + path);
                else files.Add(path, new FileObservation { Root = root, Path = path, Bytes = bytes });
                return bytes;
            }
            public string Text(string root, string candidate) => Utf8.GetString(Read(root, candidate));
            public List<Exception> Finish()
            {
                var failures = new List<Exception>();
                for (int i = Serialized.Count - 1; i >= 0; i--)
                    try { Serialized[i].Dispose(); } catch (Exception failure) { failures.Add(failure); }
                foreach (FileObservation file in files.Values)
                    try { CollectionAssert.AreEqual(file.Bytes, File.ReadAllBytes(CheckedPath(file.Root, file.Path)), "Read-only supplied observation retained whole bytes: " + file.Path); }
                    catch (Exception failure) { failures.Add(failure); }
                if (SourceRoot != null)
                    try { Assert.That(ProjectLucid.Editor.LucidArtifactIdentity.Fingerprint(SourceRoot, false), Is.EqualTo(SourceFingerprint), "The complete current source stays unchanged through the supplied assertion."); }
                    catch (Exception failure) { failures.Add(failure); }
                return failures;
            }
        }

        private static void AssertOwners(Bindings proof)
        {
            Assert.That(proof.bindings, Is.Not.Null); Assert.That(proof.bindings.Length, Is.EqualTo(17));
            var actual = new HashSet<string>(StringComparer.Ordinal);
            foreach (Binding binding in proof.bindings)
            {
                Assert.That(binding, Is.Not.Null); string owner = binding.assembly + "::" + binding.full_name;
                Assert.That(actual.Add(owner), Is.True, "No duplicate binding owner: " + owner);
            }
            CollectionAssert.AreEquivalent(ExpectedOwners, actual, "Exact owner roster; groups remain outside this admission.");
        }
        private static ExportAsset SelectAsset(AssetMap map, string guid, string path)
        {
            ExportAsset[] selected = map.assets.Where(a => a != null && a.guid == guid).ToArray();
            Assert.That(selected.Length, Is.EqualTo(1)); Assert.That(selected[0].path, Is.EqualTo(path)); return selected[0];
        }
        private static void AssertScript(MonoScript script, string guid, long? fileId)
        {
            Assert.That(script, Is.Not.Null); Assert.That(script.GetClass(), Is.EqualTo(typeof(DataDefinitions)));
            Assert.That(AssetDatabase.GetAssetPath(script), Is.EqualTo(SourcePath));
            string actualGuid; long actualId;
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out actualGuid, out actualId), Is.True);
            Assert.That(actualGuid, Is.EqualTo(guid)); Assert.That(actualId, Is.GreaterThan(0));
            if (fileId.HasValue) Assert.That(actualId, Is.EqualTo(fileId.Value), "Compare the genuine loaded MonoScript ID to the qualified receipt.");
        }
        private static void AssertIdentity(Object value, string guid, long fileId)
        {
            string actualGuid; long actualId;
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out actualGuid, out actualId), Is.True);
            Assert.That(actualGuid, Is.EqualTo(guid)); Assert.That(actualId, Is.EqualTo(fileId));
        }
        private static void ObserveMeta(Observation observed, string rawRoot, string currentRoot, string rawPath, string currentPath, string guid)
        {
            byte[] original = observed.Read(rawRoot, rawPath + ".meta"); byte[] current = observed.Read(currentRoot, currentPath + ".meta");
            Assert.That(MetaGuid(original), Is.EqualTo(guid)); CollectionAssert.AreEqual(original, current, "Whole supplied metadata stays exact.");
        }
        private static SerializedProperty Require(SerializedObject value, string name, SerializedPropertyType type)
        {
            SerializedProperty property = value.FindProperty(name); Assert.That(property, Is.Not.Null, name);
            Assert.That(property.propertyType, Is.EqualTo(type), name); return property;
        }
        private static string[] RootPointers(string text)
        {
            Assert.That(Regex.Matches(text, "^  DataDefinitionsArray:\\r?$", RegexOptions.Multiline).Count, Is.EqualTo(1));
            MatchCollection pointers = Regex.Matches(text, "^  - (\\{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\\})\\r?$", RegexOptions.Multiline);
            return pointers.Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
        }
        private static string ScriptPointer(long fileId, string guid) => "  m_Script: {fileID: " + fileId.ToString(CultureInfo.InvariantCulture) + ", guid: " + guid + ", type: 3}";
        private static string MetaGuid(byte[] bytes)
        {
            MatchCollection matches = Regex.Matches(Utf8.GetString(bytes), "^guid: ([0-9a-f]{32})\\r?$", RegexOptions.Multiline);
            Assert.That(matches.Count, Is.EqualTo(1)); return matches[0].Groups[1].Value;
        }
        private static bool IsHash(string value) => value != null && Regex.IsMatch(value, "^[0-9a-f]{64}$");
        private static string Hash(byte[] bytes)
        {
            using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private static string CheckedPath(string ownedRoot, string candidate)
        {
            string root = Path.GetFullPath(ownedRoot), path = Path.GetFullPath(candidate);
            Assert.That(path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), Is.True, "Evidence stays beneath its declared root.");
            for (string part = path; part != null; part = Path.GetDirectoryName(part))
                if (File.Exists(part) || Directory.Exists(part)) Assert.That((int)(File.GetAttributes(part) & FileAttributes.ReparsePoint), Is.EqualTo(0), "Evidence cannot follow symlinks.");
            return path;
        }
        private static void Rethrow(ExceptionDispatchInfo primary, List<Exception> secondary, string label)
        {
            if (primary != null)
            {
                if (secondary.Count != 0) try { primary.SourceException.Data[label] = new AggregateException(secondary); } catch { }
                primary.Throw();
            }
            if (secondary.Count != 0) throw new AggregateException(secondary);
        }
    }
}
