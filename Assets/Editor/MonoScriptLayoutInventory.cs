using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectLucid.Editor
{
    /// <summary>
    /// Records loaded declarations and real MonoScript GUID/fileID identities.
    /// Reflection data is read without constructing user types or attributes.
    /// This helper neither imports recovered DLLs nor rewrites asset references.
    /// </summary>
    public static class MonoScriptLayoutInventory
    {
        private static readonly List<Dictionary<string, object>> Errors = new List<Dictionary<string, object>>();
        [Serializable] private sealed class SchemaAssembly { public string name; }
        [Serializable] private sealed class NativeSchemaPointer
        {
            public int schema_version;
            public string command;
            public string status;
            public string evidence_report;
        }
        [Serializable] private sealed class SchemaSelection
        {
            public int schema_version;
            public string status;
            public string unity_version;
            public int type_count;
            public SchemaAssembly[] assemblies;
        }

        public static void Run()
        {
            Errors.Clear();
            string root = Directory.GetParent(Application.dataPath).FullName;
            string sourceIdentity = LucidArtifactIdentity.Fingerprint(root, false);
            string cache = Path.Combine(root, ".cache", "project-lucid");
            string selected = Environment.GetEnvironmentVariable("LUCID_LAYOUT_SCHEMA_PATH");
            string schemaPath;
            if (!String.IsNullOrEmpty(selected)) schemaPath = Path.GetFullPath(selected);
            else
            {
                string pointerPath = Path.Combine(cache, "native-recovery", "latest-schemas.json");
                RequireCachePath(cache, pointerPath);
                NativeSchemaPointer pointer = JsonUtility.FromJson<NativeSchemaPointer>(File.ReadAllText(pointerPath));
                if (pointer.schema_version != 1 || pointer.command != "schemas" || pointer.status != "ready" || String.IsNullOrEmpty(pointer.evidence_report))
                    throw new InvalidDataException("No complete generated native schema is available.");
                schemaPath = Path.GetFullPath(pointer.evidence_report);
            }
            RequireCachePath(cache, schemaPath);
            byte[] schemaBytes = File.ReadAllBytes(schemaPath);
            SchemaSelection schema = JsonUtility.FromJson<SchemaSelection>(Encoding.UTF8.GetString(schemaBytes));
            if (schema.schema_version != 1 || schema.status != "ready" || schema.assemblies == null)
                throw new InvalidDataException("Original layout schema is incomplete or unsupported.");
            if (schema.unity_version != Application.unityVersion)
                throw new InvalidDataException("Original schema Unity version differs from the running Editor.");
            var names = new HashSet<string>(schema.assemblies.Select(a => a.name), StringComparer.Ordinal);
            var assemblies = new List<Dictionary<string, object>>();
            using var modules = new LoadedAttributeBlobEvidence.TypeModuleScope();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => names.Contains(a.GetName().Name))
                         .OrderBy(a => a.GetName().Name, StringComparer.Ordinal))
            {
                var types = new List<Dictionary<string, object>>();
                Type[] loaded;
                try { loaded = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException error)
                {
                    loaded = error.Types.Where(t => t != null).ToArray();
                    AddError(assembly.GetName().Name, "", "", "Assembly types could not be loaded completely.");
                }
                foreach (Type type in loaded.OrderBy(t => t.FullName, StringComparer.Ordinal)) types.Add(TypeSchema(type, modules));
                string location = assembly.IsDynamic ? null : assembly.Location;
                assemblies.Add(new Dictionary<string, object>
                {
                    ["name"] = assembly.GetName().Name,
                    ["version"] = assembly.GetName().Version.ToString(),
                    ["culture"] = assembly.GetName().CultureName,
                    ["public_key_token"] = Token(assembly.GetName().GetPublicKeyToken()),
                    ["source_sha256"] = String.IsNullOrEmpty(location) ? null : Hash(File.ReadAllBytes(location)),
                    ["types"] = types
                });
            }
            var scripts = new List<Dictionary<string, object>>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (MonoScript script in MonoImporter.GetAllRuntimeMonoScripts())
            {
                string path = AssetDatabase.GetAssetPath(script);
                Type type = script.GetClass();
                string guid;
                long identifier;
                bool identity = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out guid, out identifier);
                if (identity && !seen.Add(guid + ":" + identifier.ToString(CultureInfo.InvariantCulture))) continue;
                if (type != null && !names.Contains(type.Assembly.GetName().Name)) continue;
                string resolved = Path.Combine(root, path);
                UnityEditor.PackageManager.PackageInfo package = null;
                if (path.StartsWith("Packages/", StringComparison.Ordinal))
                {
                    package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
                    int separator = path.IndexOf('/', 9);
                    resolved = package == null || separator < 0 ? null : package.resolvedPath + path.Substring(separator);
                }
                scripts.Add(new Dictionary<string, object>
                {
                    ["path"] = path, ["guid"] = identity ? guid.ToLowerInvariant() : null,
                    ["file_id"] = identity ? (object)identifier : null,
                    ["class_resolved"] = type != null,
                    ["assembly"] = type?.Assembly.GetName().Name, ["full_name"] = type?.FullName,
                    ["source_path"] = resolved == null ? null : Path.GetFullPath(resolved),
                    ["package_name"] = package?.name,
                    ["package_version"] = package?.version,
                    ["package_builtin"] = package != null && package.source == UnityEditor.PackageManager.PackageSource.BuiltIn,
                    ["package_resolved_path"] = package == null ? null : Path.GetFullPath(package.resolvedPath),
                    ["source_sha256"] = resolved != null && File.Exists(resolved) ? Hash(File.ReadAllBytes(resolved)) : null
                });
            }
            var report = new Dictionary<string, object>
            {
                ["schema_version"] = 1, ["command"] = "monoscript-inventory",
                ["status"] = Errors.Count == 0 ? "ready" : "incomplete",
                ["unity_version"] = Application.unityVersion,
                ["original_schema_sha256"] = Hash(schemaBytes),
                ["source_fingerprint"] = sourceIdentity,
                ["original_type_count"] = schema.type_count,
                ["assemblies"] = assemblies, ["assembly_count"] = assemblies.Count,
                ["type_count"] = assemblies.Sum(a => ((List<Dictionary<string, object>>)a["types"]).Count),
                ["monoscripts"] = scripts.OrderBy(s => (string)s["path"], StringComparer.Ordinal).ThenBy(s => (string)s["full_name"], StringComparer.Ordinal).ToArray(),
                ["errors"] = Errors, ["references_modified"] = false,
                ["managed_semantics_verified"] = false
            };
            if (sourceIdentity != LucidArtifactIdentity.Fingerprint(root, false))
                throw new IOException("Maintained source changed while inventorying loaded declarations.");
            modules.VerifyUnchanged();
            string output = Path.Combine(cache, "reports", "monoscript-layout-inventory.json");
            string selectedOutput = Environment.GetEnvironmentVariable("LUCID_LAYOUT_INVENTORY_PATH");
            if (!String.IsNullOrEmpty(selectedOutput)) output = Path.GetFullPath(selectedOutput);
            if (output == schemaPath) throw new IOException("Inventory cannot overwrite its original schema evidence.");
            RequireCachePath(cache, output);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            string temporary = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var json = new StringBuilder();
                WriteJson(json, report);
                modules.VerifyUnchanged();
                File.WriteAllText(temporary, json + "\n");
                if (File.Exists(output)) File.Replace(temporary, output, null);
                else File.Move(temporary, output);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            Debug.Log("Project Lucid MonoScript layout inventory: " + assemblies.Count + " assemblies, " + report["type_count"] +
                      " types, " + scripts.Count + " script identities, " + Errors.Count + " errors. " + output);
            if (Errors.Count != 0) throw new InvalidDataException("Loaded declaration inventory is incomplete; see generated report.");
        }

        private static Dictionary<string, object> TypeSchema(Type type, LoadedAttributeBlobEvidence.TypeModuleScope modules)
        {
            var result = new Dictionary<string, object>
            {
                ["assembly"] = type.Assembly.GetName().Name, ["token"] = Hex(type.MetadataToken),
                ["name"] = type.Name, ["namespace"] = type.Namespace ?? "", ["full_name"] = type.FullName,
                ["attributes"] = (int)type.Attributes, ["is_value_type"] = type.IsValueType,
                ["is_enum"] = type.IsEnum, ["is_abstract"] = type.IsAbstract,
                ["declaring_type"] = type.DeclaringType?.FullName,
                ["generic_parameters"] = new object[0],
                ["schema_complete"] = true, ["custom_attributes_complete"] = false
            };
            try
            {
                result["loaded_module"] = modules.Capture(type);
                result["loaded_module_identity_verified"] = true;
                result["loaded_module_reader"] = modules.ReaderEvidence();
                result["generic_parameters"] = type.IsGenericTypeDefinition ? type.GetGenericArguments().Select(p => new Dictionary<string, object>
                {
                    ["name"] = p.Name, ["index"] = p.GenericParameterPosition,
                    ["attributes"] = (int)p.GenericParameterAttributes,
                    ["constraints"] = p.GetGenericParameterConstraints().Select(c => TypeReference(c)).ToArray(),
                    ["constraints_complete"] = true
                }).ToArray() : new object[0];
                result["custom_attributes"] = Attributes(type.GetCustomAttributesData(), type);
                result["custom_attributes_complete"] = true;
                result["base_type"] = type.BaseType == null ? null : TypeReference(type.BaseType);
                result["unity_component"] = typeof(MonoBehaviour).IsAssignableFrom(type);
                result["unity_scriptable_object"] = typeof(ScriptableObject).IsAssignableFrom(type);
                result["enum_underlying_type"] = type.IsEnum ? TypeReference(Enum.GetUnderlyingType(type)) : null;
            }
            catch (Exception error)
            {
                result["schema_complete"] = false;
                AddError(type.Assembly.GetName().Name, type.FullName, "", error.GetType().Name);
            }
            var fields = new List<Dictionary<string, object>>();
            foreach (FieldInfo field in type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                         .OrderBy(f => f.MetadataToken))
            {
                var record = new Dictionary<string, object>
                {
                    ["name"] = field.Name, ["token"] = Hex(field.MetadataToken), ["attributes"] = (int)field.Attributes,
                    ["schema_complete"] = true, ["custom_attributes_complete"] = false
                };
                fields.Add(record);
                try
                {
                    IList<CustomAttributeData> attributes = field.GetCustomAttributesData();
                    bool serialize = attributes.Any(a => a.AttributeType.FullName == "UnityEngine.SerializeField");
                    bool reference = attributes.Any(a => a.AttributeType.FullName == "UnityEngine.SerializeReference");
                    bool nonSerialized = (field.Attributes & (FieldAttributes)128) != 0 || attributes.Any(a => a.AttributeType.FullName == "System.NonSerializedAttribute");
                    record["field_type"] = TypeReference(field.FieldType);
                    record["custom_attributes"] = Attributes(attributes, field);
                    record["custom_attributes_complete"] = true;
                    record["is_public"] = field.IsPublic; record["serialize_field"] = serialize;
                    record["serialize_reference"] = reference; record["non_serialized"] = nonSerialized;
                    record["is_static"] = field.IsStatic; record["is_const"] = field.IsLiteral;
                    record["is_readonly"] = field.IsInitOnly;
                    record["unity_serialization_candidate"] = !field.IsStatic && !field.IsLiteral && !field.IsInitOnly && !nonSerialized && (field.IsPublic || serialize || reference);
                    record["unity_type_eligibility_verified"] = false;
                    bool hasDefault = (field.Attributes & FieldAttributes.HasDefault) != 0;
                    object value = hasDefault ? field.GetRawConstantValue() : null;
                    record["has_default_value"] = hasDefault; record["default_value_complete"] = true;
                    record["default_value"] = value;
                    record["default_value_type"] = hasDefault ? TypeReference(field.FieldType.IsEnum ? Enum.GetUnderlyingType(field.FieldType) : value?.GetType() ?? field.FieldType) : null;
                }
                catch (Exception error)
                {
                    record["schema_complete"] = false; result["schema_complete"] = false;
                    AddError(type.Assembly.GetName().Name, type.FullName, field.Name, error.GetType().Name);
                }
            }
            result["fields"] = fields;
            return result;
        }

        private static Dictionary<string, object> TypeReference(Type type, int depth = 0)
        {
            if (depth > 64) throw new InvalidDataException("Type reference nesting exceeds the limit.");
            var result = new Dictionary<string, object>
            {
                ["assembly"] = type.Assembly.GetName().Name, ["assembly_display_name"] = type.Assembly.FullName,
                ["reflection_full_name"] = type.FullName, ["assembly_qualified_name"] = type.AssemblyQualifiedName
            };
            if (type.IsGenericParameter)
            {
                bool method = type.DeclaringMethod != null;
                result["kind"] = "generic_parameter"; result["parameter_index"] = type.GenericParameterPosition;
                result["parameter_owner"] = method ? "method" : "type"; result["name"] = type.Name;
                result["canonical_name"] = (method ? "!!" : "!") + type.GenericParameterPosition;
            }
            else if (type.IsArray || type.IsByRef || type.IsPointer)
            {
                Type element = type.GetElementType();
                var child = TypeReference(element, depth + 1);
                bool vector = type.IsArray && type == element.MakeArrayType();
                int rank = type.IsArray ? type.GetArrayRank() : 1;
                string suffix = type.IsArray ? vector ? "[]" : rank == 1 ? "[*]" : "[" + new string(',', rank - 1) + "]" : type.IsByRef ? "&" : "*";
                result["kind"] = type.IsArray ? "array" : type.IsByRef ? "by_reference" : "pointer";
                result["element"] = child; result["rank"] = rank; result["vector_array"] = vector;
                result["canonical_name"] = child["canonical_name"] + suffix;
            }
            else if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                Type definition = type.GetGenericTypeDefinition();
                var arguments = type.GetGenericArguments().Select(a => TypeReference(a, depth + 1)).ToArray();
                result["kind"] = "generic_instance"; result["definition"] = definition.FullName;
                result["definition_token"] = Hex(definition.MetadataToken); result["arguments"] = arguments;
                result["canonical_name"] = definition.FullName + "<" + String.Join(",", arguments.Select(a => (string)a["canonical_name"])) + ">";
            }
            else
            {
                result["kind"] = "named"; result["canonical_name"] = type.FullName; result["token"] = Hex(type.MetadataToken);
            }
            return result;
        }

        private static object[] Attributes(IList<CustomAttributeData> attributes, MemberInfo member)
        {
            return attributes.Select(attribute =>
            {
                var arguments = attribute.ConstructorArguments.Select(AttributeValue).ToArray();
                if (attribute.AttributeType.FullName == "Hardlight.ShowIfAttribute" &&
                    attribute.AttributeType.Assembly.GetName().Name == "HLUnityCore.Runtime" &&
                    attribute.Constructor.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(string), typeof(object) }) &&
                    attribute.ConstructorArguments.Count == 2 &&
                    attribute.ConstructorArguments[1].ArgumentType == typeof(object) && attribute.ConstructorArguments[1].Value == null)
                    arguments[1] = LoadedAttributeBlobEvidence.ReadBoxedStringNull(member, attribute);
                return (object)new Dictionary<string, object>
                {
                ["assembly"] = attribute.AttributeType.Assembly.GetName().Name, ["full_name"] = attribute.AttributeType.FullName,
                ["constructor_token"] = Hex(attribute.Constructor.MetadataToken),
                ["arguments"] = arguments,
                ["fields"] = attribute.NamedArguments.Where(n => n.IsField).Select(n => new Dictionary<string, object>
                    { ["name"] = n.MemberName, ["value"] = AttributeValue(n.TypedValue) }).ToArray(),
                ["properties"] = attribute.NamedArguments.Where(n => !n.IsField).Select(n => new Dictionary<string, object>
                    { ["name"] = n.MemberName, ["value"] = AttributeValue(n.TypedValue) }).ToArray()
                };
            }).ToArray();
        }

        private static Dictionary<string, object> AttributeValue(CustomAttributeTypedArgument argument)
        {
            Type type = argument.ArgumentType;
            if (type == typeof(object) && argument.Value == null)
                return new Dictionary<string, object> { ["kind"] = "null", ["value"] = null };
            if (type.IsArray)
            {
                Type element = type.GetElementType();
                return new Dictionary<string, object> { ["kind"] = "array", ["element_type"] = PrimitiveName(element),
                    ["enum_type"] = element.IsEnum ? TypeReference(element) : null,
                    ["value"] = argument.Value == null ? null : ((IList<CustomAttributeTypedArgument>)argument.Value).Select(AttributeValue).ToArray() };
            }
            if (type.IsEnum)
                return new Dictionary<string, object> { ["kind"] = "enum", ["type"] = TypeReference(type),
                    ["value"] = new Dictionary<string, object> { ["kind"] = "primitive", ["type"] = PrimitiveName(Enum.GetUnderlyingType(type)), ["value"] = argument.Value } };
            if (type == typeof(Type))
                return new Dictionary<string, object> { ["kind"] = "type", ["value"] = argument.Value == null ? null : TypeReference((Type)argument.Value) };
            return new Dictionary<string, object> { ["kind"] = "primitive", ["type"] = PrimitiveName(type), ["value"] = argument.Value };
        }

        private static string PrimitiveName(Type type)
        {
            if (type == typeof(Type)) return "IL2CPP_TYPE_IL2CPP_TYPE_INDEX";
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean: return "IL2CPP_TYPE_BOOLEAN"; case TypeCode.Char: return "IL2CPP_TYPE_CHAR";
                case TypeCode.SByte: return "IL2CPP_TYPE_I1"; case TypeCode.Byte: return "IL2CPP_TYPE_U1";
                case TypeCode.Int16: return "IL2CPP_TYPE_I2"; case TypeCode.UInt16: return "IL2CPP_TYPE_U2";
                case TypeCode.Int32: return "IL2CPP_TYPE_I4"; case TypeCode.UInt32: return "IL2CPP_TYPE_U4";
                case TypeCode.Int64: return "IL2CPP_TYPE_I8"; case TypeCode.UInt64: return "IL2CPP_TYPE_U8";
                case TypeCode.Single: return "IL2CPP_TYPE_R4"; case TypeCode.Double: return "IL2CPP_TYPE_R8";
                case TypeCode.String: return "IL2CPP_TYPE_STRING";
                default: throw new InvalidDataException("Unsupported attribute primitive: " + type.FullName);
            }
        }

        private static void AddError(string assembly, string type, string field, string reason)
            => Errors.Add(new Dictionary<string, object> { ["assembly"] = assembly, ["type"] = type, ["field"] = field, ["error"] = reason });
        private static string Hex(int value) => "0x" + value.ToString("x8", CultureInfo.InvariantCulture);
        private static string Token(byte[] value) => value == null || value.Length == 0 ? null : BitConverter.ToString(value).Replace("-", "").ToLowerInvariant();
        private static string Hash(byte[] value) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(value)).Replace("-", "").ToLowerInvariant(); }

        // JsonUtility cannot emit dictionary-shaped field/type schemas. Keep the
        // writer limited to JSON data values; it never invokes user serializers.
        private static void WriteJson(StringBuilder output, object value)
        {
            if (value == null) { output.Append("null"); return; }
            if (value is string text)
            {
                output.Append('"');
                foreach (char character in text)
                {
                    switch (character)
                    {
                        case '"': output.Append("\\\""); break; case '\\': output.Append("\\\\"); break;
                        case '\b': output.Append("\\b"); break; case '\f': output.Append("\\f"); break;
                        case '\n': output.Append("\\n"); break; case '\r': output.Append("\\r"); break; case '\t': output.Append("\\t"); break;
                        default:
                            if (character < 32 || Char.IsSurrogate(character)) output.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                            else output.Append(character);
                            break;
                    }
                }
                output.Append('"'); return;
            }
            if (value is char c) { WriteJson(output, c.ToString()); return; }
            if (value is bool boolean) { output.Append(boolean ? "true" : "false"); return; }
            if (value is IDictionary<string, object> dictionary)
            {
                output.Append('{'); bool first = true;
                foreach (var pair in dictionary.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    if (!first) output.Append(','); first = false;
                    WriteJson(output, pair.Key); output.Append(':'); WriteJson(output, pair.Value);
                }
                output.Append('}'); return;
            }
            if (value is System.Collections.IEnumerable collection)
            {
                output.Append('['); bool first = true;
                foreach (object item in collection) { if (!first) output.Append(','); first = false; WriteJson(output, item); }
                output.Append(']'); return;
            }
            if (value is float single)
            {
                if (Single.IsNaN(single) || Single.IsInfinity(single)) { WriteJson(output, single.ToString(CultureInfo.InvariantCulture)); return; }
                output.Append(single.ToString("R", CultureInfo.InvariantCulture)); return;
            }
            if (value is double number)
            {
                if (Double.IsNaN(number) || Double.IsInfinity(number)) { WriteJson(output, number.ToString(CultureInfo.InvariantCulture)); return; }
                output.Append(number.ToString("R", CultureInfo.InvariantCulture)); return;
            }
            if (value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong)
            { output.Append(((IFormattable)value).ToString(null, CultureInfo.InvariantCulture)); return; }
            throw new InvalidDataException("Unsupported inventory data value: " + value.GetType().FullName);
        }

        private static void RequireCachePath(string cache, string path)
        {
            cache = Path.GetFullPath(cache); path = Path.GetFullPath(path);
            if (!path.StartsWith(cache + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new IOException("Layout evidence must remain inside the generated recovery cache.");
            string current = cache;
            for (string parent = cache; parent != null; parent = Path.GetDirectoryName(parent))
                if (Directory.Exists(parent) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Refusing a symlinked cache destination.");
            foreach (string part in path.Substring(cache.Length + 1).Split(Path.DirectorySeparatorChar))
            {
                current = Path.Combine(current, part);
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Refusing a symlinked evidence path.");
            }
        }
    }
}
