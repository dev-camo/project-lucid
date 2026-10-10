using System;
using System.Collections;
using System.IO;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using UnityEngine;
using UnityEngine.Networking;

namespace ProjectLucid.Offline
{
    // Offline local-file request boundary. The preserved StringTable iterator
    // keeps its original string overload and is not called by this adapter.
    public static class LocalFileLocalisationLoader
    {
        public static IEnumerator LoadLocalisationDefinitions(
            Languages language, string directory, Action<ClientDataAPI.LocalisationDefinitions> callback)
        {
            string filePath = Path.Combine(Application.streamingAssetsPath, directory, language.GetString() + ".bytes");
            using (UnityWebRequest webRequest = UnityWebRequest.Get(new Uri(filePath)))
            {
                yield return webRequest.SendWebRequest();
                if (string.IsNullOrEmpty(webRequest.error))
                {
                    ClientDataAPI.LocalisationDefinitions localisationDefinition = ClientDataAPI.LocalisationDefinitions.decode(webRequest.downloadHandler.data);
                    callback(localisationDefinition);
                }
                else if (language == Language.Instance.DefaultLanguage)
                    HLOutput.LogError(string.Format(
                        "Unable to load strings file for default language {0} at path: '{1}' with error '{2}'. No fallback available.",
                        language, filePath, webRequest.error));
                else
                {
                    StringTable.AreStringsLoaded();
                    yield return LoadLocalisationDefinitions(Language.Instance.DefaultLanguage, directory, callback);
                }
            }
        }
    }
}
