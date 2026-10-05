#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;

namespace ProjectLucid
{
    // Source-based checks of native-derived iterator ordering. This bounded
    // verification driver does not replace the original game coroutine system.
    public static class FSMFileLoadingVerification
    {
        private static readonly List<string> Trace = new List<string>();
        private static readonly HashSet<string> OwnedNames = new HashSet<string>();

        public static void Run()
        {
            string directory = Path.Combine(Application.temporaryCachePath, "Lucid-FSM-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                VerifyURIHelpers();
                VerifyJSONIterator();
                VerifyRegistrationAndReuse();
                VerifyFailuresAndPartialMachines();
                VerifyFileImportAndIncludes(directory);
            }
            finally
            {
                var manager = FSMManager.GetManager();
                foreach (string name in OwnedNames)
                    for (int attempt = 0; attempt < 20 && manager.FSMExists(name); ++attempt) manager.ReleaseFSM(name);
                OwnedNames.Clear();
                Directory.Delete(directory, true);
            }
            UnityEngine.Debug.Log("Original asynchronous FSM construction/file source verification passed. ScriptableObject acquisition and authored gameplay remain unresolved.");
        }

        private static void VerifyURIHelpers()
        {
            Check(FileUtilities.GetLocalFileUri(null) == null && FileUtilities.GetLocalFileUri(" ") == " ", "URI prefix preserves null/whitespace");
            Check(FileUtilities.GetLocalFileUri("/absolute") == "file://" && FileUtilities.GetLocalFileUri("relative") == "file:///", "Mac retail URI prefixes");
            Check(FileUtilities.GetPathWithLocalFileUri(null) == "" && FileUtilities.GetPathWithLocalFileUri(" \t") == " \t \t", "path concatenation null/whitespace behavior");
            Check(FileUtilities.GetPathWithLocalFileUri("/absolute") == "file:///absolute" && FileUtilities.GetPathWithLocalFileUri("relative") == "file:///relative", "absolute/relative path prefixes");
            Check(FileUtilities.GetPathWithLocalFileUri("file:///absolute") == "file:///file:///absolute", "existing URI is not detected or normalized");
        }

        private static void VerifyJSONIterator()
        {
            bool callback = false;
            var invalid = FSMClassFactory.ConstructFSMs("{broken", (machines, errors) =>
            {
                callback = true;
                Check(machines == null && errors.Count == 1, "parse callback gets null dictionary and one message");
            });
            Check(!callback, "parse deferred until iterator runs");
            Check(!invalid.MoveNext() && callback, "parse failure ends without a yielded value");

            callback = false;
            var valid = FSMClassFactory.ConstructFSMs("{\"FSMs\":[]}", (machines, errors) =>
            {
                callback = true;
                Check(machines.Count == 0 && errors == null, "successful empty group preserves null errors");
            });
            Check(valid.MoveNext() && valid.Current is IEnumerator && !callback, "JSON iterator yields DTO iterator before callback");
            Drain((IEnumerator)valid.Current);
            Check(callback && !valid.MoveNext(), "callback runs in nested DTO iterator only");
            bool nullCallback = false;
            Throws<NullReferenceException>(() => Drain(FSMClassFactory.ConstructFSMs((string)null, (machines, errors) => nullCallback = true)));
            Check(!nullCallback, "null parsed DTO is outside parse catch");
        }

        private static void VerifyRegistrationAndReuse()
        {
            var first = MachineSetup();
            var second = MachineSetup();
            first.States.Add(State("first", second.Name));
            first.DefaultState = "first";
            second.States.Add(State("second", first.Name));
            second.DefaultState = "second";
            Dictionary<string, FiniteStateMachine> result = null;
            List<string> errors = null;
            Trace.Clear();
            Drain(FSMClassFactory.ConstructFSMs(Group(first, second), (machines, failures) => { result = machines; errors = failures; }));
            Check(result.Count == 2 && errors == null && Trace.SequenceEqual(new[] { first.Name + ":first", second.Name + ":second" }), "all machines registered before either dependent factory runs");
            Check(result[first.Name].DefaultState != null && result[second.Name].DefaultState != null, "registered group populated in original order");

            var reusedSetup = MachineSetup();
            var reused = new FiniteStateMachine(reusedSetup.Name);
            var originalState = new FSMState(reused, "retained");
            reused.DefaultState = originalState;
            reusedSetup.States.Add(State("discarded"));
            result = null;
            Trace.Clear();
            Drain(FSMClassFactory.ConstructFSMs(Group(reusedSetup), (machines, failures) => { result = machines; Check(failures == null, "reused machine no errors"); }, reuseExistingFSMs: true));
            Check(ReferenceEquals(result[reusedSetup.Name], reused) && Trace.Count == 0 && reused.States.Count == 1 && ReferenceEquals(reused.DefaultState, originalState), "reuse skips loaded machine population");
            var manager = FSMManager.GetManager();
            Check(!manager.ReleaseFSM(reusedSetup.Name) && manager.ReleaseFSM(reusedSetup.Name), "reuse acquires one original reference");
        }

        private static void VerifyFailuresAndPartialMachines()
        {
            var duplicate = MachineSetup();
            var registered = new FiniteStateMachine(duplicate.Name);
            Dictionary<string, FiniteStateMachine> result = null;
            List<string> errors = null;
            Drain(FSMClassFactory.ConstructFSMs(Group(duplicate), (machines, failures) => { result = machines; errors = failures; }));
            Check(result.Count == 0 && errors.Count == 1 && errors[0].Contains(duplicate.Name), "constructor collision caught and excluded from population");
            Check(FSMManager.GetManager().TryGetFSM(duplicate.Name, out var stored) && ReferenceEquals(stored, registered), "failed constructor retains existing manager machine");

            var first = MachineSetup();
            var second = MachineSetup();
            first.States.Add(new FSMClassFactory.JSONStateClass { Class = "AbsentFactory", Name = "missing" });
            second.States.Add(new FSMClassFactory.JSONStateClass { Class = "AbsentFactory", Name = "missing-again" });
            result = null; errors = null;
            Drain(FSMClassFactory.ConstructFSMs(Group(first, second), (machines, failures) => { result = machines; errors = failures; }));
            Check(result.Count == 2 && result.Values.All(machine => machine.States.Count == 0), "failed population retains registered partial machines");
            Check(errors.Count == 3 && errors[0] == PopulationError(first.Name) && errors[1].StartsWith("Exception when creating state", StringComparison.Ordinal) && errors[2] == PopulationError(second.Name), "error list begins lazily after first population failure");

            var missingDefault = MachineSetup();
            missingDefault.States.Add(State("present"));
            missingDefault.DefaultState = "absent";
            bool callback = false;
            Throws<KeyNotFoundException>(() => Drain(FSMClassFactory.ConstructFSMs(Group(missingDefault), (machines, failures) => callback = true)));
            Check(!callback && FSMManager.GetManager().TryGetFSM(missingDefault.Name, out stored) && stored.States.Count == 1, "population exception escapes and preserves manager mutations");

            var sameName = MachineSetup();
            Throws<ArgumentException>(() => Drain(FSMClassFactory.ConstructFSMs(Group(sameName, sameName), isOverwriteOk: true)));
            var manager = FSMManager.GetManager();
            Check(manager.TryGetFSM(sameName.Name, out stored) && stored.States.Count == 0, "dictionary duplicate escapes after replacing registered machine");
            Check(!manager.ReleaseFSM(sameName.Name) && manager.ReleaseFSM(sameName.Name), "overwrite mutation retains increased manager reference count");
        }

        private static void VerifyFileImportAndIncludes(string directory)
        {
            File.WriteAllText(Path.Combine(directory, "text.json"), "raw-text");
            string text = null, callbackPath = null;
            Drain(JSONExtensions.ImportJsonFile(directory, "text.json", (json, path) => { text = json; callbackPath = path; }));
            Check(text == "raw-text" && callbackPath == Path.Combine(directory, "text.json"), "file import yields original text and combined callback path");
            text = null;
            Drain(JSONExtensions.ImportJsonFile(directory, "text.json", json => text = json));
            Check(text == "raw-text", "JSON-only callback overload forwards unchanged text");
            text = null;
            Drain(JSONExtensions.ImportJsonFile(directory, "absent.json", (json, path) => { text = json; callbackPath = path; }));
            Check(text == "" && callbackPath == Path.Combine(directory, "absent.json"), "request error still calls back with empty JSON");
            Throws<NullReferenceException>(() => Drain(JSONExtensions.ImportJsonFile(directory, "text.json", (Action<string, string>)null)));

            File.WriteAllText(Path.Combine(directory, "empty.json"), "");
            Dictionary<string, FiniteStateMachine> result = null;
            List<string> errors = null;
            Drain(FSMClassFactory.ConstructFSMsFromFile(directory, "empty.json", (machines, failures) => { result = machines; errors = failures; }));
            Check(result == null && errors.Single() == "Failed to read FSM JSON file or file is empty: '" + Path.Combine(directory, "empty.json") + "'.", "empty file uses original ProcessJSON diagnostic");

            var included = MachineSetup();
            // Unity's JSON roundtrip writes a null string field as "". The
            // synthetic dependency-state treats every non-null argument as a
            // machine name, so give it a real self dependency for the file case.
            Check(JsonUtility.FromJson<FSMClassFactory.JSONStateClass>(JsonUtility.ToJson(State("null-string"))).CtorArgs == "", "Unity JSON roundtrip null CtorArgs becomes empty string");
            included.States.Add(State("included", included.Name));
            included.DefaultState = "included";
            Directory.CreateDirectory(Path.Combine(directory, "nested"));
            File.WriteAllText(Path.Combine(directory, "nested", "included.json"), JsonUtility.ToJson(Group(included)));
            var main = MachineSetup();
            main.States.Add(State("main", included.Name));
            main.DefaultState = "main";
            var group = Group(main);
            group.Includes = new List<string> { "nested/included.json" };
            File.WriteAllText(Path.Combine(directory, "root.json"), JsonUtility.ToJson(group));
            Trace.Clear(); result = null; errors = null;
            Drain(FSMClassFactory.ConstructFSMsFromFile(directory, "root.json", (machines, failures) => { result = machines; errors = failures; }));
            Check(errors == null && result != null && result.Count == 2 && Trace.SequenceEqual(new[] { included.Name + ":included", main.Name + ":main" }),
                "file include completes before root machine construction; included JSON=" + JsonUtility.ToJson(Group(included))
                + "; root JSON=" + JsonUtility.ToJson(group) + "; errors=" + (errors == null ? "<null>" : string.Join(" | ", errors))
                + "; machine count=" + (result == null ? "<null>" : result.Count.ToString()) + "; trace=" + string.Join(" | ", Trace));
            Check(result[main.Name].DefaultState != null && result[included.Name].DefaultState != null, "nested include and root populated");

            var duplicateInclude = Group();
            duplicateInclude.Includes = new List<string> { "nested/included.json", "nested/included.json" };
            Throws<ArgumentException>(() => Drain(FSMClassFactory.ConstructFSMs(duplicateInclude, absolutePathForIncludes: directory, reuseExistingFSMs: true)));
        }

        public sealed class MultiMachineState : FSMState
        {
            private MultiMachineState(FiniteStateMachine fsm, FSMIdentifier id) : base(fsm, id) { }
            public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier id, string json)
            {
                Trace.Add(GraphNameLookup.LookupNameUsingId(fsm.FSMId) + ":" + id.Name);
                if (json != null && !FSMManager.GetManager().FSMExists(json)) return null;
                return new MultiMachineState(fsm, id);
            }
        }

        private static FSMClassFactory.JSONFiniteStateMachineClass MachineSetup()
        {
            string name = "Lucid-async-" + Guid.NewGuid().ToString("N");
            OwnedNames.Add(name);
            return new FSMClassFactory.JSONFiniteStateMachineClass { Name = name, States = new List<FSMClassFactory.JSONStateClass>(), Transitions = new List<FSMClassFactory.JSONTransitionClass>() };
        }
        private static FSMClassFactory.JSONMultipleFSMs Group(params FSMClassFactory.JSONFiniteStateMachineClass[] machines) => new FSMClassFactory.JSONMultipleFSMs { FSMs = new List<FSMClassFactory.JSONFiniteStateMachineClass>(machines) };
        private static FSMClassFactory.JSONStateClass State(string name, string dependency = null) => new FSMClassFactory.JSONStateClass { Class = nameof(MultiMachineState), Name = name, CtorArgs = dependency };
        private static string PopulationError(string name) => "Failed to construct FSM '" + name + "' from JSON. Check that sub FSM, states, and transitions are constructed and named correctly.";

        private static void Drain(IEnumerator iterator)
        {
            try
            {
                while (iterator.MoveNext())
                {
                    if (iterator.Current is IEnumerator nested) Drain(nested);
                    else if (iterator.Current is AsyncOperation operation)
                    {
                        var timeout = Stopwatch.StartNew();
                        while (!operation.isDone)
                        {
                            if (timeout.Elapsed.TotalSeconds > 10) throw new TimeoutException("Local FSM verification request did not finish within ten seconds.");
                            Thread.Sleep(1);
                        }
                    }
                }
            }
            finally { (iterator as IDisposable)?.Dispose(); }
        }
        private static void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException("FSM file verification failed: " + label); }
        private static TException Throws<TException>(Action action) where TException : Exception
        {
            try { action(); } catch (TException exception) { return exception; }
            throw new InvalidOperationException("FSM file verification failed: expected " + typeof(TException).Name);
        }
    }
}
#endif
