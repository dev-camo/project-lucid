using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HardlightProject;

namespace ProjectLucid
{
    public static class LoadedAttributeBlobVerification
    {
        private static int checks;
        private static void Require(bool condition, string detail)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(detail);
        }

        private static byte[] Blob() => Enumerable.Range(0, 29).Select(i =>
            Convert.ToByte("0100166d5f7a6f6e654772616469656e744f766572726964650eff0000".Substring(2 * i, 2), 16)).ToArray();

        private static void Reject(byte[] blob, string argument = "m_zoneGradientOverride")
        {
            bool failed = false;
            try { LoadedAttributeBlobEvidence.VerifyBoxedStringNull(blob, argument); }
            catch (InvalidDataException) { failed = true; }
            catch (System.Text.DecoderFallbackException) { failed = true; }
            Require(failed, "Malformed or different boxed-null contract must remain rejected.");
        }

        public static int RunManaged()
        {
            checks = 0;
            byte[] original = Blob();
            LoadedAttributeBlobEvidence.VerifyBoxedStringNull(original, "m_zoneGradientOverride");
            Require(true, "Original exact attribute blob decodes.");
            for (int length = 0; length < original.Length; length++) Reject(original.Take(length).ToArray());
            foreach (var change in new[] { (0, (byte)0), (1, (byte)1), (2, (byte)0xff), (2, (byte)0x7f), (3, (byte)0xff), (25, (byte)0x1d), (26, (byte)0), (27, (byte)1), (28, (byte)1) })
            {
                byte[] altered = (byte[])original.Clone(); altered[change.Item1] = change.Item2; Reject(altered);
            }
            Reject(original.Concat(new byte[] { 0 }).ToArray());
            Reject(original, "differentField");
            byte[] noncanonical = original.Take(2).Concat(new byte[] { 0x80, 22 }).Concat(original.Skip(3)).ToArray();
            Reject(noncanonical);
            return checks;
        }

        public static int RunLoaded()
        {
            int managed = RunManaged();
            foreach (string name in new[] { "m_backgroundStartColour", "m_backgroundEndColour" })
            {
                FieldInfo member = typeof(ZoneThemeOverride).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                var attribute = member.GetCustomAttributesData().Single(a => a.AttributeType.FullName == "Hardlight.ShowIfAttribute");
                Dictionary<string, object> value = LoadedAttributeBlobEvidence.ReadBoxedStringNull(member, attribute);
                Require((string)value["kind"] == "primitive" && (string)value["type"] == "IL2CPP_TYPE_STRING" && value["value"] == null && (bool)value["value_complete"], name + " retains the actual boxed String null.");
                Require((string)value["raw_encoding_hex"] == "0100166d5f7a6f6e654772616469656e744f766572726964650eff0000", name + " retains complete original ECMA blob.");
                var owner = (Dictionary<string, object>)value["owner_module"];
                Require((string)owner["mvid"] == member.Module.ModuleVersionId.ToString("D") && (string)owner["assembly_identity"] == member.Module.Assembly.FullName, name + " has the actual loaded member module identity.");
                var constructor = (Dictionary<string, object>)value["constructor_module"];
                Require((string)constructor["mvid"] == attribute.Constructor.Module.ModuleVersionId.ToString("D") && (string)constructor["assembly_identity"] == attribute.Constructor.Module.Assembly.FullName, name + " has the actual loaded constructor module identity.");
            }
            return checks;
        }
    }
}
