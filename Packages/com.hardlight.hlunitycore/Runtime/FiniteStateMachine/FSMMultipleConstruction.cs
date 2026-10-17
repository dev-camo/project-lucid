using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Hardlight
{
    public static partial class FSMClassFactory
    {
        // HLUnityCore.Runtime.dll:Hardlight.FSMClassFactory:0x060003a9;
        // wrapper 0x1abfb24, original iterator MoveNext 0x060003c6 at
        // 0x1ac45f0. LSDA 0x2a1ede0 catches FromJson alone.
        public static IEnumerator ConstructFSMs(string json,
            Action<Dictionary<string, FiniteStateMachine>, List<string>> resultCallback = null,
            string absolutePathForIncludes = null, bool reuseExistingFSMs = false, bool isOverwriteOk = false)
        {
            JSONMultipleFSMs setup;
            try { setup = JsonUtility.FromJson<JSONMultipleFSMs>(json); }
            catch (Exception exception)
            {
                HLOutput.LogError(exception.Message);
                var errors = new List<string> { exception.Message };
                if (resultCallback != null) resultCallback(null, errors);
                yield break;
            }
            yield return ConstructFSMs(setup, resultCallback, absolutePathForIncludes, reuseExistingFSMs, isOverwriteOk);
        }

        // Original token 0x060003aa; wrapper 0x1ac0ec4, iterator MoveNext
        // 0x060003cc at 0x1ac4918. Includes are consumed first. Register every
        // new machine before populating the newly constructed machines.
        public static IEnumerator ConstructFSMs(JSONMultipleFSMs multipleFSMsSetup,
            Action<Dictionary<string, FiniteStateMachine>, List<string>> resultCallback = null,
            string absolutePathForIncludes = null, bool reuseExistingFSMs = false, bool isOverwriteOk = false)
        {
            List<string> errors = null;
            var allFSMs = new Dictionary<string, FiniteStateMachine>();
            if (multipleFSMsSetup.Includes != null)
            {
                for (int index = 0; index < multipleFSMsSetup.Includes.Count; ++index)
                {
                    // Original closure token 0x060003bf at 0x1ac420c. Included
                    // errors merge first, then dictionary.Add preserves failures
                    // for duplicate names and a null included dictionary.
                    yield return ConstructFSMsFromFile(absolutePathForIncludes, multipleFSMsSetup.Includes[index],
                        (includedFSMs, includedErrors) =>
                        {
                            if (includedErrors != null)
                            {
                                if (errors == null) errors = new List<string>();
                                errors.AddRange(includedErrors);
                            }
                            foreach (KeyValuePair<string, FiniteStateMachine> entry in includedFSMs) allFSMs.Add(entry.Key, entry.Value);
                        }, reuseExistingFSMs, isOverwriteOk);
                }
            }

            var machinesToConstruct = new List<JSONFiniteStateMachineClass>();
            for (int index = 0; index < multipleFSMsSetup.FSMs.Count; ++index)
            {
                JSONFiniteStateMachineClass setup = multipleFSMsSetup.FSMs[index];
                if (reuseExistingFSMs && FSMManager.GetManager().TryAcquireFSM(setup.Name, out FiniteStateMachine existing))
                {
                    allFSMs.Add(setup.Name, existing);
                    continue;
                }

                FiniteStateMachine machine;
                // LSDA 0x2a1ee20 covers identifier/new-FSM construction only;
                // dictionary insertion, later population and callback escape.
                try { machine = new FiniteStateMachine(new FSMIdentifier(setup.Name), isOverwriteOk); }
                catch (Exception exception)
                {
                    HLOutput.LogError(exception.Message);
                    if (errors == null) errors = new List<string>();
                    errors.Add(exception.Message);
                    continue;
                }
                allFSMs.Add(setup.Name, machine);
                machinesToConstruct.Add(setup);
            }

            for (int index = 0; index < machinesToConstruct.Count; ++index)
            {
                JSONFiniteStateMachineClass setup = machinesToConstruct[index];
                if (ConstructFSM(setup, errors, allFSMs[setup.Name], isOverwriteOk) == null)
                {
                    string error = "Failed to construct FSM '" + setup.Name + "' from JSON. Check that sub FSM, states, and transitions are constructed and named correctly.";
                    HLOutput.LogError(error);
                    if (errors == null) errors = new List<string>();
                    errors.Add(error);
                }
            }
            if (resultCallback != null) resultCallback(allFSMs, errors);
        }

        // Original token 0x060003ab; wrapper 0x1abfa80, iterator MoveNext
        // 0x060003d2 at 0x1ac548c, callback closure 0x060003c1 at 0x1ac441c.
        public static IEnumerator ConstructFSMsFromFile(string relativePathToFile,
            Action<Dictionary<string, FiniteStateMachine>, List<string>> resultCallback = null,
            bool reuseExistingFSMs = false, bool isOverwriteOk = false)
        {
            IEnumerator resultCoroutine = null;
            yield return JSONExtensions.ImportJsonFile(relativePathToFile, (json, absolutePathToJSON) =>
                resultCoroutine = ProcessJSON(json, absolutePathToJSON, resultCallback,
                    Path.GetDirectoryName(absolutePathToJSON), reuseExistingFSMs, isOverwriteOk));
            yield return resultCoroutine;
        }

        // Original token 0x060003ac; wrapper 0x1ac0fdc, iterator MoveNext
        // 0x060003d8 at 0x1ac5654, callback closure 0x060003c3 at 0x1ac4508.
        public static IEnumerator ConstructFSMsFromFile(string absolutePath, string relativePathToFile,
            Action<Dictionary<string, FiniteStateMachine>, List<string>> resultCallback = null,
            bool reuseExistingFSMs = false, bool isOverwriteOk = false)
        {
            IEnumerator resultCoroutine = null;
            yield return JSONExtensions.ImportJsonFile(absolutePath, relativePathToFile, (json, absolutePathToJSON) =>
                resultCoroutine = ProcessJSON(json, absolutePathToJSON, resultCallback,
                    Path.GetDirectoryName(absolutePathToJSON), reuseExistingFSMs, isOverwriteOk));
            yield return resultCoroutine;
        }

        // Original token 0x060003ad; wrapper 0x1ac10c8, iterator MoveNext
        // 0x060003de at 0x1ac582c. Empty data reports an error and still yields
        // a null current value once; valid data yields the JSON constructor.
        private static IEnumerator ProcessJSON(string json, string pathToFile,
            Action<Dictionary<string, FiniteStateMachine>, List<string>> resultCallback = null,
            string absolutePathForIncludes = null, bool reuseExistingFSMs = false, bool isOverwriteOk = false)
        {
            IEnumerator result = null;
            if (string.IsNullOrEmpty(json))
            {
                string error = "Failed to read FSM JSON file or file is empty: '" + pathToFile + "'.";
                HLOutput.LogError(error);
                var errors = new List<string> { error };
                if (resultCallback != null) resultCallback(null, errors);
            }
            else result = ConstructFSMs(json, resultCallback, absolutePathForIncludes, reuseExistingFSMs, isOverwriteOk);
            yield return result;
        }

        // Graph serialisation. These members follow ProcessJSON in the original
        // declaration order, so they continue this partial rather than FSMClassFactory.cs.

        // Original token 0x060003ae; x86_64 0x1abda00. The FSM list is always
        // replaced. A machine serialises itself, then each dependency as listed
        // (duplicates kept); without one, every manager-registered FSM is written.
        // Includes and view metadata of a supplied container are left untouched.
        public static JSONMultipleFSMs GenerateJSONMultipleFSMsClass(FiniteStateMachine fsm, JSONMultipleFSMs json)
        {
            if (json == null) json = new JSONMultipleFSMs();
            json.FSMs = new List<JSONFiniteStateMachineClass>();
            if (fsm != null)
            {
                json.FSMs.Add(GenerateJSONFiniteStateMachineClass(fsm));
                foreach (FiniteStateMachine dependency in fsm.GetDependencies())
                    json.FSMs.Add(GenerateJSONFiniteStateMachineClass(dependency));
            }
            else
            {
                foreach (KeyValuePair<int, FiniteStateMachine> entry in FSMManager.GetManager().FSMs)
                    json.FSMs.Add(GenerateJSONFiniteStateMachineClass(entry.Value));
            }
            return json;
        }

        // Original token 0x060003af; x86_64 0x1abe010. Classes are short type
        // names (the factory key); arguments come from each node's own
        // serialiser. Dictionary order is kept and no node metadata is written.
        public static JSONFiniteStateMachineClass GenerateJSONFiniteStateMachineClass(FiniteStateMachine fsm)
        {
            var json = new JSONFiniteStateMachineClass
            {
                Transitions = new List<JSONTransitionClass>(),
                States = new List<JSONStateClass>(),
                Name = GraphNameLookup.LookupNameUsingId(fsm.FSMId),
                DoMultipleTransitions = fsm.DoMultipleTransitions,
                UpdateTransientStates = fsm.UpdateTransientStates
            };
            if (fsm.DefaultState != null) json.DefaultState = GraphNameLookup.LookupNameUsingId(fsm.DefaultState.StateId);

            foreach (KeyValuePair<int, IFSMState> entry in fsm.States)
            {
                var stateClass = new JSONStateClass
                {
                    Name = GraphNameLookup.LookupNameUsingId(entry.Key),
                    Class = entry.Value.GetType().Name,
                    CtorArgs = entry.Value.SerialiseRuntimeToJSON(),
                    StateTransitions = new List<JSONStateTransition>()
                };
                foreach (FSMStateTransition stateTransition in entry.Value.GetStateTransitions())
                {
                    stateClass.StateTransitions.Add(new JSONStateTransition
                    {
                        Transition = GraphNameLookup.LookupNameUsingId(stateTransition.Transition.TransitionId),
                        ToState = GraphNameLookup.LookupNameUsingId(stateTransition.ToState.StateId)
                    });
                }
                json.States.Add(stateClass);
            }

            foreach (KeyValuePair<int, IFSMTransition> entry in fsm.Transitions)
            {
                json.Transitions.Add(new JSONTransitionClass
                {
                    Name = GraphNameLookup.LookupNameUsingId(entry.Key),
                    Class = entry.Value.GetType().Name,
                    CtorArgs = entry.Value.SerialiseRuntimeToJSON()
                });
            }
            return json;
        }

        // Original token 0x060003b0; x86_64 0x1abf110. Generation runs before
        // any path check. The extension test is a plain culture-sensitive
        // EndsWith. An existing file without replace is logged but the native
        // body does not return, so the StreamWriter still overwrites it.
        public static void SerialiseFSMsToFile(FiniteStateMachine finiteStateMachine, string absolutePath, bool replace = false, string extension = ".json")
        {
            string json = JsonUtility.ToJson(GenerateJSONMultipleFSMsClass(finiteStateMachine, null), true);
            if (string.IsNullOrWhiteSpace(json))
            {
                HLOutput.LogError("Failed to serialise FSM");
                return;
            }
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                HLOutput.LogError("Unable to serialise FSM, path is empty or whitespace");
                return;
            }
            if (!absolutePath.EndsWith(extension))
            {
                HLOutput.LogError("Unable to serialise FSM, file extension is not '" + extension + "'");
                return;
            }
            if (File.Exists(absolutePath))
            {
                if (replace) File.Delete(absolutePath);
                else HLOutput.LogError("Unable to serialise FSM, file exists at " + absolutePath);
            }

            string directory = Path.GetDirectoryName(absolutePath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            using (StreamWriter writer = new StreamWriter(absolutePath)) writer.Write(json);
        }
    }
}
