using System;
using System.Collections.Generic;
using Hardlight.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace ProjectLucid.RecoveredProof
{
    // Engine cases require an empty, quiescent test context. They temporarily
    // rename the unsaved runner scene and restore its name, active handle and random state.
    public static class OriginalResourceEngineVerification
    {
        static void Check(ref int n, bool ok, string label)
        { if (!ok) throw new InvalidOperationException(label); ++n; }

        public static int CurrentSceneNameReadsOwnedActiveScene()
        {
            int n = 0; var old = SceneManager.GetActiveScene();
            string oldName = null; bool nameCaptured = false;
            try
            {
                Check(ref n, old.IsValid() && old.isLoaded && old.path == string.Empty, "quiescent active runner scene is valid, loaded and unsaved");
                oldName = old.name; nameCaptured = true;
                old.name = "ResourcePreservationOwnedLeaf";
                Check(ref n, SceneManager.GetActiveScene().handle == old.handle, "renamed runner scene remains active");
                Check(ref n, ResourceUtils.CurrentSceneName == "ResourcePreservationOwnedLeaf", "original getter reads active scene leaf");
                Check(ref n, SceneManager.GetActiveScene().handle == old.handle, "getter preserves selected scene");
            }
            finally
            {
                if (nameCaptured)
                {
                    old.name = oldName;
                    if (old.IsValid() && old.isLoaded && SceneManager.GetActiveScene().handle != old.handle) SceneManager.SetActiveScene(old);
                }
            }
            Check(ref n, old.name == oldName, "runner scene name restored");
            Check(ref n, SceneManager.GetActiveScene().handle == old.handle, "active scene restored");
            return n;
        }

        sealed class EngineList : IList<int>
        {
            public readonly List<int> Values = new List<int> { 10, 20 };
            public readonly List<string> Trace = new List<string>(); public bool FailRead;
            public int Count { get { Trace.Add("count"); return Values.Count; } } public bool IsReadOnly => false;
            public int this[int i] { get { Trace.Add("get:"+i); if (FailRead) throw new InvalidOperationException("owned read"); return Values[i]; } set { Trace.Add("set:"+i+":"+value); Values[i]=value; } }
            public bool Contains(int v) => Values.Contains(v); public void Add(int v) => Values.Add(v); public void Clear() => Values.Clear();
            public int IndexOf(int v) => Values.IndexOf(v); public void Insert(int i,int v) => Values.Insert(i,v); public bool Remove(int v) => Values.Remove(v); public void RemoveAt(int i) => Values.RemoveAt(i);
            public void CopyTo(int[] a,int i) => Values.CopyTo(a,i); public IEnumerator<int> GetEnumerator() => Values.GetEnumerator(); System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
        public static int UnityShuffleConsumesOneDrawBeforeReadsAndRestoresState()
        {
            int n = 0; var old = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(1043); var before = UnityEngine.Random.state;
                int selected = UnityEngine.Random.Range(0, 2); var after = UnityEngine.Random.state;
                UnityEngine.Random.state = before; var list = new EngineList(); Hardlight.ListExtensions.Shuffle(list);
                Check(ref n, UnityEngine.Random.state.Equals(after), "exactly one genuine random draw");
                Check(ref n, list.Values[0] + list.Values[1] == 30 && list.Values[0] != list.Values[1], "permutation preserved");
                Check(ref n, list.Values[selected] == 20, "selected index receives original last");
                int lastValue = selected == 0 ? 10 : 20;
                Check(ref n, string.Join(",", list.Trace) == "count,get:1,get:"+selected+",set:"+selected+":20,set:1:"+lastValue, "original descending read and write order");
                UnityEngine.Random.state = before; list = new EngineList { FailRead = true }; Exception error = null;
                try { Hardlight.ListExtensions.Shuffle(list); } catch (Exception e) { error = e; }
                Check(ref n, error is InvalidOperationException && error.Message == "owned read", "owned first-read fault");
                Check(ref n, UnityEngine.Random.state.Equals(after), "random draw precedes read fault");
                Check(ref n, string.Join(",", list.Trace) == "count,get:1", "fault skips selected read and writes");
                Check(ref n, list.Values[0] == 10 && list.Values[1] == 20, "read fault preserves values");
            }
            finally { UnityEngine.Random.state = old; }
            Check(ref n, UnityEngine.Random.state.Equals(old), "exact random state restored");
            return n;
        }
    }
}
