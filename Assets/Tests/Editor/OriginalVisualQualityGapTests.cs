using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Original VisualQualityManager.UpdateVisualFeature (0200021c). Like the
    // GetConfiguration verification, the body is invoked through an open
    // delegate with a null receiver; it never reads its instance, so no Unity
    // component, registry row or ScalablePerformance reference is created.
    public sealed class OriginalVisualQualityGapTests
    {
        private const BindingFlags Own = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private sealed class RecordingConfiguration : VisualQualityConfiguration
        {
            public readonly string Name;
            public readonly List<string> Trace;
            public RecordingConfiguration(string name, List<string> trace, PerformanceProfile.QualityLevel level)
            {
                Name = name;
                Trace = trace;
                typeof(VisualQualityConfiguration).GetField("m_qualityLevel", Own).SetValue(this, level);
            }
            public override void Apply() => Trace.Add(Name);
        }

        // Shared generic body x86-64 0x9e6fc0: the level register starts at 0
        // (Medium) and is replaced by attribute.Level (+0x18) only when
        // PerformanceProfile.GetAttribute returns non-null; the selected
        // configuration's Apply slot is then tail-called. Fails if a missing
        // attribute falls back to configurations[0] or another level, or if
        // the selected configuration is not applied exactly once.
        [Test]
        public void MissingAttributeSelectsMediumAndAppliesTheSelection()
        {
            var trace = new List<string>();
            var configurations = new[]
            {
                new RecordingConfiguration("low", trace, PerformanceProfile.QualityLevel.Low),
                new RecordingConfiguration("medium", trace, PerformanceProfile.QualityLevel.Medium),
                new RecordingConfiguration("high", trace, PerformanceProfile.QualityLevel.High),
            };
            MethodInfo update = typeof(VisualQualityManager).GetMethod("UpdateVisualFeature", Own)
                .MakeGenericMethod(typeof(RecordingConfiguration));
            var invoke = (Action<VisualQualityManager, PerformanceProfile, ScalableFeature, RecordingConfiguration[]>)Delegate.CreateDelegate(
                typeof(Action<VisualQualityManager, PerformanceProfile, ScalableFeature, RecordingConfiguration[]>), update);
            PerformanceProfile profile = null;
            ScalableFeature present = null, missing = null;
            try
            {
                profile = ScriptableObject.CreateInstance<PerformanceProfile>();
                present = ScriptableObject.CreateInstance<ScalableFeature>();
                present.ID = "owned-visual-quality-present";
                missing = ScriptableObject.CreateInstance<ScalableFeature>();
                missing.ID = "owned-visual-quality-missing";
                var attribute = new PerformanceAttribute { Feature = present, Level = PerformanceProfile.QualityLevel.High };
                ((List<PerformanceAttribute>)typeof(PerformanceProfile).GetField("m_attributes", Own).GetValue(profile)).Add(attribute);

                invoke(null, profile, missing, configurations);
                Assert.That(trace, Is.EqualTo(new[] { "medium" }), "A missing attribute selects Medium, not the first entry.");

                trace.Clear();
                invoke(null, profile, present, configurations);
                Assert.That(trace, Is.EqualTo(new[] { "high" }), "A present attribute selects its own level.");

                // An unmatched present level keeps GetConfiguration's first-entry fallback.
                attribute.Level = PerformanceProfile.QualityLevel.VeryHigh;
                trace.Clear();
                invoke(null, profile, present, configurations);
                Assert.That(trace, Is.EqualTo(new[] { "low" }));
            }
            finally
            {
                if (missing != null) UnityEngine.Object.DestroyImmediate(missing);
                if (present != null) UnityEngine.Object.DestroyImmediate(present);
                if ((UnityEngine.Object)profile != null) UnityEngine.Object.DestroyImmediate(profile);
            }
        }
    }
}
