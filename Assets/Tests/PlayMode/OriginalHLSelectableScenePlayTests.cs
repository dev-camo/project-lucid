using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UObject = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // PlayMode because the selectable needs a scene whose name the test owns;
    // SceneManager.CreateScene is play-mode only.
    public sealed class OriginalHLSelectableScenePlayTests
    {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private const string OwnedScene = "lucid-owned-selectable-scene";

        // MoveNext 0x060006f0 (x86-64 0x1b737a0): the first call clears <>2__current
        // and yields; the second stores gameObject.scene.name into m_sceneName (+0xf0,
        // 0x1b73827), reloads it (0x1b73836) and passes it to ActiveSelectables.Add
        // 0x0600042b (0x1b7385d), then finishes; a finished machine returns false
        // untouched. Add(this), Add(this, null) or Add(this, "") leave no scene row;
        // registering before the yield or again later changes a count here.
        [UnityTest]
        public IEnumerator WaitForRootObjectsYieldsOnceThenRecordsTheSceneAndRegisters()
        {
            Assert.That(SceneManager.GetSceneByName(OwnedScene).IsValid(), Is.False, "the owned scene name must be free");
            Scene scene = SceneManager.CreateScene(OwnedScene);
            Exception failure = null;
            try
            {
                CheckRegistration(scene);
            }
            catch (Exception caught)
            {
                failure = caught;
            }
            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            while (unload != null && !unload.isDone) yield return null;
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
            Assert.That(SceneManager.GetSceneByName(OwnedScene).IsValid(), Is.False);
        }

        private static void CheckRegistration(Scene scene)
        {
            using (var registry = new Registry())
            {
                var root = new GameObject("Lucid owned inactive selectable", typeof(RectTransform));
                registry.Own(root);
                root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, scene);
                HLSelectable selectable = root.AddComponent<HLSelectable>();
                Assert.That(selectable.gameObject.scene.name, Is.EqualTo(OwnedScene));

                FieldInfo sceneName = typeof(HLSelectable).GetField("m_sceneName", Declared);
                const string sentinel = "lucid-owned-sentinel-scene";
                sceneName.SetValue(selectable, sentinel);

                var routine = (IEnumerator)typeof(HLSelectable).GetMethod("WaitForRootObjects", Declared).Invoke(selectable, null);
                Assert.That(routine, Is.Not.Null);
                Assert.That(sceneName.GetValue(selectable), Is.EqualTo(sentinel), "kickoff runs no body");
                Assert.That(registry.Registered(selectable), Is.Zero);
                Assert.That(registry.RegisteredInScene(selectable, OwnedScene), Is.Zero);

                Assert.That(routine.MoveNext(), Is.True);
                Assert.That(routine.Current, Is.Null);
                Assert.That(sceneName.GetValue(selectable), Is.EqualTo(sentinel), "the scene is read only after the yield");
                Assert.That(registry.Registered(selectable), Is.Zero);
                Assert.That(registry.RegisteredInScene(selectable, OwnedScene), Is.Zero);

                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(sceneName.GetValue(selectable), Is.EqualTo(OwnedScene));
                Assert.That(selectable.SceneName, Is.EqualTo(OwnedScene));
                Assert.That(registry.Registered(selectable), Is.EqualTo(1));
                Assert.That(registry.RegisteredInScene(selectable, OwnedScene), Is.EqualTo(1));

                // The global list uses AddUnique; the per-scene list would show a repeat Add.
                Assert.That(routine.MoveNext(), Is.False);
                Assert.That(routine.Current, Is.Null);
                Assert.That(registry.Registered(selectable), Is.EqualTo(1));
                Assert.That(registry.RegisteredInScene(selectable, OwnedScene), Is.EqualTo(1));
            }
        }

        // Snapshots the ActiveSelectables statics and restores the same list
        // instances and contents; keys created during the test are dropped.
        private sealed class Registry : IDisposable
        {
            private static readonly FieldInfo AllField = typeof(ActiveSelectables).GetField("s_allSelectables", BindingFlags.NonPublic | BindingFlags.Static);
            private static readonly FieldInfo ByScene = typeof(ActiveSelectables).GetField("s_selectables", BindingFlags.NonPublic | BindingFlags.Static);
            private readonly List<HLSelectable> m_all;
            private readonly Dictionary<string, List<HLSelectable>> m_byScene;
            private readonly List<HLSelectable> m_allBefore;
            private readonly Dictionary<string, KeyValuePair<List<HLSelectable>, HLSelectable[]>> m_byBefore;
            private GameObject m_root;

            public Registry()
            {
                m_all = (List<HLSelectable>)AllField.GetValue(null);
                m_byScene = (Dictionary<string, List<HLSelectable>>)ByScene.GetValue(null);
                m_allBefore = new List<HLSelectable>(m_all);
                m_byBefore = m_byScene.ToDictionary(pair => pair.Key,
                    pair => new KeyValuePair<List<HLSelectable>, HLSelectable[]>(pair.Value, pair.Value.ToArray()));
            }

            public void Own(GameObject root)
            {
                m_root = root;
            }

            public int Registered(HLSelectable selectable)
            {
                return m_all.Count(entry => ReferenceEquals(entry, selectable));
            }

            public int RegisteredInScene(HLSelectable selectable, string scene)
            {
                List<HLSelectable> list;
                return m_byScene.TryGetValue(scene, out list) ? list.Count(entry => ReferenceEquals(entry, selectable)) : 0;
            }

            public void Dispose()
            {
                try
                {
                    if (m_root != null) UObject.DestroyImmediate(m_root);
                }
                finally
                {
                    m_all.Clear();
                    m_all.AddRange(m_allBefore);
                    m_byScene.Clear();
                    foreach (KeyValuePair<string, KeyValuePair<List<HLSelectable>, HLSelectable[]>> pair in m_byBefore)
                    {
                        List<HLSelectable> list = pair.Value.Key;
                        list.Clear();
                        list.AddRange(pair.Value.Value);
                        m_byScene[pair.Key] = list;
                    }
                }
            }
        }
    }
}
