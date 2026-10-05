using System;
using System.Globalization;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{
    public static class PropertyListVerification
    {
        private enum Fixture { Default = 7, Open = 2 }
        private static int checks;

        public static void Run()
        {
            checks = 0;
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var list = new HLPropertyList();
                Require(list.Properties.Count == 0, "new property collection");
                Require(HLPropertyStore.DefaultSaveIdentifier == "" && HLPropertyStore.DefaultPropertyValue == "", "original empty defaults");
                Require(HLPropertyStore.GetCRC("Save_App") == HLCRC32.Generate("save_app", HLCRC32.Case.AsIs), "lowercase property CRC");
                Require(HLPropertyStore.GetCRC(null) == 0 && HLPropertyStore.GetCRC("") == 0, "absent CRC defaults");
                list.AddProperty<string>(null, "ignored");
                list.AddProperty("", "ignored");
                list.AddProperty<string>("nullable", null);
                list.AddProperty("zero-crc", 0u, "ignored");
                Require(list.Properties.Count == 0, "invalid additions must be ignored");

                list.AddProperty("Number", 1234.125f);
                HLPropertyList.Property number = list.GetProperty("NUMBER");
                Require(number != null && number.m_name == "Number" && number.m_value == "1234.125", "invariant formatting and case-insensitive lookup");
                list.AddProperty("number", 9);
                Require(ReferenceEquals(number, list.GetProperty("number")) && number.m_name == "Number" && number.m_value == "9", "retain object/name on update");
                Require(list.AssociateNameWithProperty("NUMBER") && number.m_name == "NUMBER", "explicit name association");
                Require(!list.AssociateNameWithProperty("missing"), "missing name association");
                list.AddProperty("different-name", number.m_nameCRC, 5);
                Require(list.Properties.Count == 1 && number.m_name == "NUMBER" && number.m_value == "5", "explicit CRC collision updates first property");
                number.m_name = null;
                list.AddProperty("reassociate", number.m_nameCRC, 6);
                Require(number.m_name == "reassociate", "fill an absent display name on update");
                var duplicate = new HLPropertyList.Property("duplicate", number.m_nameCRC, "second");
                list.Properties.Add(duplicate);
                Require(ReferenceEquals(number, list.GetProperty(number.m_nameCRC)), "first duplicate CRC lookup");
                Require(list.RemoveProperty(number.m_nameCRC) && ReferenceEquals(duplicate, list.GetProperty(number.m_nameCRC)), "remove only first duplicate CRC");
                Require(!list.RemoveProperty((string)null) && !list.RemoveProperty(0u), "invalid removal");
                Require(list.RemoveProperty("number") && !list.PropertyValid("number"), "remove through lowercase CRC");

                Require(list.AsString("missing", "fallback") == "fallback" && list.AsInt("missing", -11) == -11 &&
                    list.AsUInt("missing", 12u) == 12u && list.AsLong("missing", -13L) == -13L &&
                    list.AsULong("missing", 14uL) == 14uL && list.AsFloat("missing", 15.5f) == 15.5f &&
                    list.AsDouble("missing", 16.5) == 16.5 && list.AsBool("missing", true), "absent typed defaults");
                list.AddProperty("int", int.MinValue); list.AddProperty("uint", uint.MaxValue);
                list.AddProperty("long", long.MinValue); list.AddProperty("ulong", ulong.MaxValue);
                list.AddProperty("bool", true); list.AddProperty("double", -12.125);
                Require(list.AsInt("int") == int.MinValue && list.AsUInt("uint") == uint.MaxValue &&
                    list.AsLong("long") == long.MinValue && list.AsULong("ulong") == ulong.MaxValue &&
                    list.AsBool("bool") && list.AsDouble("double") == -12.125, "typed conversion boundaries");
                list.AddProperty("decimal", "- 12,125 !");
                Require(list.AsFloat("decimal") == -12.125f, "comma repair and character filtering");
                list.AddProperty("decimal", "1/25");
                Require(list.AsDouble("decimal") == 1.25, "slash repair");
                list.AddProperty("decimal", "1,2/3");
                Require(list.AsDouble("decimal") == 1.23, "only first separator replacement");
                list.AddProperty("decimal", "2.5E+2");
                Require(list.AsDouble("decimal") == 250.0, "exponent letters preserved");
                list.AddProperty("decimal", "NaN");
                Require(double.IsNaN(list.AsDouble("decimal")), "named floating value preserved");
                list.AddProperty("invalid", "broken");
                RequireThrows<FormatException>(() => list.AsInt("invalid", 77), "present invalid integer is not a fallback");
                list.AddProperty("overflow", "4294967296");
                RequireThrows<OverflowException>(() => list.AsUInt("overflow", 77), "present integer overflow");
                var nullValue = new HLPropertyList.Property("null-value", HLPropertyStore.GetCRC("null-value"), null);
                list.Properties.Add(nullValue);
                Require(list.AsString("null-value", "fallback") == null && list.AsInt("null-value", 77) == 0, "present null integer/string conversion");
                RequireThrows<NullReferenceException>(() => list.AsFloat("null-value", 77), "present null decimal preprocessor");

                list.AddProperty("enum", "Open");
                Require(list.AsEnum("enum", Fixture.Default) == Fixture.Open, "case-sensitive enum parse");
                list.AddProperty("enum", "open");
                Require(list.AsEnum("enum", Fixture.Default) == Fixture.Default, "enum parse fallback");
                list.AddProperty("enum", "123");
                Require((int)list.AsEnum("enum", Fixture.Default) == 123, "unnamed numeric enum accepted");
                Require(list.AsEnum("missing", Fixture.Default) == Fixture.Default &&
                    EnumUtilities.SafeParse<Fixture>(null, Fixture.Default) == Fixture.Default &&
                    EnumUtilities.SafeParse<int>("123", 77) == 77, "enum missing/null/non-enum fallbacks");
                Require("a_b-★é1".MakeAlphanumeric() == "abé1" && "a_b-★é1".MakeAlphanumeric('_') == "a_bé1", "Unicode letters and explicit exception");
                Require(((string)null).MakeAlphanumeric() == null && " \t".MakeAlphanumeric() == " \t" && "".MakeAlphanumeric() == "", "blank text passed through");
                list.Properties.Insert(0, null);
                RequireThrows<NullReferenceException>(() => list.GetProperty(5u), "null collection element stays an error");
                var store = new HLPropertyStore("key", 1, "unused");
                Require(!store.IsLoaded && store.CanSave && store.UnencryptedSaveVersion == 2,
                    "store construction leaves explicit loading for its caller");
                store.Shutdown();
                Debug.Log("[Project Lucid] Original property-list checks passed: " + checks);
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        private static void Require(bool condition, string message)
        {
            ++checks;
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void RequireThrows<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T) { ++checks; return; }
            throw new InvalidOperationException(message);
        }
    }
}
