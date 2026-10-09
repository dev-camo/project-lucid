using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Verification
{
    /// <summary>
    /// Bounded behavioral fixture for the restored MessageManager source.
    /// It uses only the manager's public fields and the real Core providers.
    /// </summary>
    public static class OriginalMessageManagerVerification
    {
        private const BindingFlags PublicInstanceDeclared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        private static readonly string[] FieldNames =
        {
            "ComponentMessagesWithCompletion",
            "StringMessagesWithCompletion",
            "ComponentMessagesWithoutCompletion",
            "StringMessagesWithoutCompletion",
            "ScriptableObjectMessagesWithoutCompletion"
        };
        private static readonly Type[] FieldTypes =
        {
            typeof(MessageExchangeWithCompletion<Component>),
            typeof(MessageExchangeWithCompletion<string>),
            typeof(MessageExchange<Component>),
            typeof(MessageExchange<string>),
            typeof(MessageExchange<ScriptableObjectWithGuid>)
        };

        private sealed class Scenario
        {
            private readonly List<ISystem> systems = new List<ISystem>();
            private readonly List<ISystem> providers = new List<ISystem>();
            private readonly List<IExchangeHandle> handles = new List<IExchangeHandle>();
            private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
            public bool ShutdownOwnedProvidersOnCleanup { get; set; }
            public int Checks { get; private set; }

            public void Check(bool condition, string reason)
            {
                Checks++;
                if (!condition)
                    throw new InvalidOperationException("MessageManager verification failed: " + reason);
            }

            public T OwnSystem<T>(T value) where T : class, ISystem
            {
                if (value != null)
                    systems.Add(value);
                return value;
            }

            public T OwnProvider<T>(T value) where T : class, ISystem
            {
                if (value != null)
                {
                    providers.Add(value);
                    systems.Add(value);
                }
                return value;
            }

            public IExchangeHandle OwnHandle(IExchangeHandle value)
            {
                handles.Add(value);
                return value;
            }

            public T OwnObject<T>(T value) where T : UnityEngine.Object
            {
                objects.Add(value);
                return value;
            }

            public Exception Cleanup()
            {
                Exception firstFailure = null;

                // Only the first fixture opts into this path. Never dispatch manager-wide
                // shutdown here: the second fixture deliberately replaces these callbacks.
                if (ShutdownOwnedProvidersOnCleanup)
                {
                    foreach (ISystem provider in providers)
                    {
                        try { ProcessManager.ProcessSystemAction(provider, SystemAction.Shutdown); }
                        catch (Exception exception)
                        {
                            if (firstFailure == null) firstFailure = exception;
                        }
                    }
                }

                // Shutdown normally invalidates these handles. Repeat invalidation so even
                // a partial/throwing provider shutdown cannot prevent later teardown steps.
                foreach (IExchangeHandle handle in handles)
                {
                    try { if (handle != null) handle.Invalidate(); }
                    catch (Exception exception) { if (firstFailure == null) firstFailure = exception; }
                }

                // Attempt every owned unsubscription and Unity object destruction even
                // when shutdown or handle invalidation failed.
                foreach (ISystem system in systems)
                {
                    try { if (system != null) ProcessManager.UnsubscribeFromAllActions(system); }
                    catch (Exception exception) { if (firstFailure == null) firstFailure = exception; }
                }
                foreach (UnityEngine.Object value in objects)
                {
                    try { if (value != null) UnityEngine.Object.DestroyImmediate(value); }
                    catch (Exception exception) { if (firstFailure == null) firstFailure = exception; }
                }
                return firstFailure;
            }
        }

        private sealed class ManagerFixture
        {
            public readonly ISystem Manager;
            public readonly ISystem[] Providers;

            public ManagerFixture(ISystem manager, ISystem[] providers)
            {
                Manager = manager;
                Providers = providers;
            }
        }

        private static ManagerFixture CreateManager(Scenario scenario)
        {
            Assembly gameRuntime = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "Game.Runtime")
                {
                    gameRuntime = assembly;
                    break;
                }
            }
            if (gameRuntime == null)
                throw new InvalidOperationException("Game.Runtime is not loaded");

            Type managerType = gameRuntime.GetType("HardlightProject.MessageManager", true);
            object instance = Activator.CreateInstance(managerType);
            ISystem manager = instance as ISystem;
            scenario.OwnSystem(manager);

            var providers = new ISystem[FieldNames.Length];
            var fields = new FieldInfo[FieldNames.Length];
            // Discover and own every real provider before assertions can fail. This
            // lets finally clean all constructors' subscriptions on an early mismatch.
            for (int i = 0; i < FieldNames.Length; ++i)
            {
                fields[i] = managerType.GetField(FieldNames[i], PublicInstanceDeclared);
                if (fields[i] != null)
                    providers[i] = fields[i].GetValue(instance) as ISystem;
                scenario.OwnProvider(providers[i]);
            }

            scenario.Check(manager != null, "real MessageManager implements ISystem");
            for (int i = 0; i < FieldNames.Length; ++i)
            {
                scenario.Check(fields[i] != null && fields[i].IsPublic && fields[i].IsInitOnly, FieldNames[i] + " public readonly field identity");
                scenario.Check(fields[i].FieldType == FieldTypes[i], FieldNames[i] + " closed provider type");
                scenario.Check(providers[i] != null, FieldNames[i] + " contains a real ISystem provider");
            }
            return new ManagerFixture(manager, providers);
        }

        private static IExchangeHandle NewHandle(ISystem provider, object messageKey)
        {
            MethodInfo getter = null;
            foreach (MethodInfo method in provider.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.Name == "GetExchangeHandle" && !method.IsGenericMethod && method.GetParameters().Length == 1)
                {
                    getter = method;
                    break;
                }
            }
            if (getter == null)
                throw new MissingMethodException(provider.GetType().FullName, "GetExchangeHandle(in TMessage)");
            return getter.Invoke(provider, new[] { messageKey }) as IExchangeHandle;
        }

        private static IExchangeHandle[] CreateHandles(Scenario scenario, ManagerFixture fixture, object[] keys)
        {
            var handles = new IExchangeHandle[fixture.Providers.Length];
            for (int i = 0; i < handles.Length; ++i)
            {
                handles[i] = scenario.OwnHandle(NewHandle(fixture.Providers[i], keys[i]));
                scenario.Check(handles[i] != null, "provider " + i + " returns a genuine IExchangeHandle");
            }
            return handles;
        }

        private static void DispatchManager(ISystem manager, object context)
        {
            ProcessManager.ProcessSystemAction(manager, SystemAction.Shutdown, context);
        }

        public static int RunRealFiveProviderShutdownHandles()
        {
            var scenario = new Scenario { ShutdownOwnedProvidersOnCleanup = true };
            Exception primaryFailure = null;
            try
            {
                ManagerFixture fixture = CreateManager(scenario);
                var componentCompletionObject = scenario.OwnObject(new GameObject("MessageManager completion component key"));
                var componentOrdinaryObject = scenario.OwnObject(new GameObject("MessageManager ordinary component key"));
                var guidKey = scenario.OwnObject(ScriptableObject.CreateInstance<TimeCategoryObject>());
                object[] keys =
                {
                    componentCompletionObject.transform,
                    "message-manager-completion-string",
                    componentOrdinaryObject.transform,
                    "message-manager-ordinary-string",
                    guidKey
                };

                IExchangeHandle[] firstHandles = CreateHandles(scenario, fixture, keys);
                foreach (IExchangeHandle handle in firstHandles)
                    scenario.Check(handle.Valid, "new provider handle begins valid");

                DispatchManager(fixture.Manager, new object());
                foreach (IExchangeHandle handle in firstHandles)
                    scenario.Check(!handle.Valid, "manager shutdown invalidates the old provider handle");

                IExchangeHandle[] secondHandles = CreateHandles(scenario, fixture, keys);
                for (int i = 0; i < secondHandles.Length; ++i)
                    scenario.Check(secondHandles[i].Valid && !ReferenceEquals(secondHandles[i], firstHandles[i]), "first shutdown permits a fresh distinct handle " + i);

                DispatchManager(fixture.Manager, new object());
                foreach (IExchangeHandle handle in secondHandles)
                    scenario.Check(!handle.Valid, "repeated manager shutdown invalidates the replacement handle");

                IExchangeHandle[] finalHandles = CreateHandles(scenario, fixture, keys);
                for (int i = 0; i < finalHandles.Length; ++i)
                    scenario.Check(finalHandles[i].Valid && !ReferenceEquals(finalHandles[i], secondHandles[i]), "repeated shutdown still permits a fresh valid handle " + i);
                return scenario.Checks;
            }
            catch (Exception exception)
            {
                primaryFailure = exception;
                throw;
            }
            finally
            {
                CompleteCleanup(scenario, primaryFailure);
            }
        }

        public static int RunFiveOrderedCallbacksAndFaultRetry()
        {
            var scenario = new Scenario();
            Exception primaryFailure = null;
            try
            {
                ManagerFixture fixture = CreateManager(scenario);
                var order = new List<int>();
                var contexts = new List<object>();
                for (int i = 0; i < fixture.Providers.Length; ++i)
                {
                    int index = i;
                    fixture.Providers[i].SubscribeToAction(SystemAction.Shutdown, context =>
                    {
                        order.Add(index);
                        contexts.Add(context);
                    });
                }

                DispatchManager(fixture.Manager, new object());
                CheckOrder(scenario, order, 0, 1, 2, 3, 4);
                CheckNullContexts(scenario, contexts, "normal shutdown forwards null context to provider ");

                order.Clear();
                contexts.Clear();
                var marker = new CallbackFailure();
                fixture.Providers[1].SubscribeToAction(SystemAction.Shutdown, context =>
                {
                    order.Add(1);
                    contexts.Add(context);
                    throw marker;
                });
                Exception thrown = Capture(() => DispatchManager(fixture.Manager, new object()));
                scenario.Check(ReferenceEquals(thrown, marker), "provider callback exception propagates unchanged");
                CheckOrder(scenario, order, 0, 1);
                CheckNullContexts(scenario, contexts, "throwing shutdown still receives null context at provider ");

                order.Clear();
                contexts.Clear();
                fixture.Providers[1].SubscribeToAction(SystemAction.Shutdown, context =>
                {
                    order.Add(1);
                    contexts.Add(context);
                });
                DispatchManager(fixture.Manager, new object());
                CheckOrder(scenario, order, 0, 1, 2, 3, 4);
                CheckNullContexts(scenario, contexts, "retry after provider fault forwards null context to provider ");
                return scenario.Checks;
            }
            catch (Exception exception)
            {
                primaryFailure = exception;
                throw;
            }
            finally
            {
                CompleteCleanup(scenario, primaryFailure);
            }
        }

        private static void CompleteCleanup(Scenario scenario, Exception primaryFailure)
        {
            Exception cleanupFailure = scenario.Cleanup();
            if (cleanupFailure == null)
                return;

            var reportedCleanupFailure = new InvalidOperationException("MessageManager fixture cleanup failed after all cleanup steps were attempted", cleanupFailure);
            if (primaryFailure == null)
                throw reportedCleanupFailure;

            primaryFailure.Data["MessageManagerFixtureCleanupFailure"] = cleanupFailure;
            Debug.LogException(reportedCleanupFailure);
        }

        private sealed class CallbackFailure : Exception { }

        private static Exception Capture(Action action)
        {
            try { action(); }
            catch (Exception exception) { return exception; }
            return null;
        }

        private static void CheckOrder(Scenario scenario, List<int> actual, params int[] expected)
        {
            scenario.Check(actual.Count == expected.Length, "shutdown callback prefix length");
            int count = Math.Min(actual.Count, expected.Length);
            for (int i = 0; i < count; ++i)
                scenario.Check(actual[i] == expected[i], "shutdown callback order at index " + i);
        }

        private static void CheckNullContexts(Scenario scenario, List<object> contexts, string label)
        {
            for (int i = 0; i < contexts.Count; ++i)
                scenario.Check(contexts[i] == null, label + i);
        }
    }
}
