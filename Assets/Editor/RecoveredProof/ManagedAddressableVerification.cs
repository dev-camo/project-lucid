using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ProjectLucid
{
    public static class ManagedAddressableVerification
    {
        private static int checks;
        private static void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException(label);
            checks++;
        }
        private static FieldInfo Field(string name) => typeof(ManagedAddressableAsset<UnityEngine.Object>).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static int Count(ManagedAddressableAsset<UnityEngine.Object> value) => (int)Field("m_refCount").GetValue(value);
        private static List<MemberInfo> Members(MethodInfo method)
        {
            var result = new List<MemberInfo>();
            byte[] body = method.GetMethodBody().GetILAsByteArray();
            var codes = new Dictionary<ushort, OpCode>();
            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                OpCode code = (OpCode)field.GetValue(null);
                codes[unchecked((ushort)code.Value)] = code;
            }
            for (int offset = 0; offset < body.Length;)
            {
                ushort value = body[offset++];
                if (value == 0xfe) value = (ushort)(0xfe00 | body[offset++]);
                OpCode code = codes[value];
                int size;
                switch (code.OperandType)
                {
                    case OperandType.InlineNone: size = 0; break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar: size = 1; break;
                    case OperandType.InlineVar: size = 2; break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR: size = 8; break;
                    case OperandType.InlineSwitch: size = 4 + 4 * BitConverter.ToInt32(body, offset); break;
                    default: size = 4; break;
                }
                if (code.OperandType == OperandType.InlineMethod || code.OperandType == OperandType.InlineField)
                    result.Add(method.Module.ResolveMember(BitConverter.ToInt32(body, offset), method.DeclaringType.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null));
                offset += size;
            }
            return result;
        }
        private static int Find(List<MemberInfo> members, string type, string name)
            => members.FindIndex(x => x.DeclaringType.FullName == type && x.Name == name);

        public static void Run() => Debug.Log("Project Lucid ManagedAddressable package/schema checks=" + RunManaged());

        // Uses genuine package references and invalid handles only. Engine object
        // creation, loading, completion and release require separate actual Unity proof.
        public static int RunManaged()
        {
            checks = 0;
            Type definition = typeof(ManagedAddressableAsset<>);
            Type type = typeof(ManagedAddressableAsset<UnityEngine.Object>);
            Check(definition.FullName == "HardlightProject.ManagedAddressableAsset`1" && definition.IsPublic && definition.IsSealed, "original generic sealed type");
            Type parameter = definition.GetGenericArguments()[0];
            Check(parameter.GenericParameterAttributes == GenericParameterAttributes.None && parameter.GetGenericParameterConstraints().Length == 1 && parameter.GetGenericParameterConstraints()[0] == typeof(UnityEngine.Object), "original Unity Object constraint");
            BindingFlags all = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            FieldInfo[] fields = type.GetFields(all);
            Check(fields.Length == 4, "complete own field graph");
            string[] names = { "m_assetReference", "m_loadedAsset", "m_assetHandle", "m_refCount" };
            Type[] expected = { typeof(AssetReferenceT<UnityEngine.Object>), typeof(UnityEngine.Object), typeof(AsyncOperationHandle<UnityEngine.Object>), typeof(int) };
            for (int i = 0; i < names.Length; i++)
            {
                Check(fields[i].Name == names[i] && fields[i].FieldType == expected[i], "field order/type " + i);
                Check(fields[i].IsPrivate && !fields[i].IsStatic && fields[i].IsInitOnly == (i == 0), "field modifiers " + i);
                Check(!fields[i].IsNotSerialized && fields[i].GetCustomAttributes(false).Length == 0, "original field attrs " + i);
            }
            Check(type.GetProperties(all).Length == 0 && type.GetEvents(all).Length == 0 && type.GetMethods(all).Length == 4 && type.GetConstructors(all).Length == 1, "complete original member surface");
            var options = (Il2CppSetOptionAttribute[])type.GetCustomAttributes(typeof(Il2CppSetOptionAttribute), false);
            Check(options.Length == 2 && options[0].Option == Option.NullChecks && !(bool)options[0].Value && options[1].Option == Option.ArrayBoundsChecks && !(bool)options[1].Value, "original compiler option payload/order");
            Check(typeof(AssetReferenceT<>).Assembly.GetName().Name == "Unity.Addressables", "genuine Addressables assembly");
            Check(typeof(AsyncOperationHandle<>).Assembly.GetName().Name == "Unity.ResourceManager", "genuine ResourceManager assembly");
            Check(typeof(UnityEngine.Object).Assembly.GetName().Name == "UnityEngine.CoreModule", "genuine Unity Object assembly");

            var missing = new ManagedAddressableAsset<UnityEngine.Object>(null);
            Check(Field("m_assetReference").GetValue(missing) == null && Field("m_loadedAsset").GetValue(missing) == null && Count(missing) == 0, "constructor null/reference/count defaults");
            Check(!((AsyncOperationHandle<UnityEngine.Object>)Field("m_assetHandle").GetValue(missing)).IsValid(), "constructor genuine invalid default handle");
            Check(!missing.IsValid(), "null reference invalid");
            foreach (string key in new[] { null, "", "invalid", "00000000000000000000000000000000", "0123456789abcdef0123456789abcdef" })
            {
                var reference = new AssetReferenceT<UnityEngine.Object>(key);
                var wrapper = new ManagedAddressableAsset<UnityEngine.Object>(reference);
                Check(ReferenceEquals(Field("m_assetReference").GetValue(wrapper), reference), "constructor retains reference " + key);
                Check(wrapper.IsValid() == reference.RuntimeKeyIsValid(), "original key-validity dispatch " + key);
                Check(!reference.IsValid(), "key validity distinct from actual operation validity " + key);
            }
            var subReference = new AssetReferenceT<UnityEngine.Object>("0123456789abcdef0123456789abcdef") { SubObjectName = "zone" };
            Check(new ManagedAddressableAsset<UnityEngine.Object>(subReference).IsValid(), "real subobject key validity");
            Field("m_refCount").SetValue(missing, 3);
            missing.Unload(); Check(Count(missing) == 2, "unload decreases positive count");
            missing.Unload(); Check(Count(missing) == 1, "unload retains one reference");
            missing.Unload(); Check(Count(missing) == 0, "unload reaches zero");
            missing.Unload(); Check(Count(missing) == 0, "extra unload clamps zero");
            Check(!((AsyncOperationHandle<UnityEngine.Object>)Field("m_assetHandle").GetValue(missing)).IsValid(), "invalid handle retained through unload");
            Field("m_refCount").SetValue(missing, -8);
            missing.Unload(); Check(Count(missing) == 0, "negative count clamps on unload");
            Field("m_refCount").SetValue(missing, int.MinValue);
            missing.Unload(); Check(Count(missing) == int.MaxValue, "unload native wrapped decrement");

            List<MemberInfo> load = Members(type.GetMethod("Load"));
            int compare = Find(load, "UnityEngine.Object", "op_Inequality");
            int start = Find(load, "UnityEngine.AddressableAssets.Addressables", "LoadAssetAsync");
            int wait = load.FindIndex(x => x.Name == "WaitForCompletion");
            Check(load.FindIndex(x => x.Name == "m_refCount") < compare && compare < start && start < wait, "load count/cache/start/wait order");
            MethodInfo startMethod = (MethodInfo)load[start];
            Check(startMethod.GetGenericArguments()[0] == typeof(UnityEngine.Object) && startMethod.GetParameters()[0].ParameterType == typeof(object), "static object-key load overload");
            Check(load.FindIndex(start, x => x.Name == "m_assetHandle") < wait && load.FindIndex(wait, x => x.Name == "m_loadedAsset") > wait, "handle stored before wait, result stored after");
            Check(!load.Exists(x => x.Name == "RuntimeKey" || x.Name == "RuntimeKeyIsValid" || x.Name == "get_Status" || x.Name == "IsValid"), "load lacks added validity/status gates");
            List<MemberInfo> asyncLoad = Members(type.GetMethod("LoadAsync"));
            int closureCtor = asyncLoad.FindIndex(x => x.Name == ".ctor" && x.DeclaringType.Name == "<>c__DisplayClass7_0");
            int asyncCount = asyncLoad.FindIndex(x => x.Name == "m_refCount");
            int asyncCompare = Find(asyncLoad, "UnityEngine.Object", "op_Inequality");
            int asyncStart = Find(asyncLoad, "UnityEngine.AddressableAssets.Addressables", "LoadAssetAsync");
            Check(closureCtor >= 0 && closureCtor < asyncCount && asyncCount < asyncCompare && asyncCompare < asyncStart, "async closure allocation/count/cache/load order");
            Check(asyncLoad.FindIndex(asyncStart, x => x.Name == "m_assetHandle") < asyncLoad.FindIndex(x => x.Name == "add_Completed"), "async store handle before completion registration");
            Check(!asyncLoad.Exists(x => x.Name == "get_Status" || x.Name == "IsValid" || x.Name == "get_IsDone"), "async no added inflight/status gates");
            List<MemberInfo> unload = Members(type.GetMethod("Unload"));
            int valid = unload.FindIndex(x => x.Name == "IsValid");
            int release = Find(unload, "UnityEngine.AddressableAssets.Addressables", "Release");
            Check(unload.FindIndex(x => x.Name == "m_loadedAsset") < valid && valid < release, "clear-before-validity/release");
            MethodInfo releaseMethod = (MethodInfo)unload[release];
            Check(releaseMethod.GetGenericArguments()[0] == typeof(UnityEngine.Object) && releaseMethod.GetParameters()[0].ParameterType == typeof(AsyncOperationHandle<UnityEngine.Object>), "typed handle release overload");

            Type closure = definition.GetNestedTypes(BindingFlags.NonPublic)[0].MakeGenericType(typeof(UnityEngine.Object));
            FieldInfo[] closureFields = closure.GetFields(all);
            Check(closure.Name == "<>c__DisplayClass7_0", "original compiler closure ordinal");
            Check(closureFields.Length == 2 && closureFields[0].Name == "<>4__this" && closureFields[0].FieldType == type && closureFields[1].Name == "onLoaded" && closureFields[1].FieldType == typeof(Action<UnityEngine.Object>), "complete closure fields/order/types");
            MethodInfo callback = closure.GetMethod("<LoadAsync>b__0", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(callback != null && callback.IsAssembly && callback.GetParameters()[0].ParameterType == typeof(AsyncOperationHandle<UnityEngine.Object>), "original closure callback contract");
            List<MemberInfo> completion = Members(callback);
            int result = completion.FindIndex(x => x.Name == "get_Result");
            Check(result >= 0 && completion.FindIndex(x => x.Name == "m_loadedAsset") > result && completion.FindIndex(x => x.Name == "Invoke") > completion.FindIndex(x => x.Name == "m_loadedAsset"), "callback result/store/invoke order");
            Check(!completion.Exists(x => x.Name == "m_refCount" || x.Name == "IsValid" || x.Name == "get_Status"), "callback no status/refcount gates");
            object capture = Activator.CreateInstance(closure);
            closureFields[0].SetValue(capture, missing);
            int callbacks = 0;
            closureFields[1].SetValue(capture, (Action<UnityEngine.Object>)(asset => callbacks++));
            Field("m_refCount").SetValue(missing, 9);
            try { callback.Invoke(capture, new object[] { default(AsyncOperationHandle<UnityEngine.Object>) }); throw new ApplicationException("invalid callback handle accepted"); }
            catch (TargetInvocationException ex) { Check(ex.InnerException.GetType() == typeof(Exception) && ex.InnerException.Message == "Attempting to use an invalid operation handle", "genuine invalid handle result fails before callback"); }
            Check(callbacks == 0 && Count(missing) == 9 && Field("m_loadedAsset").GetValue(missing) == null, "failed Result leaves captured owner/count/callback untouched");
            return checks;
        }
    }
}
