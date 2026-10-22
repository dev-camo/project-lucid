using ProjectLucid.CodeProgress;

// Rules of the code-progress reader's signature renderers (tools/code-progress).
internal static class Program
{
    private static int checks;

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        checks++;
    }

    private static int Main()
    {
        try
        {
            // BCL facades: the original names [mscorlib]/[System]/[System.Core], the
            // Unity compile names [netstandard]; the same type must render equally.
            string original = TypeIdentity.Qualified("mscorlib", "System.Collections.Generic.List`1");
            foreach (string facade in new[] { "mscorlib", "netstandard", "System", "System.Core" })
                Require(TypeIdentity.Qualified(facade, "System.Collections.Generic.List`1") == original, "BCL facade " + facade);
            Require(original == "[mscorlib]System.Collections.Generic.List`1", "Canonical BCL scope spelling");

            // Negative controls: a same-named type outside the BCL is a real difference.
            foreach (string other in new[] { "HLUnityCore.Runtime", "Assembly-CSharp", "Unity.Collections", "Dreamteck.Splines", "netstandard.Fake", "system" })
                Require(TypeIdentity.Qualified(other, "System.Collections.Generic.List`1") != original, "Non-BCL scope " + other + " must differ");
            Require(TypeIdentity.Qualified("HLUnityCore.Runtime", "Foo") != TypeIdentity.Qualified("HLUnityUI.Runtime", "Foo"), "Distinct proprietary scopes differ");
            Require(TypeIdentity.Qualified("mscorlib", "System.Uri") != TypeIdentity.Qualified("mscorlib", "System.Url"), "Names still differ");
            Require(TypeIdentity.Scope("HLUnityCore.Runtime") == "HLUnityCore.Runtime", "Non-BCL scope unchanged");

            // `in` parameters: only the InAttribute required modifier is stripped.
            Require(TypeIdentity.IsInAttribute("System.Runtime.InteropServices.InAttribute"), "InAttribute is stripped");
            foreach (string other in new[] { "System.Runtime.CompilerServices.IsVolatile", "System.Runtime.CompilerServices.IsExternalInit",
                         "System.Runtime.InteropServices.OutAttribute", "Other.InAttribute", "" })
                Require(!TypeIdentity.IsInAttribute(other), "Modifier " + other + " is not stripped");
            Console.WriteLine("code progress reader rule checks: " + checks);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }
}
