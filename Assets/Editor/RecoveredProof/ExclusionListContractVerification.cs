using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Verification
{
    public static partial class ExclusionListVerification
    {
        sealed class FieldFact
        {
            public readonly string Name, Type, Tooltip; public readonly int Flags, RecordedToken;
            public FieldFact(string name, string type, int flags, int recordedToken, string tooltip) { Name = name; Type = type; Flags = flags; RecordedToken = recordedToken; Tooltip = tooltip; }
        }
        sealed class TokenFact
        {
            public readonly int Offset, RecordedToken; public readonly string Key, Scope;
            public TokenFact(int offset, int recordedToken, string key, string scope) { Offset = offset; RecordedToken = recordedToken; Key = key; Scope = scope; }
        }
        sealed class MethodFact
        {
            public readonly string Name, Signature, ReturnType, Bytes, PhysicalHeader; public readonly int Flags, RecordedToken, MaxStack, PhysicalLocalToken; public readonly bool InitLocals;
            public readonly string[] ParameterNames, ParameterTypes, Locals; public readonly TokenFact[] Tokens;
            public MethodFact(string name, string signature, int flags, int token, string result, string[] names, string[] types, string bytes, string header, int stack, bool init, int localToken, string[] locals, TokenFact[] tokens)
            { Name = name; Signature = signature; Flags = flags; RecordedToken = token; ReturnType = result; ParameterNames = names; ParameterTypes = types; Bytes = bytes; PhysicalHeader = header; MaxStack = stack; InitLocals = init; PhysicalLocalToken = localToken; Locals = locals; Tokens = tokens; }
        }
        static readonly Type[] KnownTypes = { typeof(ExclusionList), typeof(object), typeof(void), typeof(bool), typeof(string), typeof(string[]), typeof(int) };
        static string TypeKey(Type type) { Require(KnownTypes.Contains(type), "closed exact owner/provider/type set"); return type.FullName; }
        static Type TypeForKey(string key) => KnownTypes.Single(x => TypeKey(x) == key);
        static string MethodKey(MethodBase method)
        {
            Require(!method.IsGenericMethod, "no generic own/provider method");
            return (method is MethodInfo info ? TypeKey(info.ReturnType) : "System.Void") + " " + TypeKey(method.DeclaringType) + "::" + method.Name + "(" + string.Join(",", method.GetParameters().Select(x => TypeKey(x.ParameterType))) + ")";
        }
        static string MemberKey(MemberInfo member)
        {
            if (member is FieldInfo field) return TypeKey(field.FieldType) + " " + TypeKey(field.DeclaringType) + "::" + field.Name;
            if (member is MethodBase method) return MethodKey(method);
            throw new InvalidOperationException("Unexpected exclusion token member kind");
        }
        static bool ExactScope(MemberInfo member, string scope)
        {
            if (scope == "HLUnityCore.Runtime.dll") return member.DeclaringType == typeof(ExclusionList) && member.Module == typeof(ExclusionList).Module;
            // These two literal netstandard facade calls forward to the exact genuine Mono types.
            if (scope == "netstandard") return (member.DeclaringType == typeof(string) || member.DeclaringType == typeof(object)) && member.DeclaringType.Assembly == typeof(object).Assembly;
            return false;
        }
        static void CheckCompleteCurrentDeclarationsAndBodies()
        {
            Type type = typeof(ExclusionList);
            Require(type.FullName == "Hardlight.ExclusionList" && type.Assembly.GetName().Name == "HLUnityCore.Runtime" && (int)type.Attributes == CurrentOwnerFlags && type.BaseType == typeof(object) && type.DeclaringType == null && !type.IsGenericType && type.GetGenericArguments().Length == 0, "complete exact original owner/base/flags");
            Require(type.GetInterfaces().Length == 0 && type.GetNestedTypes(Declared).Length == 0 && type.GetEvents(Declared).Length == 0 && type.TypeInitializer == null, "whole zero interfaces/nested/events/type initializer");
            // Serializable is an exact TypeDef bit; genuine Mono exposes its zero-argument pseudoattribute.
            IList<CustomAttributeData> attributes = type.GetCustomAttributesData();
            Require(type.IsSerializable && attributes.Count == 1 && attributes[0].AttributeType == typeof(SerializableAttribute) && attributes[0].Constructor == typeof(SerializableAttribute).GetConstructor(Type.EmptyTypes) && attributes[0].ConstructorArguments.Count == 0 && attributes[0].NamedArguments.Count == 0, "whole exact Serializable owner projection; zero stored owner CA separately proved");
            FieldFact[] fieldFacts = CurrentFields(); FieldInfo[] fields = type.GetFields(Declared).OrderBy(x => x.MetadataToken).ToArray();
            Require(fields.Length == 3 && fieldFacts.Length == 3, "whole three-field inventory");
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i]; FieldFact fact = fieldFacts[i];
                Require(field.DeclaringType == type && field.Name == fact.Name && field.FieldType == TypeForKey(fact.Type) && (int)field.Attributes == fact.Flags && !field.IsLiteral, "exact ordered field identity/flags/type");
                IList<CustomAttributeData> all = field.GetCustomAttributesData();
                Require(all.Count == (fact.Tooltip == null ? 0 : 1), "whole exact field attribute count");
                if (fact.Tooltip != null)
                {
                    CustomAttributeData tooltip = all[0];
                    Require(tooltip.AttributeType == typeof(TooltipAttribute) && tooltip.AttributeType.Assembly == typeof(TooltipAttribute).Assembly && tooltip.Constructor == typeof(TooltipAttribute).GetConstructor(new[] { typeof(string) }) && tooltip.ConstructorArguments.Count == 1 && tooltip.ConstructorArguments[0].ArgumentType == typeof(string) && Equals(tooltip.ConstructorArguments[0].Value, fact.Tooltip) && tooltip.NamedArguments.Count == 0, "exact original Tooltip constructor/payload/provider");
                }
            }
            MethodFact[] facts = CurrentMethods(); MethodBase[] methods = type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared)).OrderBy(x => x.MetadataToken).ToArray();
            Require(methods.Length == 3 && facts.Length == 3 && facts.Sum(x => x.ParameterNames.Length) == 2 && facts.Sum(x => x.Bytes.Length / 2) == 71 && facts.Sum(x => x.Tokens.Length) == 7, "complete three-method/two-param/71-CIL/seven-scoped-cell scope");
            for (int i = 0; i < methods.Length; i++) CheckCurrentMethod(methods[i], facts[i]);
            PropertyInfo[] properties = type.GetProperties(Declared);
            Require(properties.Length == 1, "whole single write-only property"); PropertyInfo property = properties[0];
            Require(property.Name == "Enabled" && property.DeclaringType == type && property.PropertyType == typeof(bool) && property.Attributes == PropertyAttributes.None && !property.CanRead && property.CanWrite && property.GetGetMethod(true) == null && property.GetSetMethod(true) == methods[0] && property.GetAccessors(true).SequenceEqual(new[] { (MethodInfo)methods[0] }) && property.GetIndexParameters().Length == 0 && property.GetCustomAttributesData().Count == 0, "whole original write-only Enabled/setter/zero attributes");
        }
        static void CheckCurrentMethod(MethodBase actual, MethodFact fact)
        {
            Require(actual.DeclaringType == typeof(ExclusionList) && actual.Name == fact.Name && MethodKey(actual) == fact.Signature && (int)actual.Attributes == fact.Flags && actual.GetMethodImplementationFlags() == 0 && !actual.IsGenericMethod && actual.GetCustomAttributesData().Count == 0 && actual.CallingConvention == (CallingConventions.Standard | CallingConventions.HasThis), "exact complete method/signature/flags/impl/convention");
            if (actual is MethodInfo method)
            {
                Require(method.ReturnType == TypeForKey(fact.ReturnType) && method.GetGenericArguments().Length == 0, "exact nongeneric return"); ParameterInfo result = method.ReturnParameter;
                // Physical proof has zero stored sequence-zero Param/Constant/CA rows.
                // Exclusion Void/Boolean HasDefaultValue/DefaultValue projections are unobserved;
                // retain exact genuine Retval/position/type/CA guard without guessing those values.
                Require(result.Attributes == ParameterAttributes.Retval && result.Position == -1 && result.ParameterType == method.ReturnType && result.GetCustomAttributesData().Count == 0, "exact rowless return projection; stored absence separate");
            }
            ParameterInfo[] parameters = actual.GetParameters(); Require(parameters.Length == fact.ParameterNames.Length, "whole exact parameter count");
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                Require(parameter.Position == i && parameter.Name == fact.ParameterNames[i] && parameter.ParameterType == TypeForKey(fact.ParameterTypes[i]) && parameter.Attributes == ParameterAttributes.None && !parameter.IsOptional && !parameter.HasDefaultValue && parameter.GetCustomAttributesData().Count == 0, "exact ordinary parameter/name/type/flags/no-default/no-CA");
            }
            MethodBody body = actual.GetMethodBody();
            Require(body != null && body.MaxStackSize == fact.MaxStack && body.InitLocals == fact.InitLocals && body.ExceptionHandlingClauses.Count == 0 && body.LocalVariables.Count == fact.Locals.Length, "whole exact header/stack/init/local/EH surface");
            Require(fact.PhysicalLocalToken == 0 ? body.LocalSignatureMetadataToken == 0 : (body.LocalSignatureMetadataToken & unchecked((int)0xff000000)) == 0x11000000 && (body.LocalSignatureMetadataToken & 0x00ffffff) != 0, "physical NIL separate from exact non-NIL local-signature table binding");
            for (int i = 0; i < body.LocalVariables.Count; i++) Require(body.LocalVariables[i].LocalIndex == i && body.LocalVariables[i].LocalType == TypeForKey(fact.Locals[i]) && !body.LocalVariables[i].IsPinned, "every ordered exact nonpinned local");
            byte[] bytes = body.GetILAsByteArray(), expected = Enumerable.Range(0, fact.Bytes.Length / 2).Select(i => Convert.ToByte(fact.Bytes.Substring(2 * i, 2), 16)).ToArray();
            Require(bytes.Length == expected.Length, "whole literal CIL length"); var cells = new HashSet<int>();
            foreach (TokenFact token in fact.Tokens)
            {
                Require(token.Offset >= 1 && token.Offset + 4 <= bytes.Length && BitConverter.ToInt32(expected, token.Offset) == token.RecordedToken, "fresh complete PE operand provenance");
                int actualToken = BitConverter.ToInt32(bytes, token.Offset);
                Require((actualToken & unchecked((int)0xff000000)) == (token.RecordedToken & unchecked((int)0xff000000)), "exact original operand metadata table before member resolution");
                MemberInfo operand = actual.Module.ResolveMember(actualToken);
                Require(ExactScope(operand, token.Scope) && MemberKey(operand) == token.Key, "exact genuine declared scoped operand binding");
                for (int i = 0; i < 4; i++) Require(cells.Add(token.Offset + i), "disjoint exact four-byte operand cells");
            }
            for (int i = 0; i < bytes.Length; i++) if (!cells.Contains(i)) Require(bytes[i] == expected[i], "every literal opcode/branch/argument/index/order byte " + i);
        }
    }
}
