using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ProjectLucid.CompilerWitness
{
    public sealed class ResponseFile
    {
        public string Argument { get; set; }
        public string ContentsBase64 { get; set; }
    }

    public sealed class SourcePair
    {
        public string Original { get; set; }
        public string Candidate { get; set; }
    }

    public sealed class WitnessRequest
    {
        public string BaseDirectory { get; set; }
        public string[] Arguments { get; set; }
        public ResponseFile[] Responses { get; set; }
        public SourcePair[] Sources { get; set; }
    }

    public sealed class SourceError
    {
        public string Id { get; set; }
        public string Path { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
    }

    public sealed class WitnessResult
    {
        public string Status { get; set; }
        public string Reason { get; set; }
        public string LanguageVersion { get; set; }
        public string DocumentationMode { get; set; }
        public string[] Defines { get; set; } = Array.Empty<string>();
        public string[] SourceFiles { get; set; } = Array.Empty<string>();
        public SourceError[] Errors { get; set; } = Array.Empty<SourceError>();
        public bool Truncated { get; set; }
    }

    public static class SyntaxWitness
    {
        public const int SyntaxErrorExit = 10;
        public const int UnavailableExit = 20;
        private const int MaximumBytes = 16 * 1024 * 1024;
        private const int MaximumDiagnostics = 32;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static WitnessResult Unavailable(string reason)
        {
            return new WitnessResult { Status = "unavailable", Reason = reason };
        }

        private static string DecodeUtf8(byte[] bytes)
        {
            if (bytes.Length > MaximumBytes) throw new ArgumentException("Input exceeds bound");
            // Other encodings are unavailable; replacement characters could invent syntax errors.
            int offset = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
            string text = StrictUtf8.GetString(bytes, offset, bytes.Length - offset);
            if (text.IndexOf('\0') >= 0) throw new ArgumentException("Unsupported NUL-containing input");
            return text;
        }

        private static string ReadSource(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaximumBytes) throw new IOException("Source unavailable");
            return DecodeUtf8(File.ReadAllBytes(path));
        }

        public static WitnessResult Evaluate(WitnessRequest request)
        {
            try
            {
                if (request == null || request.Arguments == null || request.Responses == null || request.Sources == null ||
                    string.IsNullOrEmpty(request.BaseDirectory) || !Path.IsPathFullyQualified(request.BaseDirectory))
                    return Unavailable("request-shape");
                string[] noConfig = request.Arguments.Where(argument => string.Equals(argument, "/noconfig", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (noConfig.Length != 1 || noConfig[0] != "/noconfig")
                    return Unavailable("uncaptured-default-response");
                var responses = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (ResponseFile response in request.Responses)
                {
                    if (response == null || string.IsNullOrEmpty(response.Argument) || !response.Argument.StartsWith("@", StringComparison.Ordinal) ||
                        response.ContentsBase64 == null || response.ContentsBase64.Length > MaximumBytes * 2 || responses.ContainsKey(response.Argument))
                        return Unavailable("response-shape");
                    responses.Add(response.Argument, DecodeUtf8(Convert.FromBase64String(response.ContentsBase64)));
                }
                var expanded = new List<string>();
                var consumed = new HashSet<string>(StringComparer.Ordinal);
                bool shared = false;
                foreach (string argument in request.Arguments)
                {
                    if (string.IsNullOrEmpty(argument)) return Unavailable("argument-shape");
                    if (argument == "/shared")
                    {
                        // Qualified compiler-client switch; Roslyn's language parser rejects it.
                        if (shared) return Unavailable("duplicate-client-switch");
                        shared = true;
                        continue;
                    }
                    if (!argument.StartsWith("@", StringComparison.Ordinal))
                    {
                        expanded.Add(argument);
                        continue;
                    }
                    if (!responses.TryGetValue(argument, out string text) || !consumed.Add(argument))
                        return Unavailable("missing-or-duplicate-response");
                    foreach (string token in CommandLineParser.SplitCommandLineIntoArguments(text, removeHashComments: true))
                    {
                        // Never read an uncaptured nested response or reinterpret a client option.
                        if (token.StartsWith("@", StringComparison.Ordinal) || token.Equals("/noconfig", StringComparison.OrdinalIgnoreCase))
                            return Unavailable("unsupported-response-option");
                        expanded.Add(token);
                    }
                }
                if (consumed.Count != responses.Count) return Unavailable("unused-response");
                if (expanded.Any(argument => argument.TrimStart('/', '-').StartsWith("ruleset:", StringComparison.OrdinalIgnoreCase)))
                    return Unavailable("uncaptured-auxiliary-input");
                CSharpCommandLineArguments parsed = CSharpCommandLineParser.Default.Parse(expanded, request.BaseDirectory, null);
                if (parsed.Errors.Length != 0 || parsed.SourceFiles.Length == 0)
                    return Unavailable("argument-diagnostic");
                if (parsed.Encoding != null && parsed.Encoding.CodePage != Encoding.UTF8.CodePage)
                    return Unavailable("unsupported-encoding");
                if (parsed.ParseOptions.Kind != SourceCodeKind.Regular || parsed.SourceFiles.Any(source => source.IsScript))
                    return Unavailable("unsupported-source-kind");
                string[] sources = parsed.SourceFiles.Select(source => Path.GetFullPath(source.Path, request.BaseDirectory)).ToArray();
                if (sources.Length > 10000 || sources.Any(path => path.Length > 4096) ||
                    parsed.ParseOptions.PreprocessorSymbolNames.Count() > 1000)
                    return Unavailable("metadata-bound");
                if (sources.Distinct(StringComparer.Ordinal).Count() != sources.Length)
                    return Unavailable("duplicate-compiler-source");
                var sourceSet = new HashSet<string>(sources, StringComparer.Ordinal);
                var checkedSources = new HashSet<string>(StringComparer.Ordinal);
                var errors = new List<SourceError>();
                bool truncated = false;
                foreach (SourcePair pair in request.Sources)
                {
                    if (pair == null || string.IsNullOrEmpty(pair.Original) || string.IsNullOrEmpty(pair.Candidate) ||
                        !Path.IsPathFullyQualified(pair.Original) || !Path.IsPathFullyQualified(pair.Candidate))
                        return Unavailable("source-mapping");
                    string original = Path.GetFullPath(pair.Original);
                    if (!sourceSet.Contains(original) || !checkedSources.Add(original))
                        return Unavailable("source-not-in-command");
                    SyntaxTree baseline = CSharpSyntaxTree.ParseText(ReadSource(original), parsed.ParseOptions, original, StrictUtf8);
                    if (baseline.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                        return Unavailable("invalid-control-source");
                    SyntaxTree candidate = CSharpSyntaxTree.ParseText(ReadSource(pair.Candidate), parsed.ParseOptions, original, StrictUtf8);
                    foreach (Diagnostic diagnostic in candidate.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error))
                    {
                        if (!diagnostic.Location.IsInSource || diagnostic.Location.SourceTree != candidate)
                            return Unavailable("non-source-diagnostic");
                        if (errors.Count == MaximumDiagnostics)
                        {
                            truncated = true;
                            break;
                        }
                        FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
                        errors.Add(new SourceError { Id = diagnostic.Id, Path = original,
                            Line = span.StartLinePosition.Line + 1, Column = span.StartLinePosition.Character + 1 });
                    }
                }
                return new WitnessResult
                {
                    Status = errors.Count == 0 ? "ready" : "syntax_error",
                    LanguageVersion = parsed.ParseOptions.LanguageVersion.ToString(),
                    DocumentationMode = parsed.ParseOptions.DocumentationMode.ToString(),
                    Defines = parsed.ParseOptions.PreprocessorSymbolNames.ToArray(),
                    SourceFiles = sources,
                    Errors = errors.ToArray(),
                    Truncated = truncated
                };
            }
            catch (Exception error) when (error is ArgumentException || error is IOException || error is UnauthorizedAccessException || error is FormatException)
            {
                return Unavailable("input-unavailable");
            }
        }

        public static int Main(string[] args)
        {
            WitnessResult result;
            try
            {
                if (args.Length != 1) result = Unavailable("request-argument");
                else result = Evaluate(JsonSerializer.Deserialize<WitnessRequest>(ReadSource(args[0]), JsonOptions));
            }
            catch (Exception error) when (error is JsonException || error is ArgumentException || error is IOException || error is UnauthorizedAccessException)
            {
                result = Unavailable("request-unavailable");
            }
            string output = JsonSerializer.Serialize(result, JsonOptions);
            if (StrictUtf8.GetByteCount(output) > MaximumBytes)
            {
                result = Unavailable("output-bound");
                output = JsonSerializer.Serialize(result, JsonOptions);
            }
            Console.WriteLine(output);
            return result.Status == "syntax_error" ? SyntaxErrorExit : result.Status == "ready" ? 0 : UnavailableExit;
        }
    }
}
