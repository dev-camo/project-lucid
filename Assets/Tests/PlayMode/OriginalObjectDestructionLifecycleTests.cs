using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UObj = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    public sealed class OriginalObjectDestructionLifecycleTests
    {
        private static void Check(bool condition, string message, ref int count)
        {
            ++count;
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Cleanup(params GameObject[] objects)
        {
            foreach (GameObject obj in objects)
                if (obj != null) UObj.DestroyImmediate(obj);
        }

        [UnityTest]
        public IEnumerator OriginalComponentAndGameObjectDestroyRetainDeferredFrames()
        {
            int checks = 0;
            var root = new GameObject("Project Lucid original deferred object fixture");
            var child = new GameObject("Original deferred child");
            try
            {
                child.transform.SetParent(root.transform);
                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.Destroy(false);
                Check(collider != null, "Component destruction waits for the actual frame.", ref checks);
                Check(ReferenceEquals(root.GetComponent<BoxCollider>(), collider), "Scheduled collider retains its actual component identity.", ref checks);
                child.Destroy(false);
                Check(child != null && root.transform.childCount == 1, "Scheduled GameObject remains attached before frame completion.", ref checks);
                yield return null;
                yield return null;
                Check(collider == null && !ReferenceEquals(collider, null), "Destroyed component retains its managed wrapper.", ref checks);
                Check(root != null && root.GetComponent<BoxCollider>() == null, "Actual frame removes only the scheduled component.", ref checks);
                Check(child == null && !ReferenceEquals(child, null), "Destroyed GameObject retains its managed wrapper.", ref checks);
                Check(root.transform.childCount == 0, "Actual frame removes the scheduled child.", ref checks);
                Assert.That(checks, Is.EqualTo(7));
            }
            finally { Cleanup(root, child); }
        }

        [UnityTest]
        public IEnumerator OriginalChildRemovalRetainsReverseCallbacksAndDeferredFrames()
        {
            int checks = 0;
            var root = new GameObject("Project Lucid original child destruction fixture");
            var observed = new List<int>();
            var children = new List<GameObject>();
            try
            {
                for (int index = 1; index <= 3; ++index)
                {
                    var child = new GameObject("Immediate child " + index);
                    children.Add(child);
                    child.transform.SetParent(root.transform);
                    var probe = child.AddComponent<OriginalObjectDestroyProbe>();
                    probe.Identifier = index;
                    probe.Observations = observed;
                }
                root.RemoveAllChildren(true);
                Check(root.transform.childCount == 0, "Immediate removal changes the actual hierarchy before returning.", ref checks);
                Check(observed.Count == 3, "Every actual Unity OnDestroy observer ran.", ref checks);
                Check(observed[0] == 3 && observed[1] == 2 && observed[2] == 1, "Actual immediate callbacks retain the original reverse child order.", ref checks);
                Check(children.TrueForAll(child => child == null), "All original immediate child wrappers are destroyed.", ref checks);
                children.Clear();
                observed.Clear();
                for (int index = 1; index <= 2; ++index)
                {
                    var child = new GameObject("Deferred child " + index);
                    children.Add(child);
                    child.transform.SetParent(root.transform);
                    var probe = child.AddComponent<OriginalObjectDestroyProbe>();
                    probe.Identifier = index;
                    probe.Observations = observed;
                }
                root.RemoveAllChildren();
                Check(root.transform.childCount == 2 && observed.Count == 0, "Original default child removal schedules actual destruction.", ref checks);
                yield return null;
                yield return null;
                Check(root != null && root.transform.childCount == 0, "Actual frames remove all scheduled children and preserve their parent.", ref checks);
                Check(children.TrueForAll(child => child == null) && observed.Count == 2, "Scheduled children receive genuine destruction callbacks.", ref checks);
                root.RemoveAllChildren();
                Check(root.transform.childCount == 0, "Empty deferred child removal remains valid.", ref checks);
                Assert.That(checks, Is.EqualTo(8));
            }
            finally { Cleanup(root); Cleanup(children.ToArray()); }
        }

        [UnityTest]
        public IEnumerator OriginalComponentRemovalRetainsDeferredExclusionsAndTransform()
        {
            int checks = 0;
            var root = new GameObject("Project Lucid original deferred component removal fixture");
            try
            {
                BoxCollider keep = root.AddComponent<BoxCollider>();
                CapsuleCollider remove = root.AddComponent<CapsuleCollider>();
                root.RemoveAllComponents(excludeList: new List<Component> { keep });
                Check(keep != null && remove != null && root.transform != null, "Original default removal schedules components and retains exclusions.", ref checks);
                yield return null;
                yield return null;
                Check(remove == null && !ReferenceEquals(remove, null), "Actual frames destroy the selected collider wrapper.", ref checks);
                Check(ReferenceEquals(root.GetComponent<BoxCollider>(), keep) && root.GetComponents<Component>().Length == 2, "Excluded collider and genuine Transform remain.", ref checks);
                root.RemoveAllComponents();
                Check(keep != null, "Second original default removal is also deferred.", ref checks);
                yield return null;
                yield return null;
                Check(keep == null && root.transform != null && root.GetComponents<Component>().Length == 1, "Actual frames leave only the original Transform.", ref checks);
                Assert.That(checks, Is.EqualTo(5));
            }
            finally { Cleanup(root); }
        }
    }
}
