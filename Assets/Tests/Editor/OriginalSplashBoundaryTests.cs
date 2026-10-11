using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ProjectLucid.Tests
{
    public sealed class OriginalSplashBoundaryTests
    {
        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        [Test]
        public void BootAndUnloadKeepTheirOriginalSceneJsonAndStateContracts()
        {
            Type[] owners = { typeof(ApplicationStateBoot), typeof(ApplicationStateUnloadScene) };
            string[][] optionOrder = { new[] { "null", "menu", "bounds" }, new[] { "bounds", "null", "menu" } };
            for (int i = 0; i < owners.Length; i++)
            {
                Type owner = owners[i];
                RequireOwner(owner, "HardlightProject." + owner.Name, typeof(FSMState), 1048577);
                RequireAttributes(owner, optionOrder[i]);
                RequireFields(owner, new[] { "m_sceneName" }, new[] { typeof(string) }, 33);
                RequireConstructor(owner, 6273, new[] { typeof(FiniteStateMachine), typeof(string), typeof(string) },
                    new[] { "fsm", "stateName", "sceneName" });
                RequireMethodSet(owner, "ConstructInstance", "DoOnEnter");
                RequireFactory(owner, typeof(IFSMState), i == 0 ? "transitionId" : "stateId");
                MethodInfo enter = RequireMethod(owner, "DoOnEnter", 196, typeof(void),
                    new[] { typeof(IGraphUser), typeof(FSMStateChangeAction) }, new[] { "user", "action" });
                Assert.That(enter.GetBaseDefinition().DeclaringType, Is.EqualTo(typeof(FSMState)));
                Type json = RequireJson(owner, 1056771, "bounds", "null");
                RequireFields(json, new[] { "SceneName" }, new[] { typeof(string) }, 6, "SceneName");
            }
            // Original Game.Runtime Boot063b..63e/Unload0772..775. Entry is
            // held here: real display/store/load/unload boundaries stay uninvoked.
        }

        [Test]
        public void IntroAndFairplayStatesKeepOriginalInheritanceAndRefDeclarations()
        {
            Type fairplay = typeof(ApplicationStateBootFairplay);
            RequireOwner(fairplay, "HardlightProject.ApplicationStateBootFairplay", typeof(FSMState), 1048577);
            RequireAttributes(fairplay, "bounds", "menu", "null");
            RequireFields(fairplay, Array.Empty<string>(), Array.Empty<Type>(), 33);
            RequireConstructor(fairplay, 6273, new[] { typeof(FiniteStateMachine), typeof(string) },
                new[] { "fsm", "stateName" });
            RequireFactory(fairplay, typeof(IFSMState), "transitionId");
            RequireEntry(fairplay);
            Assert.That(fairplay.GetNestedTypes(Declared), Is.Empty);

            Type cleanup = typeof(ApplicationStateIntroVideoCleanup);
            RequireOwner(cleanup, "HardlightProject.ApplicationStateIntroVideoCleanup", typeof(FSMState), 1048577);
            RequireAttributes(cleanup, "null", "bounds", "menu");
            RequireFields(cleanup, new[] { "m_appRef" }, new[] { typeof(SystemRef<App>) }, 33);
            RequireConstructor(cleanup, 6273, new[] { typeof(FiniteStateMachine), typeof(string) },
                new[] { "fsm", "stateName" });
            RequireFactory(cleanup, typeof(IFSMState), "transitionId");
            RequireEntry(cleanup);
            Assert.That(cleanup.GetNestedTypes(Declared), Is.Empty);

            Type intro = typeof(ApplicationStateLoadIntroVideo);
            RequireOwner(intro, "HardlightProject.ApplicationStateLoadIntroVideo", typeof(ApplicationStateLoadScene), 1048833);
            RequireAttributes(intro, "null", "bounds", "menu");
            RequireFields(intro, Array.Empty<string>(), Array.Empty<Type>(), 33);
            RequireConstructor(intro, 6273,
                new[] { typeof(FiniteStateMachine), typeof(string), typeof(string), typeof(bool) },
                new[] { "fsm", "stateName", "sceneName", "useAppStorage" });
            RequireFactory(intro, typeof(IFSMState), "stateId");
            RequireEntry(intro);
            Assert.That(intro.GetNestedTypes(Declared), Is.Empty);
            foreach (Type owner in new[] { fairplay, cleanup, intro })
                RequireMethodSet(owner, "ConstructInstance", "DoOnEnter");
            // Type metadata only. Cleanup/intro constructors acquire global
            // SystemRefs; Fairplay entry invokes the original platform wrapper.
        }

        [Test]
        public void ScenePredicatesKeepDistinctOriginalJsonAndStorageMethodContracts()
        {
            Type[] owners = { typeof(ApplicationTransitionIsSceneLoaded), typeof(ApplicationTransitionIsSceneUnloaded) };
            for (int i = 0; i < owners.Length; i++)
            {
                Type owner = owners[i];
                RequireOwner(owner, "HardlightProject." + owner.Name, typeof(FSMTransition), 1048577);
                RequireAttributes(owner, "menu", "bounds", "null");
                RequireFields(owner, new[] { "m_appRef", "m_sceneName", "m_useAppStorage", "m_isAddressable" },
                    new[] { typeof(SystemRef<App>), typeof(string), typeof(bool), typeof(bool) }, 33);
                RequireConstructor(owner, 6273,
                    new[] { typeof(FiniteStateMachine), typeof(FSMIdentifier), typeof(string), typeof(bool), typeof(bool) },
                    new[] { "fsm", "transitionId", "sceneName", "useAppStorage", "isAddressable" });
                RequireFactory(owner, typeof(IFSMTransition), "transitionId");
                RequireMethodSet(owner, "ConstructInstance", "DoUpdate", "TryGetStorage");
                MethodInfo update = RequireMethod(owner, "DoUpdate", 196, typeof(bool),
                    new[] { typeof(IGraphUser), typeof(FSMUpdateContext) }, new[] { "user", "context" });
                Assert.That(update.GetBaseDefinition().DeclaringType, Is.EqualTo(typeof(FSMTransition)));
                RequireMethod(owner, "TryGetStorage", 129, typeof(bool),
                    new[] { typeof(IGraphUser), typeof(IGraphStorage).MakeByRefType() }, new[] { "user", "storage" },
                    new[] { ParameterAttributes.None, ParameterAttributes.Out });
                Type json = i == 0 ? RequireJson(owner, 1056772, "bounds", "null")
                    : RequireJson(owner, 1056772, "null", "bounds");
                RequireFields(json, new[] { "SceneName", "UseAppStorage", "IsAddressable" },
                    new[] { typeof(string), typeof(bool), typeof(bool) }, 6, "SceneName");
            }
            // No scene-predicate object is constructed. Its normal field initializer
            // auto-registers/caches the global App ref even with UseAppStorage=false.
            // SceneManager/Addressables and actual App readiness stay held.
        }

        [Test]
        public void FairPlayKeepsItsOriginalAssemblyImportAndExceptionClauseWithoutInvocation()
        {
            Type owner = typeof(Apple.Arcade.FairPlay);
            Assert.That(owner.FullName, Is.EqualTo("Apple.Arcade.FairPlay"));
            Assert.That(owner.Assembly.GetName().Name, Is.EqualTo("FairPlay.Runtime"));
            Assert.That((int)owner.Attributes, Is.EqualTo(1048577));
            Assert.That(owner.BaseType, Is.EqualTo(typeof(object)));
            Assert.That(owner.GetFields(Declared), Is.Empty);
            Assert.That(owner.GetProperties(Declared), Is.Empty);
            Assert.That(owner.GetEvents(Declared), Is.Empty);
            Assert.That(owner.GetNestedTypes(Declared), Is.Empty);
            Assert.That(owner.TypeInitializer, Is.Null);
            Assert.That(owner.GetCustomAttributesData(), Is.Empty);
            RequireConstructor(owner, 6278, Array.Empty<Type>(), Array.Empty<string>());
            RequireMethodSet(owner, "DoRegisterAppWithFairPlay", "RegisterWithFairPlay");
            MethodInfo import = owner.GetMethod("DoRegisterAppWithFairPlay", Declared);
            Assert.That(import, Is.Not.Null);
            Assert.That((int)import.Attributes, Is.EqualTo(8337));
            Assert.That(import.GetMethodImplementationFlags(), Is.EqualTo(MethodImplAttributes.PreserveSig));
            Assert.That(import.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(import.GetParameters(), Is.Empty);
            Assert.That(import.IsGenericMethod, Is.False);
            Assert.That(import.GetMethodBody(), Is.Null);
            CustomAttributeData dll = import.GetCustomAttributesData().Single(a => a.AttributeType == typeof(DllImportAttribute));
            Assert.That(dll.ConstructorArguments.Count, Is.EqualTo(1));
            Assert.That(dll.ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(string)));
            Assert.That(dll.ConstructorArguments[0].Value, Is.EqualTo("FPBundleWrapper"));
            Assert.That(dll.NamedArguments.Single(a => a.MemberName == "EntryPoint").TypedValue.Value,
                Is.EqualTo("DoRegisterAppWithFairPlay"));
            Assert.That(Convert.ToInt32(dll.NamedArguments.Single(a => a.MemberName == "CallingConvention").TypedValue.Value),
                Is.EqualTo((int)CallingConvention.Winapi));
            Assert.That(Convert.ToInt32(dll.NamedArguments.Single(a => a.MemberName == "CharSet").TypedValue.Value),
                Is.EqualTo((int)CharSet.None));
            // CLR's Winapi1 / CharSet.None1 are the managed default projections;
            // IL2CPP's resolver default0 / CHARSET_NOT_SPECIFIED2 remain separate
            // original evidence, not asserted equal numeric representations.
            MethodInfo wrapper = RequireMethod(owner, "RegisterWithFairPlay", 150, typeof(void),
                Array.Empty<Type>(), Array.Empty<string>());
            MethodBody body = wrapper.GetMethodBody();
            Assert.That(body, Is.Not.Null);
            Assert.That(body.ExceptionHandlingClauses.Count, Is.EqualTo(1));
            ExceptionHandlingClause clause = body.ExceptionHandlingClauses[0];
            Assert.That(clause.Flags, Is.EqualTo(ExceptionHandlingClauseOptions.Clause));
            Assert.That(clause.CatchType, Is.EqualTo(typeof(Exception)));
            Assert.That(clause.TryLength, Is.GreaterThan(0));
            Assert.That(clause.HandlerLength, Is.GreaterThan(0));
            // Passive metadata only. Original catch/log/Quit ordering and resolver
            // strings are separate source/native evidence. Neither method nor ctor,
            // Debug.LogException, Application.Quit, or the external plugin is invoked.
        }

        [Test]
        public void BootFactoryOwnsItsOriginalSceneDataWithoutEnteringPlatformOrLoadCode()
        {
            FiniteStateMachine machine = Machine();
            FSMIdentifier id = new FSMIdentifier(Name());
            string sceneName = Name();
            IFSMState state = ApplicationStateBoot.ConstructInstance(machine, id,
                "{\"SceneName\":\"" + sceneName + "\"}");
            AssertOwnedState(machine, id, state, typeof(ApplicationStateBoot));
            Assert.That(ReadSceneName(state, typeof(ApplicationStateBoot)), Is.EqualTo(sceneName));
            FSMIdentifier nullId = new FSMIdentifier(Name());
            IFSMState absentField = ApplicationStateBoot.ConstructInstance(machine, nullId, "{}");
            AssertOwnedState(machine, nullId, absentField, typeof(ApplicationStateBoot));
            Assert.That(ReadSceneName(absentField, typeof(ApplicationStateBoot)), Is.Null);
            Assert.That(machine.States.Count, Is.EqualTo(2));
            // No OnEnter/Update/leave call: constructor and real JsonUtility only.
        }

        [Test]
        public void UnloadAbsentSceneKeyLeavesNormallyOwnedTablesAndUnrelatedValuesUntouched()
        {
            FiniteStateMachine machine = Machine();
            FSMIdentifier id = new FSMIdentifier(Name());
            string sceneName = Name();
            IFSMState state = ApplicationStateUnloadScene.ConstructInstance(machine, id,
                "{\"SceneName\":\"" + sceneName + "\"}");
            AssertOwnedState(machine, id, state, typeof(ApplicationStateUnloadScene));
            Assert.That(ReadSceneName(state, typeof(ApplicationStateUnloadScene)), Is.EqualTo(sceneName));
            var empty = new Dictionary<string, AsyncOperationHandle>(StringComparer.Ordinal);
            var unrelated = new Dictionary<string, AsyncOperationHandle>(StringComparer.Ordinal)
                { { Name(), default(AsyncOperationHandle) } };
            foreach (Dictionary<string, AsyncOperationHandle> levels in new[] { empty, unrelated })
            {
                var storage = new FSMStorage(0);
                var user = new FSMUser(storage);
                GraphStorageKey markerKey = new GraphStorageKey(Name(), 0, 0);
                var marker = new object();
                storage.SetValue(markerKey, marker);
                storage.SetValue(AppFSMKeys.LoadedLevels, levels);
                KeyValuePair<string, AsyncOperationHandle>[] before = levels.ToArray();
                Assert.That(levels.ContainsKey(sceneName), Is.False);
                state.OnEnter(user, null);
                state.OnEnter(user, null);
                Assert.That(user.Storage, Is.SameAs(storage));
                Assert.That(storage.GetCollection().Count, Is.EqualTo(2));
                Assert.That(storage.GetCollection()[markerKey], Is.SameAs(marker));
                Assert.That(storage.GetCollection()[AppFSMKeys.LoadedLevels], Is.SameAs(levels));
                Assert.That(levels.ToArray(), Is.EqualTo(before));
                Assert.That(levels.ContainsKey(sceneName), Is.False);
            }
            Assert.That(machine.States.Count, Is.EqualTo(1));
            Assert.That(machine.States[id.Id], Is.SameAs(state));
            // Only a missing requested key is used. No fabricated valid handle,
            // AddressableManager request, App/service lookup or scene unload occurs.
        }

        [Test]
        public void UnloadMissingTableStoresOriginalNullDefaultBeforeItsManagedDictionaryFault()
        {
            FiniteStateMachine machine = Machine();
            FSMIdentifier id = new FSMIdentifier(Name());
            IFSMState state = ApplicationStateUnloadScene.ConstructInstance(machine, id,
                "{\"SceneName\":\"" + Name() + "\"}");
            AssertOwnedState(machine, id, state, typeof(ApplicationStateUnloadScene));
            var storage = new FSMStorage(0);
            var user = new FSMUser(storage);
            GraphStorageKey markerKey = new GraphStorageKey(Name(), 0, 0);
            var marker = new object();
            storage.SetValue(markerKey, marker);
            Assert.That(storage.GetCollection().ContainsKey(AppFSMKeys.LoadedLevels), Is.False);
            Assert.Throws<NullReferenceException>(() => state.OnEnter(user, null));
            Assert.That(storage.GetCollection().Count, Is.EqualTo(2));
            Assert.That(storage.GetCollection()[AppFSMKeys.LoadedLevels], Is.Null);
            Assert.That(storage.GetCollection()[markerKey], Is.SameAs(marker));
            Assert.Throws<NullReferenceException>(() => state.OnEnter(user, null));
            Assert.That(user.Storage, Is.SameAs(storage));
            Assert.That(storage.GetCollection().Count, Is.EqualTo(2));
            Assert.That(storage.GetCollection()[AppFSMKeys.LoadedLevels], Is.Null);
            Assert.That(storage.GetCollection()[markerKey], Is.SameAs(marker));
            Assert.That(machine.States[id.Id], Is.SameAs(state));
            // Rebuilt managed fault only; native fault-domain/timing parity with
            // disabled IL2CPP null checks is held. No service boundary is reached.
        }

        private static string Name() => "Lucid-splash-owned-" + Guid.NewGuid().ToString("N");
        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);

        private static void AssertOwnedState(FiniteStateMachine machine, FSMIdentifier id, IFSMState state, Type expected)
        {
            Assert.That(state.GetType(), Is.EqualTo(expected));
            Assert.That(((FSMState)state).FSM, Is.SameAs(machine));
            Assert.That(state.FSMId, Is.EqualTo(machine.FSMId));
            Assert.That(state.StateId, Is.EqualTo(id.Id));
            Assert.That(machine.States[id.Id], Is.SameAs(state));
            Assert.That(machine.Transitions, Is.Empty);
            Assert.That(state.GetStateTransitions(), Is.Empty);
            Assert.That(state.SerialiseRuntimeToJSON(), Is.EqualTo(string.Empty));
        }

        private static string ReadSceneName(IFSMState state, Type owner)
        {
            FieldInfo field = owner.GetField("m_sceneName", Declared);
            Assert.That(field, Is.Not.Null);
            Assert.That((int)field.Attributes, Is.EqualTo(33));
            Assert.That(field.FieldType, Is.EqualTo(typeof(string)));
            return (string)field.GetValue(state); // Observe a normally constructed owner; never write.
        }

        private static void RequireOwner(Type owner, string name, Type baseType, int flags)
        {
            Assert.That(owner.FullName, Is.EqualTo(name));
            Assert.That(owner.Assembly.GetName().Name, Is.EqualTo("Game.Runtime"));
            Assert.That((int)owner.Attributes, Is.EqualTo(flags));
            Assert.That(owner.BaseType, Is.EqualTo(baseType));
            Assert.That(owner.IsGenericType, Is.False);
            Assert.That(owner.TypeInitializer, Is.Null);
            Assert.That(owner.GetProperties(Declared), Is.Empty);
            Assert.That(owner.GetEvents(Declared), Is.Empty);
        }

        private static Type RequireJson(Type owner, int flags, params string[] options)
        {
            Type json = owner.GetNestedType("JSONCtorArgs", Declared);
            Assert.That(json, Is.Not.Null);
            Assert.That(owner.GetNestedTypes(Declared), Is.EqualTo(new[] { json }));
            Assert.That(json.FullName, Is.EqualTo(owner.FullName + "+JSONCtorArgs"));
            Assert.That(json.DeclaringType, Is.EqualTo(owner));
            Assert.That(json.Assembly, Is.EqualTo(owner.Assembly));
            Assert.That((int)json.Attributes, Is.EqualTo(flags));
            Assert.That(json.BaseType, Is.EqualTo(typeof(object)));
            Assert.That(json.IsSerializable, Is.True);
            Assert.That(json.TypeInitializer, Is.Null);
            Assert.That(json.GetMethods(Declared), Is.Empty);
            Assert.That(json.GetProperties(Declared), Is.Empty);
            Assert.That(json.GetEvents(Declared), Is.Empty);
            Assert.That(json.GetNestedTypes(Declared), Is.Empty);
            RequireAttributes(json, options);
            RequireConstructor(json, 6278, Array.Empty<Type>(), Array.Empty<string>());
            return json;
        }

        private static void RequireAttributes(Type owner, params string[] order)
        {
            CustomAttributeData[] attributes = owner.GetCustomAttributesData()
                .Where(a => a.AttributeType != typeof(SerializableAttribute)).ToArray();
            Assert.That(attributes.Length, Is.EqualTo(order.Length));
            for (int i = 0; i < order.Length; i++)
            {
                CustomAttributeData attribute = attributes[i];
                Assert.That(attribute.NamedArguments, Is.Empty);
                if (order[i] == "menu")
                {
                    Assert.That(attribute.AttributeType, Is.EqualTo(typeof(GraphNodeMenuFormatAttribute)));
                    Assert.That(attribute.ConstructorArguments.Count, Is.EqualTo(1));
                    Assert.That(attribute.ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(string)));
                    Assert.That(attribute.ConstructorArguments[0].Value, Is.EqualTo("Application/{0}"));
                }
                else
                {
                    Assert.That(attribute.AttributeType, Is.EqualTo(typeof(Il2CppSetOptionAttribute)));
                    Assert.That(attribute.ConstructorArguments.Count, Is.EqualTo(2));
                    Assert.That(attribute.ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(Option)));
                    Assert.That(Convert.ToInt32(attribute.ConstructorArguments[0].Value), Is.EqualTo(order[i] == "null" ? 1 : 2));
                    Assert.That(attribute.ConstructorArguments[1].ArgumentType, Is.EqualTo(typeof(bool)));
                    Assert.That(attribute.ConstructorArguments[1].Value, Is.EqualTo(false));
                }
            }
        }

        private static void RequireFields(Type owner, string[] names, Type[] types, int flags, params string[] sceneFields)
        {
            FieldInfo[] fields = owner.GetFields(Declared).OrderBy(f => f.MetadataToken).ToArray();
            Assert.That(fields.Select(f => f.Name), Is.EqualTo(names));
            Assert.That(fields.Select(f => f.FieldType), Is.EqualTo(types));
            foreach (FieldInfo field in fields)
            {
                Assert.That((int)field.Attributes, Is.EqualTo(flags));
                IList<CustomAttributeData> attributes = field.GetCustomAttributesData();
                if (!sceneFields.Contains(field.Name)) { Assert.That(attributes, Is.Empty); continue; }
                Assert.That(attributes.Count, Is.EqualTo(1));
                Assert.That(attributes[0].AttributeType, Is.EqualTo(typeof(GraphUnitySceneAttribute)));
                Assert.That(attributes[0].Constructor.GetParameters(), Is.Empty);
                Assert.That(attributes[0].ConstructorArguments, Is.Empty);
                Assert.That(attributes[0].NamedArguments, Is.Empty);
            }
        }

        private static void RequireEntry(Type owner) => RequireMethod(owner, "DoOnEnter", 196, typeof(void),
            new[] { typeof(IGraphUser), typeof(FSMStateChangeAction) }, new[] { "user", "action" });
        private static void RequireMethodSet(Type owner, params string[] names) =>
            Assert.That(owner.GetMethods(Declared).Select(m => m.Name), Is.EquivalentTo(names));
        private static void RequireFactory(Type owner, Type returnType, string idName) => RequireMethod(owner,
            "ConstructInstance", 150, returnType, new[] { typeof(FiniteStateMachine), typeof(FSMIdentifier), typeof(string) },
            new[] { "fsm", idName, "jsonCtorArgs" });
        private static void RequireConstructor(Type owner, int flags, Type[] types, string[] names)
        {
            ConstructorInfo[] constructors = owner.GetConstructors(Declared);
            Assert.That(constructors.Length, Is.EqualTo(1));
            RequireParameters(constructors[0], flags, types, names, null);
        }
        private static MethodInfo RequireMethod(Type owner, string name, int flags, Type returnType,
            Type[] types, string[] names, ParameterAttributes[] parameterFlags = null)
        {
            MethodInfo method = owner.GetMethod(name, Declared);
            Assert.That(method, Is.Not.Null);
            Assert.That(method.ReturnType, Is.EqualTo(returnType));
            RequireParameters(method, flags, types, names, parameterFlags);
            return method;
        }
        private static void RequireParameters(MethodBase method, int flags, Type[] types, string[] names,
            ParameterAttributes[] parameterFlags)
        {
            Assert.That((int)method.Attributes, Is.EqualTo(flags));
            Assert.That(method.GetMethodImplementationFlags(), Is.EqualTo(MethodImplAttributes.IL));
            Assert.That(method.IsGenericMethod, Is.False);
            Assert.That(method.GetCustomAttributesData(), Is.Empty);
            ParameterInfo[] parameters = method.GetParameters();
            Assert.That(parameters.Select(p => p.ParameterType), Is.EqualTo(types));
            Assert.That(parameters.Select(p => p.Name), Is.EqualTo(names));
            Assert.That(parameters.Select(p => p.Attributes), Is.EqualTo(parameterFlags ??
                Enumerable.Repeat(ParameterAttributes.None, types.Length).ToArray()));
            foreach (ParameterInfo parameter in parameters)
            {
                Assert.That(parameter.HasDefaultValue, Is.False);
                // Original Param flags retain Out=2 and no physical CAD row. Mono
                // separately projects a pseudo attribute from this checked flag.
                var attributes = parameter.GetCustomAttributesData();
                if (parameter.Attributes == ParameterAttributes.Out)
                {
                    Assert.That(attributes.Count, Is.EqualTo(1));
                    CustomAttributeData projected = attributes[0];
                    Assert.That(projected.AttributeType,
                        Is.EqualTo(typeof(System.Runtime.InteropServices.OutAttribute)));
                    Assert.That(projected.Constructor.GetParameters(), Is.Empty);
                    Assert.That(projected.ConstructorArguments, Is.Empty);
                    Assert.That(projected.NamedArguments, Is.Empty);
                }
                else
                {
                    Assert.That(attributes, Is.Empty);
                }
            }
        }
    }
}
