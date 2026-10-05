using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AssetRipper.Primitives;
using Cpp2IL.Core;
using Cpp2IL.Core.Extensions;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.Model.CustomAttributes;
using Cpp2IL.Core.Utils;
using LibCpp2IL.BinaryStructures;

namespace ProjectLucid.NativeRecovery;

internal static class Program
{
    private const string Pin = "b5ad444b82267cb1e4b88b8b373c008105bdea52";
    private const string ArchiveHash = "e396fc9a5321a121458b261d6a164945b916c9d024bf2cc96e2953dfe7d4069f";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };
    private static readonly string[] Limitations =
    [
        "Schemas and native/ISIL evidence are research outputs, never Unity runtime assemblies.",
        "Unity field candidates use visibility/attributes/flags; actual Unity type eligibility and serialized layouts require independent verification.",
        "Native method addresses come from pinned Cpp2IL metadata interpretation and are not automatically verified against binary symbols.",
        "Raw ISIL is an initial instruction translation, may contain unsupported or inferred instructions, and does not establish recovered managed semantics."
    ];

    public static int Main(string[] arguments)
    {
        try
        {
            var args = Parse(arguments);
            string output = ValidateOutput(args["--output"]);
            string binary = ValidateInput(args["--binary"], 2L * 1024 * 1024 * 1024);
            string metadata = ValidateInput(args["--metadata"], 512L * 1024 * 1024);
            ValidateBinary(binary);
            UnityVersion version = UnityVersion.Parse(args["--unity-version"]);
            uint? token = args.TryGetValue("--token", out string? tokenText) ? ParseMethodToken(tokenText) : null;
            int rawMetadataVersion = MetadataVersion(metadata);
            Console.Error.WriteLine(args["command"] == "recover-method"
                ? "Initializing exact pinned Core for one experimental, bounded method recovery."
                : "Initializing exact pinned Cpp2IL Core; no method Analyze/StackAnalyzer pipeline will be invoked.");
            new Cpp2IlCorePlugin().OnLoad();
            Cpp2IlApi.InitializeLibCpp2Il(binary, metadata, version, false);
            var app = Cpp2IlApi.CurrentAppContext!;
            var assemblies = app.Assemblies.Where(a => a.Definition is not null).ToList();
            if (args.TryGetValue("--assembly", out string? assemblyName))
            {
                assemblies = assemblies.Where(a => a.DefaultName == assemblyName).ToList();
                if (assemblies.Count != 1)
                    throw new ArgumentException("Assembly must match one exact original assembly name: " + assemblyName);
            }
            var report = new Dictionary<string, object?>
            {
                ["schema_version"] = 1, ["command"] = args["command"], ["status"] = "ready",
                ["cpp2il_pin"] = Pin, ["cpp2il_source_archive_sha256"] = ArchiveHash,
                ["native_binary"] = binary, ["native_binary_sha256"] = HashFile(binary),
                ["metadata"] = metadata, ["metadata_sha256"] = HashFile(metadata),
                ["unity_version"] = version.ToString(), ["metadata_header_version"] = rawMetadataVersion,
                ["metadata_interpreted_version"] = app.MetadataVersion,
                ["custom_attribute_string_encoding"] = "metadata-v29-utf8-length-preserved",
                ["instruction_set"] = app.Binary.InstructionSetId.ToString(),
                ["output_dir"] = output, ["managed_semantics_recovered"] = false,
                ["native_addresses_verified"] = false, ["limitations"] = Limitations
            };
            if (args["command"] is "method" or "recover-method")
            {
                var matches = assemblies[0].Types.SelectMany(t => t.Methods)
                    .Where(m => m.Definition is not null && m.Token == token!.Value).ToList();
                if (matches.Count != 1)
                    throw new ArgumentException("Method token must match one method in the selected assembly: " + Hex(token!.Value));
                var method = matches[0];
                if (args["command"] == "recover-method") ExperimentalRecovery.ValidateSelection(method);
                if (method.UnderlyingPointer == 0 || method.IsAbstract || method.RawBytes.Length == 0)
                    throw new ArgumentException("Selected method has no concrete native body.");
                if (method.RawBytes.Length > 30000)
                    throw new ArgumentException("Selected method exceeds the 30,000-byte evidence limit.");
                var identity = MethodIdentity(method);
                identity["native_pointer"] = "0x" + method.UnderlyingPointer.ToString("x", CultureInfo.InvariantCulture);
                identity["native_rva"] = "0x" + method.Rva.ToString("x", CultureInfo.InvariantCulture);
                identity["native_size"] = method.RawBytes.Length;
                report["method"] = identity;
                Directory.CreateDirectory(output);
                // Flush the selected identity before any disassembler call, retaining a
                // reproducible crash locator even if the upstream native decoder fails.
                report["status"] = "pending";
                WriteReport(output, report);
                Console.Error.WriteLine("Selected method: " + JsonSerializer.Serialize(identity));
                File.WriteAllBytes(Path.Combine(output, "native.bin"), method.RawBytes.ToArray());
                File.WriteAllText(Path.Combine(output, "native.txt"), app.InstructionSet.PrintAssembly(method) + "\n", new UTF8Encoding(false));
                var isil = app.InstructionSet.GetIsilFromMethod(method);
                File.WriteAllLines(Path.Combine(output, "isil.txt"), isil.Select(i => i.ToString()), new UTF8Encoding(false));
                identity["native_sha256"] = HashFile(Path.Combine(output, "native.bin"));
                report["isil_instruction_count"] = isil.Count;
                report["analysis_pipeline_invoked"] = false;
                report["status"] = "ready";
                if (args["command"] == "recover-method")
                {
                    report["status"] = "pending";
                    report["experimental"] = true;
                    WriteReport(output, report);
                    try
                    {
                        ExperimentalRecovery.Recover(method, binary, output, report);
                        report["status"] = "ready";
                    }
                    catch (Exception error)
                    {
                        report["status"] = "incomplete";
                        report["recovery_error"] = error.Message;
                    }
                }
            }
            else
            {
                var errors = new List<Dictionary<string, string>>();
                report["assemblies"] = assemblies.Select(a => AssemblySchema(a, errors)).ToArray();
                report["assembly_count"] = assemblies.Count;
                report["type_count"] = assemblies.Sum(a => a.Types.Count(t => t.Definition is not null));
                report["field_count"] = assemblies.Sum(a => a.Types.Where(t => t.Definition is not null).Sum(t => t.Fields.Count));
                report["errors"] = errors;
                if (errors.Count != 0) report["status"] = "incomplete";
                Directory.CreateDirectory(output);
            }
            WriteReport(output, report);
            Console.WriteLine(JsonSerializer.Serialize(new { status = report["status"], report = Path.Combine(output, "report.json") }));
            return report["status"] as string == "ready" ? 0 : 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new { status = "failed", error = error.Message, exception_type = error.GetType().FullName }));
            return 2;
        }
    }

    private static Dictionary<string, string> Parse(string[] arguments)
    {
        if (arguments.Length == 0 || arguments[0] is not ("schemas" or "method" or "recover-method"))
            throw new ArgumentException("Usage: schemas|method|recover-method --binary FILE --metadata FILE --unity-version VERSION --output FRESH_CACHE_DIR [--assembly EXACT_NAME] [--token 0x06xxxxxx]. recover-method is an experimental threshold-only cache proof.");
        var result = new Dictionary<string, string>(StringComparer.Ordinal) { ["command"] = arguments[0] };
        string[] options = ["--binary", "--metadata", "--unity-version", "--output", "--assembly", "--token"];
        for (int i = 1; i < arguments.Length; i += 2)
        {
            if (!options.Contains(arguments[i]) || i + 1 == arguments.Length || arguments[i + 1].StartsWith("--", StringComparison.Ordinal) ||
                !result.TryAdd(arguments[i], arguments[i + 1]))
                throw new ArgumentException("Unknown, duplicate, or missing option value: " + arguments[i]);
        }
        foreach (string required in new[] { "--binary", "--metadata", "--unity-version", "--output" })
            if (!result.ContainsKey(required)) throw new ArgumentException("Missing required option: " + required);
        if (arguments[0] is "method" or "recover-method" && (!result.ContainsKey("--assembly") || !result.ContainsKey("--token")))
            throw new ArgumentException(arguments[0] + " requires exact --assembly and --token selections.");
        if (arguments[0] == "schemas" && result.ContainsKey("--token"))
            throw new ArgumentException("--token is only valid with a method command.");
        return result;
    }

    private static uint ParseMethodToken(string text)
    {
        if (!text.StartsWith("0x", StringComparison.Ordinal) || text.Length != 10 ||
            !uint.TryParse(text[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint token) ||
            (token & 0xff000000) != 0x06000000 || (token & 0x00ffffff) == 0)
            throw new ArgumentException("Method token must be an original MethodDef token in exact 0x06xxxxxx form.");
        return token;
    }

    private static void RejectLinks(string path)
    {
        for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
            if ((current.Exists || File.Exists(current.FullName)) && (File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("Input/output paths cannot contain symbolic links or reparse points.");
    }

    private static string ValidateInput(string value, long limit)
    {
        string path = Path.GetFullPath(value);
        RejectLinks(path);
        var file = new FileInfo(path);
        if (!file.Exists || file.Length < 8 || file.Length > limit)
            throw new ArgumentException("Recovery input is missing, truncated, or exceeds its size limit: " + path);
        return path;
    }

    private static string ValidateOutput(string value)
    {
        string path = Path.GetFullPath(value);
        RejectLinks(path);
        if (File.Exists(path) || Directory.Exists(path)) throw new ArgumentException("Output directory must be fresh.");
        var cache = new DirectoryInfo(path).Parent;
        while (cache is not null && !(cache.Name == "project-lucid" && cache.Parent?.Name == ".cache")) cache = cache.Parent;
        if (cache?.Parent?.Parent is null) throw new ArgumentException("Outputs are restricted to the repository's generated recovery cache.");
        string lockPath = Path.Combine(cache.Parent.Parent.FullName, "tools", "tool-lock.json");
        if (!File.Exists(lockPath)) throw new ArgumentException("Output path does not belong to a recovery repository.");
        using var toolLock = JsonDocument.Parse(File.ReadAllText(lockPath));
        var source = toolLock.RootElement.GetProperty("artifacts").GetProperty("cpp2il-source");
        if (source.GetProperty("version").GetString() != Pin || source.GetProperty("sha256").GetString() != ArchiveHash)
            throw new ArgumentException("Repository tool lock does not match the harness source pin.");
        return path;
    }

    private static int MetadataVersion(string metadata)
    {
        using var file = File.OpenRead(metadata);
        using var reader = new BinaryReader(file);
        if (reader.ReadUInt32() != 0xfab11baf) throw new ArgumentException("Invalid IL2CPP metadata signature.");
        int version = reader.ReadInt32();
        if (version < 29 || version > 31) throw new ArgumentException("This metadata-only attribute route supports metadata header versions 29 through 31.");
        return version;
    }

    private static void ValidateBinary(string binary)
    {
        using var file = File.OpenRead(binary);
        using var reader = new BinaryReader(file);
        if (reader.ReadUInt32() != 0xfeedfacf || reader.ReadUInt32() is not (0x01000007 or 0x0100000c))
            throw new ArgumentException("Native input must be a thin, little-endian x86_64 or arm64 Mach-O binary.");
        if (file.Length < 32) throw new ArgumentException("Native Mach-O header is truncated.");
    }

    private static string HashFile(string path)
    {
        using var file = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();
    }

    private static string Hex(uint value) => "0x" + value.ToString("x8", CultureInfo.InvariantCulture);

    private static void WriteReport(string output, Dictionary<string, object?> report)
    {
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, JsonOptions) + "\n", new UTF8Encoding(false));
    }

    private static Dictionary<string, object?> AssemblySchema(AssemblyAnalysisContext assembly, List<Dictionary<string, string>> errors)
        => new()
        {
            ["name"] = assembly.DefaultName, ["image_name"] = assembly.Definition!.Image.Name,
            ["token"] = Hex(assembly.Token), ["version"] = assembly.DefaultVersion.ToString(),
            ["culture"] = assembly.DefaultCulture, ["public_key_token"] = assembly.DefaultPublicKeyToken is null ? null :
                Convert.ToHexString(assembly.DefaultPublicKeyToken).ToLowerInvariant(),
            ["types"] = assembly.Types.Where(t => t.Definition is not null).Select(t => TypeSchema(t, errors)).ToArray()
        };

    private static Dictionary<string, object?> TypeSchema(TypeAnalysisContext type, List<Dictionary<string, string>> errors)
    {
        var record = new Dictionary<string, object?>
        {
            ["assembly"] = type.DeclaringAssembly.DefaultName, ["token"] = Hex(type.Token),
            ["name"] = type.DefaultName, ["namespace"] = type.DefaultNamespace, ["full_name"] = type.DefaultFullName,
            ["attributes"] = (int)type.DefaultAttributes, ["is_value_type"] = type.IsValueType,
            ["is_enum"] = type.IsEnumType, ["is_abstract"] = (type.DefaultAttributes & TypeAttributes.Abstract) != 0,
            ["declaring_type"] = type.DeclaringType?.DefaultFullName,
            ["generic_parameters"] = Array.Empty<object>(),
            ["schema_complete"] = true, ["custom_attributes_complete"] = false
        };
        try
        {
            // Constraints are separate metadata from generic parameter flags.
            // Keep the full referenced type graph; flags alone cannot prove an
            // unconstrained parameter or a UnityEngine.Object constraint.
            record["generic_parameters"] = type.GenericParameters.Select(p => new
            {
                name = p.DefaultName, index = p.Index, attributes = (int)p.DefaultAttributes,
                constraints = p.ConstraintTypes.Select(c => TypeReference(c)).ToArray(),
                constraints_complete = true
            }).ToArray();
            record["custom_attributes"] = Attributes(type);
            record["custom_attributes_complete"] = true;
            record["base_type"] = type.BaseType is null ? null : TypeReference(type.BaseType);
            record["unity_component"] = HasBase(type, "UnityEngine.MonoBehaviour");
            record["unity_scriptable_object"] = HasBase(type, "UnityEngine.ScriptableObject");
            record["enum_underlying_type"] = type.IsEnumType ?
                TypeReference(type.AppContext.ResolveIl2CppType(type.Definition!.EnumUnderlyingType)) : null;
        }
        catch (Exception error)
        {
            record["schema_complete"] = false;
            AddError(errors, type, null, error);
        }
        var fields = new List<Dictionary<string, object?>>();
        foreach (var field in type.Fields.Where(f => f.BackingData is not null))
        {
            var fieldRecord = new Dictionary<string, object?>
            {
                ["name"] = field.DefaultName, ["token"] = Hex(field.Token), ["attributes"] = (int)field.DefaultAttributes,
                ["native_offset"] = field.DefaultOffset, ["schema_complete"] = true,
                ["custom_attributes_complete"] = false, ["unity_serialization_candidate"] = null,
                ["has_default_value"] = (field.DefaultAttributes & FieldAttributes.HasDefault) != 0,
                ["default_value_complete"] = false, ["default_value"] = null, ["default_value_type"] = null
            };
            fields.Add(fieldRecord);
            try
            {
                fieldRecord["field_type"] = TypeReference(field.DefaultFieldType);
                fieldRecord["custom_attributes"] = Attributes(field);
                fieldRecord["custom_attributes_complete"] = true;
                FieldConstant(field, fieldRecord);
                bool isPublic = (field.DefaultAttributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public;
                bool serializeField = field.HasCustomAttributeWithFullName("UnityEngine.SerializeField");
                bool serializeReference = field.HasCustomAttributeWithFullName("UnityEngine.SerializeReference");
                bool nonSerialized = field.HasCustomAttributeWithFullName("System.NonSerializedAttribute");
                int flags = (int)field.DefaultAttributes;
                // ECMA-335's NotSerialized flag is 0x80; newer runtimes obsolete
                // its Reflection alias but the original metadata bit remains evidence.
                bool excluded = nonSerialized || (flags & ((int)FieldAttributes.Static | (int)FieldAttributes.Literal | (int)FieldAttributes.InitOnly | 0x80)) != 0;
                fieldRecord["is_public"] = isPublic;
                fieldRecord["serialize_field"] = serializeField;
                fieldRecord["serialize_reference"] = serializeReference;
                fieldRecord["non_serialized"] = nonSerialized || (flags & 0x80) != 0;
                fieldRecord["is_static"] = (flags & (int)FieldAttributes.Static) != 0;
                fieldRecord["is_const"] = (flags & (int)FieldAttributes.Literal) != 0;
                fieldRecord["is_readonly"] = (flags & (int)FieldAttributes.InitOnly) != 0;
                fieldRecord["unity_serialization_candidate"] = !excluded && (isPublic || serializeField || serializeReference);
                fieldRecord["unity_type_eligibility_verified"] = false;
            }
            catch (Exception error)
            {
                record["schema_complete"] = false;
                fieldRecord["schema_complete"] = false;
                fieldRecord["error"] = error.Message;
                AddError(errors, type, field, error);
            }
        }
        record["fields"] = fields;
        record["methods"] = type.Methods.Where(m => m.Definition is not null).Select(MethodIdentity).ToArray();
        return record;
    }

    private static void AddError(List<Dictionary<string, string>> errors, TypeAnalysisContext type, FieldAnalysisContext? field, Exception error)
        => errors.Add(new() { ["assembly"] = type.DeclaringAssembly.DefaultName, ["type"] = type.DefaultFullName,
                             ["field"] = field?.DefaultName ?? "", ["error"] = error.Message });

    private static void FieldConstant(FieldAnalysisContext field, Dictionary<string, object?> record)
    {
        if ((field.DefaultAttributes & FieldAttributes.HasDefault) == 0)
        {
            if ((field.DefaultAttributes & FieldAttributes.Literal) != 0)
                throw new InvalidDataException("Literal field has no original HasDefault flag.");
            record["default_value_complete"] = true;
            return;
        }
        var constant = field.BackingData!.Field.DefaultValue ??
            throw new InvalidDataException("HasDefault field has no metadata default-value record.");
        var rawType = field.AppContext.Binary.GetType(constant.typeIndex);
        record["default_value_type"] = TypeReference(field.AppContext.ResolveIl2CppType(rawType));
        // LibCpp2IL returns null for unsupported non-null default encodings.
        // Refuse that ambiguity instead of labeling an unknown enum value zero.
        if (!constant.dataIndex.IsNull && rawType.Type is not (
            Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN or Il2CppTypeEnum.IL2CPP_TYPE_CHAR or
            Il2CppTypeEnum.IL2CPP_TYPE_I1 or Il2CppTypeEnum.IL2CPP_TYPE_U1 or
            Il2CppTypeEnum.IL2CPP_TYPE_I2 or Il2CppTypeEnum.IL2CPP_TYPE_U2 or
            Il2CppTypeEnum.IL2CPP_TYPE_I4 or Il2CppTypeEnum.IL2CPP_TYPE_U4 or
            Il2CppTypeEnum.IL2CPP_TYPE_I8 or Il2CppTypeEnum.IL2CPP_TYPE_U8 or
            Il2CppTypeEnum.IL2CPP_TYPE_R4 or Il2CppTypeEnum.IL2CPP_TYPE_R8 or
            Il2CppTypeEnum.IL2CPP_TYPE_STRING))
            throw new InvalidDataException("Unsupported non-null default-value encoding: " + rawType.Type);
        record["default_value"] = constant.Value;
        record["default_value_complete"] = true;
    }

    private static List<Dictionary<string, object?>> Attributes(HasCustomAttributes member)
    {
        // Header version >=29 was checked before initialization: attributes are
        // read directly from metadata, without analyzing native cache generators.
        member.AnalyzeCustomAttributeData(false);
        var strings = AttributeStrings(member);
        return (member.CustomAttributes ?? []).Select(a => new Dictionary<string, object?>
        {
            ["assembly"] = a.Constructor.DeclaringType!.DeclaringAssembly.DefaultName,
            ["full_name"] = a.Constructor.DeclaringType.DefaultFullName,
            ["constructor_token"] = Hex(a.Constructor.Token),
            ["arguments"] = a.ConstructorParameters.Select(p => AttributeParameter(p, strings)).ToArray(),
            ["fields"] = a.Fields.Select(f => new
            {
                name = f.Field.DefaultName, token = Hex(f.Field.Token), value = AttributeParameter(f.Value, strings)
            }).ToArray(),
            ["properties"] = a.Properties.Select(p => new
            {
                name = p.Property.DefaultName, token = Hex(p.Property.Token), value = AttributeParameter(p.Value, strings)
            }).ToArray()
        }).ToList();
    }

    private sealed record AttributeString(string? Value, int Length, long BlobOffset, string EncodingSha256);

    private static Dictionary<BaseCustomAttributeParameter, AttributeString> AttributeStrings(HasCustomAttributes member)
    {
        // Original metadata v29+ distinguishes length 0 (empty) from -1 (null).
        // The pinned Core reader collapses both to null. Replay the same bounded
        // blob and preserve strings without changing the verified upstream source.
        var result = new Dictionary<BaseCustomAttributeParameter, AttributeString>();
        byte[] bytes = member.RawIl2CppCustomAttributeData.ToArray();
        if (bytes.Length == 0) return result;
        using var stream = new MemoryStream(bytes, false);
        using var reader = new BinaryReader(stream, Encoding.Unicode, true);
        uint count = stream.ReadUnityCompressedUint();
        var attributes = member.CustomAttributes ?? [];
        if (count != attributes.Count || count > bytes.Length / 4)
            throw new InvalidDataException("Custom attribute blob constructor count differs from decoded attributes.");
        var constructors = V29AttributeUtils.ReadConstructors(stream, count, member.AppContext);
        for (int index = 0; index < attributes.Count; index++)
        {
            var attribute = attributes[index];
            if (!ReferenceEquals(constructors[index], attribute.Constructor))
                throw new InvalidDataException("Custom attribute blob constructor identity differs.");
            if (stream.ReadUnityCompressedUint() != attribute.ConstructorParameters.Count ||
                stream.ReadUnityCompressedUint() != attribute.Fields.Count ||
                stream.ReadUnityCompressedUint() != attribute.Properties.Count)
                throw new InvalidDataException("Custom attribute blob parameter counts differ.");
            foreach (var parameter in attribute.ConstructorParameters) ReadParameter(parameter);
            foreach (var field in attribute.Fields) { ReadParameter(field.Value); SkipMemberIndex(); }
            foreach (var property in attribute.Properties) { ReadParameter(property.Value); SkipMemberIndex(); }
        }
        if (stream.Position != stream.Length)
            throw new InvalidDataException("Custom attribute blob was not consumed completely.");
        return result;

        void SkipMemberIndex()
        {
            if (stream.ReadUnityCompressedInt() < 0) stream.ReadUnityCompressedUint();
        }

        void ReadParameter(BaseCustomAttributeParameter parameter, Il2CppTypeEnum? arrayType = null, int depth = 0)
        {
            if (depth > 64) throw new InvalidDataException("Custom attribute parameter nesting exceeds the limit.");
            var rawType = arrayType ?? (Il2CppTypeEnum)reader.ReadByte();
            var decoded = V29AttributeUtils.ConstructParameterForType(reader, member.AppContext, rawType,
                parameter.Owner, parameter.Kind, parameter.Index);
            if (decoded.GetType() != parameter.GetType())
                throw new InvalidDataException("Custom attribute blob parameter shape differs.");
            if (decoded is CustomAttributePrimitiveParameter primitive && primitive.PrimitiveType == Il2CppTypeEnum.IL2CPP_TYPE_STRING)
            {
                long start = stream.Position;
                int length = stream.ReadUnityCompressedInt();
                if (length < -1 || length > stream.Length - stream.Position)
                    throw new InvalidDataException("Custom attribute string length is invalid or truncated.");
                string? value = length == -1 ? null : new UTF8Encoding(false, true).GetString(reader.ReadBytes(length));
                int encodedSize = checked((int)(stream.Position - start));
                result.Add(parameter, new(value, length, start,
                    Convert.ToHexString(SHA256.HashData(bytes.AsSpan((int)start, encodedSize))).ToLowerInvariant()));
            }
            else if (decoded is CustomAttributeArrayParameter && parameter is CustomAttributeArrayParameter array)
            {
                int length = stream.ReadUnityCompressedInt();
                if (length == -1)
                {
                    if (!array.IsNullArray) throw new InvalidDataException("Custom attribute array null state differs.");
                    return;
                }
                if (length < 0 || length > stream.Length - stream.Position || array.IsNullArray || length != array.ArrayElements.Count)
                    throw new InvalidDataException("Custom attribute array length differs or is invalid.");
                var elementType = (Il2CppTypeEnum)reader.ReadByte();
                if (elementType == Il2CppTypeEnum.IL2CPP_TYPE_ENUM)
                {
                    var enumParameter = (CustomAttributeEnumParameter)V29AttributeUtils.ConstructParameterForType(reader,
                        member.AppContext, elementType, parameter.Owner, CustomAttributeParameterKind.ArrayElement, 0);
                    elementType = enumParameter.UnderlyingPrimitiveParameter.PrimitiveType;
                }
                bool prefixed = reader.ReadBoolean();
                if (elementType != array.ArrType || (prefixed && elementType != Il2CppTypeEnum.IL2CPP_TYPE_OBJECT))
                    throw new InvalidDataException("Custom attribute array element type differs.");
                foreach (var element in array.ArrayElements) ReadParameter(element, prefixed ? null : elementType, depth + 1);
            }
            else decoded.ReadFromV29Blob(reader, member.AppContext);
        }
    }

    private static object AttributeParameter(BaseCustomAttributeParameter parameter,
        Dictionary<BaseCustomAttributeParameter, AttributeString> strings) => parameter switch
    {
        CustomAttributePrimitiveParameter p when p.PrimitiveType == Il2CppTypeEnum.IL2CPP_TYPE_STRING =>
            strings.TryGetValue(p, out var value) ? new
            {
                kind = "primitive", type = p.PrimitiveType.ToString(), value = value.Value,
                value_complete = true, string_length = value.Length, blob_offset = value.BlobOffset,
                raw_encoding_sha256 = value.EncodingSha256
            } : throw new InvalidDataException("Custom attribute string has no verified original encoding."),
        CustomAttributePrimitiveParameter p => new { kind = "primitive", type = p.PrimitiveType.ToString(), value = (object?)p.PrimitiveValue },
        CustomAttributeEnumParameter p => new { kind = "enum", type = TypeReference(p.EnumTypeContext), value = AttributeParameter(p.UnderlyingPrimitiveParameter, strings) },
        BaseCustomAttributeTypeParameter p => new { kind = "type", value = p.TypeContext is null ? null : TypeReference(p.TypeContext) },
        CustomAttributeNullParameter => new { kind = "null", value = (object?)null },
        CustomAttributeArrayParameter p => new
        {
            kind = "array", element_type = p.ArrType.ToString(),
            enum_type = p.EnumType is null ? null : TypeReference(p.Owner.Constructor.AppContext.ResolveIl2CppType(p.EnumType)),
            value = p.IsNullArray ? null : p.ArrayElements.Select(v => AttributeParameter(v, strings)).ToArray()
        },
        _ => throw new InvalidDataException("Unsupported custom attribute parameter: " + parameter.GetType().Name)
    };

    private static bool HasBase(TypeAnalysisContext type, string name)
    {
        var seen = new HashSet<TypeAnalysisContext>();
        for (var current = type; current is not null; current = current is GenericInstanceTypeAnalysisContext generic ? generic.GenericType.DefaultBaseType : current.DefaultBaseType)
        {
            if (!seen.Add(current) || seen.Count > 256) throw new InvalidDataException("Cyclic or excessive base type hierarchy.");
            if (current.DefaultFullName == name) return true;
        }
        return false;
    }

    private static Dictionary<string, object?> MethodIdentity(MethodAnalysisContext method)
        => new()
        {
            ["assembly"] = method.DeclaringType!.DeclaringAssembly.DefaultName,
            ["declaring_type"] = method.DeclaringType.DefaultFullName,
            ["token"] = Hex(method.Token), ["name"] = method.DefaultName,
            ["signature"] = method.Definition!.HumanReadableSignature
        };

    private static Dictionary<string, object?> TypeReference(TypeAnalysisContext type, int depth = 0)
    {
        if (depth > 64) throw new InvalidDataException("Field type nesting exceeds the limit.");
        var record = new Dictionary<string, object?>
        {
            ["assembly"] = type.DeclaringAssembly.DefaultName,
            ["assembly_display_name"] = AssemblyDisplay(type.DeclaringAssembly)
        };
        if (type is GenericInstanceTypeAnalysisContext generic)
        {
            var arguments = generic.GenericArguments.Select(a => TypeReference(a, depth + 1)).ToArray();
            record["kind"] = "generic_instance";
            record["definition"] = generic.GenericType.DefaultFullName;
            record["definition_token"] = Hex(generic.GenericType.Token);
            record["arguments"] = arguments;
            record["canonical_name"] = generic.GenericType.DefaultFullName + "<" + string.Join(",", arguments.Select(a => a["canonical_name"])) + ">";
            record["reflection_full_name"] = arguments.Any(a => a["reflection_full_name"] is null) ? null :
                generic.GenericType.DefaultFullName + "[[" + string.Join("],[", generic.GenericArguments.Select((a, i) =>
                    arguments[i]["reflection_full_name"] + ", " + AssemblyDisplay(a.DeclaringAssembly))) + "]]";
        }
        else if (type is GenericParameterTypeAnalysisContext parameter)
        {
            record["kind"] = "generic_parameter";
            record["parameter_index"] = parameter.Index;
            record["parameter_owner"] = parameter.Owner is MethodAnalysisContext ? "method" : "type";
            record["name"] = parameter.DefaultName;
            record["canonical_name"] = (parameter.Owner is MethodAnalysisContext ? "!!" : "!") + parameter.Index;
            record["reflection_full_name"] = null;
        }
        else if (type is WrappedTypeAnalysisContext wrapped)
        {
            var element = TypeReference(wrapped.ElementType, depth + 1);
            string suffix = type switch
            {
                SzArrayTypeAnalysisContext => "[]",
                ArrayTypeAnalysisContext array => array.Rank == 1 ? "[*]" : "[" + new string(',', array.Rank - 1) + "]",
                ByRefTypeAnalysisContext => "&",
                PointerTypeAnalysisContext => "*",
                _ => throw new InvalidDataException("Unsupported field type wrapper: " + type.GetType().Name)
            };
            record["kind"] = type is SzArrayTypeAnalysisContext or ArrayTypeAnalysisContext ? "array" : suffix == "&" ? "by_reference" : "pointer";
            record["element"] = element;
            record["rank"] = type is ArrayTypeAnalysisContext multi ? multi.Rank : 1;
            record["vector_array"] = type is SzArrayTypeAnalysisContext;
            record["canonical_name"] = element["canonical_name"] + suffix;
            record["reflection_full_name"] = element["reflection_full_name"] is null ? null : element["reflection_full_name"] + suffix;
        }
        else
        {
            record["kind"] = "named";
            record["canonical_name"] = type.DefaultFullName;
            record["reflection_full_name"] = type.DefaultFullName;
            record["token"] = Hex(type.Token);
        }
        record["assembly_qualified_name"] = record["reflection_full_name"] is null ? null :
            record["reflection_full_name"] + ", " + AssemblyDisplay(type.DeclaringAssembly);
        return record;
    }

    private static string AssemblyDisplay(AssemblyAnalysisContext assembly)
        => assembly.DefaultName + ", Version=" + assembly.DefaultVersion + ", Culture=" +
           (string.IsNullOrEmpty(assembly.DefaultCulture) ? "neutral" : assembly.DefaultCulture) + ", PublicKeyToken=" +
           (assembly.DefaultPublicKeyToken is null ? "null" : Convert.ToHexString(assembly.DefaultPublicKeyToken).ToLowerInvariant());
}
