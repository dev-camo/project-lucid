using System;
using System.Collections;
using System.IO;
using Hardlight.Utils;
using UnityEngine;
using UnityEngine.Networking;

namespace Hardlight
{
    public static class JSONExtensions
    {
        // HLUnityCore.Runtime.dll:Hardlight.JSONExtensions:0x06000288;
        // wrapper 0x1ab48fc, iterator MoveNext 0x06000292 at 0x1ab4c90;
        // callback closure 0x0600028d forwards JSON only, without a null guard.
        public static IEnumerator ImportJsonFile(string relativePathToFile, Action<string> resultCallback)
        {
            yield return ImportJsonFile(relativePathToFile, (json, path) => resultCallback(json));
        }

        // Original token 0x06000289; wrapper 0x1ab49b4;
        // iterator 0x06000298 at 0x1ab4df8, callback closure 0x0600028f.
        public static IEnumerator ImportJsonFile(string absolutePath, string relativePathToFile, Action<string> resultCallback)
        {
            yield return ImportJsonFile(absolutePath, relativePathToFile, (json, path) => resultCallback(json));
        }

        // Original token 0x0600028a; arm64 0x1ab4a88. The default root is
        // captured when the helper is called, before its returned iterator runs.
        public static IEnumerator ImportJsonFile(string relativePathToFile, Action<string, string> resultCallback)
        {
            return ImportJsonFile(Application.streamingAssetsPath, relativePathToFile, resultCallback);
        }

        // Original token 0x0600028b; wrapper 0x1ab4b70, iterator MoveNext
        // 0x0600029e at 0x1ab509c, finally 0x0600029f. Dispose the request
        // before combining the callback path and invoking the required callback.
        public static IEnumerator ImportJsonFile(string absolutePath, string relativePathToFile, Action<string, string> resultCallback)
        {
            // The native null-root path yields the default-root import, then
            // continues; it contains no yield-break after the nested operation.
            if (absolutePath == null) yield return ImportJsonFile(relativePathToFile, resultCallback);
            string json = string.Empty;
            string requestPath = FileUtilities.GetPathWithLocalFileUri(absolutePath + "/" + relativePathToFile);
            using (UnityWebRequest request = UnityWebRequest.Get(requestPath))
            {
                yield return request.SendWebRequest();
                if (string.IsNullOrEmpty(request.error)) json = request.downloadHandler.text;
                else HLOutput.LogError("JSON file \"" + requestPath + "\" could not be read: " + request.error);
            }
            resultCallback(json, Path.Combine(absolutePath, relativePathToFile));
        }
    }
}
