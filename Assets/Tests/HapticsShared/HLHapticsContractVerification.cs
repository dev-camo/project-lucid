using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace ProjectLucid.Verification
{
 public static partial class HLHapticsVerification
 {
  internal const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
  internal static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("HLHaptics original whole contract: " + message); }
  sealed class ArgumentFact { public readonly string Type, Value; public ArgumentFact(string type,string value) { Type=type; Value=value; } }
  sealed class NamedFact { public readonly bool Field; public readonly string Name; public readonly ArgumentFact Argument; public NamedFact(bool field,string name,ArgumentFact argument) { Field=field;Name=name;Argument=argument; } }
  sealed class AttributeFact { public readonly string Type,Constructor; public readonly ArgumentFact[] Arguments; public readonly NamedFact[] Named; public AttributeFact(string type,string constructor,ArgumentFact[] arguments,NamedFact[] named) {Type=type;Constructor=constructor;Arguments=arguments;Named=named;} }
  sealed class FieldFact { public readonly string Name,Type; public readonly int Flags; public readonly AttributeFact[] Attributes; public FieldFact(string name,string type,int flags,AttributeFact[] attributes) { Name=name;Type=type;Flags=flags;Attributes=attributes; } }
  sealed class TokenFact { public readonly int Offset,Token; public readonly string Kind,Key,Scope; public TokenFact(int offset,int token,string kind,string key,string scope) { Offset=offset;Token=token;Kind=kind;Key=key;Scope=scope; } }
  sealed class MethodFact
  {
   public readonly string Name,Key,Return,Bytes; public readonly int Flags,Impl,Stack,LocalToken; public readonly bool Init;
   public readonly string[] Names,Types,Overrides,Locals; public readonly AttributeFact[] Attributes; public readonly TokenFact[] Tokens;
   public MethodFact(string name,string key,int flags,int impl,string result,string[] names,string[] types,AttributeFact[] attributes,string[] overrides,string bytes,int stack,bool init,int localToken,string[] locals,TokenFact[] tokens) {Name=name;Key=key;Flags=flags;Impl=impl;Return=result;Names=names;Types=types;Attributes=attributes;Overrides=overrides;Bytes=bytes;Stack=stack;Init=init;LocalToken=localToken;Locals=locals;Tokens=tokens;}
  }
  sealed class PropertyFact { public readonly string Name,Type,Getter,Setter; public readonly int Flags; public readonly AttributeFact[] Attributes; public PropertyFact(string name,string type,int flags,string getter,string setter,AttributeFact[] attributes) {Name=name;Type=type;Flags=flags;Getter=getter;Setter=setter;Attributes=attributes;} }
  sealed class OwnerFact { public readonly string Name,Base; public readonly int Flags; public readonly string[] Interfaces,Nested; public readonly AttributeFact[] Attributes; public readonly FieldFact[] Fields; public readonly MethodFact[] Methods; public readonly PropertyFact[] Properties; public OwnerFact(string name,int flags,string parent,string[] interfaces,string[] nested,AttributeFact[] attributes,FieldFact[] fields,MethodFact[] methods,PropertyFact[] properties) {Name=name;Flags=flags;Base=parent;Interfaces=interfaces;Nested=nested;Attributes=attributes;Fields=fields;Methods=methods;Properties=properties;} }
  static readonly Type[] ProviderRoots = {
   typeof(object),typeof(void),typeof(bool),typeof(int),typeof(float),typeof(string),typeof(byte),typeof(Array),typeof(ValueType),typeof(Type),typeof(RuntimeFieldHandle),typeof(RuntimeHelpers),typeof(System.ComponentModel.EditorBrowsableAttribute),typeof(System.ComponentModel.EditorBrowsableState),typeof(System.CodeDom.Compiler.GeneratedCodeAttribute),typeof(IDisposable),typeof(IEnumerator),typeof(IEnumerator<>),typeof(IEnumerable<>),typeof(ICollection<>),typeof(IReadOnlyCollection<>),typeof(HashSet<>),typeof(NotSupportedException),typeof(CompilerGeneratedAttribute),typeof(IteratorStateMachineAttribute),typeof(DebuggerHiddenAttribute),typeof(DebuggerBrowsableAttribute),typeof(DebuggerBrowsableState),typeof(SerializableAttribute),
   typeof(Il2CppSetOptionAttribute),typeof(Option),typeof(SystemConfigurationAsset),typeof(IModuleConfigurationWithFileList),typeof(ISystem),typeof(Hardlight.CollectionExtensions),typeof(ProcessManager),typeof(SystemRef),typeof(CoroutineUtils),typeof(HashEnumAttribute),
   typeof(ScriptableObject),typeof(MonoBehaviour),typeof(Coroutine),typeof(UnityEngine.Object),typeof(CreateAssetMenuAttribute),typeof(SerializeField),typeof(VibrationEvent),typeof(VibrationType)
  };
  static string TypeKey(Type type)
  {
   if(type.IsGenericParameter) return (type.DeclaringMethod==null?"!":"!!")+type.GenericParameterPosition;
   if(type.IsArray) {Require(type.GetArrayRank()==1&&type==type.GetElementType().MakeArrayType(),"exact SZARRAY identity; rank-one non-SZARRAY rejected");return TypeKey(type.GetElementType())+"[]";}
   if(type.IsByRef) return TypeKey(type.GetElementType())+"&";
   Type root=type.IsGenericType?type.GetGenericTypeDefinition():type;
   bool own=root.Assembly==typeof(VibrationManager).Assembly && CurrentOwners().Any(x=>x.Name==root.FullName.Replace('+','/'));
   Require(own || ProviderRoots.Contains(root),"closed exact genuine type/provider: "+root.FullName);
   string name=root.FullName.Replace('+','/');
   return type.IsGenericType && !type.IsGenericTypeDefinition ? name+"<"+string.Join(",",type.GetGenericArguments().Select(TypeKey))+">" : name;
  }
  static bool ExactScope(Type type,string scope)
  {
   while(type.HasElementType) type=type.GetElementType();
   Type root=type.IsGenericType?type.GetGenericTypeDefinition():type;
   if(scope=="HLHaptics.Runtime.dll")return root.Assembly==typeof(VibrationManager).Assembly && CurrentOwners().Any(x=>x.Name==TypeKey(root));
   if(scope=="HLUnityCore.Runtime")return ProviderRoots.Contains(root)&&root.Assembly==typeof(ProcessManager).Assembly;
   if(scope=="HLAutoGenerated")return (root==typeof(VibrationEvent)||root==typeof(VibrationType))&&root.Assembly==typeof(VibrationEvent).Assembly;
   if(scope=="UnityEngine.CoreModule")return ProviderRoots.Contains(root)&&root.Assembly==typeof(UnityEngine.Object).Assembly;
   if(scope=="netstandard")return ProviderRoots.Contains(root)&&root.Assembly==ProviderRoots.Single(x=>x==root).Assembly && (root.Namespace=="System"||root.Namespace.StartsWith("System.",StringComparison.Ordinal));
   return false;
  }
  static MethodBase Definition(MethodBase method)
  {
   if(method.IsGenericMethod)method=((MethodInfo)method).GetGenericMethodDefinition();
   if(method.DeclaringType.IsGenericType&&!method.DeclaringType.IsGenericTypeDefinition)
    return method.DeclaringType.GetGenericTypeDefinition().GetMethods(Declared).Cast<MethodBase>().Concat(method.DeclaringType.GetGenericTypeDefinition().GetConstructors(Declared)).Single(x=>x.MetadataToken==method.MetadataToken);
   return method;
  }
  static string MethodKey(MethodBase method)
  {
   MethodBase definition=Definition(method);string name=method.Name;
   if(method.IsGenericMethod&&!method.IsGenericMethodDefinition) name+="<"+string.Join(",",method.GetGenericArguments().Select(TypeKey))+">";
   return (definition is MethodInfo info?TypeKey(info.ReturnType):"System.Void")+" "+TypeKey(method.DeclaringType)+"::"+name+"("+string.Join(",",definition.GetParameters().Select(x=>TypeKey(x.ParameterType)))+")";
  }
  static string MemberKey(MemberInfo member)
  {
   if(member is FieldInfo field)return TypeKey(field.FieldType)+" "+TypeKey(field.DeclaringType)+"::"+field.Name;
   if(member is MethodBase method)return MethodKey(method);
   throw new InvalidOperationException("Unexpected exact token member kind "+member.MemberType);
  }
  static string ValueKey(object value)
  {
   if(value==null)return "null";
   if(value is Type type)return "type:"+TypeKey(type);
   if(value is CustomAttributeTypedArgument wrapped)return ValueKey(wrapped.Value);
   return value.GetType().FullName+":"+Convert.ToString(value,CultureInfo.InvariantCulture);
  }
  static void CheckAttributes(IList<CustomAttributeData> actual,AttributeFact[] facts,bool serializable=false)
  {
   Require(actual.Count==facts.Length+(serializable?1:0),"whole exact stored/pseudo attribute count");
   for(int i=0;i<facts.Length;i++)
   {
    CustomAttributeData a=actual[i];AttributeFact f=facts[i];Require(TypeKey(a.AttributeType)==f.Type&&MethodKey(a.Constructor)==f.Constructor&&a.ConstructorArguments.Count==f.Arguments.Length&&a.NamedArguments.Count==f.Named.Length,"exact attribute/constructor/count/order");
    for(int j=0;j<f.Arguments.Length;j++)Require(TypeKey(a.ConstructorArguments[j].ArgumentType)==f.Arguments[j].Type&&ValueKey(a.ConstructorArguments[j].Value)==f.Arguments[j].Value,"exact typed attribute argument");
    for(int j=0;j<f.Named.Length;j++)Require(a.NamedArguments[j].IsField==f.Named[j].Field&&a.NamedArguments[j].MemberName==f.Named[j].Name&&TypeKey(a.NamedArguments[j].TypedValue.ArgumentType)==f.Named[j].Argument.Type&&ValueKey(a.NamedArguments[j].TypedValue.Value)==f.Named[j].Argument.Value,"exact named attribute value/kind/order");
   }
   if(serializable) {CustomAttributeData a=actual[facts.Length];Require(a.AttributeType==typeof(SerializableAttribute)&&a.Constructor==typeof(SerializableAttribute).GetConstructor(Type.EmptyTypes)&&a.ConstructorArguments.Count==0&&a.NamedArguments.Count==0,"exact genuine Serializable pseudoattribute; stored absence separate");}
  }
  internal static void CheckWholeContract()
  {
   Assembly assembly=typeof(VibrationManager).Assembly;Require(assembly.GetName().Name=="HLHaptics.Runtime"&&typeof(VibrationEventData).Assembly==assembly&&typeof(HLHapticsConfigurationAsset).Assembly==assembly,"complete original assembly owners");
   OwnerFact[] facts=CurrentOwners();Type[] types=assembly.GetTypes();Require(types.Length==facts.Length&&facts.Sum(x=>x.Methods.Length)==26&&facts.Where(x=>x.Name=="VibrationEventData"||x.Name.StartsWith("VibrationEventData/",StringComparison.Ordinal)||x.Name=="Hardlight.HLHapticsConfigurationAsset"||x.Name=="Hardlight.VibrationManager"||x.Name.StartsWith("Hardlight.VibrationManager/",StringComparison.Ordinal)).Sum(x=>x.Methods.Length)==24,"whole original24 plus separate generator2; whole owner inventory");
   foreach(OwnerFact f in facts)
   {
    Type type=assembly.GetType(f.Name.Replace('/','+'),true);Require((int)type.Attributes==f.Flags&&TypeKey(type.BaseType)==f.Base&&!type.IsGenericType&&type.GetGenericArguments().Length==0&&type.GetEvents(Declared).Length==0,"whole exact owner/base/flags/generic/event scope");
    Require(type.GetInterfaces().Select(TypeKey).OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(f.Interfaces.OrderBy(x=>x,StringComparer.Ordinal)),"complete interface identities; stored declaration order separately retained");
    Require(type.GetNestedTypes(Declared).OrderBy(x=>x.MetadataToken).Select(TypeKey).SequenceEqual(f.Nested),"whole ordered nested owners");CheckAttributes(type.GetCustomAttributesData(),f.Attributes,type.IsSerializable);
    FieldInfo[] fields=type.GetFields(Declared).OrderBy(x=>x.MetadataToken).ToArray();Require(fields.Length==f.Fields.Length,"whole exact ordered field count");
    for(int i=0;i<fields.Length;i++){FieldInfo field=fields[i];FieldFact ff=f.Fields[i];Require(field.DeclaringType==type&&field.Name==ff.Name&&TypeKey(field.FieldType)==ff.Type&&(int)field.Attributes==ff.Flags&&!field.IsLiteral,"exact complete field/name/type/flags");CheckAttributes(field.GetCustomAttributesData(),ff.Attributes);}
    MethodBase[] methods=type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared)).OrderBy(x=>x.MetadataToken).ToArray();Require(methods.Length==f.Methods.Length,"whole exact method inventory");for(int i=0;i<methods.Length;i++)CheckMethod(methods[i],f.Methods[i]);
    PropertyInfo[] properties=type.GetProperties(Declared).OrderBy(x=>x.MetadataToken).ToArray();Require(properties.Length==f.Properties.Length,"whole property count");
    for(int i=0;i<properties.Length;i++){PropertyInfo p=properties[i];PropertyFact pf=f.Properties[i];Require(p.Name==pf.Name&&TypeKey(p.PropertyType)==pf.Type&&(int)p.Attributes==pf.Flags&&p.GetIndexParameters().Length==0&&(p.GetGetMethod(true)==null?null:MethodKey(p.GetGetMethod(true)))==pf.Getter&&(p.GetSetMethod(true)==null?null:MethodKey(p.GetSetMethod(true)))==pf.Setter&&p.GetAccessors(true).Length==(pf.Getter==null?0:1)+(pf.Setter==null?0:1),"whole exact property/accessor scope");CheckAttributes(p.GetCustomAttributesData(),pf.Attributes);}
   }
   CheckProviderSourcesAndShapes();
  }
  static void CheckMethod(MethodBase actual,MethodFact f)
  {
   Require(actual.Name==f.Name&&MethodKey(actual)==f.Key&&(int)actual.Attributes==f.Flags&&(int)actual.GetMethodImplementationFlags()==f.Impl&&!actual.IsGenericMethod&&actual.CallingConvention==(CallingConventions.Standard|(actual.IsStatic?0:CallingConventions.HasThis)),"complete exact method/name/signature/flags/impl/convention");CheckAttributes(actual.GetCustomAttributesData(),f.Attributes);
   if(actual is MethodInfo info){ParameterInfo r=info.ReturnParameter;Require(TypeKey(info.ReturnType)==f.Return&&r.Attributes==ParameterAttributes.Retval&&r.Position==-1&&r.ParameterType==info.ReturnType&&r.GetCustomAttributesData().Count==0,"exact rowless return projection; default projection unobserved and not guessed");}
   ParameterInfo[] parameters=actual.GetParameters();Require(parameters.Length==f.Names.Length,"whole ordinary parameter count");for(int i=0;i<parameters.Length;i++)Require(parameters[i].Position==i&&parameters[i].Name==f.Names[i]&&TypeKey(parameters[i].ParameterType)==f.Types[i]&&parameters[i].Attributes==0&&!parameters[i].IsOptional&&!parameters[i].HasDefaultValue&&parameters[i].GetCustomAttributesData().Count==0,"exact ordinary parameter/no default/no attribute");
   string[] overrides=actual.DeclaringType.GetInterfaces().SelectMany(t=>{InterfaceMapping map=actual.DeclaringType.GetInterfaceMap(t);return Enumerable.Range(0,map.TargetMethods.Length).Where(i=>map.TargetMethods[i]==actual).Select(i=>MethodKey(map.InterfaceMethods[i]));}).OrderBy(x=>x,StringComparer.Ordinal).ToArray();string[] projected=f.Overrides;
   if(actual.DeclaringType==typeof(HLHapticsConfigurationAsset)&&f.Name=="GetIncludedNativeFiles")projected=new[]{"System.Collections.Generic.IEnumerable`1<System.String> Hardlight.IModuleConfigurationWithFileList::GetIncludedNativeFiles()"};
   if(actual.DeclaringType==typeof(HLHapticsConfigurationAsset)&&f.Name=="GetNativeCodePath")projected=new[]{"System.String Hardlight.IModuleConfigurationWithFileList::GetNativeCodePath()"};
   Require(overrides.SequenceEqual(projected.OrderBy(x=>x,StringComparer.Ordinal)),"whole exact explicit and genuine implicit interface map; stored MethodImpl rows/order remain separate");
   if(actual.DeclaringType==typeof(HLHapticsConfigurationAsset)&&f.Name=="Validate")Require(((MethodInfo)actual).GetBaseDefinition().DeclaringType==typeof(SystemConfigurationAsset)&&((MethodInfo)actual).GetBaseDefinition().Name=="Validate","exact original abstract provider override");
   MethodBody body=actual.GetMethodBody();Require(body!=null&&body.MaxStackSize==f.Stack&&body.InitLocals==f.Init&&body.ExceptionHandlingClauses.Count==0&&body.LocalVariables.Count==f.Locals.Length,"complete physical header/stack/init/local/zero EH witness");Require(f.LocalToken==0?body.LocalSignatureMetadataToken==0:(body.LocalSignatureMetadataToken&unchecked((int)0xff000000))==0x11000000&&(body.LocalSignatureMetadataToken&0x00ffffff)!=0,"physical NIL vs complete nonNIL StandAloneSig binding");
   for(int i=0;i<f.Locals.Length;i++)Require(body.LocalVariables[i].LocalIndex==i&&!body.LocalVariables[i].IsPinned&&TypeKey(body.LocalVariables[i].LocalType)==f.Locals[i],"complete ordered locals");
   byte[] bytes=body.GetILAsByteArray(),expected=Enumerable.Range(0,f.Bytes.Length/2).Select(i=>Convert.ToByte(f.Bytes.Substring(i*2,2),16)).ToArray();Require(bytes.Length==expected.Length,"whole literal CIL length");var cells=new HashSet<int>();
   foreach(TokenFact t in f.Tokens)
   {
    Require(t.Offset>=1&&t.Offset+4<=bytes.Length&&BitConverter.ToInt32(expected,t.Offset)==t.Token,"exact recorded complete token cell");int token=BitConverter.ToInt32(bytes,t.Offset);Require((token&unchecked((int)0xff000000))==(t.Token&unchecked((int)0xff000000)),"exact token table before resolution");
    if(t.Kind=="string")Require(t.Scope=="literal #US"&&actual.Module.ResolveString(token)==t.Key,"exact literal user string");
    else if(t.Kind=="type"){Type type=actual.Module.ResolveType(token);Require(TypeKey(type)==t.Key&&ExactScope(type,t.Scope),"exact scoped type cell");}
    else {MemberInfo member=actual.Module.ResolveMember(token);Require(MemberKey(member)==t.Key&&ExactScope(member.DeclaringType,t.Scope),"exact scoped genuine member cell");}
    for(int i=0;i<4;i++)Require(cells.Add(t.Offset+i),"disjoint exact token cell");
   }
   for(int i=0;i<bytes.Length;i++)if(!cells.Contains(i))Require(bytes[i]==expected[i],"every complete opcode/branch/index/noncell byte "+i);
  }
 }
}
