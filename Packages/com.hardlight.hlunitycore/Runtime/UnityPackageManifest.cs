using System;
using System.Collections.Generic;
using Hardlight.JSON;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x020001c7, method 0x06000c39: the fields
    // of a package.json manifest read from a parsed JSON object.
    [Serializable]
    public readonly struct UnityPackageManifest
    {
        public readonly string name;
        public readonly string version;
        public readonly string displayName;
        public readonly string description;
        public readonly string unity;
        public readonly UnityPackageAuthor author;
        public readonly Dictionary<string, string> dependencies;

        // 0x06000c39 (x86-64 0x1b06440): a missing "author" object faults in the
        // author constructor; "dependencies" is optional and its values are read
        // as strings in key order.
        public UnityPackageManifest(JSONHashtable json)
        {
            name = json.GetString("name");
            version = json.GetString("version");
            displayName = json.GetString("displayName");
            description = json.GetString("description");
            unity = json.GetString("unity");
            author = new UnityPackageAuthor(json.GetObject("author"));
            dependencies = new Dictionary<string, string>();
            JSONHashtable dependenciesJson = json.GetObject("dependencies");
            if (dependenciesJson != null)
            {
                foreach (string key in dependenciesJson.Keys)
                    dependencies.Add(key, dependenciesJson.GetString(key));
            }
        }
    }
}
