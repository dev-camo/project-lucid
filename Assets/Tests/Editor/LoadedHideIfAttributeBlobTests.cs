using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ProjectLucid.Tests
{
    public sealed class LoadedHideIfAttributeBlobTests
    {
        // Original Game.Runtime field 0x04001f1b; HideIf ctor 0x06000f20 / 0x1B1BFA0.
        private const string Argument = "m_randomiseRotationSpeedOnAwake";
        private const string OriginalBlob = "01001f6d5f72616e646f6d697365526f746174696f6e53706565644f6e4177616b650eff0000";
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        private sealed class Rows
        {
#pragma warning disable 0169
            [HideIf(Argument, null)] private bool first;
            [HideIf(Argument, null)] private bool second;
            [HideIf("other", null)] private bool other;
            private bool missing;
            [SerializeField] private bool unrelated;
            [HideIf(InspectorConditionalAttribute.ComparisonType.All, (object)null, "other")] private bool overload;
#pragma warning restore 0169
        }

        private static FieldInfo Field(Type owner, string name) => owner.GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static CustomAttributeData Attribute(FieldInfo field) => field.GetCustomAttributesData()
            .Single(a => a.AttributeType == typeof(HideIfAttribute));
        private static byte[] Bytes(string hex) => Enumerable.Range(0, hex.Length / 2)
            .Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16)).ToArray();
        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static object Invoke(MethodBase method, object receiver, params object[] arguments)
        {
            try
            {
                return method is ConstructorInfo constructor ? constructor.Invoke(arguments) :
                    ((MethodInfo)method).Invoke(receiver, arguments);
            }
            catch (TargetInvocationException failure) when (failure.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure.InnerException).Throw();
                throw;
            }
        }

        private static void CheckModule(Dictionary<string, object> evidence, Module module)
        {
            Assert.That(evidence["path"], Is.EqualTo(Path.GetFullPath(module.FullyQualifiedName)));
            Assert.That(evidence["assembly_identity"], Is.EqualTo(module.Assembly.FullName));
            Assert.That(evidence["mvid"], Is.EqualTo(module.ModuleVersionId.ToString("D")));
            Assert.That(evidence["sha256"], Is.EqualTo(Hash(File.ReadAllBytes(module.FullyQualifiedName))));
        }

        private static void CheckValue(Dictionary<string, object> value, FieldInfo field, CustomAttributeData attribute)
        {
            Assert.That(value["kind"], Is.EqualTo("primitive"));
            Assert.That(value["type"], Is.EqualTo("IL2CPP_TYPE_STRING"));
            Assert.That(value["value"], Is.Null);
            Assert.That(value["value_complete"], Is.EqualTo(true));
            Assert.That(value["string_length"], Is.EqualTo(-1));
            Assert.That(value["boxed_element_tag"], Is.EqualTo(14));
            Assert.That(value["raw_encoding"], Is.EqualTo("ecma335-custom-attribute"));
            Assert.That(value["raw_encoding_hex"], Is.EqualTo(OriginalBlob));
            Assert.That(value["raw_encoding_sha256"], Is.EqualTo(Hash(Bytes(OriginalBlob))));
            Assert.That(value["member_token"], Is.EqualTo("0x" + field.MetadataToken.ToString("x8")));
            Assert.That(value["constructor_token"], Is.EqualTo("0x" + attribute.Constructor.MetadataToken.ToString("x8")));
            CheckModule((Dictionary<string, object>)value["owner_module"], field.Module);
            CheckModule((Dictionary<string, object>)value["constructor_module"], attribute.Constructor.Module);
            string readerPath = Path.GetFullPath(Path.Combine(EditorApplication.applicationContentsPath, "Managed", "Unity.Cecil.dll"));
            var reader = (Dictionary<string, object>)value["reader_module"];
            Assert.That(reader["path"], Is.EqualTo(readerPath));
            CheckModule(reader, AppDomain.CurrentDomain.GetAssemblies().Single(a =>
                !a.IsDynamic && a.Location == readerPath).ManifestModule);
        }

        [Test]
        public void TransformRotatorHideIfRetainsItsOriginalRawStringNull()
        {
            FieldInfo field = Field(typeof(TransformRotator), "m_rotationSpeed");
            Assert.That(field.Attributes, Is.EqualTo(FieldAttributes.Private));
            Assert.That(field.FieldType, Is.EqualTo(typeof(Vector3)));
            Assert.That(field.GetCustomAttributesData().Select(a => a.AttributeType),
                Is.EqualTo(new[] { typeof(SerializeField), typeof(HideIfAttribute) }));
            CustomAttributeData attribute = Attribute(field);
            Assert.That(attribute.AttributeType.Assembly.GetName().Name, Is.EqualTo("HLUnityCore.Runtime"));
            Assert.That(attribute.Constructor.GetParameters().Select(p => p.ParameterType),
                Is.EqualTo(new[] { typeof(string), typeof(object) }));
            Assert.That(attribute.ConstructorArguments[0].Value, Is.EqualTo(Argument));
            Assert.That(attribute.ConstructorArguments[1].Value, Is.Null);
            Assert.That(attribute.NamedArguments, Is.Empty);
            CheckValue(LoadedAttributeBlobEvidence.ReadBoxedStringNull(field, attribute), field, attribute);
        }

        [Test]
        public void InventoryPublishesHideIfRawEvidenceWithoutChangingAttributeOrder()
        {
            FieldInfo field = Field(typeof(TransformRotator), "m_rotationSpeed");
            Type inventory = AppDomain.CurrentDomain.GetAssemblies().Select(a =>
                a.GetType("ProjectLucid.Editor.MonoScriptLayoutInventory", false)).Single(t => t != null);
            var rows = (object[])Invoke(inventory.GetMethod("Attributes", PrivateStatic), null,
                field.GetCustomAttributesData(), field);
            Assert.That(rows.Select(r => ((Dictionary<string, object>)r)["full_name"]),
                Is.EqualTo(new[] { "UnityEngine.SerializeField", "Hardlight.HideIfAttribute" }));
            var row = (Dictionary<string, object>)rows[1];
            Assert.That(row["assembly"], Is.EqualTo("HLUnityCore.Runtime"));
            var arguments = (Dictionary<string, object>[])row["arguments"];
            Assert.That(arguments, Has.Length.EqualTo(2));
            Assert.That(arguments[0]["value"], Is.EqualTo(Argument));
            CheckValue(arguments[1], field, Attribute(field));
        }

        [Test]
        public void HideIfReaderRequiresTheMembersOwnUniqueRowAndExactOverload()
        {
            foreach (string name in new[] { "first", "second" })
            {
                FieldInfo field = Field(typeof(Rows), name);
                CheckValue(LoadedAttributeBlobEvidence.ReadBoxedStringNull(field, Attribute(field)), field, Attribute(field));
            }
            FieldInfo first = Field(typeof(Rows), "first"), other = Field(typeof(Rows), "other");
            Assert.That(LoadedAttributeBlobEvidence.ReadBoxedStringNull(other, Attribute(other))["raw_encoding_hex"],
                Is.EqualTo("0100056f746865720eff0000"));
            foreach (string name in new[] { "missing", "unrelated" })
                Assert.Throws<InvalidDataException>(() => LoadedAttributeBlobEvidence.ReadBoxedStringNull(
                    Field(typeof(Rows), name), Attribute(first)), name);
            Assert.Throws<InvalidDataException>(() => LoadedAttributeBlobEvidence.ReadBoxedStringNull(first, Attribute(other)));
            FieldInfo overload = Field(typeof(Rows), "overload");
            Assert.Throws<InvalidDataException>(() => LoadedAttributeBlobEvidence.ReadBoxedStringNull(overload, Attribute(overload)));
            FieldInfo unrelated = Field(typeof(Rows), "unrelated");
            Assert.Throws<InvalidDataException>(() => LoadedAttributeBlobEvidence.ReadBoxedStringNull(unrelated,
                unrelated.GetCustomAttributesData().Single()));
        }

        [Test]
        public void HideIfBlobRejectsOtherNullTypesEmptyValuesAndMalformedEncoding()
        {
            byte[] original = Bytes(OriginalBlob);
            Assert.DoesNotThrow(() => LoadedAttributeBlobEvidence.VerifyBoxedStringNull(original, Argument));
            for (int length = 0; length < original.Length; length++)
                Assert.Throws<InvalidDataException>(() => LoadedAttributeBlobEvidence.VerifyBoxedStringNull(original.Take(length).ToArray(), Argument));
            foreach (string suffix in new[] { "50ff0000", "0e000000", "0e01780000", "0eff0100", "0eff000000" })
                Assert.Throws<InvalidDataException>(() => LoadedAttributeBlobEvidence.VerifyBoxedStringNull(
                    Bytes(OriginalBlob.Substring(0, OriginalBlob.Length - 8) + suffix), Argument), suffix);
            foreach (int index in new[] { 0, 1, 2 })
            {
                byte[] altered = (byte[])original.Clone(); altered[index] = index == 1 ? (byte)1 : (byte)0;
                Assert.Throws<InvalidDataException>(() => LoadedAttributeBlobEvidence.VerifyBoxedStringNull(altered, Argument));
            }
            byte[] noncanonical = original.Take(2).Concat(new byte[] { 0x80, 31 }).Concat(original.Skip(3)).ToArray();
            Assert.Throws<InvalidDataException>(() => LoadedAttributeBlobEvidence.VerifyBoxedStringNull(noncanonical, Argument));
            byte[] invalidUtf8 = (byte[])original.Clone(); invalidUtf8[3] = 0xff;
            Assert.Throws<System.Text.DecoderFallbackException>(() => LoadedAttributeBlobEvidence.VerifyBoxedStringNull(invalidUtf8, Argument));
        }

        // These adapters supply deliberately false module metadata to the existing
        // identity guard; no loaded file, runtime object or installed module is changed.
        private sealed class ModuleMetadata : Module
        {
            private readonly Module original;
            private readonly string path;
            private readonly Guid mvid;
            public ModuleMetadata(Module original, string path, Guid mvid)
            { this.original = original; this.path = path; this.mvid = mvid; }
            public override Assembly Assembly => original.Assembly;
            public override string FullyQualifiedName => path;
            public override Guid ModuleVersionId => mvid;
        }

        [Test]
        public void LoadedModuleEvidenceRejectsMissingPathsAndMismatchedIdentity()
        {
            string readerPath = Path.Combine(EditorApplication.applicationContentsPath, "Managed", "Unity.Cecil.dll");
            Assembly reader = Assembly.LoadFrom(readerPath);
            MethodInfo read = reader.GetType("Mono.Cecil.ModuleDefinition", true).GetMethod("ReadModule", new[] { typeof(string) });
            Type pinned = typeof(LoadedAttributeBlobEvidence).GetNestedType("PinnedModule", BindingFlags.NonPublic);
            ConstructorInfo constructor = pinned.GetConstructors().Single();
            Module real = typeof(TransformRotator).Module;
            string absent = Path.Combine(Path.GetTempPath(), "lucid-absent-module-" + Guid.NewGuid().ToString("N") + ".dll");
            Assert.That(File.Exists(absent), Is.False);
            var missing = new ModuleMetadata(real, absent, real.ModuleVersionId);
            Assert.Throws<InvalidDataException>(() => Invoke(constructor, null, missing, read));
            var mismatch = new ModuleMetadata(real, real.FullyQualifiedName, Guid.Empty);
            Assert.That(real.ModuleVersionId, Is.Not.EqualTo(Guid.Empty));
            Assert.Throws<InvalidDataException>(() => Invoke(constructor, null, mismatch, read));
            var wrongPath = new ModuleMetadata(real, typeof(HideIfAttribute).Module.FullyQualifiedName, real.ModuleVersionId);
            Assert.Throws<InvalidDataException>(() => Invoke(constructor, null, wrongPath, read));
        }

        [Test]
        public void SameNamedAttributeFromAnotherAssemblyIsRejected()
        {
            AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("LucidHideIfIdentityControl"), AssemblyBuilderAccess.RunAndCollect);
            ModuleBuilder module = assembly.DefineDynamicModule("IdentityControl");
            TypeBuilder builder = module.DefineType("Hardlight.HideIfAttribute", TypeAttributes.Public, typeof(Attribute));
            ConstructorBuilder constructor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard,
                new[] { typeof(string), typeof(object) });
            ILGenerator il = constructor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(Attribute).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null));
            il.Emit(OpCodes.Ret);
            Type impostor = builder.CreateType();
            TypeBuilder host = module.DefineType("HideIfIdentityHost", TypeAttributes.Public);
            FieldBuilder field = host.DefineField("value", typeof(bool), FieldAttributes.Public);
            field.SetCustomAttribute(new CustomAttributeBuilder(impostor.GetConstructor(new[] { typeof(string), typeof(object) }), new object[] { Argument, null }));
            FieldInfo loaded = host.CreateType().GetField("value");
            CustomAttributeData attribute = loaded.GetCustomAttributesData().Single();
            Assert.That(attribute.AttributeType.FullName, Is.EqualTo("Hardlight.HideIfAttribute"));
            Assert.That(attribute.AttributeType.Assembly.GetName().Name, Is.Not.EqualTo("HLUnityCore.Runtime"));
            Assert.That(attribute.Constructor.GetParameters().Select(p => p.ParameterType),
                Is.EqualTo(new[] { typeof(string), typeof(object) }));
            Assert.That(attribute.ConstructorArguments, Has.Count.EqualTo(2));
            Assert.That(attribute.ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(string)));
            Assert.That(attribute.ConstructorArguments[0].Value, Is.EqualTo(Argument));
            Assert.That(attribute.ConstructorArguments[1].ArgumentType, Is.EqualTo(typeof(object)));
            Assert.That(attribute.ConstructorArguments[1].Value, Is.Null);
            Assert.That(attribute.NamedArguments, Is.Empty);
            // Reject the foreign assembly at admission, not later because its module has no disk image.
            InvalidDataException failure = Assert.Throws<InvalidDataException>(() =>
                LoadedAttributeBlobEvidence.ReadBoxedStringNull(loaded, attribute));
            Assert.That(failure.Message, Does.StartWith("Attribute is outside the exact boxed-null "));
        }
    }
}
