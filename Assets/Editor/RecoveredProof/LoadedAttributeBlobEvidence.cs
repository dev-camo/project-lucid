using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;

namespace ProjectLucid
{
    // Reflection loses the element tag of a boxed null custom-attribute argument.
    // Read the original blob from the exact loaded module before representing it.
    public static class LoadedAttributeBlobEvidence
    {
        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static object Property(object value, string name)
        {
            // Cecil definitions hide some reference properties with a narrower
            // return type. Bind the nearest declaring type without ambiguity.
            for (Type current = value?.GetType(); current != null; current = current.BaseType)
            {
                PropertyInfo[] properties = current.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(p => p.Name == name && p.GetIndexParameters().Length == 0 && p.GetMethod != null).ToArray();
                if (properties.Length > 1) throw new InvalidDataException("Ambiguous installed metadata property " + name + ".");
                if (properties.Length == 1) return properties[0].GetValue(value);
            }
            throw new InvalidDataException("Installed metadata API lacks " + name + ".");
        }

        private static string Text(object value, string name) => (string)Property(value, name);
        private static object[] Items(object value, string name) => ((IEnumerable)Property(value, name)).Cast<object>().ToArray();

        private sealed class PinnedModule : IDisposable
        {
            public readonly string Path;
            public readonly string Sha256;
            public readonly string Mvid;
            public readonly string AssemblyIdentity;
            public readonly object Definition;

            public PinnedModule(Module loaded, MethodInfo read)
            {
                Path = System.IO.Path.GetFullPath(loaded.FullyQualifiedName);
                if (loaded.Assembly.IsDynamic || !File.Exists(Path) ||
                    System.IO.Path.GetFullPath(loaded.Assembly.Location) != Path)
                    throw new InvalidDataException("Loaded declaration does not have an exact disk module.");
                Sha256 = Hash(File.ReadAllBytes(Path));
                Definition = read.Invoke(null, new object[] { Path });
                try
                {
                    Mvid = ((Guid)Property(Definition, "Mvid")).ToString("D");
                    AssemblyIdentity = Text(Property(Property(Definition, "Assembly"), "Name"), "FullName");
                    if (Mvid != loaded.ModuleVersionId.ToString("D") || AssemblyIdentity != loaded.Assembly.FullName)
                        throw new InvalidDataException("Disk module differs from the loaded declaration.");
                    CheckUnchanged();
                }
                catch { ((IDisposable)Definition).Dispose(); throw; }
            }

            public object Lookup(MemberInfo member)
            {
                MethodInfo lookup = Definition.GetType().GetMethod("LookupToken", new[] { typeof(int) });
                if (lookup == null) throw new InvalidDataException("Installed metadata reader lacks token lookup.");
                object result = lookup.Invoke(Definition, new object[] { member.MetadataToken });
                if (result == null || Text(result, "Name") != member.Name ||
                    (member.DeclaringType != null && Text(Property(result, "DeclaringType"), "FullName").Replace('/', '+') != member.DeclaringType.FullName))
                    throw new InvalidDataException("Disk member differs from the loaded token identity.");
                return result;
            }

            public Dictionary<string, object> Evidence() => new Dictionary<string, object>
            {
                ["path"] = Path, ["sha256"] = Sha256, ["mvid"] = Mvid,
                ["assembly_identity"] = AssemblyIdentity
            };

            public void CheckUnchanged()
            {
                if (Hash(File.ReadAllBytes(Path)) != Sha256)
                    throw new IOException("Loaded declaration module changed during blob inspection.");
            }

            public void Dispose() => ((IDisposable)Definition).Dispose();
        }

        private static string[] ParameterNames(object constructor) => Items(constructor, "Parameters")
            .Select(p => Text(Property(p, "ParameterType"), "FullName")).ToArray();

        // This decoder intentionally admits only ShowIf(String,Object) with a boxed
        // String null and no named arguments. Other shapes retain their own evidence.
        public static void VerifyBoxedStringNull(byte[] blob, string expectedFirstArgument)
        {
            if (blob == null) throw new ArgumentNullException(nameof(blob));
            int offset = 0;
            Func<byte> take = () =>
            {
                if (offset >= blob.Length) throw new InvalidDataException("Truncated custom-attribute blob.");
                return blob[offset++];
            };
            if (take() != 1 || take() != 0)
                throw new InvalidDataException("Custom-attribute prolog differs.");
            byte first = take();
            int length;
            if ((first & 0x80) == 0) length = first;
            else if ((first & 0xc0) == 0x80)
            {
                length = ((first & 0x3f) << 8) | take();
                if (length < 0x80) throw new InvalidDataException("Noncanonical compressed string length.");
            }
            else if ((first & 0xe0) == 0xc0)
            {
                length = ((first & 0x1f) << 24) | (take() << 16) | (take() << 8) | take();
                if (length < 0x4000) throw new InvalidDataException("Noncanonical compressed string length.");
            }
            else throw new InvalidDataException("Unsupported first argument string encoding.");
            if (length > blob.Length - offset)
                throw new InvalidDataException("Custom-attribute string exceeds the blob.");
            string text = new UTF8Encoding(false, true).GetString(blob, offset, length);
            offset += length;
            if (text != expectedFirstArgument)
                throw new InvalidDataException("Blob string differs from loaded Reflection argument.");
            if (take() != 0x0e || take() != 0xff)
                throw new InvalidDataException("Boxed argument is not a String null.");
            if (take() != 0 || take() != 0 || offset != blob.Length)
                throw new InvalidDataException("Unexpected named arguments or trailing attribute bytes.");
        }

        public static Dictionary<string, object> ReadBoxedStringNull(MemberInfo member, CustomAttributeData attribute)
        {
            if (member == null || attribute == null) throw new ArgumentNullException();
            Type type = attribute.AttributeType;
            ConstructorInfo constructor = attribute.Constructor;
            Type[] signature = constructor.GetParameters().Select(p => p.ParameterType).ToArray();
            if (type.FullName != "Hardlight.ShowIfAttribute" || type.Assembly.GetName().Name != "HLUnityCore.Runtime" ||
                !signature.SequenceEqual(new[] { typeof(string), typeof(object) }) ||
                attribute.ConstructorArguments.Count != 2 || attribute.NamedArguments.Count != 0 ||
                attribute.ConstructorArguments[0].ArgumentType != typeof(string) ||
                !(attribute.ConstructorArguments[0].Value is string) ||
                attribute.ConstructorArguments[1].ArgumentType != typeof(object) || attribute.ConstructorArguments[1].Value != null)
                throw new InvalidDataException("Attribute is outside the exact boxed-null ShowIf contract.");

            string readerPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(EditorApplication.applicationContentsPath, "Managed", "Unity.Cecil.dll"));
            string readerHash = Hash(File.ReadAllBytes(readerPath));
            Assembly reader = Assembly.LoadFrom(readerPath);
            if (System.IO.Path.GetFullPath(reader.Location) != readerPath || Hash(File.ReadAllBytes(readerPath)) != readerHash)
                throw new InvalidDataException("Loaded metadata reader differs from the installed module.");
            MethodInfo read = reader.GetType("Mono.Cecil.ModuleDefinition", true).GetMethod("ReadModule", new[] { typeof(string) });
            if (read == null) throw new InvalidDataException("Installed reader lacks ReadModule.");
            using (var readerModule = new PinnedModule(reader.ManifestModule, read))
            using (var owner = new PinnedModule(member.Module, read))
            using (var declaring = new PinnedModule(constructor.Module, read))
            {
                object definition = owner.Lookup(member);
                object diskConstructor = declaring.Lookup(constructor);
                string[] expectedSignature = { "System.String", "System.Object" };
                if (!ParameterNames(diskConstructor).SequenceEqual(expectedSignature))
                    throw new InvalidDataException("Loaded attribute constructor disk signature differs.");
                object[] matches = Items(definition, "CustomAttributes").Where(a =>
                {
                    object c = Property(a, "Constructor");
                    return Text(Property(c, "DeclaringType"), "FullName") == type.FullName &&
                        Text(c, "Name") == ".ctor" && ParameterNames(c).SequenceEqual(expectedSignature);
                }).ToArray();
                if (matches.Length != 1) throw new InvalidDataException("Boxed-null attribute token is not unique.");
                object imported = Property(matches[0], "Constructor");
                object scope = Property(Property(imported, "DeclaringType"), "Scope");
                if (Text(scope, "FullName") != declaring.AssemblyIdentity)
                    throw new InvalidDataException("Imported attribute constructor assembly differs.");
                MethodInfo getBlob = matches[0].GetType().GetMethod("GetBlob", Type.EmptyTypes);
                if (getBlob == null) throw new InvalidDataException("Installed metadata reader lacks raw attribute blob access.");
                byte[] blob = (byte[])getBlob.Invoke(matches[0], null);
                VerifyBoxedStringNull(blob, (string)attribute.ConstructorArguments[0].Value);
                owner.CheckUnchanged(); declaring.CheckUnchanged(); readerModule.CheckUnchanged();
                return new Dictionary<string, object>
                {
                    ["kind"] = "primitive", ["type"] = "IL2CPP_TYPE_STRING", ["value"] = null,
                    ["value_complete"] = true, ["string_length"] = -1,
                    ["raw_encoding"] = "ecma335-custom-attribute",
                    ["raw_encoding_sha256"] = Hash(blob), ["raw_encoding_hex"] = BitConverter.ToString(blob).Replace("-", "").ToLowerInvariant(),
                    ["boxed_element_tag"] = 0x0e, ["member_token"] = "0x" + member.MetadataToken.ToString("x8", CultureInfo.InvariantCulture),
                    ["constructor_token"] = "0x" + constructor.MetadataToken.ToString("x8", CultureInfo.InvariantCulture),
                    ["owner_module"] = owner.Evidence(), ["constructor_module"] = declaring.Evidence(),
                    ["reader_module"] = readerModule.Evidence()
                };
            }
        }
    }
}
