#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{
    // Source-based expectations from JSONClassFactory tokens 0x06000935-39,
    // JSONFactoryClass 0x0600093b and ScopedStringBuilder 0x06000a97-9b.
    // This verifies factory behavior, not authored-game startup or gameplay.
    public static class JSONClassFactoryVerification
    {
        public static void Run()
        {
            VerifyScope();
            VerifyDiscovery();
            VerifyInvocation();
            VerifyGenericCache();
            Debug.Log("Original JSON factory reflection and construction source-based verification passed.");
        }

        private static void VerifyScope()
        {
            var empty = default(ScopedStringBuilder);
            Check(empty.Length == 0, "default scope length");
            empty.Append("ignored");
            empty.AppendLine();
            empty.Dispose();
            Check(HLOutput.GetLogScopedStringBuilder().Length == 0, "retail log scope default return");

            var builder = new StringBuilder("old content");
            int disposals = 0;
            var scope = new ScopedStringBuilder(builder, value =>
            {
                Check(ReferenceEquals(value, builder), "dispose passes same builder");
                ++disposals;
            });
            Check(builder.Length == 0 && scope.Length == 0, "constructor clears builder");
            scope.Append("one");
            scope.Append(null);
            scope.AppendLine("two");
            scope.AppendLine();
            Check(builder.ToString() == "onetwo" + Environment.NewLine + Environment.NewLine, "append and null append-line semantics");
            Check(scope.Length == builder.Length, "scope length forwards to builder");
            scope.Dispose();
            scope.Dispose();
            Check(disposals == 2 && builder.Length != 0, "disposal repeats without clearing");
            int nullDisposals = 0;
            var nullBuilder = new ScopedStringBuilder(null, value => { Check(value == null, "callback accepts null builder"); ++nullDisposals; });
            nullBuilder.Dispose();
            Check(nullDisposals == 1, "null builder still invokes callback");
            var throwing = new ScopedStringBuilder(null, value => { throw new InvalidOperationException("scope failure"); });
            Check(Throws<InvalidOperationException>(() => throwing.Dispose()).Message == "scope failure", "dispose callback exception propagates");

            var usage = typeof(JSONCtorArgsAttribute).GetCustomAttribute<AttributeUsageAttribute>();
            Check(usage.ValidOn == AttributeTargets.Class && usage.Inherited && !usage.AllowMultiple, "original constructor-args attribute usage");
            var method = typeof(SingleFactory).GetMethod("ConstructInstance");
            var record = new JSONFactoryClass(typeof(SingleFactory), method, typeof(SingleFactory).GetNestedType("JSONCtorArgs", BindingFlags.NonPublic));
            Check(record.ClassType == typeof(SingleFactory) && ReferenceEquals(record.ConstructInstanceMethod, method) && record.JSONCtorArgsType != null, "factory record keeps supplied references");
            record = new JSONFactoryClass(null, null, null);
            Check(record.ClassType == null && record.ConstructInstanceMethod == null && record.JSONCtorArgsType == null, "factory record accepts null references");
        }

        private static void VerifyDiscovery()
        {
            IDictionary<string, JSONFactoryClass> lookup = new Dictionary<string, JSONFactoryClass>();
            var builder = new StringBuilder();
            var scope = new ScopedStringBuilder(builder, null);
            Check(!lookup.TryCacheFactoryType<FactoryProduct>(typeof(Unrelated), scope), "unassignable type skipped");
            Check(!lookup.TryCacheFactoryType<FactoryProduct>(typeof(AbstractFactory), scope), "abstract type skipped");
            Check(!lookup.TryCacheFactoryType<FactoryProduct>(typeof(InstanceFactory), scope), "instance method skipped");
            Check(!lookup.TryCacheFactoryType<FactoryProduct>(typeof(NoFactory), scope), "missing method skipped");
            Check(!lookup.TryCacheFactoryType<FactoryProduct>(typeof(InheritedOnlyFactory), scope), "inherited static method excluded by default reflection flags");
            Check(lookup.Count == 0 && builder.Length == 0, "skipped candidates leave lookup and log unchanged");
            Check(lookup.TryCacheFactoryType<FactoryProduct>(typeof(SingleFactory), scope), "static factory cached");
            Check(lookup.ContainsKey(nameof(SingleFactory)) && !lookup.ContainsKey(typeof(SingleFactory).FullName), "cache uses short name");
            Check(builder.ToString() == "Found FactoryProduct class: " + typeof(SingleFactory).FullName + Environment.NewLine, "native discovery log text");
            Check(lookup[nameof(SingleFactory)].JSONCtorArgsType == typeof(SingleFactory).GetNestedType("JSONCtorArgs", BindingFlags.NonPublic), "private named args selected");
            Check(lookup.TryCacheFactoryType<FactoryProduct>(typeof(DerivedFactory), default), "derived public static method cached");
            Check(lookup[nameof(DerivedFactory)].JSONCtorArgsType == lookup[nameof(SingleFactory)].JSONCtorArgsType, "base-type args search");
            Check(lookup.TryCacheFactoryType<FactoryProduct>(typeof(AttributedFactory), default), "attributed args factory cached");
            Check(lookup[nameof(AttributedFactory)].JSONCtorArgsType == typeof(AttributedFactory.ArgumentData), "attribute selects differently named args");
            Check(lookup.TryCacheFactoryType<FactoryProduct>(typeof(PairFactory), default) && lookup[nameof(PairFactory)].JSONCtorArgsType == null, "factory without args metadata accepted");
            Check(lookup.TryCacheFactoryType<FactoryProduct>(typeof(FirstContainer.Collision), default), "first colliding short name cached");
            Check(lookup.TryCacheFactoryType<FactoryProduct>(typeof(SecondContainer.Collision), default), "second colliding short name cached");
            Check(lookup[nameof(FirstContainer.Collision)].ClassType == typeof(SecondContainer.Collision), "collision replaces earlier entry");
            Throws<AmbiguousMatchException>(() => lookup.TryCacheFactoryType<FactoryProduct>(typeof(AmbiguousFactory), default));
            Check(!lookup.ContainsKey(nameof(AmbiguousFactory)), "ambiguous reflection error does not create entry");
        }

        private static void VerifyInvocation()
        {
            IDictionary<string, JSONFactoryClass> lookup = new Dictionary<string, JSONFactoryClass>();
            foreach (Type type in new[] { typeof(SingleFactory), typeof(PairFactory), typeof(DerivedFactory), typeof(WrongReturnFactory), typeof(ThrowingFactory) })
                Check(lookup.TryCacheFactoryType<FactoryProduct>(type, default), "invocation candidate cached");
            var first = new object();
            var second = new object();
            const string rawJson = "unparsed constructor text {";
            var single = lookup.ConstructInstance<FactoryProduct>(nameof(SingleFactory), first, rawJson, new Type[] { null });
            Check(ReferenceEquals(single.First, first) && single.Second == null && single.JSON == rawJson, "one-object wrapper and raw JSON; nongeneric ignores generic arguments");
            var pair = lookup.ConstructInstance<FactoryProduct>(nameof(PairFactory), first, second, rawJson);
            Check(ReferenceEquals(pair.First, first) && ReferenceEquals(pair.Second, second) && pair.JSON == rawJson, "two-object wrapper parameter order");
            Check(lookup.ConstructInstance<FactoryProduct>(nameof(DerivedFactory), first, rawJson).GetType() == typeof(SingleFactory), "factory returning base class is invoked unchanged");
            Check(lookup.ConstructInstance<FactoryProduct>(nameof(WrongReturnFactory), first, rawJson) == null, "incompatible factory return becomes null");
            var invocationFailure = Throws<TargetInvocationException>(() => lookup.ConstructInstance<FactoryProduct>(nameof(ThrowingFactory), first, rawJson));
            Check(invocationFailure.InnerException is InvalidOperationException && invocationFailure.InnerException.Message == rawJson, "factory exception retains reflection wrapper");
            var missing = Throws<Exception>(() => lookup.ConstructInstance<FactoryProduct>("missing", first, rawJson));
            Check(missing.GetType() == typeof(Exception) && missing.Message == "Failed to construct with class name 'missing' as no class with that name is registered in the class-factory.", "original missing-factory exception");
        }

        private static void VerifyGenericCache()
        {
            IDictionary<string, JSONFactoryClass> lookup = new Dictionary<string, JSONFactoryClass>();
            Type openType = typeof(GenericFactory<>);
            string name = openType.Name;
            Check(lookup.TryCacheFactoryType<FactoryProduct>(openType, default), "open generic factory cached");
            string prefix = "Failed to construct generic with class name '" + name;
            foreach (Type[] arguments in new[] { (Type[])null, Array.Empty<Type>() })
                Check(Throws<Exception>(() => lookup.ConstructInstance<FactoryProduct>(name, null, "json", arguments)).Message == prefix + "' as no generic arguments have been provided.", "missing generic arguments");
            Check(Throws<Exception>(() => lookup.ConstructInstance<FactoryProduct>(name, null, "json", new Type[] { null })).Message == prefix + "' as one or more generic arguments are null.", "null generic argument");
            Check(lookup.Count == 1, "generic validation failures do not create cache entries");
            Throws<ArgumentException>(() => lookup.ConstructInstance<FactoryProduct>(name, null, "json", new[] { typeof(int), typeof(string) }));
            Check(lookup.Count == 1, "generic arity failure remains MakeGenericType failure");
            var argument = new object();
            var result = lookup.ConstructInstance<FactoryProduct>(name, argument, "raw", new[] { typeof(int) });
            string constructedName = typeof(GenericFactory<int>).ToString();
            Check(result.GenericType == typeof(int) && ReferenceEquals(result.First, argument) && result.JSON == "raw", "closed generic invocation");
            Check(lookup.Count == 2 && lookup[constructedName].ClassType == typeof(GenericFactory<int>), "closed cache uses Type.ToString");
            Check(ReferenceEquals(lookup[constructedName].JSONCtorArgsType, lookup[name].JSONCtorArgsType) && lookup[constructedName].JSONCtorArgsType.ContainsGenericParameters, "closed record retains open args metadata");
            var cached = lookup[constructedName];
            lookup.ConstructInstance<FactoryProduct>(name, null, "again", new[] { typeof(int) });
            Check(lookup.Count == 2 && ReferenceEquals(cached, lookup[constructedName]), "closed cache entry reused");
            lookup.ConstructInstance<FactoryProduct>(name, null, "other", new[] { typeof(string) });
            Check(lookup.Count == 3 && lookup.ContainsKey(typeof(GenericFactory<string>).ToString()), "distinct generic closure gets distinct cache entry");
        }

        public class FactoryProduct
        {
            public object First;
            public object Second;
            public string JSON;
            public Type GenericType;
        }

        public class SingleFactory : FactoryProduct
        {
            private class JSONCtorArgs { }
            public static FactoryProduct ConstructInstance(object first, string json) => new SingleFactory { First = first, JSON = json };
        }

        public class InheritedOnlyFactory : SingleFactory { }
        public class DerivedFactory : SingleFactory
        {
            public new static FactoryProduct ConstructInstance(object first, string json) => SingleFactory.ConstructInstance(first, json);
        }
        public class PairFactory : FactoryProduct
        {
            public static FactoryProduct ConstructInstance(object first, object second, string json) => new PairFactory { First = first, Second = second, JSON = json };
        }

        public class AttributedFactory : FactoryProduct
        {
            [JSONCtorArgs] public class ArgumentData { }
            public static FactoryProduct ConstructInstance(object first, string json) => new AttributedFactory();
        }

        public class GenericFactory<T> : FactoryProduct
        {
            public class JSONCtorArgs { }
            public static FactoryProduct ConstructInstance(object first, string json) => new GenericFactory<T> { First = first, JSON = json, GenericType = typeof(T) };
        }

        public class WrongReturnFactory : FactoryProduct
        {
            public static object ConstructInstance(object first, string json) => new object();
        }

        public class ThrowingFactory : FactoryProduct
        {
            public static FactoryProduct ConstructInstance(object first, string json) => throw new InvalidOperationException(json);
        }

        public class Unrelated { public static object ConstructInstance(object first, string json) => null; }
        public abstract class AbstractFactory : FactoryProduct { }
        public class InstanceFactory : FactoryProduct { public FactoryProduct ConstructInstance(object first, string json) => null; }
        public class NoFactory : FactoryProduct { }
        public class AmbiguousFactory : FactoryProduct
        {
            public static FactoryProduct ConstructInstance(object first, string json) => null;
            public static FactoryProduct ConstructInstance(object first, object second, string json) => null;
        }

        public static class FirstContainer
        {
            public class Collision : FactoryProduct { public static FactoryProduct ConstructInstance(object first, string json) => new Collision(); }
        }
        public static class SecondContainer
        {
            public class Collision : FactoryProduct { public static FactoryProduct ConstructInstance(object first, string json) => new Collision(); }
        }

        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("JSON factory verification failed: " + label);
        }

        private static TException Throws<TException>(Action action) where TException : Exception
        {
            try { action(); }
            catch (TException exception) { return exception; }
            throw new InvalidOperationException("JSON factory verification failed: expected " + typeof(TException).Name);
        }
    }
}
#endif
