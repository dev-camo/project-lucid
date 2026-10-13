using System.IO;

namespace ProjectLucid.CodeProgress;

internal static class SourceDocuments
{
    // Unity package PDBs contain project-relative URLs. Tool isolation must not
    // change their meaning; absolute URLs retain their original identity.
    internal static string Resolve(string sourceRoot, string documentUrl) =>
        Path.GetFullPath(documentUrl, sourceRoot);
}
