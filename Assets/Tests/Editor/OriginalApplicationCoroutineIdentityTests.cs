using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HardlightProject;
using NUnit.Framework;
using UnityEditor.Compilation;

namespace ProjectLucid.Editor
{
    // Compiler-generated nested types, fields, methods and iterator attributes of the
    // application FSM coroutines, compared with the original Game.Runtime metadata (dump.cs,
    // TypeDefIndex in each case) in metadata-token order, which is the dump order. The
    // original was emitted with Release code optimization; Debug emission hoists different
    // locals, so the expectations only apply to a Release compilation.
    public sealed class OriginalApplicationCoroutineIdentityTests
    {
        private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // TypeDefIndex 221-224.
        [Test]
        public void LoadAssetsMatchesTheOriginalGeneratedMembers()
        {
            AssertGenerated(typeof(ApplicationStateLoadAssets),
                new[] { ("InitialiseSaveData", "<InitialiseSaveData>d__3") },
                Array.Empty<string>(),
                Closure("<>c", new[] { "<>9", "<>9__2_0" }, "<DoOnEnter>b__2_0"),
                Closure("<>c__DisplayClass2_0", new[] { "loginSuccess" }, "<DoOnEnter>b__1"),
                StateMachine("<InitialiseSaveData>d__3", new[] { "saveManager", "user" }));
        }

        // TypeDefIndex 302-304.
        [Test]
        public void UnloadUnusedAssetsMatchesTheOriginalGeneratedMembers()
        {
            AssertGenerated(typeof(ApplicationStateUnloadUnusedAssets),
                new[] { ("ReleaseUnusedAssets", "<ReleaseUnusedAssets>d__6") },
                Array.Empty<string>(),
                StateMachine("<ReleaseUnusedAssets>d__6", new[] { "<>4__this", "user", "<i>5__2" }));
        }

        // TypeDefIndex 335-337.
        [Test]
        public void RestartSetupMatchesTheOriginalGeneratedMembers()
        {
            AssertGenerated(typeof(ApplicationStateRestartSetup),
                new[] { ("DestroyCharacter", "<DestroyCharacter>d__5") },
                Array.Empty<string>(),
                StateMachine("<DestroyCharacter>d__5", new[] { "levelManager", "user", "loadedLevel", "<>4__this", "<characterManager>5__2" }));
        }

        // TypeDefIndex 214-218. The <>7__wrap3 enumerator, <>m__Finally1 and the per-iteration
        // display class come from a foreach over MissionStates with a captured iteration local.
        [Test]
        public void InitialiseCharacterMatchesTheOriginalGeneratedMembers()
        {
            AssertGenerated(typeof(ApplicationStateInitialiseCharacter),
                new[] { ("InitialiseMissions", "<InitialiseMissions>d__8"), ("ReplaceCharacter", "<ReplaceCharacter>d__9") },
                new[] { "<ReplaceCharacter>b__9_0" },
                Closure("<>c__DisplayClass8_0", new[] { "missionState" }, "<InitialiseMissions>b__0"),
                StateMachine("<InitialiseMissions>d__8", new[] { "app", "loadedLevel", "<>4__this", "<missionManager>5__2", "<activeMissionContext>5__3", "<>7__wrap3" }, "<>m__Finally1"),
                StateMachine("<ReplaceCharacter>d__9", new[] { "app", "<>4__this", "<characterId>5__2", "<characterManager>5__3" }));
        }

        // TypeDefIndex 309-311.
        [Test]
        public void LoadLevelEnsureOneLevelMatchesTheOriginalGeneratedMembers()
        {
            AssertGenerated(typeof(ApplicationStateLoadLevelEnsureOneLevel),
                new[] { ("EnsureOneLevel", "<EnsureOneLevel>d__6") },
                Array.Empty<string>(),
                StateMachine("<EnsureOneLevel>d__6", new[] { "user", "<>4__this" }));
        }

        // TypeDefIndex 314-317.
        [Test]
        public void LoadLevelSceneMatchesTheOriginalGeneratedMembers()
        {
            AssertGenerated(typeof(ApplicationStateLoadLevelScene),
                new[] { ("LoadLevel", "<LoadLevel>d__6") },
                Array.Empty<string>(),
                Closure("<>c__DisplayClass6_0", new[] { "levelManager", "sceneDefinitionGUID" }, "<LoadLevel>b__0"),
                StateMachine("<LoadLevel>d__6", new[] { "user", "<>4__this", "<>8__1", "<sceneDefinition>5__2" }));
        }

        // TypeDefIndex 230-233.
        [Test]
        public void LoadOrUnloadSceneMatchesTheOriginalGeneratedMembers()
        {
            AssertGenerated(typeof(ApplicationStateLoadOrUnloadScene),
                new[] { ("LoadScene", "<LoadScene>d__10") },
                Array.Empty<string>(),
                StateMachine("<LoadScene>d__10", new[] { "user", "<>4__this", "<loadHandle>5__2" }));
        }

        // TypeDefIndex 237-239.
        [Test]
        public void LoadThenUnloadSceneMatchesTheOriginalGeneratedMembers()
        {
            AssertGenerated(typeof(ApplicationStateLoadThenUnloadScene),
                new[] { ("LoadAsyncScene", "<LoadAsyncScene>d__6") },
                Array.Empty<string>(),
                StateMachine("<LoadAsyncScene>d__6", new[] { "<>4__this", "user" }));
        }

        // TypeDefIndex 329-331.
        [Test]
        public void MetaGameSetupMatchesTheOriginalGeneratedMembers()
        {
            AssertGenerated(typeof(ApplicationStateMetaGameSetup),
                new[] { ("DestroyCharacter", "<DestroyCharacter>d__10") },
                Array.Empty<string>(),
                StateMachine("<DestroyCharacter>d__10", new[] { "<>4__this", "user", "missionDefinition", "<characterManager>5__2" }));
        }

        // TypeDefIndex 258-267. DisplayClass14_0 holds the onComplete parameter and the
        // method-scope startingPosition and customIntroSequence locals in one class.
        [Test]
        public void PlayIntroSequencesMatchesTheOriginalGeneratedMembers()
        {
            AssertGenerated(typeof(ApplicationStatePlayIntroSequences),
                new[] { ("PlayLevelAudio", "<PlayLevelAudio>d__13"), ("PlayCustomIntroSequence", "<PlayCustomIntroSequence>d__16") },
                Array.Empty<string>(),
                Closure("<>c__DisplayClass11_0", new[] { "<>4__this", "currentCharacter", "app", "currentLevel" }, "<DoOnEnter>b__0"),
                Closure("<>c__DisplayClass13_0", new[] { "audioManager" }, "<PlayLevelAudio>b__0"),
                Closure("<>c__DisplayClass14_0", new[] { "customIntroSequence", "onComplete", "startingPosition" }, "<TryGetCustomIntroSequence>b__0"),
                Closure("<>c__DisplayClass15_0", new[] { "<>4__this", "app", "currentCharacter" }, "<PlayIntroSequence>b__0"),
                Closure("<>c__DisplayClass16_0", new[] { "<>4__this", "app", "currentCharacter" }, "<PlayCustomIntroSequence>b__0"),
                StateMachine("<PlayCustomIntroSequence>d__16", new[] { "<>4__this", "app", "currentCharacter", "stateIntroSequence", "<>8__1" }),
                StateMachine("<PlayLevelAudio>d__13", new[] { "<>4__this", "loadedLevel", "<>8__1", "<levelAudioClipIdentifier>5__2", "<startAtRandomTime>5__3" }));
        }

        // TypeDefIndex 320-322.
        [Test]
        public void UnloadAllLevelsMatchesTheOriginalGeneratedMembers()
        {
            AssertGenerated(typeof(ApplicationStateUnloadAllLevels),
                new[] { ("UnloadAllLevels", "<UnloadAllLevels>d__5") },
                Array.Empty<string>(),
                StateMachine("<UnloadAllLevels>d__5", new[] { "<>4__this", "user" }));
        }

        // DummyDll Game.Runtime: ApplicationStateFadeTransition 0x020000ce nests JSONCtorArgs
        // 0x020000cf and <>c__DisplayClass4_0 0x020000d0 (fields 0x040003e4-6 user,
        // fadeParameters, <>4__this; methods 0x0600065e-60). DoOnEnter is member 4, after
        // JSONCtorArgs, m_eventOnMidpoint, the constructor and ConstructInstance. A
        // hand-written nested class before it shifts the natural closure to DisplayClass5_0
        // and stays behind as an escaped "_003C" type, which the "<" filter would not see.
        [Test]
        public void FadeTransitionMatchesTheOriginalGeneratedMembers()
        {
            AssertGenerated(typeof(ApplicationStateFadeTransition),
                Array.Empty<(string, string)>(),
                Array.Empty<string>(),
                Closure("<>c__DisplayClass4_0", new[] { "user", "fadeParameters", "<>4__this" }, "<DoOnEnter>b__0", "<DoOnEnter>b__1"));
            AssertOnlyNested(typeof(ApplicationStateFadeTransition), "JSONCtorArgs", "<>c__DisplayClass4_0");
        }

        // DummyDll Game.Runtime: ApplicationStateStartUITransition 0x02000124 nests
        // JSONCtorArgs 0x02000125 and <>c__DisplayClass7_0 0x02000126 (fields 0x040004ad-e
        // <>4__this, user; methods 0x06000761-63). DoOnEnter is member 7, after JSONCtorArgs,
        // the four fields, the constructor and ConstructInstance.
        [Test]
        public void StartUITransitionMatchesTheOriginalGeneratedMembers()
        {
            AssertGenerated(typeof(ApplicationStateStartUITransition),
                Array.Empty<(string, string)>(),
                Array.Empty<string>(),
                Closure("<>c__DisplayClass7_0", new[] { "<>4__this", "user" }, "<DoOnEnter>b__0", "<DoOnEnter>b__1"));
            AssertOnlyNested(typeof(ApplicationStateStartUITransition), "JSONCtorArgs", "<>c__DisplayClass7_0");
        }

        // Every nested type in token order, so escaped compiler names cannot hide, and the
        // closure's exact TypeDef flags 0x00100103 (NestedPrivate|Sealed|BeforeFieldInit).
        private static void AssertOnlyNested(Type owner, params string[] names)
        {
            Type[] nested = owner.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).OrderBy(t => t.MetadataToken).ToArray();
            Assert.That(nested.Select(t => t.Name), Is.EqualTo(names), $"{owner.Name} nested types");
            Assert.That(nested.Where(t => t.Name.StartsWith("_003C")), Is.Empty, $"{owner.Name} escaped compiler names");
            foreach (Type type in nested.Where(t => t.Name.StartsWith("<>c__DisplayClass")))
                Assert.That((int)type.Attributes, Is.EqualTo(0x00100103), $"{owner.Name}+{type.Name} flags");
        }

        private static (string name, string[] fields, string[] methods) Closure(string name, string[] fields, params string[] methods) => (name, fields, methods);

        private static (string name, string[] fields, string[] methods) StateMachine(string name, string[] hoisted, params string[] methods) =>
            (name, new[] { "<>1__state", "<>2__current" }.Concat(hoisted).ToArray(), methods);

        private static void AssertGenerated(Type owner, (string method, string stateMachine)[] iterators, string[] ownerMethods, params (string name, string[] fields, string[] methods)[] generated)
        {
            Assert.That(CompilationPipeline.codeOptimization, Is.EqualTo(CodeOptimization.Release), "the original metadata oracle assumes Release emission");
            Type[] nested = owner.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .Where(t => t.Name.StartsWith("<"))
                .OrderBy(t => t.MetadataToken)
                .ToArray();
            Assert.That(nested.Select(t => t.Name), Is.EqualTo(generated.Select(g => g.name)), $"{owner.Name} generated nested types");

            foreach ((string name, string[] fields, string[] methods) in generated)
            {
                Type type = nested.Single(t => t.Name == name);
                Assert.That(type.IsDefined(typeof(CompilerGeneratedAttribute), false), Is.True, $"{name} is compiler generated");
                Assert.That(type.GetFields(Declared).OrderBy(f => f.MetadataToken).Select(f => f.Name), Is.EqualTo(fields), $"{owner.Name}+{name} fields");
                Assert.That(GeneratedMethods(type), Is.EqualTo(methods), $"{owner.Name}+{name} generated methods");
            }

            foreach ((string method, string stateMachine) in iterators)
            {
                MethodInfo factory = owner.GetMethod(method, Declared);
                Assert.That(factory, Is.Not.Null, method);
                Type type = factory.GetCustomAttribute<IteratorStateMachineAttribute>(false)?.StateMachineType;
                Assert.That(type, Is.Not.Null, $"{owner.Name}.{method} iterator");
                Assert.That(type.Name, Is.EqualTo(stateMachine), $"{owner.Name}.{method} iterator");
                Assert.That(type.DeclaringType, Is.EqualTo(owner), $"{owner.Name}.{method} iterator owner");
            }

            Assert.That(GeneratedMethods(owner), Is.EqualTo(ownerMethods), $"{owner.Name} generated methods");
        }

        private static string[] GeneratedMethods(Type type) =>
            type.GetMethods(Declared).Where(m => m.Name.StartsWith("<")).OrderBy(m => m.MetadataToken).Select(m => m.Name).ToArray();
    }
}
