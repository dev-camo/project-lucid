using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;

namespace ProjectLucid.Tests
{
    public sealed class OriginalFontReadinessTransitionTests : OwnedGraphNamesFixture
    {
        public OriginalFontReadinessTransitionTests() : base("Lucid-font-readiness-") { }

        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        [Test]
        public void FontReadinessTransitionRetainsOriginalFactoryAndPrivateOverrideContracts()
        {
            // Original Game.Runtime 02000171 / 0600084b..84d. Metadata inspection
            // does not construct App, a SystemRef, or any custom attribute instance.
            Type type = typeof(ApplicationTransitionFontsReady);
            Assert.That(type.FullName, Is.EqualTo("HardlightProject.ApplicationTransitionFontsReady"));
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("Game.Runtime"));
            Assert.That((int)type.Attributes, Is.EqualTo(1048577));
            Assert.That(type.BaseType, Is.EqualTo(typeof(FSMTransition)));
            Assert.That(type.GetFields(Declared), Is.Empty);
            Assert.That(type.GetProperties(Declared), Is.Empty);
            Assert.That(type.GetEvents(Declared), Is.Empty);
            Assert.That(type.GetNestedTypes(Declared), Is.Empty);
            var attributes = type.GetCustomAttributesData();
            Assert.That(attributes.Count, Is.EqualTo(3));
            for (int i = 0; i < 2; i++)
            {
                Assert.That(attributes[i].AttributeType, Is.EqualTo(typeof(Il2CppSetOptionAttribute)));
                Assert.That(attributes[i].Constructor.GetParameters().Select(p => p.ParameterType),
                    Is.EqualTo(new[] { typeof(Option), typeof(object) }));
                Assert.That(attributes[i].ConstructorArguments.Count, Is.EqualTo(2));
                Assert.That(attributes[i].ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(Option)));
                Assert.That(Convert.ToInt32(attributes[i].ConstructorArguments[0].Value), Is.EqualTo(i + 1));
                Assert.That(attributes[i].ConstructorArguments[1].ArgumentType, Is.EqualTo(typeof(bool)));
                Assert.That(attributes[i].ConstructorArguments[1].Value, Is.EqualTo(false));
                Assert.That(attributes[i].NamedArguments, Is.Empty);
            }
            Assert.That(attributes[2].AttributeType, Is.EqualTo(typeof(GraphNodeMenuFormatAttribute)));
            Assert.That(attributes[2].ConstructorArguments.Count, Is.EqualTo(1));
            Assert.That(attributes[2].ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(string)));
            Assert.That(attributes[2].ConstructorArguments[0].Value, Is.EqualTo("Application/{0}"));
            Assert.That(attributes[2].NamedArguments, Is.Empty);
            ConstructorInfo[] constructors = type.GetConstructors(Declared);
            Assert.That(constructors.Length, Is.EqualTo(1));
            Require(constructors[0], 6273, new[] { typeof(FiniteStateMachine), typeof(FSMIdentifier) },
                new[] { "fsm", "transitionId" });
            MethodInfo[] methods = type.GetMethods(Declared);
            Assert.That(methods.Select(m => m.Name), Is.EquivalentTo(new[] { "ConstructInstance", "DoUpdate" }));
            MethodInfo factory = type.GetMethod("ConstructInstance", Declared);
            Assert.That(factory.ReturnType, Is.EqualTo(typeof(IFSMTransition)));
            Require(factory, 150, new[] { typeof(FiniteStateMachine), typeof(FSMIdentifier), typeof(string) },
                new[] { "fsm", "transitionId", "jsonCtorArgs" });
            MethodInfo update = type.GetMethod("DoUpdate", Declared);
            Assert.That(update.ReturnType, Is.EqualTo(typeof(bool)));
            Require(update, 196, new[] { typeof(IGraphUser), typeof(FSMUpdateContext) }, new[] { "user", "context" });
            Assert.That(update.GetBaseDefinition().DeclaringType, Is.EqualTo(typeof(FSMTransition)));
        }

        [Test]
        public void FontReadinessFactoryIgnoresJsonAndUsesTheGenuineOwnedTransitionRegistry()
        {
            var machine = new FiniteStateMachine("Lucid-font-readiness-" + Guid.NewGuid().ToString("N"),
                skipAddToManager: true);
            FSMIdentifier id = new FSMIdentifier(110010103);
            IFSMTransition first = ApplicationTransitionFontsReady.ConstructInstance(machine, id, "malformed { JSON");
            Assert.That(first, Is.TypeOf<ApplicationTransitionFontsReady>());
            Assert.That(first.FSMId, Is.EqualTo(machine.FSMId));
            Assert.That(first.TransitionId, Is.EqualTo(id.Id));
            Assert.That(machine.Transitions[id.Id], Is.SameAs(first));
            Assert.That(((FSMTransition)first).FSM, Is.SameAs(machine));
            Assert.That(first.SerialiseRuntimeToJSON(), Is.EqualTo(string.Empty));
            IFSMTransition second = ApplicationTransitionFontsReady.ConstructInstance(machine, id, null);
            Assert.That(ReferenceEquals(first, second), Is.False);
            Assert.That(machine.Transitions.Count, Is.EqualTo(1));
            Assert.That(machine.Transitions[id.Id], Is.SameAs(second));
            Assert.Throws<NullReferenceException>(() => ApplicationTransitionFontsReady.ConstructInstance(null, id, "{"));
            Assert.That(machine.Transitions[id.Id], Is.SameAs(second));
            // Update would acquire the genuine global App. Its two eager storage
            // reads are retained in original source/native evidence, not simulated here.
        }

        private static void Require(MethodBase method, int flags, Type[] types, string[] names)
        {
            Assert.That((int)method.Attributes, Is.EqualTo(flags));
            Assert.That(method.GetMethodImplementationFlags(), Is.EqualTo(MethodImplAttributes.IL));
            Assert.That(method.IsGenericMethod, Is.False);
            ParameterInfo[] parameters = method.GetParameters();
            Assert.That(parameters.Select(p => p.ParameterType), Is.EqualTo(types));
            Assert.That(parameters.Select(p => p.Name), Is.EqualTo(names));
            foreach (ParameterInfo parameter in parameters)
            {
                Assert.That(parameter.Attributes, Is.EqualTo(ParameterAttributes.None));
                Assert.That(parameter.HasDefaultValue, Is.False);
                Assert.That(parameter.GetCustomAttributesData(), Is.Empty);
            }
        }
    }
}
