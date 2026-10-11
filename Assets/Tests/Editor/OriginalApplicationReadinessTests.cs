using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ProjectLucid.Tests
{
    public sealed class OriginalApplicationReadinessTests
    {
        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        [Test]
        public void AppReadinessPreservesItsOriginalFactoryAndOverrideDeclarations()
        {
            // Game.Runtime 0200017a / 06000860..862. These assertions inspect
            // metadata; they do not construct attributes or acquire an App.
            Type type = typeof(ApplicationTransitionIsAppReady);
            RequireOwner(type, "HardlightProject.ApplicationTransitionIsAppReady", typeof(FSMTransition));
            RequireTypeAttributes(type, "menu", "null", "bounds");
            RequireFields(type, Array.Empty<string>(), Array.Empty<Type>(), 33);
            Assert.That(type.GetNestedTypes(Declared), Is.Empty);
            RequireConstructor(type, 6273, new[] { typeof(FiniteStateMachine), typeof(FSMIdentifier) },
                new[] { "fsm", "transitionId" });
            RequireMethodSet(type, "ConstructInstance", "DoUpdate");
            RequireFactory(type, typeof(IFSMTransition), "transitionId");
            MethodInfo update = RequireMethod(type, "DoUpdate", 196, typeof(bool),
                new[] { typeof(IGraphUser), typeof(FSMUpdateContext) }, new[] { "user", "context" });
            Assert.That(update.GetBaseDefinition().DeclaringType, Is.EqualTo(typeof(FSMTransition)));
        }

        [Test]
        public void GuiReadinessPreservesItsOriginalStorageSelectionAndJsonDeclarations()
        {
            // Game.Runtime 0200017f/180 / 0600086b..86f. The genuine constructor
            // acquires SystemRefs, so this case observes declarations only.
            Type type = typeof(ApplicationTransitionIsGuiReady);
            RequireOwner(type, "HardlightProject.ApplicationTransitionIsGuiReady", typeof(FSMTransition));
            RequireTypeAttributes(type, "menu", "bounds", "null");
            RequireFields(type, new[] { "m_appRef", "m_uiRuntimeConfigRef", "m_useAppStorage" },
                new[] { typeof(SystemRef<App>), typeof(SystemRef<UIRuntimeConfiguration>), typeof(bool) }, 33);
            RequireConstructor(type, 6273,
                new[] { typeof(FiniteStateMachine), typeof(FSMIdentifier), typeof(bool) },
                new[] { "fsm", "transitionId", "useAppStorage" });
            RequireMethodSet(type, "ConstructInstance", "DoUpdate", "TryGetStorage");
            RequireFactory(type, typeof(IFSMTransition), "transitionId");
            MethodInfo update = RequireMethod(type, "DoUpdate", 196, typeof(bool),
                new[] { typeof(IGraphUser), typeof(FSMUpdateContext) }, new[] { "user", "context" });
            Assert.That(update.GetBaseDefinition().DeclaringType, Is.EqualTo(typeof(FSMTransition)));
            RequireMethod(type, "TryGetStorage", 129, typeof(bool),
                new[] { typeof(IGraphUser), typeof(IGraphStorage).MakeByRefType() },
                new[] { "user", "storage" }, new[] { ParameterAttributes.None, ParameterAttributes.Out });
            Type json = RequireJson(type, 1056771);
            RequireFields(json, new[] { "UseAppStorage" }, new[] { typeof(bool) }, 6);
            Assert.That(type.GetNestedTypes(Declared), Is.EqualTo(new[] { json }));
        }

        [Test]
        public void OpenUiContainerPreservesItsOriginalFieldsFactoriesAndJsonDeclarations()
        {
            // Game.Runtime 020000fa/fb / 060006e8..6ed. Original callback closure
            // ordinals and its algorithm remain separately preserved native evidence.
            Type type = typeof(ApplicationStateOpenUIContainer);
            RequireOwner(type, "HardlightProject.ApplicationStateOpenUIContainer", typeof(FSMState));
            RequireTypeAttributes(type, "null", "bounds", "menu");
            RequireFields(type, new[] { "m_containerIdentifier", "m_playScreenTransition",
                    "m_transitionOverrideContainer", "m_queueScreenTransitionIfAlreadyActive", "m_closeContainers" },
                new[] { typeof(UIContainerIdentifier), typeof(bool), typeof(UIContainerIdentifier), typeof(bool),
                    typeof(IReadOnlyList<UIContainerIdentifier>) }, 33);
            RequireConstructor(type, 6273,
                new[] { typeof(FiniteStateMachine), typeof(string), typeof(UIContainerIdentifier), typeof(bool),
                    typeof(UIContainerIdentifier), typeof(bool), typeof(IReadOnlyList<UIContainerIdentifier>) },
                new[] { "fsm", "stateName", "containerIdentifier", "playScreenTransition",
                    "transitionOverrideContainer", "queueScreenTransitionIfAlreadyActive", "closeContainers" });
            RequireMethodSet(type, "ConstructInstance", "DoOnEnter", "CloseContainers", "OpenContainer");
            RequireFactory(type, typeof(IFSMState), "stateId");
            MethodInfo enter = RequireMethod(type, "DoOnEnter", 196, typeof(void),
                new[] { typeof(IGraphUser), typeof(FSMStateChangeAction) }, new[] { "user", "action" });
            Assert.That(enter.GetBaseDefinition().DeclaringType, Is.EqualTo(typeof(FSMState)));
            RequireMethod(type, "CloseContainers", 129, typeof(void), Array.Empty<Type>(), Array.Empty<string>());
            RequireMethod(type, "OpenContainer", 129, typeof(void), new[] { typeof(IGraphUser) }, new[] { "user" });
            Type json = RequireJson(type, 1056772);
            RequireFields(json, new[] { "Container", "PlayScreenTransition", "TransitionOverrideContainer",
                    "QueueScreenTransitionIfAlreadyActive", "CloseContainers" },
                new[] { typeof(string), typeof(bool), typeof(string), typeof(bool), typeof(List<string>) }, 6,
                "Container", "TransitionOverrideContainer", "CloseContainers");
            // QueueScreenTransitionIfAlreadyActive's true initializer is a body
            // behavior preserved in source/native evidence, not a metadata constant.
        }

        [Test]
        public void AppReadinessEarlyPathsRetainOwnedStorageAndTheOriginalNullFault()
        {
            // Original 06000862 returns before App lookup for missing table or
            // s_common. A present null table faults at its dictionary call. Do not
            // insert any handle: that path eagerly resolves the genuine global App.
            string name = "Lucid-app-readiness-" + Guid.NewGuid().ToString("N");
            var machine = new FiniteStateMachine(name, skipAddToManager: true);
            var id = new FSMIdentifier(name + "-transition");
            IFSMTransition transition = ApplicationTransitionIsAppReady.ConstructInstance(machine, id,
                "malformed { JSON");
            Assert.That(transition, Is.TypeOf<ApplicationTransitionIsAppReady>());
            Assert.That(((FSMTransition)transition).FSM, Is.SameAs(machine));
            Assert.That(transition.FSMId, Is.EqualTo(machine.FSMId));
            Assert.That(transition.TransitionId, Is.EqualTo(id.Id));
            Assert.That(machine.Transitions[id.Id], Is.SameAs(transition));
            Assert.That(transition.SerialiseRuntimeToJSON(), Is.EqualTo(string.Empty));
            var context = new FSMUpdateContext(0.25f, FSMUpdateType.Manual);

            var missing = new FSMStorage(0);
            var missingUser = new FSMUser(missing);
            Assert.That(missingUser.Storage, Is.SameAs(missing));
            Assert.That(missing.GetCollection(), Is.Empty);
            Assert.That(transition.Update(missingUser, context), Is.False);
            Assert.That(missing.GetCollection(), Is.Empty);
            Assert.That(missing.GetCollection().ContainsKey(AppFSMKeys.LoadedLevels), Is.False);

            var empty = new FSMStorage(0);
            var levels = new Dictionary<string, AsyncOperationHandle>(StringComparer.Ordinal);
            empty.SetValue(AppFSMKeys.LoadedLevels, levels);
            var emptyUser = new FSMUser(empty);
            Assert.That(empty.GetCollection()[AppFSMKeys.LoadedLevels], Is.SameAs(levels));
            Assert.That(transition.Update(emptyUser, context), Is.False);
            Assert.That(emptyUser.Storage, Is.SameAs(empty));
            Assert.That(empty.GetCollection().Count, Is.EqualTo(1));
            Assert.That(empty.GetCollection()[AppFSMKeys.LoadedLevels], Is.SameAs(levels));
            Assert.That(levels, Is.Empty);
            Assert.That(levels.ContainsKey("s_common"), Is.False);

            var nullTable = new FSMStorage(0);
            nullTable.SetValue<Dictionary<string, AsyncOperationHandle>>(AppFSMKeys.LoadedLevels, null);
            var nullUser = new FSMUser(nullTable);
            Assert.That(nullTable.GetCollection().ContainsKey(AppFSMKeys.LoadedLevels), Is.True);
            Assert.That(nullTable.GetCollection()[AppFSMKeys.LoadedLevels], Is.Null);
            Assert.Throws<NullReferenceException>(() => transition.Update(nullUser, context));
            Assert.That(nullUser.Storage, Is.SameAs(nullTable));
            Assert.That(nullTable.GetCollection().Count, Is.EqualTo(1));
            Assert.That(nullTable.GetCollection().ContainsKey(AppFSMKeys.LoadedLevels), Is.True);
            Assert.That(nullTable.GetCollection()[AppFSMKeys.LoadedLevels], Is.Null);
            Assert.That(machine.Transitions.Count, Is.EqualTo(1));
            Assert.That(machine.Transitions[id.Id], Is.SameAs(transition));
            Assert.That(machine.States, Is.Empty);
            // This is the rebuilt managed NullReferenceException; it does not
            // establish native fault-domain parity with IL2CPP null checks disabled.
        }

        private static void RequireOwner(Type type, string name, Type baseType)
        {
            Assert.That(type.FullName, Is.EqualTo(name));
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("Game.Runtime"));
            Assert.That((int)type.Attributes, Is.EqualTo(1048577));
            Assert.That(type.BaseType, Is.EqualTo(baseType));
            Assert.That(type.IsGenericType, Is.False);
            Assert.That(type.GetProperties(Declared), Is.Empty);
            Assert.That(type.GetEvents(Declared), Is.Empty);
            Assert.That(type.TypeInitializer, Is.Null);
        }

        private static Type RequireJson(Type owner, int flags)
        {
            Type json = owner.GetNestedType("JSONCtorArgs", Declared);
            Assert.That(json, Is.Not.Null);
            Assert.That(json.FullName, Is.EqualTo(owner.FullName + "+JSONCtorArgs"));
            Assert.That(json.DeclaringType, Is.EqualTo(owner));
            Assert.That(json.Assembly, Is.EqualTo(owner.Assembly));
            Assert.That((int)json.Attributes, Is.EqualTo(flags));
            Assert.That(json.BaseType, Is.EqualTo(typeof(object)));
            Assert.That(json.IsGenericType, Is.False);
            Assert.That(json.IsSerializable, Is.True);
            Assert.That(json.GetMethods(Declared), Is.Empty);
            Assert.That(json.GetProperties(Declared), Is.Empty);
            Assert.That(json.GetEvents(Declared), Is.Empty);
            Assert.That(json.GetNestedTypes(Declared), Is.Empty);
            Assert.That(json.TypeInitializer, Is.Null);
            RequireTypeAttributes(json, "null", "bounds");
            RequireConstructor(json, 6278, Array.Empty<Type>(), Array.Empty<string>());
            return json;
        }

        private static void RequireTypeAttributes(Type type, params string[] order)
        {
            // Mono can project SerializableAttribute from the already checked
            // Serializable flag; the original physical annotation list excludes it.
            CustomAttributeData[] attributes = type.GetCustomAttributesData()
                .Where(a => a.AttributeType != typeof(SerializableAttribute)).ToArray();
            Assert.That(attributes.Length, Is.EqualTo(order.Length));
            for (int i = 0; i < order.Length; i++)
            {
                CustomAttributeData attribute = attributes[i];
                Assert.That(attribute.NamedArguments, Is.Empty);
                if (order[i] == "menu")
                {
                    Assert.That(attribute.AttributeType, Is.EqualTo(typeof(GraphNodeMenuFormatAttribute)));
                    Assert.That(attribute.Constructor.GetParameters().Select(p => p.ParameterType),
                        Is.EqualTo(new[] { typeof(string) }));
                    Assert.That(attribute.ConstructorArguments.Count, Is.EqualTo(1));
                    Assert.That(attribute.ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(string)));
                    Assert.That(attribute.ConstructorArguments[0].Value, Is.EqualTo("Application/{0}"));
                }
                else
                {
                    Assert.That(attribute.AttributeType, Is.EqualTo(typeof(Il2CppSetOptionAttribute)));
                    Assert.That(attribute.Constructor.GetParameters().Select(p => p.ParameterType),
                        Is.EqualTo(new[] { typeof(Option), typeof(object) }));
                    Assert.That(attribute.ConstructorArguments.Count, Is.EqualTo(2));
                    Assert.That(attribute.ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(Option)));
                    Assert.That(Convert.ToInt32(attribute.ConstructorArguments[0].Value),
                        Is.EqualTo(order[i] == "null" ? 1 : 2));
                    Assert.That(attribute.ConstructorArguments[1].ArgumentType, Is.EqualTo(typeof(bool)));
                    Assert.That(attribute.ConstructorArguments[1].Value, Is.EqualTo(false));
                }
            }
        }

        private static void RequireFields(Type type, string[] names, Type[] types, int flags,
            params string[] objectFields)
        {
            FieldInfo[] fields = type.GetFields(Declared).OrderBy(f => f.MetadataToken).ToArray();
            Assert.That(fields.Select(f => f.Name), Is.EqualTo(names));
            Assert.That(fields.Select(f => f.FieldType), Is.EqualTo(types));
            foreach (FieldInfo field in fields)
            {
                Assert.That((int)field.Attributes, Is.EqualTo(flags));
                var attributes = field.GetCustomAttributesData();
                if (!objectFields.Contains(field.Name))
                {
                    Assert.That(attributes, Is.Empty);
                    continue;
                }
                Assert.That(attributes.Count, Is.EqualTo(1));
                Assert.That(attributes[0].AttributeType, Is.EqualTo(typeof(GraphUnityObjectAttribute)));
                Assert.That(attributes[0].Constructor.GetParameters().Select(p => p.ParameterType),
                    Is.EqualTo(new[] { typeof(Type) }));
                Assert.That(attributes[0].ConstructorArguments.Count, Is.EqualTo(1));
                Assert.That(attributes[0].ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(Type)));
                Assert.That(attributes[0].ConstructorArguments[0].Value, Is.EqualTo(typeof(UIContainerIdentifier)));
                Assert.That(attributes[0].NamedArguments, Is.Empty);
            }
        }

        private static void RequireConstructor(Type type, int flags, Type[] types, string[] names)
        {
            ConstructorInfo[] constructors = type.GetConstructors(Declared);
            Assert.That(constructors.Length, Is.EqualTo(1));
            RequireParameters(constructors[0], flags, types, names, null);
        }

        private static void RequireMethodSet(Type type, params string[] names) =>
            Assert.That(type.GetMethods(Declared).Select(m => m.Name), Is.EquivalentTo(names));

        private static void RequireFactory(Type type, Type returnType, string idName) =>
            RequireMethod(type, "ConstructInstance", 150, returnType,
                new[] { typeof(FiniteStateMachine), typeof(FSMIdentifier), typeof(string) },
                new[] { "fsm", idName, "jsonCtorArgs" });

        private static MethodInfo RequireMethod(Type type, string name, int flags, Type returnType,
            Type[] types, string[] names, ParameterAttributes[] parameterFlags = null)
        {
            MethodInfo method = type.GetMethod(name, Declared);
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
