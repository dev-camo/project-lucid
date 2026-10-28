namespace ProjectLucid.CodeProgress;

// Pure naming rules shared by the native and player signature renderers. Kept free
// of Cecil/Cpp2IL types so tools/tests/CodeProgress can compile and test them alone.
internal static class TypeIdentity
{
    // The original il2cpp metadata names base-class-library types by their defining
    // assembly ([mscorlib], [System], [System.Core], [System.Xml]); the Unity player compile only
    // references the netstandard facade, whose type forwards lead to those same
    // types. Rule: these five scopes are one BCL identity space, because a
    // namespace-qualified BCL type name is unique across it, so the forward target is
    // implied by the name. No assembly is loaded to follow the forwards (the reader
    // forbids implicit resolution). Every other scope (Unity, HL*, Dreamteck,
    // Assembly-CSharp) keeps its own name, so a same-named type from a different
    // assembly still renders differently and stays an unresolved difference.
    private static readonly HashSet<string> BclFacades = new(StringComparer.Ordinal) { "mscorlib", "netstandard", "System", "System.Core", "System.Xml" };
    internal const string BclScope = "mscorlib";

    internal static string Scope(string assembly) => BclFacades.Contains(assembly) ? BclScope : assembly;
    // The one place both renderers build a scoped, non-primitive type name.
    internal static string Qualified(string assembly, string name) => "[" + Scope(assembly) + "]" + name;

    // C# `in` parameters compile to modreq(InAttribute) T&. The original metadata
    // signature keeps no such modifier (it records direction 1 on a plain by-ref), so
    // the modifier is stripped and the by-ref kept. Other modifiers are not stripped:
    // they stay unsupported and fail loudly.
    internal const string InAttribute = "System.Runtime.InteropServices.InAttribute";
    internal static bool IsInAttribute(string modifierFullName) => modifierFullName == InAttribute;
}
