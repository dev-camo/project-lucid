using System.Reflection;
using HardlightProject;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // ApplicationStateLoadLevelScene.LoadLevel waits on its closure <LoadLevel>b__0 (060007a9 @0x658E80), whose whole
    // body is a tail call to LevelManager.IsLoadedSceneGUID (0x658e97) with the captured scene GUID.
    public sealed class ApplicationStateLoadLevelSceneWaitTests
    {
        [Test]
        public void LevelLoadWaitAsksTheLevelManagerWhetherTheSceneIsLoaded()
        {
            MethodInfo wait = typeof(ApplicationStateLoadLevelScene).GetNestedType("<>c__DisplayClass6_0", BindingFlags.NonPublic)
                .GetMethod("<LoadLevel>b__0", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(OriginalILCallSequence.Names(OriginalILCallSequence.Calls(wait)), Is.EqualTo(new[] { "LevelManager.IsLoadedSceneGUID" }));
        }
    }
}
