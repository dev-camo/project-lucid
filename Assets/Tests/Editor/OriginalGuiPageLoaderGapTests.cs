using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // The loader's static lookup is swapped for an owned empty dictionary and
    // the borrowed one is put back unmodified. No scene is loaded.
    public sealed class OriginalGuiPageLoaderGapTests
    {
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.NonPublic;

        public enum DuplicatePageIdentifier
        {
            None = 0,
            First = 1,
            Twin = 7,
            TwinAlias = 7,
            Last = 9
        }

        // GuiPageLoader.InitaliseGUIPageLoaderFromEnum 0x06000164, shared
        // x86-64 0x92e980: Enum.GetValues 0x92ea6f, then for each nonzero value
        // String.Format("{0}{1}") 0x92ec5b and Dictionary.Add 0x92ead4. A
        // repeated value faults on Add; the enumerator is disposed and the
        // fault rethrown 0x92ed2e, so earlier additions stay and later values
        // are never added. TryAdd or an indexer write fails here. The
        // permanent-scene skips in UnloadAllScenes and UnloadSceneFromValue
        // need loaded scenes to observe and are not covered.
        [Test]
        public void RepeatedEnumValueFaultsAfterKeepingEarlierMappings()
        {
            FieldInfo lookupField = typeof(GuiPageLoader).GetField("m_sceneNameLookup", AnyStatic);
            MethodInfo fromEnum = typeof(GuiPageLoader).GetMethod("InitaliseGUIPageLoaderFromEnum", AnyStatic)
                .MakeGenericMethod(typeof(DuplicatePageIdentifier));
            object borrowed = lookupField.GetValue(null);
            var owned = new Dictionary<int, string>();
            lookupField.SetValue(null, owned);
            try
            {
                var fault = Assert.Throws<TargetInvocationException>(() => fromEnum.Invoke(null, new object[] { "s_lucid_" }));
                Assert.That(fault.InnerException, Is.InstanceOf<ArgumentException>());
                Assert.That(lookupField.GetValue(null), Is.SameAs(owned));
                Assert.That(owned.Count, Is.EqualTo(2));
                Assert.That(owned[1], Is.EqualTo("s_lucid_First"));
                Assert.That(owned[7], Is.EqualTo("s_lucid_" + Enum.GetName(typeof(DuplicatePageIdentifier), 7)));
                Assert.That(owned.ContainsKey(9), Is.False, "Values after the repeated one are never reached.");
                Assert.That(owned.ContainsKey(0), Is.False);
            }
            finally
            {
                lookupField.SetValue(null, borrowed);
            }
        }
    }
}
