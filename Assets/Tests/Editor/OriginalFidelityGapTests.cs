using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Gaps found by sweeping the nested JSONCtorArgs constructors and the platform logging plugin against the
    // 1.10.1 x86-64 bodies, and the IHLModuleInfo declaration against the DummyDll metadata.
    public sealed class OriginalFidelityGapTests
    {
        private const BindingFlags AnyNested = BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyField = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private static object ParseArgs(Type owner, string json) =>
            JsonUtility.FromJson(json, owner.GetNestedType("JSONCtorArgs", AnyNested));

        private static T Field<T>(object args, string name) => (T)args.GetType().GetField(name, AnyField).GetValue(args);

        // 0x650C80 stores 0x3dcccccd (0.1f) into ControlsDelayTime before the Object constructor; the parse of an
        // empty object keeps it, and an authored value replaces it.
        [Test]
        public void PlayIntroSequencesArgsDefaultTheControlsDelayToATenthOfASecond()
        {
            object defaults = ParseArgs(typeof(ApplicationStatePlayIntroSequences), "{}");
            Assert.That(Field<float>(defaults, "ControlsDelayTime"), Is.EqualTo(0.1f));
            Assert.That(Field<string>(defaults, "EventId"), Is.Null.Or.Empty);

            object authored = ParseArgs(typeof(ApplicationStatePlayIntroSequences), "{\"ControlsDelayTime\":0.75}");
            Assert.That(Field<float>(authored, "ControlsDelayTime"), Is.EqualTo(0.75f));
        }

        // 0x70EEC0 stores 1 into NoneInMotionCausesTransition; AdditionalPauseSeconds has no initialiser.
        [Test]
        public void BossesInMotionArgsDefaultToNoneInMotionCausingTheTransition()
        {
            object defaults = ParseArgs(typeof(BossTransition_BossesInMotion), "{}");
            Assert.That(Field<bool>(defaults, "NoneInMotionCausesTransition"), Is.True);
            Assert.That(Field<float>(defaults, "AdditionalPauseSeconds"), Is.EqualTo(0f));

            object authored = ParseArgs(typeof(BossTransition_BossesInMotion), "{\"NoneInMotionCausesTransition\":false}");
            Assert.That(Field<bool>(authored, "NoneInMotionCausesTransition"), Is.False);
        }

        // 0x1B03D80 ignores the flag and reports the platform through HLOutput.LogError, the same message as
        // InternalTestNativeLogError (0x1B03E40).
        [Test]
        public void UnsupportedPluginReportsThePlatformWhateverTheDevelopmentFlag()
        {
            string expected = "Native logging is not supported in platform " + Application.platform + ".|0";
            var plugin = new HLOutputPluginUnsupported();
            using (var capture = new OwnedHLOutputErrorCapture())
            {
                Assert.That(capture.Errors, Is.Empty);
                plugin.InternalSetDevelopmentBuild(true);
                plugin.InternalSetDevelopmentBuild(false);
                plugin.InternalTestNativeLogError();
                Assert.That(capture.Errors, Is.EqualTo(new[] { expected, expected, expected }));
            }
        }

        // TypeDefIndex 9204 (0x020001c5): a public attribute-less interface of six getters in this order.
        [Test]
        public void ModuleInfoInterfaceMatchesTheShippedDeclaration()
        {
            Type type = typeof(IHLModuleInfo);
            Assert.That(type.IsInterface && type.IsPublic && type.Namespace == "Hardlight", Is.True);
            Assert.That(type.GetCustomAttributes(false), Is.Empty);
            Assert.That(type.GetFields(), Is.Empty);
            MethodInfo[] methods = type.GetMethods().OrderBy(m => m.MetadataToken).ToArray();
            Assert.That(methods.Select(m => m.Name + ":" + m.ReturnType.Name), Is.EqualTo(new[]
            {
                "get_IsUnityPackage:Boolean", "get_ModulePath:String", "get_AssetPath:String",
                "get_AbsolutePath:String", "get_RealPath:String", "get_Manifest:UnityPackageManifest",
            }));
            Assert.That(methods.All(m => m.IsAbstract && m.IsSpecialName && m.GetParameters().Length == 0), Is.True);
            Assert.That(type.GetProperties().Select(p => p.Name).OrderBy(n => n),
                Is.EqualTo(new[] { "AbsolutePath", "AssetPath", "IsUnityPackage", "Manifest", "ModulePath", "RealPath" }));
            // No shipped type implements it.
            Assert.That(typeof(HLUnityCore).Assembly.GetTypes().Concat(typeof(Boss).Assembly.GetTypes())
                .Where(t => t != type && type.IsAssignableFrom(t)), Is.Empty);
        }
    }
}
