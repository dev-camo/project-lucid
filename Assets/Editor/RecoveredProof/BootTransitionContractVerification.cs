using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid.Verification
{
    public static partial class BootTransitionVerification
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        sealed class AttributeFact
        {
            public readonly string Kind, Text; public readonly int Option;
            public AttributeFact(string kind, int option, string text) { Kind = kind; Option = option; Text = text; }
        }
        sealed class FieldFact
        {
            public readonly string Name, Type; public readonly int Flags, RecordedToken;
            public FieldFact(string name, string type, int flags, int recordedToken) { Name = name; Type = type; Flags = flags; RecordedToken = recordedToken; }
        }
        sealed class TokenFact
        {
            public readonly int Offset, RecordedToken; public readonly string Key, Scope;
            public TokenFact(int offset, int recordedToken, string key, string scope) { Offset = offset; RecordedToken = recordedToken; Key = key; Scope = scope; }
        }
        sealed class MethodFact
        {
            public readonly string Name, Signature, ReturnType, Bytes; public readonly int Flags, RecordedToken;
            public readonly string[] ParameterNames, ParameterTypes; public readonly TokenFact[] Tokens;
            public MethodFact(string name, string signature, int flags, int recordedToken, string returnType, string[] names, string[] types, string bytes, TokenFact[] tokens)
            { Name = name; Signature = signature; Flags = flags; RecordedToken = recordedToken; ReturnType = returnType; ParameterNames = names; ParameterTypes = types; Bytes = bytes; Tokens = tokens; }
        }
        sealed class OwnerFact
        {
            public readonly string Name, Base; public readonly int Flags, RecordedToken;
            public readonly AttributeFact[] Attributes; public readonly FieldFact[] Fields; public readonly MethodFact[] Methods;
            public OwnerFact(string name, int flags, int recordedToken, string basis, AttributeFact[] attributes, FieldFact[] fields, MethodFact[] methods)
            { Name = name; Flags = flags; RecordedToken = recordedToken; Base = basis; Attributes = attributes; Fields = fields; Methods = methods; }
        }
        // This exact closed type set binds the qualified Game/Core/Unity providers.
        // The one nested name is mapped explicitly; arbitrary '+'/'/' or scope normalization is forbidden.
        static readonly Type[] KnownTypes = {
            typeof(void), typeof(bool), typeof(string), typeof(object), typeof(FSMTransition), typeof(FiniteStateMachine),
            typeof(FSMIdentifier), typeof(IGraphUser), typeof(FSMUpdateContext), typeof(IFSMTransition), typeof(IGraphStorage),
            typeof(GraphStorageKey), typeof(JsonUtility), typeof(AppFSMKeys), typeof(ApplicationTransitionGameSaveLoaded),
            typeof(ApplicationTransitionIsUnityEditor), typeof(ApplicationTransitionGameSaveLoaded.JSONCtorArgs),
            typeof(Option), typeof(Il2CppSetOptionAttribute), typeof(GraphNodeMenuFormatAttribute), typeof(SerializableAttribute)
        };
        static string TypeKey(Type type)
        {
            if (type.IsGenericParameter)
            {
                Require(type.DeclaringMethod != null && type.GenericParameterPosition == 0, "only the exact two method-generic T signatures");
                return "!!0";
            }
            Require(KnownTypes.Contains(type), "unexpected type outside whole qualified Boot provider set");
            return type == typeof(ApplicationTransitionGameSaveLoaded.JSONCtorArgs)
                ? "HardlightProject.ApplicationTransitionGameSaveLoaded/JSONCtorArgs" : type.FullName;
        }
        static Type TypeForKey(string key) => KnownTypes.Single(x => TypeKey(x) == key);
        static string MethodKey(MethodBase method)
        {
            MethodBase definition = method;
            string name = method.Name;
            if (method is MethodInfo generic && generic.IsGenericMethod)
            {
                Require(!generic.IsGenericMethodDefinition && generic.GetGenericArguments().Length == 1, "exact constructed single-generic provider call");
                Type argument = generic.GetGenericArguments()[0];
                Require((generic.DeclaringType == typeof(JsonUtility) && generic.Name == "FromJson" && argument == typeof(ApplicationTransitionGameSaveLoaded.JSONCtorArgs)) ||
                    (generic.DeclaringType == typeof(IGraphStorage) && generic.Name == "GetValue" && argument == typeof(bool)), "only qualified JSON/Boolean generic operand");
                definition = generic.GetGenericMethodDefinition(); name += "<" + TypeKey(argument) + ">";
            }
            string result = definition is MethodInfo info ? TypeKey(info.ReturnType) : "System.Void";
            return result + " " + TypeKey(method.DeclaringType) + "::" + name + "(" + string.Join(",", definition.GetParameters().Select(x => TypeKey(x.ParameterType))) + ")";
        }
        static string MemberKey(MemberInfo member)
        {
            if (member is MethodBase method) return MethodKey(method);
            if (member is FieldInfo field) return TypeKey(field.FieldType) + " " + TypeKey(field.DeclaringType) + "::" + field.Name;
            throw new InvalidOperationException("Unexpected Boot token member kind");
        }
        static bool ExactScope(MemberInfo member, string scope)
        {
            Type owner = member.DeclaringType;
            if (scope == "Game.Runtime.dll") return new[] { typeof(ApplicationTransitionGameSaveLoaded), typeof(ApplicationTransitionIsUnityEditor), typeof(ApplicationTransitionGameSaveLoaded.JSONCtorArgs), typeof(AppFSMKeys) }.Contains(owner) && owner.Assembly == typeof(ApplicationTransitionGameSaveLoaded).Assembly;
            if (scope == "HLUnityCore.Runtime") return new[] { typeof(FSMTransition), typeof(IGraphUser), typeof(IGraphStorage) }.Contains(owner) && owner.Assembly == typeof(FSMTransition).Assembly;
            if (scope == "UnityEngine.JSONSerializeModule") return owner == typeof(JsonUtility) && owner.Assembly == typeof(JsonUtility).Assembly;
            // The recorded netstandard Object constructor facade forwards to this exact genuine Mono Object type.
            if (scope == "netstandard") return owner == typeof(object) && owner.Assembly == typeof(object).Assembly;
            return false;
        }
        static object AttributeValue(CustomAttributeTypedArgument argument)
        {
            object value = argument.Value;
            while (value is CustomAttributeTypedArgument boxed) value = boxed.Value;
            return value;
        }
        static void CheckOwnerAttributes(Type type, OwnerFact fact)
        {
            IList<CustomAttributeData> all = type.GetCustomAttributesData();
            bool json = type == typeof(ApplicationTransitionGameSaveLoaded.JSONCtorArgs);
            // Serializable is a stored TypeDef bit, not a stored CustomAttribute row.
            // This exact one-owner Mono projection is prospective; it never removes other attributes.
            CustomAttributeData[] pseudo = all.Where(x => x.AttributeType == typeof(SerializableAttribute)).ToArray();
            Require(pseudo.Length == (json ? 1 : 0) && type.IsSerializable == json, "exact Serializable bit and one-owner pseudo projection");
            if (json) Require(pseudo[0].Constructor == typeof(SerializableAttribute).GetConstructor(Type.EmptyTypes) && pseudo[0].ConstructorArguments.Count == 0 && pseudo[0].NamedArguments.Count == 0, "exact genuine Serializable zero-argument pseudoattribute");
            CustomAttributeData[] stored = all.Where(x => x.AttributeType != typeof(SerializableAttribute)).ToArray();
            Require(stored.Length == fact.Attributes.Length && all.Count == fact.Attributes.Length + (json ? 1 : 0), "closed complete owner attribute surface");
            for (int i = 0; i < stored.Length; i++)
            {
                CustomAttributeData actual = stored[i]; AttributeFact expected = fact.Attributes[i];
                Require(actual.NamedArguments.Count == 0, "no named owner arguments");
                if (expected.Kind == "option")
                    Require(actual.AttributeType == typeof(Il2CppSetOptionAttribute) && actual.Constructor == typeof(Il2CppSetOptionAttribute).GetConstructor(new[] { typeof(Option), typeof(object) }) && actual.ConstructorArguments.Count == 2 && actual.ConstructorArguments[0].ArgumentType == typeof(Option) && Convert.ToInt32(AttributeValue(actual.ConstructorArguments[0])) == expected.Option && AttributeValue(actual.ConstructorArguments[1]) is bool disabled && !disabled, "exact original option constructor/payload/order");
                else
                    Require(expected.Kind == "menu" && actual.AttributeType == typeof(GraphNodeMenuFormatAttribute) && actual.Constructor == typeof(GraphNodeMenuFormatAttribute).GetConstructor(new[] { typeof(string) }) && actual.ConstructorArguments.Count == 1 && actual.ConstructorArguments[0].ArgumentType == typeof(string) && Equals(AttributeValue(actual.ConstructorArguments[0]), expected.Text), "exact original menu constructor/string/order");
            }
        }
        static MethodBase[] DeclaredMethods(Type type)
        {
            var methods = type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared)).ToList();
            if (type.TypeInitializer != null && !methods.Any(x => x.MetadataToken == type.TypeInitializer.MetadataToken)) methods.Add(type.TypeInitializer);
            return methods.OrderBy(x => x.MetadataToken).ToArray();
        }
        static void CheckCompleteCurrentDeclarationsAndBodies()
        {
            OwnerFact[] facts = CurrentFacts();
            Require(facts.Length == 3 && facts.Sum(x => x.Methods.Length) == 7 && facts.Sum(x => x.Fields.Length) == 2 && facts.Sum(x => x.Methods.Sum(m => m.ParameterNames.Length)) == 15 && facts.Sum(x => x.Methods.Sum(m => m.Bytes.Length / 2)) == 87 && facts.Sum(x => x.Methods.Sum(m => m.Tokens.Length)) == 12, "whole three-owner/seven-main/two-field/fifteen-parameter/eighty-seven-CIL scope");
            foreach (OwnerFact fact in facts)
            {
                Type type = TypeForKey(fact.Name); bool json = type == typeof(ApplicationTransitionGameSaveLoaded.JSONCtorArgs);
                Require((int)type.Attributes == fact.Flags && type.BaseType == TypeForKey(fact.Base) && !type.IsGenericType && type.GetGenericArguments().Length == 0, "whole original flags/base/generic identity " + fact.Name);
                // Static PE proof establishes own InterfaceImpl=0. Reflection checks the separate inherited surface.
                Type[] inherited = json ? Type.EmptyTypes : new[] { typeof(IFSMTransition) };
                Require(type.GetInterfaces().SequenceEqual(inherited) && (json || typeof(FSMTransition).GetInterfaces().SequenceEqual(inherited)), "exact genuine inherited IFSMTransition surface");
                Require(type.DeclaringType == (json ? typeof(ApplicationTransitionGameSaveLoaded) : null), "exact original nested parent");
                Type[] children = type == typeof(ApplicationTransitionGameSaveLoaded) ? new[] { typeof(ApplicationTransitionGameSaveLoaded.JSONCtorArgs) } : Type.EmptyTypes;
                Require(type.GetNestedTypes(Declared).SequenceEqual(children), "whole exact nested owner graph");
                CheckOwnerAttributes(type, fact);
                Require(type.GetProperties(Declared).Length == 0 && type.GetEvents(Declared).Length == 0, "whole zero property/event inventory");
                FieldInfo[] fields = type.GetFields(Declared).OrderBy(x => x.MetadataToken).ToArray(); Require(fields.Length == fact.Fields.Length, "whole field inventory");
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i]; FieldFact expected = fact.Fields[i];
                    Require(field.Name == expected.Name && field.FieldType == TypeForKey(expected.Type) && (int)field.Attributes == expected.Flags && field.GetCustomAttributesData().Count == 0 && !field.IsLiteral, "exact full field declaration " + field.Name);
                }
                MethodBase[] methods = DeclaredMethods(type); Require(methods.Length == fact.Methods.Length, "whole method/constructor inventory");
                for (int i = 0; i < methods.Length; i++) CheckMethod(methods[i], fact.Methods[i]);
            }
        }
        static void CheckMethod(MethodBase actual, MethodFact fact)
        {
            Require(actual.Name == fact.Name && MethodKey(actual) == fact.Signature && (int)actual.Attributes == fact.Flags && actual.GetMethodImplementationFlags() == 0 && !actual.IsGenericMethod && actual.GetCustomAttributesData().Count == 0, "whole exact method identity/flags " + fact.Signature);
            Require(actual.CallingConvention == (CallingConventions.Standard | (actual.IsStatic ? 0 : CallingConventions.HasThis)), "exact original calling convention");
            if (actual is MethodInfo method)
            {
                Require(method.ReturnType == TypeForKey(fact.ReturnType) && method.GetGenericArguments().Length == 0, "exact original nongeneric return type");
                // Fresh physical proof has no sequence-zero Param row and stored flags zero.
                // Unity Mono projects these four rowless returns as Retval8 and a null
                // default (HasDefaultValue=true). The physical Param/Constant absence
                // remains separate; ordinary parameters retain their strict no-default checks.
                ParameterInfo result = method.ReturnParameter;
                Require(result.Attributes == ParameterAttributes.Retval && result.Position == -1 && result.ParameterType == method.ReturnType && result.HasDefaultValue && result.DefaultValue == null && result.RawDefaultValue == null && result.GetCustomAttributesData().Count == 0, "exact rowless return projection; physical absence separate");
                if (actual.Name == "DoUpdate") Require(method.GetBaseDefinition().DeclaringType == typeof(FSMTransition) && method.GetBaseDefinition().Name == "DoUpdate" && method.GetBaseDefinition().ReturnType == typeof(bool) && method.GetBaseDefinition().GetParameters().Select(x => x.ParameterType).SequenceEqual(new[] { typeof(IGraphUser), typeof(FSMUpdateContext) }), "genuine original implicit virtual override slot");
            }
            ParameterInfo[] parameters = actual.GetParameters(); Require(parameters.Length == fact.ParameterNames.Length, "whole parameter count");
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                Require(parameter.Position == i && parameter.Name == fact.ParameterNames[i] && parameter.ParameterType == TypeForKey(fact.ParameterTypes[i]) && parameter.Attributes == ParameterAttributes.None && !parameter.IsOptional && !parameter.HasDefaultValue && parameter.GetCustomAttributesData().Count == 0, "exact ordinary parameter/name/type/no-default/no-attributes");
            }
            MethodBody body = actual.GetMethodBody();
            Require(body != null && body.MaxStackSize == 8 && !body.InitLocals && body.LocalVariables.Count == 0 && body.ExceptionHandlingClauses.Count == 0 && body.LocalSignatureMetadataToken == 0, "whole tiny-body runtime surface; physically NIL signature separately proved");
            byte[] bytes = body.GetILAsByteArray(), expectedBytes = Enumerable.Range(0, fact.Bytes.Length / 2).Select(i => Convert.ToByte(fact.Bytes.Substring(i * 2, 2), 16)).ToArray();
            Require(bytes.Length == expectedBytes.Length, "whole exact CIL byte length"); var tokenBytes = new HashSet<int>();
            foreach (TokenFact token in fact.Tokens)
            {
                Require(token.Offset >= 1 && token.Offset + 4 <= bytes.Length && BitConverter.ToInt32(expectedBytes, token.Offset) == token.RecordedToken, "literal fresh physical token cell provenance");
                MemberInfo operand = actual.Module.ResolveMember(BitConverter.ToInt32(bytes, token.Offset));
                Require(ExactScope(operand, token.Scope) && MemberKey(operand) == token.Key, "exact genuine Module token/member/provider binding");
                for (int i = 0; i < 4; i++) Require(tokenBytes.Add(token.Offset + i), "disjoint complete four-byte token cell");
            }
            for (int i = 0; i < bytes.Length; i++) if (!tokenBytes.Contains(i)) Require(bytes[i] == expectedBytes[i], "every original CIL opcode/literal/argument/call-order byte " + i);
            // Only these twelve fully enumerated scoped cells may bind the freshly compiled Unity module.
            // Every other byte is literal; no NOP filtering, predicate rewrite, opcode or branch normalization.
        }
    }
}
