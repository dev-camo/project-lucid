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
    }
}
