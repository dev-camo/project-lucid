using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace ProjectLucid.Editor
{
    internal sealed class CompletionApiVerification
    {
        private int checks;
        private void Check(bool v, string m)
        {
            checks++;
            if (!v)
                throw new Exception("API " + m);
        }

        private static string Normalize(string name)
        {
            name = Regex.Replace(name, @"`\d+", "");
            // A nested generic type carries its enclosing parameters only once.
            name = Regex.Replace(name, @"<[^<>]*>\+", "+");
            return name.Replace(" ", "").Replace("!0", "TDelegate");
        }

        private static string FormatType(Type type)
        {
            if (type == null)
                return "";
            if (type.IsByRef)
                return FormatType(type.GetElementType()) + "&";
            if (type.IsGenericParameter)
                return type.Name;
            string name = type.IsGenericType ? type.GetGenericTypeDefinition().FullName : type.FullName;
            return Normalize(name) + (type.IsGenericType ? "<" + string.Join(",", type.GetGenericArguments().Select(FormatType)) + ">" : "");
        }

        private void VerifyMethod(Type t, string token, string name, int generic, string returns, string attributes, string[] types, string[] names, string[] pattrs, object[] defaults)
        {
            var ms = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Cast<MethodBase>().Concat(t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly));
            var matches = ms.Where(m => m.Name == name && (m is MethodInfo ? ((MethodInfo)m).GetGenericArguments().Length : 0) == generic && m.GetParameters().Select(p => FormatType(p.ParameterType)).SequenceEqual(types.Select(Normalize))).ToArray();
            Check(matches.Length == 1, token + " match");
            var a = matches[0];
            Check(a.Attributes == (MethodAttributes)Enum.Parse(typeof(MethodAttributes), attributes), token + " method flags " + a.Attributes);
            Check(Normalize(a is MethodInfo ? FormatType(((MethodInfo)a).ReturnType) : "System.Void") == Normalize(returns), token + " return");
            var ps = a.GetParameters();
            for (int i = 0; i < ps.Length; i++)
            {
                Check(ps[i].Name == names[i], token + " param name");
                Check(ps[i].Attributes == (ParameterAttributes)Enum.Parse(typeof(ParameterAttributes), pattrs[i]), token + " parameter flags");
                if ((ps[i].Attributes & ParameterAttributes.Optional) != 0)
                    Check(Equals(ps[i].DefaultValue, defaults[i]), token + " default");
                if (ps[i].IsIn && ps[i].ParameterType.IsByRef)
                    Check(ps[i].GetCustomAttributesData().Any(x => x.AttributeType.Name == "IsReadOnlyAttribute"), token + " readonly IN");
            }
        }

        public int Run()
        {
            var asm = typeof(MessageExchangeWithCompletion<>).Assembly;
            Type t;
            FieldInfo[] fs;
            int[] opts;
            t = asm.GetType("Hardlight.MessageCallbackWithCompletion", true);
            Check((int)t.Attributes == 257, "0x0200019b type attributes");
            Check(t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length + t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 4, "0x0200019b complete local API");
            fs = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(fs.Length == 0, "0x0200019b fields");
            opts = t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).Select(a => (int)a.Option).ToArray();
            Check(opts.SequenceEqual(new int[]{}) && t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).All(a => Equals(a.Value, false)), "0x0200019b options/order/false");
            VerifyMethod(t, "0x06000b79", ".ctor", 0, "System.Void", "Public, HideBySig, SpecialName, RTSpecialName", new string[]{"System.Object", "System.IntPtr"}, new string[]{"object", "method"}, new string[]{"None", "None"}, new object[]{null, null});
            VerifyMethod(t, "0x06000b7a", "Invoke", 0, "System.Void", "Public, Virtual, HideBySig, VtableLayoutMask", new string[]{"System.Action"}, new string[]{"completed"}, new string[]{"None"}, new object[]{null});
            VerifyMethod(t, "0x06000b7b", "BeginInvoke", 0, "System.IAsyncResult", "Public, Virtual, HideBySig, VtableLayoutMask", new string[]{"System.Action", "System.AsyncCallback", "System.Object"}, new string[]{"completed", "callback", "object"}, new string[]{"None", "None", "None"}, new object[]{null, null, null});
            VerifyMethod(t, "0x06000b7c", "EndInvoke", 0, "System.Void", "Public, Virtual, HideBySig, VtableLayoutMask", new string[]{"System.IAsyncResult"}, new string[]{"result"}, new string[]{"None"}, new object[]{null});
            t = asm.GetType("Hardlight.MessageCallbackWithCompletion`1", true);
            Check((int)t.Attributes == 257, "0x0200019c type attributes");
            Check(t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length + t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 4, "0x0200019c complete local API");
            fs = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(fs.Length == 0, "0x0200019c fields");
            opts = t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).Select(a => (int)a.Option).ToArray();
            Check(opts.SequenceEqual(new int[]{}) && t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).All(a => Equals(a.Value, false)), "0x0200019c options/order/false");
            VerifyMethod(t, "0x06000b7d", ".ctor", 0, "System.Void", "Public, HideBySig, SpecialName, RTSpecialName", new string[]{"System.Object", "System.IntPtr"}, new string[]{"object", "method"}, new string[]{"None", "None"}, new object[]{null, null});
            VerifyMethod(t, "0x06000b7e", "Invoke", 0, "System.Void", "Public, Virtual, HideBySig, VtableLayoutMask", new string[]{"T&", "System.Action"}, new string[]{"obj", "completed"}, new string[]{"In", "None"}, new object[]{null, null});
            VerifyMethod(t, "0x06000b7f", "BeginInvoke", 0, "System.IAsyncResult", "Public, Virtual, HideBySig, VtableLayoutMask", new string[]{"T&", "System.Action", "System.AsyncCallback", "System.Object"}, new string[]{"obj", "completed", "callback", "object"}, new string[]{"In", "None", "None", "None"}, new object[]{null, null, null, null});
            VerifyMethod(t, "0x06000b80", "EndInvoke", 0, "System.Void", "Public, Virtual, HideBySig, VtableLayoutMask", new string[]{"T&", "System.IAsyncResult"}, new string[]{"obj", "result"}, new string[]{"In", "None"}, new object[]{null, null});
            t = asm.GetType("Hardlight.MessageCallbackWithCompletion`2", true);
            Check((int)t.Attributes == 257, "0x0200019d type attributes");
            Check(t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length + t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 4, "0x0200019d complete local API");
            fs = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(fs.Length == 0, "0x0200019d fields");
            opts = t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).Select(a => (int)a.Option).ToArray();
            Check(opts.SequenceEqual(new int[]{}) && t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).All(a => Equals(a.Value, false)), "0x0200019d options/order/false");
            VerifyMethod(t, "0x06000b81", ".ctor", 0, "System.Void", "Public, HideBySig, SpecialName, RTSpecialName", new string[]{"System.Object", "System.IntPtr"}, new string[]{"object", "method"}, new string[]{"None", "None"}, new object[]{null, null});
            VerifyMethod(t, "0x06000b82", "Invoke", 0, "System.Void", "Public, Virtual, HideBySig, VtableLayoutMask", new string[]{"T1&", "T2&", "System.Action"}, new string[]{"arg1", "arg2", "completed"}, new string[]{"In", "In", "None"}, new object[]{null, null, null});
            VerifyMethod(t, "0x06000b83", "BeginInvoke", 0, "System.IAsyncResult", "Public, Virtual, HideBySig, VtableLayoutMask", new string[]{"T1&", "T2&", "System.Action", "System.AsyncCallback", "System.Object"}, new string[]{"arg1", "arg2", "completed", "callback", "object"}, new string[]{"In", "In", "None", "None", "None"}, new object[]{null, null, null, null, null});
            VerifyMethod(t, "0x06000b84", "EndInvoke", 0, "System.Void", "Public, Virtual, HideBySig, VtableLayoutMask", new string[]{"T1&", "T2&", "System.IAsyncResult"}, new string[]{"arg1", "arg2", "result"}, new string[]{"In", "In", "None"}, new object[]{null, null, null});
            t = asm.GetType("Hardlight.MessageCallbackWithCompletion`3", true);
            Check((int)t.Attributes == 257, "0x0200019e type attributes");
            Check(t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length + t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 4, "0x0200019e complete local API");
            fs = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(fs.Length == 0, "0x0200019e fields");
            opts = t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).Select(a => (int)a.Option).ToArray();
            Check(opts.SequenceEqual(new int[]{}) && t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).All(a => Equals(a.Value, false)), "0x0200019e options/order/false");
            VerifyMethod(t, "0x06000b85", ".ctor", 0, "System.Void", "Public, HideBySig, SpecialName, RTSpecialName", new string[]{"System.Object", "System.IntPtr"}, new string[]{"object", "method"}, new string[]{"None", "None"}, new object[]{null, null});
            VerifyMethod(t, "0x06000b86", "Invoke", 0, "System.Void", "Public, Virtual, HideBySig, VtableLayoutMask", new string[]{"T1&", "T2&", "T3&", "System.Action"}, new string[]{"arg1", "arg2", "arg3", "completed"}, new string[]{"In", "In", "In", "None"}, new object[]{null, null, null, null});
            VerifyMethod(t, "0x06000b87", "BeginInvoke", 0, "System.IAsyncResult", "Public, Virtual, HideBySig, VtableLayoutMask", new string[]{"T1&", "T2&", "T3&", "System.Action", "System.AsyncCallback", "System.Object"}, new string[]{"arg1", "arg2", "arg3", "completed", "callback", "object"}, new string[]{"In", "In", "In", "None", "None", "None"}, new object[]{null, null, null, null, null, null});
            VerifyMethod(t, "0x06000b88", "EndInvoke", 0, "System.Void", "Public, Virtual, HideBySig, VtableLayoutMask", new string[]{"T1&", "T2&", "T3&", "System.IAsyncResult"}, new string[]{"arg1", "arg2", "arg3", "result"}, new string[]{"In", "In", "In", "None"}, new object[]{null, null, null, null});
            t = asm.GetType("Hardlight.MessageCallbackWithCompletion`4", true);
            Check((int)t.Attributes == 257, "0x0200019f type attributes");
            Check(t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length + t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 4, "0x0200019f complete local API");
            fs = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(fs.Length == 0, "0x0200019f fields");
            opts = t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).Select(a => (int)a.Option).ToArray();
            Check(opts.SequenceEqual(new int[]{}) && t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).All(a => Equals(a.Value, false)), "0x0200019f options/order/false");
            VerifyMethod(t, "0x06000b89", ".ctor", 0, "System.Void", "Public, HideBySig, SpecialName, RTSpecialName", new string[]{"System.Object", "System.IntPtr"}, new string[]{"object", "method"}, new string[]{"None", "None"}, new object[]{null, null});
            VerifyMethod(t, "0x06000b8a", "Invoke", 0, "System.Void", "Public, Virtual, HideBySig, VtableLayoutMask", new string[]{"T1&", "T2&", "T3&", "T4&", "System.Action"}, new string[]{"arg1", "arg2", "arg3", "arg4", "completed"}, new string[]{"In", "In", "In", "In", "None"}, new object[]{null, null, null, null, null});
            VerifyMethod(t, "0x06000b8b", "BeginInvoke", 0, "System.IAsyncResult", "Public, Virtual, HideBySig, VtableLayoutMask", new string[]{"T1&", "T2&", "T3&", "T4&", "System.Action", "System.AsyncCallback", "System.Object"}, new string[]{"arg1", "arg2", "arg3", "arg4", "completed", "callback", "object"}, new string[]{"In", "In", "In", "In", "None", "None", "None"}, new object[]{null, null, null, null, null, null, null});
            VerifyMethod(t, "0x06000b8c", "EndInvoke", 0, "System.Void", "Public, Virtual, HideBySig, VtableLayoutMask", new string[]{"T1&", "T2&", "T3&", "T4&", "System.IAsyncResult"}, new string[]{"arg1", "arg2", "arg3", "arg4", "result"}, new string[]{"In", "In", "In", "In", "None"}, new object[]{null, null, null, null, null});
            t = asm.GetType("Hardlight.MessageCallbackWithCompletion`5", true);
            Check((int)t.Attributes == 257, "0x020001a0 type attributes");
            Check(t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length + t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 4, "0x020001a0 complete local API");
            fs = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(fs.Length == 0, "0x020001a0 fields");
            opts = t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).Select(a => (int)a.Option).ToArray();
            Check(opts.SequenceEqual(new int[]{}) && t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).All(a => Equals(a.Value, false)), "0x020001a0 options/order/false");
            VerifyMethod(t, "0x06000b8d", ".ctor", 0, "System.Void", "Public, HideBySig, SpecialName, RTSpecialName", new string[]{"System.Object", "System.IntPtr"}, new string[]{"object", "method"}, new string[]{"None", "None"}, new object[]{null, null});
            VerifyMethod(t, "0x06000b8e", "Invoke", 0, "System.Void", "Public, Virtual, HideBySig, VtableLayoutMask", new string[]{"T1&", "T2&", "T3&", "T4&", "T5&", "System.Action"}, new string[]{"arg1", "arg2", "arg3", "arg4", "arg5", "completed"}, new string[]{"In", "In", "In", "In", "In", "None"}, new object[]{null, null, null, null, null, null});
            VerifyMethod(t, "0x06000b8f", "BeginInvoke", 0, "System.IAsyncResult", "Public, Virtual, HideBySig, VtableLayoutMask", new string[]{"T1&", "T2&", "T3&", "T4&", "T5&", "System.Action", "System.AsyncCallback", "System.Object"}, new string[]{"arg1", "arg2", "arg3", "arg4", "arg5", "completed", "callback", "object"}, new string[]{"In", "In", "In", "In", "In", "None", "None", "None"}, new object[]{null, null, null, null, null, null, null, null});
            VerifyMethod(t, "0x06000b90", "EndInvoke", 0, "System.Void", "Public, Virtual, HideBySig, VtableLayoutMask", new string[]{"T1&", "T2&", "T3&", "T4&", "T5&", "System.IAsyncResult"}, new string[]{"arg1", "arg2", "arg3", "arg4", "arg5", "result"}, new string[]{"In", "In", "In", "In", "In", "None"}, new object[]{null, null, null, null, null, null});
            t = asm.GetType("Hardlight.MessageExchangeWithCompletion`1", true);
            Check((int)t.Attributes == 1048833, "0x020001a8 type attributes");
            Check(t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length + t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 26, "0x020001a8 complete local API");
            fs = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(fs.Length == 1, "0x020001a8 fields");
            Check(fs[0].Name == "m_includeDebugInformation" && Normalize(FormatType(fs[0].FieldType)) == Normalize("System.Boolean") && (int)fs[0].Attributes == 33, "0x04000610 ordered field/type/attributes");
            opts = t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).Select(a => (int)a.Option).ToArray();
            Check(opts.SequenceEqual(new int[]{1, 2}) && t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).All(a => Equals(a.Value, false)), "0x020001a8 options/order/false");
            VerifyMethod(t, "0x06000bcc", ".ctor", 0, "System.Void", "Public, HideBySig, SpecialName, RTSpecialName", new string[]{"System.Boolean"}, new string[]{"includeDebugInformation"}, new string[]{"Optional, HasDefault"}, new object[]{false});
            VerifyMethod(t, "0x06000bcd", "SubscribeToMessage", 0, "Hardlight.SubscribeHandle`1<Hardlight.MessageCallbackWithCompletion>", "Public, HideBySig", new string[]{"TMessage&", "Hardlight.MessageCallbackWithCompletion"}, new string[]{"message", "completionCallback"}, new string[]{"In", "None"}, new object[]{null, null});
            VerifyMethod(t, "0x06000bce", "SubscribeToMessage", 1, "Hardlight.SubscribeHandle`1<Hardlight.MessageCallbackWithCompletion`1<T>>", "Public, HideBySig", new string[]{"TMessage&", "Hardlight.MessageCallbackWithCompletion`1<T>"}, new string[]{"message", "completionCallback"}, new string[]{"In", "None"}, new object[]{null, null});
            VerifyMethod(t, "0x06000bcf", "SubscribeToMessage", 2, "Hardlight.SubscribeHandle`1<Hardlight.MessageCallbackWithCompletion`2<T1, T2>>", "Public, HideBySig", new string[]{"TMessage&", "Hardlight.MessageCallbackWithCompletion`2<T1, T2>"}, new string[]{"message", "completionCallback"}, new string[]{"In", "None"}, new object[]{null, null});
            VerifyMethod(t, "0x06000bd0", "SubscribeToMessage", 3, "Hardlight.SubscribeHandle`1<Hardlight.MessageCallbackWithCompletion`3<T1, T2, T3>>", "Public, HideBySig", new string[]{"TMessage&", "Hardlight.MessageCallbackWithCompletion`3<T1, T2, T3>"}, new string[]{"message", "completionCallback"}, new string[]{"In", "None"}, new object[]{null, null});
            VerifyMethod(t, "0x06000bd1", "SubscribeToMessage", 4, "Hardlight.SubscribeHandle`1<Hardlight.MessageCallbackWithCompletion`4<T1, T2, T3, T4>>", "Public, HideBySig", new string[]{"TMessage&", "Hardlight.MessageCallbackWithCompletion`4<T1, T2, T3, T4>"}, new string[]{"message", "completionCallback"}, new string[]{"In", "None"}, new object[]{null, null});
            VerifyMethod(t, "0x06000bd2", "SubscribeToMessage", 5, "Hardlight.SubscribeHandle`1<Hardlight.MessageCallbackWithCompletion`5<T1, T2, T3, T4, T5>>", "Public, HideBySig", new string[]{"TMessage&", "Hardlight.MessageCallbackWithCompletion`5<T1, T2, T3, T4, T5>"}, new string[]{"message", "completionCallback"}, new string[]{"In", "None"}, new object[]{null, null});
            VerifyMethod(t, "0x06000bd3", "PublishMessage", 0, "System.Void", "Public, HideBySig", new string[]{"TMessage&", "System.Action", "System.Int32"}, new string[]{"message", "completionCallback", "timeoutMs"}, new string[]{"In", "None", "Optional, HasDefault"}, new object[]{null, null, -1});
            VerifyMethod(t, "0x06000bd4", "PublishMessage", 1, "System.Void", "Public, HideBySig", new string[]{"TMessage&", "T&", "System.Action", "System.Int32"}, new string[]{"message", "obj", "completionCallback", "timeoutMs"}, new string[]{"In", "In", "None", "Optional, HasDefault"}, new object[]{null, null, null, -1});
            VerifyMethod(t, "0x06000bd5", "PublishMessage", 2, "System.Void", "Public, HideBySig", new string[]{"TMessage&", "T1&", "T2&", "System.Action", "System.Int32"}, new string[]{"message", "arg1", "arg2", "completionCallback", "timeoutMs"}, new string[]{"In", "In", "In", "None", "Optional, HasDefault"}, new object[]{null, null, null, null, -1});
            VerifyMethod(t, "0x06000bd6", "PublishMessage", 3, "System.Void", "Public, HideBySig", new string[]{"TMessage&", "T1&", "T2&", "T3&", "System.Action", "System.Int32"}, new string[]{"message", "arg1", "arg2", "arg3", "completionCallback", "timeoutMs"}, new string[]{"In", "In", "In", "In", "None", "Optional, HasDefault"}, new object[]{null, null, null, null, null, -1});
            VerifyMethod(t, "0x06000bd7", "PublishMessage", 4, "System.Void", "Public, HideBySig", new string[]{"TMessage&", "T1&", "T2&", "T3&", "T4&", "System.Action", "System.Int32"}, new string[]{"message", "arg1", "arg2", "arg3", "arg4", "completionCallback", "timeoutMs"}, new string[]{"In", "In", "In", "In", "In", "None", "Optional, HasDefault"}, new object[]{null, null, null, null, null, null, -1});
            VerifyMethod(t, "0x06000bd8", "PublishMessage", 5, "System.Void", "Public, HideBySig", new string[]{"TMessage&", "T1&", "T2&", "T3&", "T4&", "T5&", "System.Action", "System.Int32"}, new string[]{"message", "arg1", "arg2", "arg3", "arg4", "arg5", "completionCallback", "timeoutMs"}, new string[]{"In", "In", "In", "In", "In", "In", "None", "Optional, HasDefault"}, new object[]{null, null, null, null, null, null, null, -1});
            VerifyMethod(t, "0x06000bd9", "UnsubscribeFromMessage", 0, "System.Boolean", "Public, HideBySig", new string[]{"TMessage&", "Hardlight.MessageCallbackWithCompletion"}, new string[]{"message", "completionCallback"}, new string[]{"In", "None"}, new object[]{null, null});
            VerifyMethod(t, "0x06000bda", "UnsubscribeFromMessage", 1, "System.Boolean", "Public, HideBySig", new string[]{"TMessage&", "Hardlight.MessageCallbackWithCompletion`1<T>"}, new string[]{"message", "completionCallback"}, new string[]{"In", "None"}, new object[]{null, null});
            VerifyMethod(t, "0x06000bdb", "UnsubscribeFromMessage", 2, "System.Boolean", "Public, HideBySig", new string[]{"TMessage&", "Hardlight.MessageCallbackWithCompletion`2<T1, T2>"}, new string[]{"message", "completionCallback"}, new string[]{"In", "None"}, new object[]{null, null});
            VerifyMethod(t, "0x06000bdc", "UnsubscribeFromMessage", 3, "System.Boolean", "Public, HideBySig", new string[]{"TMessage&", "Hardlight.MessageCallbackWithCompletion`3<T1, T2, T3>"}, new string[]{"message", "completionCallback"}, new string[]{"In", "None"}, new object[]{null, null});
            VerifyMethod(t, "0x06000bdd", "UnsubscribeFromMessage", 4, "System.Boolean", "Public, HideBySig", new string[]{"TMessage&", "Hardlight.MessageCallbackWithCompletion`4<T1, T2, T3, T4>"}, new string[]{"message", "completionCallback"}, new string[]{"In", "None"}, new object[]{null, null});
            VerifyMethod(t, "0x06000bde", "UnsubscribeFromMessage", 5, "System.Boolean", "Public, HideBySig", new string[]{"TMessage&", "Hardlight.MessageCallbackWithCompletion`5<T1, T2, T3, T4, T5>"}, new string[]{"message", "completionCallback"}, new string[]{"In", "None"}, new object[]{null, null});
            VerifyMethod(t, "0x06000bdf", "GetExchangeHandle", 0, "Hardlight.ExchangeHandleWithCompletion", "Public, HideBySig", new string[]{"TMessage&"}, new string[]{"message"}, new string[]{"In"}, new object[]{null});
            VerifyMethod(t, "0x06000be0", "GetExchangeHandle", 1, "Hardlight.ExchangeHandleWithCompletion`1<T>", "Public, HideBySig", new string[]{"TMessage&"}, new string[]{"message"}, new string[]{"In"}, new object[]{null});
            VerifyMethod(t, "0x06000be1", "GetExchangeHandle", 2, "Hardlight.ExchangeHandleWithCompletion`2<T1, T2>", "Public, HideBySig", new string[]{"TMessage&"}, new string[]{"message"}, new string[]{"In"}, new object[]{null});
            VerifyMethod(t, "0x06000be2", "GetExchangeHandle", 3, "Hardlight.ExchangeHandleWithCompletion`3<T1, T2, T3>", "Public, HideBySig", new string[]{"TMessage&"}, new string[]{"message"}, new string[]{"In"}, new object[]{null});
            VerifyMethod(t, "0x06000be3", "GetExchangeHandle", 4, "Hardlight.ExchangeHandleWithCompletion`4<T1, T2, T3, T4>", "Public, HideBySig", new string[]{"TMessage&"}, new string[]{"message"}, new string[]{"In"}, new object[]{null});
            VerifyMethod(t, "0x06000be4", "GetExchangeHandle", 5, "Hardlight.ExchangeHandleWithCompletion`5<T1, T2, T3, T4, T5>", "Public, HideBySig", new string[]{"TMessage&"}, new string[]{"message"}, new string[]{"In"}, new object[]{null});
            VerifyMethod(t, "0x06000be5", "GetExchangeHandleInternal", 2, "TExchangeHandle", "Family, Virtual, HideBySig", new string[]{"TMessage&"}, new string[]{"message"}, new string[]{"In"}, new object[]{null});
            t = asm.GetType("Hardlight.ExchangeHandleWithCompletion", true);
            Check((int)t.Attributes == 1048577, "0x020001b2 type attributes");
            Check(t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length + t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 2, "0x020001b2 complete local API");
            fs = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(fs.Length == 0, "0x020001b2 fields");
            opts = t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).Select(a => (int)a.Option).ToArray();
            Check(opts.SequenceEqual(new int[]{2, 1}) && t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).All(a => Equals(a.Value, false)), "0x020001b2 options/order/false");
            VerifyMethod(t, "0x06000c01", "PublishMessage", 0, "System.Void", "Public, HideBySig", new string[]{"System.Action", "System.Int32"}, new string[]{"finishedCallback", "timeoutMs"}, new string[]{"None", "Optional, HasDefault"}, new object[]{null, -1});
            VerifyMethod(t, "0x06000c02", ".ctor", 0, "System.Void", "Public, HideBySig, SpecialName, RTSpecialName", new string[]{}, new string[]{}, new string[]{}, new object[]{});
            t = asm.GetType("Hardlight.ExchangeHandleWithCompletion`1", true);
            Check((int)t.Attributes == 1048577, "0x020001b3 type attributes");
            Check(t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length + t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 2, "0x020001b3 complete local API");
            fs = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(fs.Length == 0, "0x020001b3 fields");
            opts = t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).Select(a => (int)a.Option).ToArray();
            Check(opts.SequenceEqual(new int[]{2, 1}) && t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).All(a => Equals(a.Value, false)), "0x020001b3 options/order/false");
            VerifyMethod(t, "0x06000c03", "PublishMessage", 0, "System.Void", "Public, HideBySig", new string[]{"T&", "System.Action", "System.Int32"}, new string[]{"obj", "finishedCallback", "timeoutMs"}, new string[]{"In", "None", "Optional, HasDefault"}, new object[]{null, null, -1});
            VerifyMethod(t, "0x06000c04", ".ctor", 0, "System.Void", "Public, HideBySig, SpecialName, RTSpecialName", new string[]{}, new string[]{}, new string[]{}, new object[]{});
            t = asm.GetType("Hardlight.ExchangeHandleWithCompletion`2", true);
            Check((int)t.Attributes == 1048577, "0x020001b4 type attributes");
            Check(t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length + t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 2, "0x020001b4 complete local API");
            fs = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(fs.Length == 0, "0x020001b4 fields");
            opts = t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).Select(a => (int)a.Option).ToArray();
            Check(opts.SequenceEqual(new int[]{1, 2}) && t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).All(a => Equals(a.Value, false)), "0x020001b4 options/order/false");
            VerifyMethod(t, "0x06000c05", "PublishMessage", 0, "System.Void", "Public, HideBySig", new string[]{"T1&", "T2&", "System.Action", "System.Int32"}, new string[]{"arg1", "arg2", "finishedCallback", "timeoutMs"}, new string[]{"In", "In", "None", "Optional, HasDefault"}, new object[]{null, null, null, -1});
            VerifyMethod(t, "0x06000c06", ".ctor", 0, "System.Void", "Public, HideBySig, SpecialName, RTSpecialName", new string[]{}, new string[]{}, new string[]{}, new object[]{});
            t = asm.GetType("Hardlight.ExchangeHandleWithCompletion`3", true);
            Check((int)t.Attributes == 1048577, "0x020001b5 type attributes");
            Check(t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length + t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 2, "0x020001b5 complete local API");
            fs = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(fs.Length == 0, "0x020001b5 fields");
            opts = t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).Select(a => (int)a.Option).ToArray();
            Check(opts.SequenceEqual(new int[]{1, 2}) && t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).All(a => Equals(a.Value, false)), "0x020001b5 options/order/false");
            VerifyMethod(t, "0x06000c07", "PublishMessage", 0, "System.Void", "Public, HideBySig", new string[]{"T1&", "T2&", "T3&", "System.Action", "System.Int32"}, new string[]{"arg1", "arg2", "arg3", "finishedCallback", "timeoutMs"}, new string[]{"In", "In", "In", "None", "Optional, HasDefault"}, new object[]{null, null, null, null, -1});
            VerifyMethod(t, "0x06000c08", ".ctor", 0, "System.Void", "Public, HideBySig, SpecialName, RTSpecialName", new string[]{}, new string[]{}, new string[]{}, new object[]{});
            t = asm.GetType("Hardlight.ExchangeHandleWithCompletion`4", true);
            Check((int)t.Attributes == 1048577, "0x020001b6 type attributes");
            Check(t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length + t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 2, "0x020001b6 complete local API");
            fs = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(fs.Length == 0, "0x020001b6 fields");
            opts = t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).Select(a => (int)a.Option).ToArray();
            Check(opts.SequenceEqual(new int[]{2, 1}) && t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).All(a => Equals(a.Value, false)), "0x020001b6 options/order/false");
            VerifyMethod(t, "0x06000c09", "PublishMessage", 0, "System.Void", "Public, HideBySig", new string[]{"T1&", "T2&", "T3&", "T4&", "System.Action", "System.Int32"}, new string[]{"arg1", "arg2", "arg3", "arg4", "finishedCallback", "timeoutMs"}, new string[]{"In", "In", "In", "In", "None", "Optional, HasDefault"}, new object[]{null, null, null, null, null, -1});
            VerifyMethod(t, "0x06000c0a", ".ctor", 0, "System.Void", "Public, HideBySig, SpecialName, RTSpecialName", new string[]{}, new string[]{}, new string[]{}, new object[]{});
            t = asm.GetType("Hardlight.ExchangeHandleWithCompletion`5", true);
            Check((int)t.Attributes == 1048577, "0x020001b7 type attributes");
            Check(t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length + t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 2, "0x020001b7 complete local API");
            fs = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(fs.Length == 0, "0x020001b7 fields");
            opts = t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).Select(a => (int)a.Option).ToArray();
            Check(opts.SequenceEqual(new int[]{2, 1}) && t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).All(a => Equals(a.Value, false)), "0x020001b7 options/order/false");
            VerifyMethod(t, "0x06000c0b", "PublishMessage", 0, "System.Void", "Public, HideBySig", new string[]{"T1&", "T2&", "T3&", "T4&", "T5&", "System.Action", "System.Int32"}, new string[]{"arg1", "arg2", "arg3", "arg4", "arg5", "finishedCallback", "timeoutMs"}, new string[]{"In", "In", "In", "In", "In", "None", "Optional, HasDefault"}, new object[]{null, null, null, null, null, null, -1});
            VerifyMethod(t, "0x06000c0c", ".ctor", 0, "System.Void", "Public, HideBySig, SpecialName, RTSpecialName", new string[]{}, new string[]{}, new string[]{}, new object[]{});
            t = asm.GetType("Hardlight.ExchangeHandleWithCompletionBase`1+ICompletionData", true);
            Check((int)t.Attributes == 164, "0x020001b9 type attributes");
            Check(t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length + t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 2, "0x020001b9 complete local API");
            fs = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(fs.Length == 0, "0x020001b9 fields");
            opts = t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).Select(a => (int)a.Option).ToArray();
            Check(opts.SequenceEqual(new int[]{}) && t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).All(a => Equals(a.Value, false)), "0x020001b9 options/order/false");
            VerifyMethod(t, "0x06000c12", "RunTimeout", 0, "System.Void", "Public, Virtual, HideBySig, VtableLayoutMask, Abstract", new string[]{"System.Int32"}, new string[]{"timeoutMs"}, new string[]{"None"}, new object[]{null});
            VerifyMethod(t, "0x06000c13", "GetSubscriberCompletedCallback", 0, "System.Action", "Public, Virtual, HideBySig, VtableLayoutMask, Abstract", new string[]{"System.Int32"}, new string[]{"subscriberIndex"}, new string[]{"None"}, new object[]{null});
            t = asm.GetType("Hardlight.ExchangeHandleWithCompletionBase`1+CompletionData+CompletionDataDebug", true);
            Check((int)t.Attributes == 1048579, "0x020001bb type attributes");
            Check(t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length + t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 4, "0x020001bb complete local API");
            fs = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(fs.Length == 1, "0x020001bb fields");
            Check(fs[0].Name == "m_subscribers" && Normalize(FormatType(fs[0].FieldType)) == Normalize("System.Collections.Generic.List`1<System.Delegate>") && (int)fs[0].Attributes == 33, "0x04000620 ordered field/type/attributes");
            opts = t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).Select(a => (int)a.Option).ToArray();
            Check(opts.SequenceEqual(new int[]{1, 2}) && t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).All(a => Equals(a.Value, false)), "0x020001bb options/order/false");
            VerifyMethod(t, "0x06000c1c", "Setup", 0, "System.Void", "Public, HideBySig", new string[]{"System.Collections.Generic.IReadOnlyCollection`1<System.Delegate>"}, new string[]{"subscribers"}, new string[]{"None"}, new object[]{null});
            VerifyMethod(t, "0x06000c1d", "OnSubscriberFinished", 0, "System.Void", "Public, HideBySig", new string[]{"System.Int32"}, new string[]{"subscriberIndex"}, new string[]{"None"}, new object[]{null});
            VerifyMethod(t, "0x06000c1e", "LogTimedOutSubscribers", 0, "System.Void", "Public, HideBySig", new string[]{}, new string[]{}, new string[]{}, new object[]{});
            VerifyMethod(t, "0x06000c1f", ".ctor", 0, "System.Void", "Public, HideBySig, SpecialName, RTSpecialName", new string[]{}, new string[]{}, new string[]{}, new object[]{});
            t = asm.GetType("Hardlight.ExchangeHandleWithCompletionBase`1+CompletionData", true);
            Check((int)t.Attributes == 1048579, "0x020001ba type attributes");
            Check(t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length + t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 8, "0x020001ba complete local API");
            fs = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(fs.Length == 7, "0x020001ba fields");
            Check(fs[0].Name == "m_id" && Normalize(FormatType(fs[0].FieldType)) == Normalize("System.Int32") && (int)fs[0].Attributes == 1, "0x04000619 ordered field/type/attributes");
            Check(fs[1].Name == "m_subscriberCount" && Normalize(FormatType(fs[1].FieldType)) == Normalize("System.Int32") && (int)fs[1].Attributes == 1, "0x0400061a ordered field/type/attributes");
            Check(fs[2].Name == "m_subscriberCallback" && Normalize(FormatType(fs[2].FieldType)) == Normalize("System.Action") && (int)fs[2].Attributes == 1, "0x0400061b ordered field/type/attributes");
            Check(fs[3].Name == "m_finishedCallback" && Normalize(FormatType(fs[3].FieldType)) == Normalize("System.Action") && (int)fs[3].Attributes == 1, "0x0400061c ordered field/type/attributes");
            Check(fs[4].Name == "m_complete" && Normalize(FormatType(fs[4].FieldType)) == Normalize("System.Boolean") && (int)fs[4].Attributes == 1, "0x0400061d ordered field/type/attributes");
            Check(fs[5].Name == "m_finishedCount" && Normalize(FormatType(fs[5].FieldType)) == Normalize("System.Int32") && (int)fs[5].Attributes == 1, "0x0400061e ordered field/type/attributes");
            Check(fs[6].Name == "m_completionDataDebug" && Normalize(FormatType(fs[6].FieldType)) == Normalize("Hardlight.ExchangeHandleWithCompletionBase`1+CompletionData+CompletionDataDebug<!0>") && (int)fs[6].Attributes == 33, "0x0400061f ordered field/type/attributes");
            opts = t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).Select(a => (int)a.Option).ToArray();
            Check(opts.SequenceEqual(new int[]{1, 2}) && t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).All(a => Equals(a.Value, false)), "0x020001ba options/order/false");
            VerifyMethod(t, "0x06000c14", ".ctor", 0, "System.Void", "Public, HideBySig, SpecialName, RTSpecialName", new string[]{"System.Boolean"}, new string[]{"includeDebugInformation"}, new string[]{"None"}, new object[]{null});
            VerifyMethod(t, "0x06000c15", "SetData", 0, "System.Void", "Public, HideBySig", new string[]{"System.Int32", "System.Collections.Generic.IReadOnlyCollection`1<System.Delegate>", "System.Action"}, new string[]{"id", "subscribers", "finishedCallback"}, new string[]{"None", "None", "None"}, new object[]{null, null, null});
            VerifyMethod(t, "0x06000c16", "Invalidate", 0, "System.Void", "Public, HideBySig", new string[]{}, new string[]{}, new string[]{}, new object[]{});
            VerifyMethod(t, "0x06000c17", "RunTimeout", 0, "System.Void", "Public, Final, Virtual, HideBySig, VtableLayoutMask", new string[]{"System.Int32"}, new string[]{"timeoutMs"}, new string[]{"None"}, new object[]{null});
            VerifyMethod(t, "0x06000c18", "GetSubscriberCompletedCallback", 0, "System.Action", "Public, Final, Virtual, HideBySig, VtableLayoutMask", new string[]{"System.Int32"}, new string[]{"subscriberIndex"}, new string[]{"None"}, new object[]{null});
            VerifyMethod(t, "0x06000c19", "OnSubscriberFinished", 0, "System.Void", "Private, HideBySig", new string[]{"System.Int32", "System.Int32"}, new string[]{"id", "subscriberIndex"}, new string[]{"None", "None"}, new object[]{null, null});
            VerifyMethod(t, "0x06000c1a", "Timeout", 0, "System.Threading.Tasks.Task", "Private, HideBySig", new string[]{"System.Int32"}, new string[]{"timeoutMs"}, new string[]{"None"}, new object[]{null});
            VerifyMethod(t, "0x06000c1b", "IsComplete", 0, "System.Boolean", "Private, HideBySig", new string[]{"System.Int32"}, new string[]{"id"}, new string[]{"None"}, new object[]{null});
            t = asm.GetType("Hardlight.ExchangeHandleWithCompletionBase`1", true);
            Check((int)t.Attributes == 1048705, "0x020001b8 type attributes");
            Check(t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length + t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 5, "0x020001b8 complete local API");
            fs = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(fs.Length == 4, "0x020001b8 fields");
            Check(fs[0].Name == "m_nextMessageID" && Normalize(FormatType(fs[0].FieldType)) == Normalize("System.Int32") && (int)fs[0].Attributes == 1, "0x04000615 ordered field/type/attributes");
            Check(fs[1].Name == "m_completionDataPool" && Normalize(FormatType(fs[1].FieldType)) == Normalize("System.Collections.Generic.Stack`1<Hardlight.ExchangeHandleWithCompletionBase`1+CompletionData<!0>>") && (int)fs[1].Attributes == 33, "0x04000616 ordered field/type/attributes");
            Check(fs[2].Name == "m_completionDataLookup" && Normalize(FormatType(fs[2].FieldType)) == Normalize("System.Collections.Generic.Dictionary`2<System.Int32,Hardlight.ExchangeHandleWithCompletionBase`1+CompletionData<!0>>") && (int)fs[2].Attributes == 33, "0x04000617 ordered field/type/attributes");
            Check(fs[3].Name == "m_includeDebugInformation" && Normalize(FormatType(fs[3].FieldType)) == Normalize("System.Boolean") && (int)fs[3].Attributes == 1, "0x04000618 ordered field/type/attributes");
            opts = t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).Select(a => (int)a.Option).ToArray();
            Check(opts.SequenceEqual(new int[]{1, 2}) && t.GetCustomAttributes<Il2CppSetOptionAttribute>(false).All(a => Equals(a.Value, false)), "0x020001b8 options/order/false");
            VerifyMethod(t, "0x06000c0d", "EnableDebugInformation", 0, "System.Void", "Public, HideBySig", new string[]{"System.Boolean"}, new string[]{"enabled"}, new string[]{"None"}, new object[]{null});
            VerifyMethod(t, "0x06000c0e", "Invalidate", 0, "System.Void", "Public, Virtual, HideBySig", new string[]{}, new string[]{}, new string[]{}, new object[]{});
            VerifyMethod(t, "0x06000c0f", "TryGetCompletionData", 0, "System.Boolean", "Family, HideBySig", new string[]{"System.Action", "Hardlight.ExchangeHandleWithCompletionBase+ICompletionData<TDelegate>&"}, new string[]{"finishedCallback", "completionData"}, new string[]{"None", "Out"}, new object[]{null, null});
            VerifyMethod(t, "0x06000c10", "GetCompletionData", 0, "Hardlight.ExchangeHandleWithCompletionBase`1<TDelegate>+CompletionData<TDelegate>", "Private, HideBySig", new string[]{}, new string[]{}, new string[]{}, new object[]{});
            VerifyMethod(t, "0x06000c11", ".ctor", 0, "System.Void", "Family, HideBySig, SpecialName, RTSpecialName", new string[]{}, new string[]{}, new string[]{}, new object[]{});
            return checks;
        }
    }
}
