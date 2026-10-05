using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid
{
    public static class InspectorConditionVerification
    {
        public static int RunManaged()
        {
            int count = 0;
            Action<bool, string> check = (value, label) =>
            {
                if (!value) throw new InvalidOperationException(label);
                count++;
            };
            object first = new object();
            object[] values = { first, null, "value" };
            string[] fields = { "first", null };
            var product = new ShowIfAttribute(InspectorConditionalAttribute.ComparisonType.Any, values, fields);
            check(product.ConditionalFields.Length == 6, "Cartesian array size");
            check(product.Comparison == InspectorConditionalAttribute.ComparisonType.Any, "comparison retained");
            for (int i = 0; i < 6; i++)
            {
                check(product.ConditionalFields[i].FieldName == fields[i / 3], "outer field order " + i);
                check(ReferenceEquals(product.ConditionalFields[i].ComparisonValue, values[i % 3]), "inner value order " + i);
            }
            values[0] = new object();
            fields[0] = "changed";
            check(ReferenceEquals(product.ConditionalFields[0].ComparisonValue, first), "array element references captured");
            check(product.ConditionalFields[0].FieldName == "first", "name reference captured");
            product.ConditionalFields[0] = new InspectorConditionalField("mutable", 7);
            check(product.ConditionalFields[0].FieldName == "mutable", "readonly array remains mutable");
            var same = new ShowIfAttribute((InspectorConditionalAttribute.ComparisonType)9, first, "a", "a", null);
            check(same.Comparison == (InspectorConditionalAttribute.ComparisonType)9, "unknown comparison retained");
            check(same.ConditionalFields.Length == 3, "same value count");
            foreach (InspectorConditionalField field in same.ConditionalFields)
                check(ReferenceEquals(field.ComparisonValue, first), "same object retained");
            check(same.ConditionalFields[0].FieldName == "a" && same.ConditionalFields[1].FieldName == "a"
                && same.ConditionalFields[2].FieldName == null, "duplicates and null names retained");
            var single = new ShowIfAttribute((string)null);
            check(single.Comparison == InspectorConditionalAttribute.ComparisonType.All, "single defaults All");
            check(single.ConditionalFields.Length == 1 && single.ConditionalFields[0].FieldName == null
                && single.ConditionalFields[0].ComparisonValue == null, "single retains both nulls");
            check(new ShowIfAttribute(InspectorConditionalAttribute.ComparisonType.Any,
                Array.Empty<object>(), "a").ConditionalFields.Length == 0, "empty values product");
            check(new ShowIfAttribute(InspectorConditionalAttribute.ComparisonType.Any,
                new[] { first }, Array.Empty<string>()).ConditionalFields.Length == 0, "empty names product");
            check(new ShowIfAttribute(InspectorConditionalAttribute.ComparisonType.Any,
                first, Array.Empty<string>()).ConditionalFields.Length == 0, "empty names shared value");
            Action<Action, string> nullError = (action, label) =>
            {
                bool failed = false;
                try { action(); } catch (NullReferenceException) { failed = true; }
                check(failed, label);
            };
            nullError(() => new ShowIfAttribute(InspectorConditionalAttribute.ComparisonType.All,
                (object[])null, Array.Empty<string>()), "null values dereferenced even with empty names");
            nullError(() => new ShowIfAttribute(InspectorConditionalAttribute.ComparisonType.All,
                Array.Empty<object>(), (string[])null), "null names product");
            nullError(() => new ShowIfAttribute(InspectorConditionalAttribute.ComparisonType.All,
                first, (string[])null), "null names shared value");
            var direct = new InspectorConditionalField(null);
            check(direct.FieldName == null && direct.ComparisonValue == null, "readonly field null arguments retained");
            check(!single.ShouldShow(false) && single.ShouldShow(true), "ShouldShow identity");
            check(single.ShouldEnable(false) && single.ShouldEnable(true), "ShouldEnable unconditional true");
            check(single.order == 0, "genuine PropertyAttribute base defaults");
            check(typeof(InspectorConditionalAttribute).BaseType == typeof(PropertyAttribute)
                && typeof(InspectorConditionalAttribute).IsAbstract, "real abstract PropertyAttribute base");
            foreach (string name in new[] { "ShouldShow", "ShouldEnable" })
                check(typeof(InspectorConditionalAttribute).GetMethod(name).IsAbstract, name + " original abstract contract");
            foreach (FieldInfo field in typeof(InspectorConditionalAttribute).GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance))
                check(field.IsInitOnly, field.Name + " readonly");
            foreach (FieldInfo field in typeof(InspectorConditionalField).GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance))
                check(field.IsInitOnly, field.Name + " readonly struct field");
            check(typeof(InspectorConditionalField).GetCustomAttributes(false).Any(a => a.GetType().FullName
                == "System.Runtime.CompilerServices.IsReadOnlyAttribute"), "original readonly struct metadata");
            var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(typeof(ShowIfAttribute), typeof(AttributeUsageAttribute), true);
            check(usage.ValidOn == AttributeTargets.Field && !usage.AllowMultiple && usage.Inherited, "inherited exact usage");
            check(!typeof(ShowIfAttribute).IsSealed && typeof(ShowIfAttribute).BaseType == typeof(InspectorConditionalAttribute), "original nonsealed ShowIf base");
            foreach (Type type in new[] { typeof(InspectorConditionalAttribute), typeof(ShowIfAttribute) })
            {
                var options = type.GetCustomAttributes(typeof(Il2CppSetOptionAttribute), false).Cast<Il2CppSetOptionAttribute>().ToArray();
                check(options.Length == 2, type.Name + " option count");
                check(options.Any(a => a.Option == Option.NullChecks && Equals(a.Value, false))
                    && options.Any(a => a.Option == Option.ArrayBoundsChecks && Equals(a.Value, false)), type.Name + " exact options");
            }
            foreach (ConstructorInfo ctor in typeof(ShowIfAttribute).GetConstructors())
            {
                ParameterInfo last = ctor.GetParameters().Last();
                if (last.ParameterType == typeof(string[]))
                    check(last.IsDefined(typeof(ParamArrayAttribute), false), "params field names metadata");
                else check(last.IsOptional && last.DefaultValue == null, "single optional null metadata");
            }
            return count;
        }
        public static void Run() => Console.WriteLine("PASS InspectorConditionalAttribute/ShowIf " + RunManaged() + " checks");
    }
}
