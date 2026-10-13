using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using Hardlight.Localisation;
using HardlightProject;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;

namespace ProjectLucid.Tests
{
    // Rebuilt managed boundaries and read-only metadata/IL only. Never construct
    // App or CharacterCustomisationManager, enter real systems, or claim native fault parity.
    public sealed class OriginalLoadingBootstrapTests
    {
        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private static string Name() => "Lucid-loading-" + Guid.NewGuid().ToString("N");
        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);

        [Test]
        public void LoadingFactoryIgnoresJsonAndRegistersTheGenuineOwnedState()
        {
            var machine = Machine(); FSMIdentifier firstId = Name(); FSMIdentifier secondId = Name();
            IFSMState first = ApplicationStateLoading.ConstructInstance(machine, firstId, "not JSON");
            IFSMState second = ApplicationStateLoading.ConstructInstance(machine, secondId, null);
            Assert.That(first, Is.TypeOf<ApplicationStateLoading>());
            Assert.That(second, Is.TypeOf<ApplicationStateLoading>());
            Assert.That(first.StateId, Is.EqualTo(firstId.Id));
            Assert.That(second.StateId, Is.EqualTo(secondId.Id));
            Assert.That(machine.States.Count, Is.EqualTo(2));
            Assert.That(machine.States[firstId.Id], Is.SameAs(first));
            Assert.That(machine.States[secondId.Id], Is.SameAs(second));
            Assert.That(first.SerialiseRuntimeToJSON(), Is.EqualTo(string.Empty));
            Assert.That(first.GetStateTransitions(), Is.Empty);
            Assert.That(second.GetStateTransitions(), Is.Empty);
            Assert.That(((ApplicationStateLoading)first).FSM, Is.SameAs(machine));
        }

        [Test]
        public void LoadingConstructorKeepsOriginalStateReplacementWithoutProviderEntry()
        {
            var machine = Machine(); string name = Name(); FSMIdentifier id = name;
            var first = new ApplicationStateLoading(machine, name);
            var replacement = new ApplicationStateLoading(machine, name);
            Assert.That(first.StateId, Is.EqualTo(id.Id));
            Assert.That(replacement.StateId, Is.EqualTo(id.Id));
            Assert.That(machine.States.Count, Is.EqualTo(1));
            Assert.That(machine.States[id.Id], Is.SameAs(replacement));
            Assert.That(replacement.GetStateTransitions(), Is.Empty);
        }

        [Test]
        public void LoadingNullUserFaultsBeforeAppSystemsAndRetainsOwnedRegistration()
        {
            var machine = Machine(); var state = new ApplicationStateLoading(machine, Name());
            // With no transitions the genuine base hook returns, GetAs<App>
            // propagates null, and the first App instance call faults.
            Assert.Throws<NullReferenceException>(() => state.OnEnter(null, null));
            Assert.That(machine.States.Count, Is.EqualTo(1));
            Assert.That(machine.States[state.StateId], Is.SameAs(state));
            Assert.That(state.GetStateTransitions(), Is.Empty);
        }

        [Test]
        public void LoadingRealWrongGraphUserFaultsBeforeSystemsAndRetainsOwnedStorage()
        {
            var storage = new FSMStorage(0); var user = new FSMUser(storage);
            var machine = Machine(); var state = new ApplicationStateLoading(machine, Name());
            // A real FSMUser is not an App. No substitute provider or App is made.
            Assert.Throws<InvalidCastException>(() => state.OnEnter(user, null));
            Assert.That(user.Storage, Is.SameAs(storage));
            Assert.That(storage.GetCollection(), Is.Empty);
            Assert.That(machine.States[state.StateId], Is.SameAs(state));
            Assert.That(state.GetStateTransitions(), Is.Empty);
        }

        [Test]
        public void OriginalOwnersRetainDeclarationsTypedAttributesAndNaturalCallbacks()
        {
            Type loading = typeof(ApplicationStateLoading), character = typeof(CharacterCustomisationManager);
            foreach (Type owner in new[] { loading, character })
            {
                Assert.That(owner.Assembly.GetName().Name, Is.EqualTo("Game.Runtime"));
                Assert.That(owner.Namespace, Is.EqualTo("HardlightProject"));
                Assert.That(owner.IsPublic && !owner.IsSealed && !owner.IsAbstract, Is.True);
                Assert.That(owner.GetFields(Declared), Is.Empty);
                Assert.That(owner.GetProperties(Declared), Is.Empty);
                Assert.That(owner.GetEvents(Declared), Is.Empty);
            }
            Assert.That(loading.BaseType, Is.EqualTo(typeof(FSMState)));
            CustomAttributeData[] loadingAttributes = loading.GetCustomAttributesData().ToArray();
            Assert.That(loadingAttributes.Select(a => a.AttributeType), Is.EqualTo(new[] {
                typeof(GraphNodeMenuFormatAttribute), typeof(Il2CppSetOptionAttribute), typeof(Il2CppSetOptionAttribute) }));
            Assert.That(loadingAttributes[0].ConstructorArguments.Single().Value, Is.EqualTo("Application/{0}"));
            Assert.That(loadingAttributes[0].Constructor.GetParameters().Single().ParameterType, Is.EqualTo(typeof(string)));
            CheckOptions(loadingAttributes.Skip(1).ToArray());
            CheckOptions(character.GetCustomAttributesData().ToArray());

            ConstructorInfo constructor = loading.GetConstructors(Declared).Single();
            Assert.That(constructor.IsPublic, Is.True);
            AssertParameters(constructor, new[] { typeof(FiniteStateMachine), typeof(string) }, new[] { "fsm", "stateName" });
            MethodInfo factory = Method(loading, "ConstructInstance");
            Assert.That(factory.IsPublic && factory.IsStatic && factory.ReturnType == typeof(IFSMState), Is.True);
            AssertParameters(factory, new[] { typeof(FiniteStateMachine), typeof(FSMIdentifier), typeof(string) },
                new[] { "fsm", "transitionId", "jsonCtorArgs" });
            MethodInfo enter = Method(loading, "DoOnEnter");
            Assert.That(enter.IsFamily && enter.GetBaseDefinition().DeclaringType == typeof(FSMState), Is.True);
            AssertParameters(enter, new[] { typeof(IGraphUser), typeof(FSMStateChangeAction) }, new[] { "user", "action" });
            Assert.That(loading.GetMethods(Declared).Length, Is.EqualTo(2));
            Assert.That(loading.GetNestedTypes(Declared).Length, Is.EqualTo(1));
            MethodInfo loadingCallback = LoadingCallback();
            Assert.That(loadingCallback.DeclaringType.DeclaringType, Is.EqualTo(loading));
            Assert.That(loadingCallback.DeclaringType.IsNestedPrivate && loadingCallback.DeclaringType.IsSealed, Is.True);
            Assert.That(loadingCallback.GetParameters().Single().ParameterType.Assembly.GetName().Name, Is.EqualTo("HLHaptics.Runtime"));

            Assert.That(character.BaseType, Is.EqualTo(typeof(object)));
            // Reflection interface enumeration is not an original physical table-order proof.
            Assert.That(character.GetInterfaces(), Is.EquivalentTo(new[] { typeof(ISystem), typeof(ISaveGameListener) }));
            Assert.That(character.GetNestedTypes(Declared), Is.Empty);
            ConstructorInfo characterConstructor = character.GetConstructors(Declared).Single();
            Assert.That(characterConstructor.IsPublic, Is.True);
            Assert.That(characterConstructor.GetParameters(), Is.Empty);
            Assert.That(character.GetMethods(Declared).Length, Is.EqualTo(5));
            MethodInfo characterCallback = CharacterCallback();
            Assert.That(characterCallback.DeclaringType, Is.EqualTo(character));
            Assert.That(characterCallback.IsPrivate && !characterCallback.IsStatic, Is.True);
            foreach (string name in new[] { "OnSaveGameOpen", "OnSaveGameClose" })
            {
                MethodInfo method = Method(character, name);
                Assert.That(method.IsPublic && !method.IsStatic && method.ReturnType == typeof(void), Is.True);
                AssertParameters(method, new[] { typeof(SaveDataGame) }, new[] { "saveDataGame" });
            }
        }

        [Test]
        public void CharacterEmptyOriginalReturnsAndDevelopmentConditionRemain()
        {
            foreach (string name in new[] { "OnLanguageChanged", "OnSaveGameClose" })
            {
                MethodBody body = Method(typeof(CharacterCustomisationManager), name).GetMethodBody();
                byte[] bytes = body.GetILAsByteArray();
                // ECMA-335 NOP=00 and RET=2a have no operands. Retain every byte;
                // no call, branch, field access or exception handler is permitted.
                Assert.That(bytes.Count(b => b == 0x2a), Is.EqualTo(1));
                Assert.That(bytes.All(b => b == 0x00 || b == 0x2a), Is.True);
                Assert.That(body.ExceptionHandlingClauses, Is.Empty);
            }
            CustomAttributeData condition = Method(typeof(CharacterCustomisationManager), "AddCharacterSkinDebug")
                .GetCustomAttributesData().Single();
            MethodInfo debug = Method(typeof(CharacterCustomisationManager), "AddCharacterSkinDebug");
            Assert.That(debug.IsPublic && debug.IsStatic && debug.ReturnType == typeof(void), Is.True);
            Assert.That(debug.GetParameters(), Is.Empty);
            Assert.That(condition.AttributeType, Is.EqualTo(typeof(ConditionalAttribute)));
            Assert.That(condition.ConstructorArguments.Single().Value, Is.EqualTo("BUILD_DEVELOPMENT"));
            Assert.That(condition.NamedArguments, Is.Empty);
        }

        private static MethodInfo Method(Type owner, string name) => owner.GetMethods(Declared).Single(m => m.Name == name);
        private static MethodInfo CharacterCallback() => typeof(CharacterCustomisationManager).GetMethods(Declared).Single(m =>
            m.IsDefined(typeof(CompilerGeneratedAttribute), false) && m.ReturnType == typeof(void) &&
            m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(SaveManager) }));
        private static MethodInfo LoadingCallback() => typeof(ApplicationStateLoading).GetNestedTypes(Declared)
            .Where(t => t.IsDefined(typeof(CompilerGeneratedAttribute), false)).SelectMany(t => t.GetMethods(Declared))
            .Single(m => m.ReturnType == typeof(void) && m.GetParameters().Length == 1 &&
                m.GetParameters()[0].ParameterType.FullName == "Hardlight.VibrationManager");

        private static void AssertParameters(MethodBase method, Type[] types, string[] names)
        {
            Assert.That(method.GetParameters().Select(p => p.ParameterType), Is.EqualTo(types));
            Assert.That(method.GetParameters().Select(p => p.Name), Is.EqualTo(names));
            Assert.That(method.GetParameters().All(p => !p.IsOptional && !p.HasDefaultValue), Is.True);
        }

        private static void CheckOptions(CustomAttributeData[] attributes)
        {
            Assert.That(attributes.Length, Is.EqualTo(2));
            Option[] options = { Option.NullChecks, Option.ArrayBoundsChecks };
            for (int i = 0; i < attributes.Length; i++)
            {
                CustomAttributeData attribute = attributes[i];
                Assert.That(attribute.AttributeType, Is.EqualTo(typeof(Il2CppSetOptionAttribute)));
                Assert.That(attribute.Constructor.GetParameters().Select(p => p.ParameterType), Is.EqualTo(new[] { typeof(Option), typeof(object) }));
                Assert.That(attribute.ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(Option)));
                Assert.That(Convert.ToInt32(attribute.ConstructorArguments[0].Value), Is.EqualTo((int)options[i]));
                Assert.That(attribute.ConstructorArguments[1].ArgumentType, Is.EqualTo(typeof(bool)));
                Assert.That(attribute.ConstructorArguments[1].Value, Is.EqualTo(false));
                Assert.That(attribute.NamedArguments, Is.Empty);
            }
        }

    }
}
