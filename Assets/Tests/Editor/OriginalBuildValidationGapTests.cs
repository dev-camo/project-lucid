using System;
using System.Collections.Generic;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Owned configuration assets. Unity serialization never stores null
    // lookup records or exclusion strings, so the owned collections are read
    // and given null elements directly; no private field is reassigned.
    public sealed class OriginalBuildValidationGapTests
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        // BuildValidationConfiguration.GetSeverity 0x060017e6 (x86-64 0x796930)
        // runs FindIndex with the captured predicate 0x060017ee, x86-64
        // 0x796c90: the lookup argument is null-tested first and faults
        // 0x796ca4 before the captured enum (+0x10) is compared. A null-
        // conditional predicate (falling back to Error) fails here; a match
        // ahead of the null record is still found without reaching it.
        [Test]
        public void NullSeverityRecordFaultsUnlessAnEarlierRecordMatches()
        {
            var config = ScriptableObject.CreateInstance<BuildValidationConfiguration>();
            try
            {
                using (var serialized = new SerializedObject(config))
                {
                    SerializedProperty entries = serialized.FindProperty("m_errorSeverityLookups");
                    entries.arraySize = 1;
                    entries.GetArrayElementAtIndex(0).FindPropertyRelative("m_errorType").intValue = (int)ValidationErrorTypes.Audio;
                    entries.GetArrayElementAtIndex(0).FindPropertyRelative("m_errorSeverity").intValue = (int)ValidationErrorSeverity.Log;
                    Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
                }
                var lookups = (List<BuildValidationConfiguration.ErrorSeverityLookup>)typeof(BuildValidationConfiguration)
                    .GetField("m_errorSeverityLookups", AnyInstance).GetValue(config);
                lookups.Add(null);
                Assert.That(config.GetSeverity(ValidationErrorTypes.Audio), Is.EqualTo(ValidationErrorSeverity.Log));
                Assert.Throws<NullReferenceException>(() => config.GetSeverity(ValidationErrorTypes.Collider));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        // BuildValidationConfiguration.ShouldValidateScene 0x060017e7, x86-64
        // 0x796a30: after IsNullOrWhiteSpace 0x796a49 each exclusion is passed
        // straight to String.Contains 0x796a6a, in order. A null exclusion
        // throws ArgumentNullException unless an earlier one already matched.
        // Skipping null entries fails here.
        [Test]
        public void NullSceneExclusionFaultsUnlessAnEarlierExclusionMatches()
        {
            var config = ScriptableObject.CreateInstance<BuildValidationConfiguration>();
            try
            {
                using (var serialized = new SerializedObject(config))
                {
                    SerializedProperty paths = serialized.FindProperty("m_excludedScenePaths");
                    paths.arraySize = 2;
                    paths.GetArrayElementAtIndex(0).stringValue = "Boss";
                    paths.GetArrayElementAtIndex(1).stringValue = "replaced";
                    Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
                }
                var exclusions = (string[])typeof(BuildValidationConfiguration)
                    .GetField("m_excludedScenePaths", AnyInstance).GetValue(config);
                Assert.That(exclusions.Length, Is.EqualTo(2));
                exclusions[1] = null;
                Assert.That(config.ShouldValidateScene("Assets/s_BossGate.unity"), Is.False);
                Assert.Throws<ArgumentNullException>(() => config.ShouldValidateScene("Assets/s_act.unity"));
                Assert.That(config.ShouldValidateScene(" "), Is.False, "Blank input returns before the exclusions.");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
