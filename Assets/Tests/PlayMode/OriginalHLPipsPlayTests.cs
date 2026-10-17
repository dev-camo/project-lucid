using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // HLPips.DestroyPips (HLUnityUI.Runtime 0x06000077 @0x1B3BC30) in play mode,
    // where its Object.Destroy is deferred to the end of the frame. The case owns
    // one inactive root and destroys it in finally.
    public sealed class OriginalHLPipsPlayTests
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // The list is emptied at once (RemoveAt from the end) while each pip object
        // survives until the frame ends; the count and both indices are kept.
        [UnityTest]
        public IEnumerator DestroyPipsEmptiesTheListNowAndDestroysThePipsAtFrameEnd()
        {
            GameObject root = null;
            try
            {
                root = new GameObject("OriginalHLPipsPlay-owned", typeof(RectTransform));
                root.SetActive(false);
                HLPips pips = root.AddComponent<HLPips>();
                var pipRoot = new GameObject("PipRoot", typeof(RectTransform)).transform;
                pipRoot.SetParent(root.transform, false);
                var prefab = new GameObject("Element").AddComponent<HLPipsElement>();
                prefab.transform.SetParent(root.transform, false);
                prefab.OnActivate = new UnityEvent();
                prefab.OnDeactivate = new UnityEvent();
                prefab.OnInitialiseActive = new UnityEvent();
                prefab.OnInitialiseInactive = new UnityEvent();
                Set(pips, "m_pipRoot", pipRoot);
                Set(pips, "m_pipsElementPrefab", prefab);
                pips.SetupPips(3, 2);
                var list = (List<HLPipsElement>)typeof(HLPips).GetField("m_pips", Own).GetValue(pips);
                HLPipsElement[] created = list.ToArray();
                Assert.That(created.Length, Is.EqualTo(3));

                pips.DestroyPips();

                Assert.That(list, Is.Empty);
                Assert.That(created.All(pip => pip != null), Is.True, "Destroy is deferred.");
                yield return null;
                Assert.That(created.All(pip => pip == null), Is.True);
                Assert.That(prefab != null && pipRoot != null, Is.True);
                Assert.That(pipRoot.childCount, Is.EqualTo(0));
                Assert.That(Get(pips, "m_pipCount"), Is.EqualTo(3));
                Assert.That(Get(pips, "m_currentPipIndex"), Is.EqualTo(2));
                Assert.That(Get(pips, "m_previousPipIndex"), Is.EqualTo(-1));
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
            }
        }

        // The loop runs from Count - 1 down to 0 and removes each entry right after
        // its own Destroy (0x1b3bcbf..cb5). With pip 1 already destroyed, its
        // gameObject read throws after only index 2 has been destroyed and removed.
        [UnityTest]
        public IEnumerator DestroyPipsWalksBackwardsAndStopsAtADestroyedPip()
        {
            GameObject root = null;
            try
            {
                root = new GameObject("OriginalHLPipsPlay-owned", typeof(RectTransform));
                root.SetActive(false);
                HLPips pips = root.AddComponent<HLPips>();
                var pipRoot = new GameObject("PipRoot", typeof(RectTransform)).transform;
                pipRoot.SetParent(root.transform, false);
                var prefab = new GameObject("Element").AddComponent<HLPipsElement>();
                prefab.transform.SetParent(root.transform, false);
                prefab.OnActivate = new UnityEvent();
                prefab.OnDeactivate = new UnityEvent();
                prefab.OnInitialiseActive = new UnityEvent();
                prefab.OnInitialiseInactive = new UnityEvent();
                Set(pips, "m_pipRoot", pipRoot);
                Set(pips, "m_pipsElementPrefab", prefab);
                pips.SetupPips(3, 0);
                var list = (List<HLPipsElement>)typeof(HLPips).GetField("m_pips", Own).GetValue(pips);
                HLPipsElement[] created = list.ToArray();
                Object.DestroyImmediate(created[1].gameObject);

                Assert.Throws<MissingReferenceException>(() => pips.DestroyPips());

                Assert.That(list.Count, Is.EqualTo(2));
                Assert.That(ReferenceEquals(list[0], created[0]) && ReferenceEquals(list[1], created[1]), Is.True);
                yield return null;
                Assert.That(created[0] != null, Is.True);
                Assert.That(created[2] == null, Is.True);
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
            }
        }

        private static object Get(HLPips pips, string name) => typeof(HLPips).GetField(name, Own).GetValue(pips);
        private static void Set(HLPips pips, string name, object value) => typeof(HLPips).GetField(name, Own).SetValue(pips, value);
    }
}
