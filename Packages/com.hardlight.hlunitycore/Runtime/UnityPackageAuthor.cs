using System;
using Hardlight.JSON;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x020001c6, methods 0x06000c37/0x06000c38:
    // the "author" object of a package.json manifest.
    [Serializable]
    public readonly struct UnityPackageAuthor
    {
        public readonly string name;
        public readonly string author;
        public readonly string url;

        // 0x06000c37: keys are read in author, name, url order; a null object faults.
        public UnityPackageAuthor(JSONHashtable authorJson)
            : this(authorJson.GetString("author"), authorJson.GetString("name"), authorJson.GetString("url")) { }

        // 0x06000c38: note the author-first parameter order.
        public UnityPackageAuthor(string author, string name, string url)
        {
            this.author = author;
            this.name = name;
            this.url = url;
        }
    }
}
