using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectLucid.Tests
{
    // These are original contract checks. No fabricated concrete controller is
    // attached, and they do not establish menu navigation or input behavior.
    public sealed class OriginalSelectableBasePreservationTests
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        [Test]
        public void CompleteOriginalBasesRetainAbstractSlotsAndEmptyInstanceShape()
        {
            CheckBase(typeof(SelectableBase), typeof(UIBehaviour), 8);
            CheckBase(typeof(SelectableControllerBase), typeof(MonoBehaviour), 9);

            CheckMethod(typeof(SelectableBase), "OnMove", typeof(void),
                new[] { typeof(AxisEventData) }, new[] { "eventData" });
            foreach (string name in new[] { "OnPointerDown", "OnPointerUp", "OnPointerEnter", "OnPointerExit" })
                CheckMethod(typeof(SelectableBase), name, typeof(void),
                    new[] { typeof(PointerEventData) }, new[] { "eventData" });
            foreach (string name in new[] { "OnSelect", "OnDeselect" })
                CheckMethod(typeof(SelectableBase), name, typeof(void),
                    new[] { typeof(BaseEventData) }, new[] { "eventData" });
            CheckMethod(typeof(SelectableBase), "IsInteractable", typeof(bool), Type.EmptyTypes, new string[0]);

            CheckMethod(typeof(SelectableControllerBase), "ShouldBlockFindSelectable", typeof(bool), Type.EmptyTypes, new string[0]);
            CheckMethod(typeof(SelectableControllerBase), "TryGetCachedSelectable", typeof(SelectableBase), Type.EmptyTypes, new string[0]);
            CheckMethod(typeof(SelectableControllerBase), "SelectThis", typeof(void),
                new[] { typeof(SelectableBase), typeof(bool) }, new[] { "selectable", "instantTransition" });
            CheckMethod(typeof(SelectableControllerBase), "ActivateSystem", typeof(void), Type.EmptyTypes, new string[0]);
            CheckMethod(typeof(SelectableControllerBase), "DeactivateSystem", typeof(void),
                new[] { typeof(bool) }, new[] { "isBeingDestroyed" });
            foreach (string name in new[] { "RegisterExclusiveScene", "DeregisterExclusiveScene" })
                CheckMethod(typeof(SelectableControllerBase), name, typeof(void),
                    new[] { typeof(string) }, new[] { "sceneName" });
            CheckMethod(typeof(SelectableControllerBase), "RemoveHighlighterFromScene", typeof(void), Type.EmptyTypes, new string[0]);
            CheckMethod(typeof(SelectableControllerBase), "GetButtonTransitionTime", typeof(float), Type.EmptyTypes, new string[0]);
        }

        [Test]
        public void OriginalEventInterfacesBindToDeclaredAbstractHandlers()
        {
            Type[] interfaces = { typeof(IMoveHandler), typeof(IEventSystemHandler),
                typeof(IPointerDownHandler), typeof(IPointerUpHandler), typeof(IPointerEnterHandler),
                typeof(IPointerExitHandler), typeof(ISelectHandler), typeof(IDeselectHandler) };
            Assert.That(typeof(SelectableBase).GetInterfaces(), Is.EquivalentTo(interfaces));
            foreach (Type contract in interfaces)
            {
                InterfaceMapping map = typeof(SelectableBase).GetInterfaceMap(contract);
                Assert.That(map.InterfaceMethods.Length, Is.EqualTo(contract == typeof(IEventSystemHandler) ? 0 : 1));
                for (int i = 0; i < map.InterfaceMethods.Length; i++)
                {
                    Assert.That(map.TargetMethods[i].DeclaringType, Is.EqualTo(typeof(SelectableBase)));
                    Assert.That(map.TargetMethods[i].IsAbstract, Is.True);
                    Assert.That(map.TargetMethods[i].Name, Is.EqualTo(map.InterfaceMethods[i].Name));
                    Assert.That(map.TargetMethods[i].ReturnType, Is.EqualTo(map.InterfaceMethods[i].ReturnType));
                    Assert.That(map.TargetMethods[i].GetParameters().Select(p => p.ParameterType),
                        Is.EqualTo(map.InterfaceMethods[i].GetParameters().Select(p => p.ParameterType)));
                }
            }
            Assert.That(typeof(SelectableControllerBase).GetInterfaces(), Is.EqualTo(new[] { typeof(ISystem) }));
            InterfaceMapping marker = typeof(SelectableControllerBase).GetInterfaceMap(typeof(ISystem));
            Assert.That(marker.InterfaceMethods, Is.Empty);
            Assert.That(marker.TargetMethods, Is.Empty);
        }

        [Test]
        public void DestructionParameterRetainsOriginalOptionalFalseDefault()
        {
            foreach (Type type in new[] { typeof(SelectableBase), typeof(SelectableControllerBase) })
            foreach (MethodInfo method in type.GetMethods(Own))
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                bool expectedDefault = type == typeof(SelectableControllerBase) && method.Name == "DeactivateSystem";
                Assert.That(parameter.IsOptional, Is.EqualTo(expectedDefault));
                Assert.That(parameter.HasDefaultValue, Is.EqualTo(expectedDefault));
                Assert.That((int)parameter.Attributes, Is.EqualTo(expectedDefault ? 4112 : 0));
                if (expectedDefault) Assert.That(parameter.RawDefaultValue, Is.EqualTo(false));
            }
        }

        private static void CheckBase(Type type, Type parent, int abstractCount)
        {
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That((int)type.Attributes, Is.EqualTo(1048705));
            Assert.That(type.BaseType, Is.EqualTo(parent));
            Assert.That(type.GetCustomAttributesData(), Is.Empty);
            Assert.That(type.GetFields(Own), Is.Empty);
            Assert.That(type.GetProperties(Own), Is.Empty);
            Assert.That(type.GetEvents(Own), Is.Empty);
            Assert.That(type.GetNestedTypes(Own), Is.Empty);
            Assert.That(type.TypeInitializer, Is.Null);
            Assert.That(type.GetMethods(Own).Length, Is.EqualTo(abstractCount));
            ConstructorInfo constructor = type.GetConstructors(Own).Single();
            Assert.That((int)constructor.Attributes, Is.EqualTo(6276));
            Assert.That(constructor.GetParameters(), Is.Empty);
            Assert.That(constructor.GetCustomAttributesData(), Is.Empty);
        }

        private static void CheckMethod(Type type, string name, Type result, Type[] parameters, string[] names)
        {
            MethodInfo method = type.GetMethods(Own).Single(m => m.Name == name);
            Assert.That((int)method.Attributes, Is.EqualTo(1478));
            Assert.That(method.GetMethodImplementationFlags(), Is.EqualTo(MethodImplAttributes.IL));
            Assert.That(method.IsAbstract, Is.True);
            Assert.That(method.GetMethodBody(), Is.Null);
            Assert.That(method.GetBaseDefinition(), Is.EqualTo(method));
            Assert.That(method.ReturnType, Is.EqualTo(result));
            Assert.That(method.GetParameters().Select(p => p.ParameterType), Is.EqualTo(parameters));
            Assert.That(method.GetParameters().Select(p => p.Name), Is.EqualTo(names));
            Assert.That(method.GetCustomAttributesData(), Is.Empty);
            Assert.That(method.IsGenericMethod, Is.False);
        }
    }
}
