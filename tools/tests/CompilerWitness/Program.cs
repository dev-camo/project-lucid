using System;
using System.IO;
using System.Linq;
using System.Text;
using ProjectLucid.CompilerWitness;

internal static class WitnessTests
{
    private static int checks;

    private static void Require(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    private static ResponseFile Response(string argument, string text)
    {
        return new ResponseFile { Argument = argument, ContentsBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text)) };
    }

    public static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "lucid-witness-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string original = Path.Combine(root, "original source.cs");
            string candidate = Path.Combine(root, "candidate source.cs");
            File.WriteAllText(original, "public class Source { public int Value; }", new UTF8Encoding(false));
            File.WriteAllText(candidate, "public class Source { public int Value; }", new UTF8Encoding(false));
            var request = new WitnessRequest
            {
                BaseDirectory = root,
                Arguments = new[] { "/nostdlib", "/noconfig", "/shared", "@first.rsp", "@second.rsp" },
                Responses = new[] { Response("@first.rsp", "/target:library\n/langversion:9.0\n/define:ACTIVE\n\"" + original + "\""), Response("@second.rsp", "") },
                Sources = new[] { new SourcePair { Original = original, Candidate = candidate } }
            };
            WitnessResult result = SyntaxWitness.Evaluate(request);
            Require(result.Status == "ready", "Valid syntax must not be a positive failure");
            Require(result.LanguageVersion == "CSharp9", "Capture language version, do not guess a parser default");
            Require(result.DocumentationMode == "None", "Preserve actual compiler documentation mode");
            Require(result.Defines.SequenceEqual(new[] { "ACTIVE" }), "Preserve actual conditional symbols");
            Require(result.SourceFiles.SequenceEqual(new[] { original }), "Preserve quoted source paths");

            File.WriteAllText(candidate, "public class Source { public int Value = ; }");
            result = SyntaxWitness.Evaluate(request);
            Require(result.Status == "syntax_error" && result.Errors.Length > 0, "Actual source parser must detect malformed syntax");
            Require(result.Errors.All(error => error.Id == "CS1525" && error.Path == original), "Only source diagnostics from the mapped original path");
            File.WriteAllText(candidate, "#if INACTIVE\npublic class Broken { int Value = ; }\n#endif\npublic class Source {}\n");
            Require(SyntaxWitness.Evaluate(request).Status == "ready", "Inactive conditional syntax is not an error");
            File.WriteAllText(candidate, "#if ACTIVE\npublic class Broken { int Value = ; }\n#endif\n");
            Require(SyntaxWitness.Evaluate(request).Status == "syntax_error", "Active conditional syntax must be checked");
            File.WriteAllText(candidate, "public class Source { MissingType Value; }");
            Require(SyntaxWitness.Evaluate(request).Status == "ready", "Semantic errors are outside the syntax witness");
            File.WriteAllText(candidate, "/// <summary\npublic class Source {}\n");
            Require(SyntaxWitness.Evaluate(request).Status == "ready", "DocumentationMode.None must not invent XML documentation errors");
            File.WriteAllText(candidate, "#line 999 \"forged.cs\"\npublic class Source { int Value = ; }\n");
            result = SyntaxWitness.Evaluate(request);
            Require(result.Status == "syntax_error" && result.Errors.All(error => error.Path == original && error.Line == 2),
                    "Diagnostic identity must use the actual parsed tree, not a mapped #line path");

            string[] arguments = request.Arguments;
            request.Arguments = arguments.Where(argument => argument != "/noconfig").ToArray();
            Require(SyntaxWitness.Evaluate(request).Status == "unavailable", "Implicit csc.rsp has not been captured");
            request.Arguments = arguments.Concat(new[] { "/noconfig" }).ToArray();
            Require(SyntaxWitness.Evaluate(request).Status == "unavailable", "Duplicate noconfig is outside the qualified invocation");
            request.Arguments = arguments.Concat(new[] { "/shared" }).ToArray();
            Require(SyntaxWitness.Evaluate(request).Status == "unavailable", "Duplicate client switch is unavailable");
            request.Arguments = arguments.Concat(new[] { "/warnaserror+" }).ToArray();
            File.WriteAllText(candidate, "#warning owned warning\npublic class Source {}\n");
            Require(SyntaxWitness.Evaluate(request).Status == "ready", "Promoted compilation warnings are outside the syntax witness");
            foreach (string option in new[] { "/shared:unqualified-server", "/keepalive:99", "/unrecognized-option", "/codepage:1200", "/ruleset:uncaptured.ruleset" })
            {
                request.Arguments = arguments.Concat(new[] { option }).ToArray();
                Require(SyntaxWitness.Evaluate(request).Status == "unavailable", "Unsupported/argument option is not a source error: " + option);
            }
            request.Arguments = arguments;
            ResponseFile[] responses = request.Responses;
            request.Responses = responses.Take(1).ToArray();
            Require(SyntaxWitness.Evaluate(request).Status == "unavailable", "Missing response cannot select default parse options");
            request.Responses = responses.Concat(new[] { responses[0] }).ToArray();
            Require(SyntaxWitness.Evaluate(request).Status == "unavailable", "Duplicate response evidence is ambiguous");
            request.Responses = new[] { responses[0], Response("@second.rsp", "@uncaptured.rsp") };
            Require(SyntaxWitness.Evaluate(request).Status == "unavailable", "Nested response files require separate admission, never filesystem fallback");
            foreach (string option in new[] { "/noconfig", "/shared" })
            {
                request.Responses = new[] { responses[0], Response("@second.rsp", option) };
                Require(SyntaxWitness.Evaluate(request).Status == "unavailable", "Client switch inside response has not been qualified: " + option);
            }
            request.Responses = new[] { responses[0], new ResponseFile { Argument = "@second.rsp", ContentsBase64 = "not base64" } };
            Require(SyntaxWitness.Evaluate(request).Status == "unavailable", "Malformed capture bytes are not source syntax errors");
            request.Responses = responses;

            File.WriteAllText(candidate, "public class Source { " + string.Join(" ", Enumerable.Range(0, 100).Select(index => "int value" + index + " = ;")) + " }");
            result = SyntaxWitness.Evaluate(request);
            Require(result.Status == "syntax_error" && result.Errors.Length == 32 && result.Truncated,
                    "Private diagnostic output must be bounded with explicit truncation");

            File.WriteAllBytes(candidate, new byte[] { 0xff, 0xfe, 0x41, 0x00 });
            Require(SyntaxWitness.Evaluate(request).Status == "unavailable", "Unsupported UTF-16 must not be decoded as source errors");
            File.WriteAllBytes(candidate, new byte[] { 0xff, 0x41 });
            Require(SyntaxWitness.Evaluate(request).Status == "unavailable", "Invalid UTF-8 must not use replacement characters");
            File.WriteAllBytes(candidate, new byte[] { 0x41, 0x00, 0x42, 0x00 });
            Require(SyntaxWitness.Evaluate(request).Status == "unavailable", "NUL-containing text cannot be guessed as UTF-8 source");
            File.WriteAllText(candidate, "public class Source { int Value = ; }");
            File.WriteAllText(original, "public class Source { int ExistingError = ; }");
            Require(SyntaxWitness.Evaluate(request).Status == "unavailable", "Invalid control source cannot provide a qualified baseline");
            File.WriteAllText(original, "public class Source {}");
            SourcePair[] pairs = request.Sources;
            request.Sources = pairs.Concat(new[] { new SourcePair { Original = original, Candidate = candidate } }).ToArray();
            Require(SyntaxWitness.Evaluate(request).Status == "unavailable", "Duplicate source mapping is ambiguous");
            request.Sources = new[] { new SourcePair { Original = Path.Combine(root, "not in command.cs"), Candidate = candidate } };
            Require(SyntaxWitness.Evaluate(request).Status == "unavailable", "Only actual compiler input sources may be diagnosed");
            request.Sources = pairs;
            File.Delete(candidate);
            Require(SyntaxWitness.Evaluate(request).Status == "unavailable", "IO failure cannot become positive syntax evidence");

            Console.WriteLine("Compiler witness parser checks: " + checks + " passed; Roslyn assembly " +
                              typeof(Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree).Assembly.GetName().Version + ".");
            return 0;
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
