using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unity.IL2CPP.CompilerServices;

namespace ProjectLucid.Verification
{
    public static partial class CameraInputDisablerVerification
    {
        sealed class OwnerFact
        {
            public string Name, Base; public int Flags; public string[] Interfaces, Attributes; public FieldFact[] Fields; public MethodFact[] Methods; public PropertyFact[] Properties;
            public OwnerFact(string name, int flags, string basis, string[] interfaces, string[] attributes, FieldFact[] fields, MethodFact[] methods, PropertyFact[] properties) { Name = name; Flags = flags; Base = basis; Interfaces = interfaces; Attributes = attributes; Fields = fields; Methods = methods; Properties = properties; }
        }
        sealed class FieldFact
        {
            public string Name, Type; public int Flags; public string[] Attributes;
            public FieldFact(string name, string type, int flags, string[] attributes) { Name = name; Type = type; Flags = flags; Attributes = attributes; }
        }
        sealed class PropertyFact
        {
            public string Name, Type, Getter, Setter; public int Flags;
            public PropertyFact(string name, string type, int flags, string getter, string setter) { Name = name; Type = type; Flags = flags; Getter = getter; Setter = setter; }
        }
        sealed class TokenFact
        {
            public int Offset; public string Key, Scope;
            public TokenFact(int offset, string key, string scope) { Offset = offset; Key = key; Scope = scope; }
        }
        sealed class RegionFact { public int TryOffset, TryLength, HandlerOffset, HandlerLength; public RegionFact(int a,int b,int c,int d) { TryOffset=a; TryLength=b; HandlerOffset=c; HandlerLength=d; } }
        sealed class MethodFact
        {
            public string Name, Signature, Bytes; public int Flags, Impl, ReturnFlags, MaxStack; public bool InitLocals; public string[] Attributes, ParameterNames, ParameterTypes, Locals; public int[] ParameterFlags; public object[] Constants; public bool[] HasConstant; public TokenFact[] Tokens; public RegionFact[] Regions;
            public MethodFact(string name, string signature, int flags, int impl, int returnFlags, string[] attributes, string[] names, string[] types, int[] parameterFlags, object[] constants, bool[] hasConstant, string bytes, int maxStack, bool initLocals, string[] locals, TokenFact[] tokens, RegionFact[] regions)
            { Name = name; Signature = signature; Flags = flags; Impl = impl; ReturnFlags = returnFlags; Attributes = attributes; ParameterNames = names; ParameterTypes = types; ParameterFlags = parameterFlags; Constants = constants; HasConstant = hasConstant; Bytes = bytes; MaxStack = maxStack; InitLocals = initLocals; Locals = locals; Tokens = tokens; Regions = regions; }
        }
        static string TypeKey(Type type)
        {
            if (type.IsByRef) return TypeKey(type.GetElementType()) + "&";
            if (type.IsPointer) return TypeKey(type.GetElementType()) + "*";
            if (type.IsArray) return TypeKey(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
            if (type.IsGenericParameter) return (type.DeclaringMethod == null ? "!" : "!!") + type.GenericParameterPosition;
            if (type.IsGenericType) return type.GetGenericTypeDefinition().FullName.Replace('+', '/') + "<" + string.Join(",", type.GetGenericArguments().Select(TypeKey)) + ">";
            return type.FullName.Replace('+', '/');
        }
        static MethodBase SignatureDefinition(MethodBase method)
        {
            MethodBase definition = method;
            if (method.DeclaringType.IsGenericType && !method.DeclaringType.IsGenericTypeDefinition)
            {
                Type owner = method.DeclaringType.GetGenericTypeDefinition(); definition = owner.GetMethods(Declared).Cast<MethodBase>().Concat(owner.GetConstructors(Declared)).Single(x => x.MetadataToken == method.MetadataToken);
            }
            if (definition is MethodInfo info && info.IsGenericMethod) definition = info.GetGenericMethodDefinition(); return definition;
        }
        static string MethodKey(MethodBase method)
        {
            MethodBase definition = SignatureDefinition(method); string result = definition is MethodInfo info ? TypeKey(info.ReturnType) : "System.Void";
            string name = method.Name; if (method is MethodInfo generic && generic.IsGenericMethod && !generic.IsGenericMethodDefinition) name += "<" + string.Join(",", generic.GetGenericArguments().Select(TypeKey)) + ">";
            return result + " " + TypeKey(method.DeclaringType) + "::" + name + "(" + string.Join(",", definition.GetParameters().Select(x => TypeKey(x.ParameterType))) + ")";
        }
        static string MemberKey(MemberInfo member)
        {
            if (member is MethodBase method) return MethodKey(method);
            if (member is FieldInfo field) return TypeKey(field.FieldType) + " " + TypeKey(field.DeclaringType) + "::" + field.Name;
            if (member is Type type) return TypeKey(type); throw new InvalidOperationException("Unexpected token member kind");
        }
        static bool Scope(MemberInfo member, string expected)
        {
            Type owner = member is Type type ? type : member.DeclaringType;
            if (expected == "Game.Runtime.dll") return owner.Assembly == typeof(HardlightProject.CameraInputBrain).Assembly;
            if (expected == "HLUnityCore.Runtime") return owner.Assembly == typeof(Hardlight.ProcessManager).Assembly;
            if (expected == "HLInput.Runtime") return owner.Assembly == typeof(Hardlight.ControlMapping).Assembly;
            if (expected == "HLAutoGenerated") return owner.Assembly == typeof(Hardlight.GameInput).Assembly;
            // The pinned Unity compilation facade forwards these genuine BCL types to Mono's actual core library.
            // This exact facade mapping is fixture binding only; the physical MemberRef scope still needs root emission approval.
            if (expected == "netstandard") return owner.Namespace != null && owner.Namespace.StartsWith("System", StringComparison.Ordinal) && owner.Assembly == typeof(object).Assembly;
            return owner.Assembly.GetName().Name == expected;
        }
        static string[] AttributeNames(IList<CustomAttributeData> data) { return data.Select(x => TypeKey(x.AttributeType)).ToArray(); }
        static object AttributeValue(CustomAttributeTypedArgument argument) { object value = argument.Value; while (value is CustomAttributeTypedArgument nested) value = nested.Value; return value; }
        static void Options(Type type, IList<CustomAttributeData> attributes)
        {
            CustomAttributeData[] options = attributes.Where(x => x.AttributeType == typeof(Il2CppSetOptionAttribute)).ToArray();
            if (options.Length == 0) return;
            Require(options.Length == 2, "complete original options"); int first = type == typeof(HardlightProject.CameraInputBrain) ? 2 : 1;
            for (int i = 0; i < 2; i++) Require(options[i].Constructor.DeclaringType == typeof(Il2CppSetOptionAttribute) && options[i].ConstructorArguments.Count == 2 && options[i].ConstructorArguments[0].ArgumentType == typeof(Option) && Convert.ToInt32(AttributeValue(options[i].ConstructorArguments[0])) == (i == 0 ? first : 3 - first) && Equals(AttributeValue(options[i].ConstructorArguments[1]), false) && options[i].NamedArguments.Count == 0, "original option argument/order");
        }
        static MethodBase[] DeclaredMethods(Type type)
        {
            var methods = type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared)).ToList(); if (type.TypeInitializer != null && !methods.Any(x => x.MetadataToken == type.TypeInitializer.MetadataToken)) methods.Add(type.TypeInitializer);
            return methods.OrderBy(x => x.MetadataToken).ToArray();
        }
        static void CheckCompleteCurrentDeclarationsAndBodies()
        {
            OwnerFact[] facts = CurrentFacts(); Require(facts.Length == 3 && facts.Sum(x => x.Methods.Length) == 37 && facts.Sum(x => x.Fields.Length) == 17, "complete three-owner/37-declaration scope");
            foreach (OwnerFact fact in facts)
            {
                Type type = typeof(HardlightProject.CameraInputBrain).Assembly.GetType(fact.Name.Replace('/', '+'), true);
                Require(TypeKey(type) == fact.Name && (int)type.Attributes == fact.Flags && TypeKey(type.BaseType) == fact.Base && !type.IsGenericType && type.GetInterfaces().Select(TypeKey).SequenceEqual(fact.Interfaces), "exact prospective current owner " + fact.Name);
                var attributes = type.GetCustomAttributesData(); Require(AttributeNames(attributes).SequenceEqual(fact.Attributes), "whole ordered owner attributes"); Options(type, attributes); Require(type.GetGenericArguments().Length == 0 && attributes.All(x => x.NamedArguments.Count == 0 && (x.AttributeType == typeof(Il2CppSetOptionAttribute) || (x.AttributeType == typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute) && x.ConstructorArguments.Count == 0))), "full nongeneric unnamed owner attribute scope");
                FieldInfo[] fields = type.GetFields(Declared).OrderBy(x => x.MetadataToken).ToArray(); Require(fields.Length == fact.Fields.Length && type.GetEvents(Declared).Length == 0, "whole field/event count");
                for (int i = 0; i < fields.Length; i++) { FieldFact field = fact.Fields[i]; Require(fields[i].Name == field.Name && TypeKey(fields[i].FieldType) == field.Type && (int)fields[i].Attributes == field.Flags && AttributeNames(fields[i].GetCustomAttributesData()).SequenceEqual(field.Attributes), "ordered exact field/generated capture " + field.Name); CheckFieldAttributes(fields[i]); }
                MethodBase[] methods = DeclaredMethods(type); Require(methods.Length == fact.Methods.Length, "whole methods/constructors count");
                for (int i = 0; i < methods.Length; i++) CheckMethod(methods[i], fact.Methods[i]);
                PropertyInfo[] properties = type.GetProperties(Declared).OrderBy(x => x.MetadataToken).ToArray(); Require(properties.Length == fact.Properties.Length, "whole property count");
                for (int i = 0; i < properties.Length; i++)
                {
                    PropertyFact property = fact.Properties[i]; PropertyInfo actual = properties[i]; Require(actual.Name == property.Name && TypeKey(actual.PropertyType) == property.Type && (int)actual.Attributes == property.Flags && actual.GetIndexParameters().Length == 0 && actual.GetCustomAttributesData().Count == 0, "whole property identity");
                    Require((actual.GetGetMethod(true) == null ? null : MethodKey(actual.GetGetMethod(true))) == property.Getter && (actual.GetSetMethod(true) == null ? null : MethodKey(actual.GetSetMethod(true))) == property.Setter, "exact linked accessor names/signatures");
                }
                string[] expectedNested = facts.Where(x => x.Name.StartsWith(fact.Name + "/", StringComparison.Ordinal) && x.Name.Substring(fact.Name.Length + 1).IndexOf('/') < 0).Select(x => x.Name).OrderBy(x => x).ToArray();
                Require(type.GetNestedTypes(Declared).Select(TypeKey).OrderBy(x => x).SequenceEqual(expectedNested), "complete nested generated/delegate owner graph");
            }
        }
        static void CheckFieldAttributes(FieldInfo field)
        {
            foreach (CustomAttributeData attribute in field.GetCustomAttributesData())
            {
                Require(attribute.NamedArguments.Count == 0, "no named field attribute arguments");
                if (attribute.AttributeType == typeof(UnityEngine.TooltipAttribute))
                {
                    string text = field.Name == "m_gameInputs" ? "GameInputs to disable when triggered." : "If true, disable the GameInputs when this component is enabled. GameInputs are always re-enabled when component is disabled.";
                    Require(field.DeclaringType == typeof(HardlightProject.GameInputDisabler) && (field.Name == "m_gameInputs" || field.Name == "m_activateOnEnable") && attribute.ConstructorArguments.Count == 1 && attribute.ConstructorArguments[0].ArgumentType == typeof(string) && Equals(AttributeValue(attribute.ConstructorArguments[0]), text), "exact original Tooltip string");
                }
                else Require(attribute.ConstructorArguments.Count == 0 && (attribute.AttributeType == typeof(UnityEngine.SerializeField) || attribute.AttributeType == typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)), "exact no-argument field attribute");
            }
        }
        static void CheckMethod(MethodBase actual, MethodFact fact)
        {
            Require(actual.Name == fact.Name && MethodKey(actual) == fact.Signature && (int)actual.Attributes == fact.Flags && (int)actual.GetMethodImplementationFlags() == fact.Impl && !actual.IsGenericMethod && AttributeNames(actual.GetCustomAttributesData()).SequenceEqual(fact.Attributes), "exact full ordered method " + fact.Signature);
            Require(actual.CallingConvention == (CallingConventions.Standard | (actual.IsStatic ? 0 : CallingConventions.HasThis)), "exact method calling convention");
            // Stored return flags remain fact.ReturnFlags==0 in the separately reviewed PE.
            // Unity Mono's actual rowless-return diagnostic exposes Retval8; do not equate the two surfaces.
            // Root must physically qualify sequence-zero Param-row absence for this exact complete family before execution.
            if (actual is MethodInfo method) Require(fact.ReturnFlags == 0 && method.ReturnParameter.Attributes == ParameterAttributes.Retval && method.ReturnParameter.Position == -1 && method.ReturnParameter.ParameterType == method.ReturnType && method.ReturnParameter.GetCustomAttributesData().Count == 0, "exact Unity Mono rowless return projection; stored flags held separately");
            ParameterInfo[] parameters = actual.GetParameters(); Require(parameters.Length == fact.ParameterNames.Length, "full parameter count");
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i]; Require(parameter.Position == i && parameter.Name == fact.ParameterNames[i] && TypeKey(parameter.ParameterType) == fact.ParameterTypes[i] && (int)parameter.Attributes == fact.ParameterFlags[i] && parameter.HasDefaultValue == fact.HasConstant[i], "exact parameter identity/default presence");
                Require(parameter.GetCustomAttributesData().All(x => x.AttributeType == typeof(System.Runtime.InteropServices.OptionalAttribute) && parameter.IsOptional && x.ConstructorArguments.Count == 0 && x.NamedArguments.Count == 0), "no real parameter custom attributes");
                if (fact.HasConstant[i]) Require(Equals(parameter.DefaultValue, fact.Constants[i]), "exact original optional default");
            }
            MethodBody body = actual.GetMethodBody(); if (fact.Bytes == null) { Require(body == null, "true abstract or delegate runtime body"); return; }
            Require(body != null && body.ExceptionHandlingClauses.Count == fact.Regions.Length && body.MaxStackSize == fact.MaxStack && body.InitLocals == fact.InitLocals && body.LocalVariables.Select(x => TypeKey(x.LocalType)).SequenceEqual(fact.Locals) && body.LocalVariables.All(x => !x.IsPinned), "full prospective physical body header/local/EH contract");
            for (int i = 0; i < fact.Regions.Length; i++) { ExceptionHandlingClause actualRegion = body.ExceptionHandlingClauses[i]; RegionFact region = fact.Regions[i]; Require(actualRegion.Flags == ExceptionHandlingClauseOptions.Finally && actualRegion.TryOffset == region.TryOffset && actualRegion.TryLength == region.TryLength && actualRegion.HandlerOffset == region.HandlerOffset && actualRegion.HandlerLength == region.HandlerLength, "complete original finally bounds/kind"); }
            byte[] expected = Enumerable.Range(0, fact.Bytes.Length / 2).Select(i => Convert.ToByte(fact.Bytes.Substring(i * 2, 2), 16)).ToArray(), bytes = body.GetILAsByteArray(); Require(bytes.Length == expected.Length, "complete physical CIL length"); var tokenBytes = new HashSet<int>();
            foreach (TokenFact token in fact.Tokens)
            {
                Require(token.Offset >= 1 && token.Offset + 4 <= bytes.Length, "complete token operand boundary"); MemberInfo member = actual.Module.ResolveMember(BitConverter.ToInt32(bytes, token.Offset)); Require(MemberKey(member) == token.Key && Scope(member, token.Scope), "exact loaded Module/token/scoped signature operand");
                for (int i = 0; i < 4; i++) Require(tokenBytes.Add(token.Offset + i), "disjoint physical operand cell");
            }
            for (int i = 0; i < bytes.Length; i++) if (!tokenBytes.Contains(i)) Require(bytes[i] == expected[i], "every physical opcode/branch/literal/local byte " + i);
            // Exactly matched non-token bytes retain full branch destinations and every opcode; no NOP filtering.
        }
    }
}
