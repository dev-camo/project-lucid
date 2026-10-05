using System;
using System.Collections;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    public class BootSceneUnloadTests
    {
        private sealed class ThrowingUser : IGraphUser
        {
            public IGraphStorage Storage => throw new InvalidOperationException("original unload should not read user storage");
            public void DestroyUser() { }
        }
        [UnityTest]
        public IEnumerator OriginalBootStateUnloadsNamedScene()
        {
            Assert.That(SceneManager.GetSceneByName("s_boot").IsValid(), Is.False,
                "bounded proof requires no actual original boot scene already loaded");
            Scene proofScene = SceneManager.CreateScene("s_boot");
            var proofObject = new GameObject("ProjectLucid-Unload-scene-proof");
            SceneManager.MoveGameObjectToScene(proofObject, proofScene);
            var machine = new FiniteStateMachine("ProjectLucid-Unload-live-" + Guid.NewGuid().ToString("N"), skipAddToManager: true);
            IFSMState state = ApplicationStateUnloadBoot.ConstructInstance(machine, "UnloadBoot", null);
            try
            {
                state.OnEnter(new ThrowingUser(), null);
                for (int frame = 0; proofScene.IsValid() && frame < 120; frame++) yield return null;
                Assert.That(proofScene.IsValid(), Is.False, "actual original SceneManager unload request removes its named scene");
                Assert.That(proofObject == null, Is.True, "scene object is destroyed by actual unload");
                Assert.That(SceneManager.sceneCount, Is.GreaterThan(0), "other test scene remains loaded");
            }
            finally
            {
                if (proofScene.IsValid()) SceneManager.UnloadSceneAsync(proofScene);
            }
            // This proves the original state operation on an isolated runtime scene,
            // not authored Splash execution or whole-game behavior.
        }
    }
}
