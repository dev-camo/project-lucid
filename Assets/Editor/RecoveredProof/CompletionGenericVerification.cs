using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;

namespace ProjectLucid.Editor
{
    internal sealed class CompletionGenericVerification
    {
        private int n;
        private void Check(bool x, string m)
        {
            n++;
            if (!x)
                throw new Exception(m);
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

        private void VerifyParameter(Type t, string name, int attrs, string[] cs, string token)
        {
            Check(t.Name == name, token + " generic name");
            Check((int)t.GenericParameterAttributes == attrs, token + " generic attributes");
            Check(t.GetGenericParameterConstraints().Select(FormatType).SequenceEqual(cs.Select(Normalize)), token + " real generic constraints");
        }

        public int Run()
        {
            var asm = typeof(MessageExchangeWithCompletion<>).Assembly;
            Type t;
            MethodInfo m;
            t = asm.GetType("Hardlight.MessageCallbackWithCompletion", true);
            Check(t.GetGenericArguments().Length == 0, "0x0200019b type generic count");
            Check(Normalize(FormatType(t.BaseType)) == Normalize("System.MulticastDelegate"), "0x0200019b genuine base");
            t = asm.GetType("Hardlight.MessageCallbackWithCompletion`1", true);
            Check(t.GetGenericArguments().Length == 1, "0x0200019c type generic count");
            VerifyParameter(t.GetGenericArguments()[0], "T", 0, new string[]{}, "0x0200019c");
            Check(Normalize(FormatType(t.BaseType)) == Normalize("System.MulticastDelegate"), "0x0200019c genuine base");
            t = asm.GetType("Hardlight.MessageCallbackWithCompletion`2", true);
            Check(t.GetGenericArguments().Length == 2, "0x0200019d type generic count");
            VerifyParameter(t.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x0200019d");
            VerifyParameter(t.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x0200019d");
            Check(Normalize(FormatType(t.BaseType)) == Normalize("System.MulticastDelegate"), "0x0200019d genuine base");
            t = asm.GetType("Hardlight.MessageCallbackWithCompletion`3", true);
            Check(t.GetGenericArguments().Length == 3, "0x0200019e type generic count");
            VerifyParameter(t.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x0200019e");
            VerifyParameter(t.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x0200019e");
            VerifyParameter(t.GetGenericArguments()[2], "T3", 0, new string[]{}, "0x0200019e");
            Check(Normalize(FormatType(t.BaseType)) == Normalize("System.MulticastDelegate"), "0x0200019e genuine base");
            t = asm.GetType("Hardlight.MessageCallbackWithCompletion`4", true);
            Check(t.GetGenericArguments().Length == 4, "0x0200019f type generic count");
            VerifyParameter(t.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x0200019f");
            VerifyParameter(t.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x0200019f");
            VerifyParameter(t.GetGenericArguments()[2], "T3", 0, new string[]{}, "0x0200019f");
            VerifyParameter(t.GetGenericArguments()[3], "T4", 0, new string[]{}, "0x0200019f");
            Check(Normalize(FormatType(t.BaseType)) == Normalize("System.MulticastDelegate"), "0x0200019f genuine base");
            t = asm.GetType("Hardlight.MessageCallbackWithCompletion`5", true);
            Check(t.GetGenericArguments().Length == 5, "0x020001a0 type generic count");
            VerifyParameter(t.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x020001a0");
            VerifyParameter(t.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x020001a0");
            VerifyParameter(t.GetGenericArguments()[2], "T3", 0, new string[]{}, "0x020001a0");
            VerifyParameter(t.GetGenericArguments()[3], "T4", 0, new string[]{}, "0x020001a0");
            VerifyParameter(t.GetGenericArguments()[4], "T5", 0, new string[]{}, "0x020001a0");
            Check(Normalize(FormatType(t.BaseType)) == Normalize("System.MulticastDelegate"), "0x020001a0 genuine base");
            t = asm.GetType("Hardlight.MessageExchangeWithCompletion`1", true);
            Check(t.GetGenericArguments().Length == 1, "0x020001a8 type generic count");
            VerifyParameter(t.GetGenericArguments()[0], "TMessage", 0, new string[]{}, "0x020001a8");
            Check(Normalize(FormatType(t.BaseType)) == Normalize("Hardlight.MessageExchangeBase`1<TMessage>"), "0x020001a8 genuine base");
            m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.Name == "SubscribeToMessage" && x.GetGenericArguments().Length == 1);
            VerifyParameter(m.GetGenericArguments()[0], "T", 0, new string[]{}, "0x06000bce");
            m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.Name == "SubscribeToMessage" && x.GetGenericArguments().Length == 2);
            VerifyParameter(m.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x06000bcf");
            VerifyParameter(m.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x06000bcf");
            m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.Name == "SubscribeToMessage" && x.GetGenericArguments().Length == 3);
            VerifyParameter(m.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x06000bd0");
            VerifyParameter(m.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x06000bd0");
            VerifyParameter(m.GetGenericArguments()[2], "T3", 0, new string[]{}, "0x06000bd0");
            m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.Name == "SubscribeToMessage" && x.GetGenericArguments().Length == 4);
            VerifyParameter(m.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x06000bd1");
            VerifyParameter(m.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x06000bd1");
            VerifyParameter(m.GetGenericArguments()[2], "T3", 0, new string[]{}, "0x06000bd1");
            VerifyParameter(m.GetGenericArguments()[3], "T4", 0, new string[]{}, "0x06000bd1");
            m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.Name == "SubscribeToMessage" && x.GetGenericArguments().Length == 5);
            VerifyParameter(m.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x06000bd2");
            VerifyParameter(m.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x06000bd2");
            VerifyParameter(m.GetGenericArguments()[2], "T3", 0, new string[]{}, "0x06000bd2");
            VerifyParameter(m.GetGenericArguments()[3], "T4", 0, new string[]{}, "0x06000bd2");
            VerifyParameter(m.GetGenericArguments()[4], "T5", 0, new string[]{}, "0x06000bd2");
            m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.Name == "PublishMessage" && x.GetGenericArguments().Length == 1);
            VerifyParameter(m.GetGenericArguments()[0], "T", 0, new string[]{}, "0x06000bd4");
            m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.Name == "PublishMessage" && x.GetGenericArguments().Length == 2);
            VerifyParameter(m.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x06000bd5");
            VerifyParameter(m.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x06000bd5");
            m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.Name == "PublishMessage" && x.GetGenericArguments().Length == 3);
            VerifyParameter(m.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x06000bd6");
            VerifyParameter(m.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x06000bd6");
            VerifyParameter(m.GetGenericArguments()[2], "T3", 0, new string[]{}, "0x06000bd6");
            m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.Name == "PublishMessage" && x.GetGenericArguments().Length == 4);
            VerifyParameter(m.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x06000bd7");
            VerifyParameter(m.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x06000bd7");
            VerifyParameter(m.GetGenericArguments()[2], "T3", 0, new string[]{}, "0x06000bd7");
            VerifyParameter(m.GetGenericArguments()[3], "T4", 0, new string[]{}, "0x06000bd7");
            m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.Name == "PublishMessage" && x.GetGenericArguments().Length == 5);
            VerifyParameter(m.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x06000bd8");
            VerifyParameter(m.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x06000bd8");
            VerifyParameter(m.GetGenericArguments()[2], "T3", 0, new string[]{}, "0x06000bd8");
            VerifyParameter(m.GetGenericArguments()[3], "T4", 0, new string[]{}, "0x06000bd8");
            VerifyParameter(m.GetGenericArguments()[4], "T5", 0, new string[]{}, "0x06000bd8");
            m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.Name == "UnsubscribeFromMessage" && x.GetGenericArguments().Length == 1);
            VerifyParameter(m.GetGenericArguments()[0], "T", 0, new string[]{}, "0x06000bda");
            m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.Name == "UnsubscribeFromMessage" && x.GetGenericArguments().Length == 2);
            VerifyParameter(m.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x06000bdb");
            VerifyParameter(m.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x06000bdb");
            m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.Name == "UnsubscribeFromMessage" && x.GetGenericArguments().Length == 3);
            VerifyParameter(m.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x06000bdc");
            VerifyParameter(m.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x06000bdc");
            VerifyParameter(m.GetGenericArguments()[2], "T3", 0, new string[]{}, "0x06000bdc");
            m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.Name == "UnsubscribeFromMessage" && x.GetGenericArguments().Length == 4);
            VerifyParameter(m.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x06000bdd");
            VerifyParameter(m.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x06000bdd");
            VerifyParameter(m.GetGenericArguments()[2], "T3", 0, new string[]{}, "0x06000bdd");
            VerifyParameter(m.GetGenericArguments()[3], "T4", 0, new string[]{}, "0x06000bdd");
            m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.Name == "UnsubscribeFromMessage" && x.GetGenericArguments().Length == 5);
            VerifyParameter(m.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x06000bde");
            VerifyParameter(m.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x06000bde");
            VerifyParameter(m.GetGenericArguments()[2], "T3", 0, new string[]{}, "0x06000bde");
            VerifyParameter(m.GetGenericArguments()[3], "T4", 0, new string[]{}, "0x06000bde");
            VerifyParameter(m.GetGenericArguments()[4], "T5", 0, new string[]{}, "0x06000bde");
            m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.Name == "GetExchangeHandle" && x.GetGenericArguments().Length == 1);
            VerifyParameter(m.GetGenericArguments()[0], "T", 0, new string[]{}, "0x06000be0");
            m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.Name == "GetExchangeHandle" && x.GetGenericArguments().Length == 2);
            VerifyParameter(m.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x06000be1");
            VerifyParameter(m.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x06000be1");
            m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.Name == "GetExchangeHandle" && x.GetGenericArguments().Length == 3);
            VerifyParameter(m.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x06000be2");
            VerifyParameter(m.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x06000be2");
            VerifyParameter(m.GetGenericArguments()[2], "T3", 0, new string[]{}, "0x06000be2");
            m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.Name == "GetExchangeHandle" && x.GetGenericArguments().Length == 4);
            VerifyParameter(m.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x06000be3");
            VerifyParameter(m.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x06000be3");
            VerifyParameter(m.GetGenericArguments()[2], "T3", 0, new string[]{}, "0x06000be3");
            VerifyParameter(m.GetGenericArguments()[3], "T4", 0, new string[]{}, "0x06000be3");
            m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.Name == "GetExchangeHandle" && x.GetGenericArguments().Length == 5);
            VerifyParameter(m.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x06000be4");
            VerifyParameter(m.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x06000be4");
            VerifyParameter(m.GetGenericArguments()[2], "T3", 0, new string[]{}, "0x06000be4");
            VerifyParameter(m.GetGenericArguments()[3], "T4", 0, new string[]{}, "0x06000be4");
            VerifyParameter(m.GetGenericArguments()[4], "T5", 0, new string[]{}, "0x06000be4");
            m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single(x => x.Name == "GetExchangeHandleInternal" && x.GetGenericArguments().Length == 2);
            VerifyParameter(m.GetGenericArguments()[0], "TCallback", 0, new string[]{"System.Delegate"}, "0x06000be5");
            VerifyParameter(m.GetGenericArguments()[1], "TExchangeHandle", 20, new string[]{"Hardlight.IExchangeHandle"}, "0x06000be5");
            t = asm.GetType("Hardlight.ExchangeHandleWithCompletion", true);
            Check(t.GetGenericArguments().Length == 0, "0x020001b2 type generic count");
            Check(Normalize(FormatType(t.BaseType)) == Normalize("Hardlight.ExchangeHandleWithCompletionBase`1<Hardlight.MessageCallbackWithCompletion>"), "0x020001b2 genuine base");
            t = asm.GetType("Hardlight.ExchangeHandleWithCompletion`1", true);
            Check(t.GetGenericArguments().Length == 1, "0x020001b3 type generic count");
            VerifyParameter(t.GetGenericArguments()[0], "T", 0, new string[]{}, "0x020001b3");
            Check(Normalize(FormatType(t.BaseType)) == Normalize("Hardlight.ExchangeHandleWithCompletionBase`1<Hardlight.MessageCallbackWithCompletion`1<T>>"), "0x020001b3 genuine base");
            t = asm.GetType("Hardlight.ExchangeHandleWithCompletion`2", true);
            Check(t.GetGenericArguments().Length == 2, "0x020001b4 type generic count");
            VerifyParameter(t.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x020001b4");
            VerifyParameter(t.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x020001b4");
            Check(Normalize(FormatType(t.BaseType)) == Normalize("Hardlight.ExchangeHandleWithCompletionBase`1<Hardlight.MessageCallbackWithCompletion`2<T1,T2>>"), "0x020001b4 genuine base");
            t = asm.GetType("Hardlight.ExchangeHandleWithCompletion`3", true);
            Check(t.GetGenericArguments().Length == 3, "0x020001b5 type generic count");
            VerifyParameter(t.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x020001b5");
            VerifyParameter(t.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x020001b5");
            VerifyParameter(t.GetGenericArguments()[2], "T3", 0, new string[]{}, "0x020001b5");
            Check(Normalize(FormatType(t.BaseType)) == Normalize("Hardlight.ExchangeHandleWithCompletionBase`1<Hardlight.MessageCallbackWithCompletion`3<T1,T2,T3>>"), "0x020001b5 genuine base");
            t = asm.GetType("Hardlight.ExchangeHandleWithCompletion`4", true);
            Check(t.GetGenericArguments().Length == 4, "0x020001b6 type generic count");
            VerifyParameter(t.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x020001b6");
            VerifyParameter(t.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x020001b6");
            VerifyParameter(t.GetGenericArguments()[2], "T3", 0, new string[]{}, "0x020001b6");
            VerifyParameter(t.GetGenericArguments()[3], "T4", 0, new string[]{}, "0x020001b6");
            Check(Normalize(FormatType(t.BaseType)) == Normalize("Hardlight.ExchangeHandleWithCompletionBase`1<Hardlight.MessageCallbackWithCompletion`4<T1,T2,T3,T4>>"), "0x020001b6 genuine base");
            t = asm.GetType("Hardlight.ExchangeHandleWithCompletion`5", true);
            Check(t.GetGenericArguments().Length == 5, "0x020001b7 type generic count");
            VerifyParameter(t.GetGenericArguments()[0], "T1", 0, new string[]{}, "0x020001b7");
            VerifyParameter(t.GetGenericArguments()[1], "T2", 0, new string[]{}, "0x020001b7");
            VerifyParameter(t.GetGenericArguments()[2], "T3", 0, new string[]{}, "0x020001b7");
            VerifyParameter(t.GetGenericArguments()[3], "T4", 0, new string[]{}, "0x020001b7");
            VerifyParameter(t.GetGenericArguments()[4], "T5", 0, new string[]{}, "0x020001b7");
            Check(Normalize(FormatType(t.BaseType)) == Normalize("Hardlight.ExchangeHandleWithCompletionBase`1<Hardlight.MessageCallbackWithCompletion`5<T1,T2,T3,T4,T5>>"), "0x020001b7 genuine base");
            t = asm.GetType("Hardlight.ExchangeHandleWithCompletionBase`1+ICompletionData", true);
            Check(t.GetGenericArguments().Length == 1, "0x020001b9 type generic count");
            VerifyParameter(t.GetGenericArguments()[0], "TDelegate", 0, new string[]{"System.Delegate"}, "0x020001b9");
            t = asm.GetType("Hardlight.ExchangeHandleWithCompletionBase`1+CompletionData+CompletionDataDebug", true);
            Check(t.GetGenericArguments().Length == 1, "0x020001bb type generic count");
            VerifyParameter(t.GetGenericArguments()[0], "TDelegate", 0, new string[]{"System.Delegate"}, "0x020001bb");
            Check(Normalize(FormatType(t.BaseType)) == Normalize("System.Object"), "0x020001bb genuine base");
            t = asm.GetType("Hardlight.ExchangeHandleWithCompletionBase`1+CompletionData", true);
            Check(t.GetGenericArguments().Length == 1, "0x020001ba type generic count");
            VerifyParameter(t.GetGenericArguments()[0], "TDelegate", 0, new string[]{"System.Delegate"}, "0x020001ba");
            Check(Normalize(FormatType(t.BaseType)) == Normalize("System.Object"), "0x020001ba genuine base");
            t = asm.GetType("Hardlight.ExchangeHandleWithCompletionBase`1", true);
            Check(t.GetGenericArguments().Length == 1, "0x020001b8 type generic count");
            VerifyParameter(t.GetGenericArguments()[0], "TDelegate", 0, new string[]{"System.Delegate"}, "0x020001b8");
            Check(Normalize(FormatType(t.BaseType)) == Normalize("Hardlight.ExchangeHandleBase`1<TDelegate>"), "0x020001b8 genuine base");
            return n;
        }
    }
}
