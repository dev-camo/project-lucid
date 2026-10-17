using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using OriginalResizer = Hardlight.GUIBlockerBoxColliderResizer;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Specification of the original HLUnityUI.Runtime Hardlight.GUIBlockerBoxColliderResizer
    // (TypeDef 0x020000c5, methods 0x0600055c..5f) from the x86-64 bodies. Each case owns
    // its GameObjects and destroys them.
    [TestFixture]
    public sealed class OriginalGUIBlockerBoxColliderResizerTests
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // dump.cs TypeDefIndex 12021: sealed UIBehaviour, RequireComponent(BoxCollider)
        // before Il2CppSetOption(ArrayBoundsChecks) and (NullChecks); +0x20
        // m_trackedTransform and +0x28 m_boxCollider, both [SerializeField].
        [Test]
        public void DeclarationKeepsTheOriginalAttributesFieldsAndOverrides()
        {
            Type type = typeof(OriginalResizer);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.Namespace, Is.EqualTo("Hardlight"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(UIBehaviour)));
            Assert.That((int)type.Attributes, Is.EqualTo(1048833));
            Assert.That(type.GetNestedTypes(Declared), Is.Empty);
            Assert.That(type.GetProperties(Declared), Is.Empty);

            IList<CustomAttributeData> attributes = type.GetCustomAttributesData();
            Assert.That(attributes.Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(RequireComponent), typeof(Il2CppSetOptionAttribute), typeof(Il2CppSetOptionAttribute) }));
            Assert.That(attributes[0].ConstructorArguments.Single().Value, Is.EqualTo(typeof(BoxCollider)));
            Assert.That(Convert.ToInt32(attributes[1].ConstructorArguments[0].Value), Is.EqualTo(2));
            Assert.That(Convert.ToInt32(attributes[2].ConstructorArguments[0].Value), Is.EqualTo(1));
            Assert.That(attributes[1].ConstructorArguments[1].Value, Is.EqualTo(false));
            Assert.That(attributes[2].ConstructorArguments[1].Value, Is.EqualTo(false));

            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "m_trackedTransform", "m_boxCollider" }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[] { typeof(RectTransform), typeof(BoxCollider) }));
            foreach (FieldInfo field in fields)
            {
                Assert.That(field.Attributes, Is.EqualTo(FieldAttributes.Private), field.Name);
                Assert.That(field.GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(SerializeField) }), field.Name);
            }

            MethodInfo[] methods = type.GetMethods(Declared);
            Assert.That(methods.Select(m => m.Name).ToArray(), Is.EqualTo(new[] { "OnEnable", "OnRectTransformDimensionsChange", "UpdateCollider" }));
            Assert.That(methods.Select(m => (int)m.Attributes).ToArray(), Is.EqualTo(new[] { 196, 196, 129 }));
            Assert.That(methods.All(m => m.ReturnType == typeof(void) && m.GetParameters().Length == 0), Is.True);
            Assert.That(methods[0].GetBaseDefinition().DeclaringType, Is.EqualTo(typeof(UIBehaviour)));
            Assert.That(methods[1].GetBaseDefinition().DeclaringType, Is.EqualTo(typeof(UIBehaviour)));
            Assert.That((int)type.GetConstructors(Declared).Single().Attributes, Is.EqualTo(6278));
        }

        // UpdateCollider 0x0600055e: size = (rect.width, rect.height, 1f) (0x1B65FB8..0x1B65FED),
        // then center = (size.x * (0.5 - pivot.x), size.y * (0.5 - pivot.y), 0) with the
        // 0.5 pair at 0x29A6D30 and the size read back from the collider. A 200 x 100 rect
        // pivoted at (0.25, 1): size (200, 100, 1), center (50, -50, 0). OnEnable
        // 0x0600055c runs it after UIBehaviour.OnEnable. Neither the resizer nor UIBehaviour
        // is [ExecuteAlways], so EditMode activation sends no OnEnable and the call is
        // made directly.
        [Test]
        public void EnablingFitsTheColliderToTheTrackedRect()
        {
            using (var owned = new Owned())
            {
                owned.Rect.sizeDelta = new Vector2(200f, 100f);
                owned.Rect.pivot = new Vector2(0.25f, 1f);
                owned.Wire(owned.Rect, owned.Collider);
                owned.Owner.SetActive(true);
                Assert.That(owned.Collider.size, Is.EqualTo(Vector3.one));
                Assert.That(owned.Collider.center, Is.EqualTo(Vector3.zero));
                Invoke(owned.Resizer, "OnEnable");
                Assert.That(owned.Collider.size, Is.EqualTo(new Vector3(200f, 100f, 1f)));
                Assert.That(owned.Collider.center, Is.EqualTo(new Vector3(50f, -50f, 0f)));
            }
        }

        // OnRectTransformDimensionsChange 0x0600055d: base, then UpdateCollider. The
        // tracked rect need not be the owner's; a centred pivot zeroes the centre and z
        // stays one unit deep whatever the previous size was.
        [Test]
        public void DimensionChangesRefitTheColliderToAnyTrackedRect()
        {
            using (var owned = new Owned())
            {
                var other = new GameObject("OriginalResizer tracked", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    var tracked = (RectTransform)other.transform;
                    tracked.sizeDelta = new Vector2(30f, 12f);
                    tracked.pivot = new Vector2(0f, 0.5f);
                    owned.Collider.size = new Vector3(5f, 6f, 7f);
                    owned.Collider.center = new Vector3(1f, 2f, 3f);
                    owned.Wire(tracked, owned.Collider);
                    Invoke(owned.Resizer, "OnRectTransformDimensionsChange");
                    Assert.That(owned.Collider.size, Is.EqualTo(new Vector3(30f, 12f, 1f)));
                    Assert.That(owned.Collider.center, Is.EqualTo(new Vector3(15f, 0f, 0f)));

                    tracked.pivot = new Vector2(0.5f, 0.5f);
                    Invoke(owned.Resizer, "UpdateCollider");
                    Assert.That(owned.Collider.center, Is.EqualTo(Vector3.zero));
                }
                finally { Object.DestroyImmediate(other); }
            }
        }

        // 0x1B65F5C..0x1B65FAB: a null tracked transform or a null collider (Unity ==)
        // returns before anything is read or written.
        [Test]
        public void MissingReferencesLeaveTheColliderUntouched()
        {
            using (var owned = new Owned())
            {
                owned.Rect.sizeDelta = new Vector2(200f, 100f);
                owned.Collider.size = new Vector3(5f, 6f, 7f);
                owned.Collider.center = new Vector3(1f, 2f, 3f);
                owned.Wire(null, owned.Collider);
                Assert.DoesNotThrow(() => Invoke(owned.Resizer, "UpdateCollider"));
                owned.Wire(owned.Rect, null);
                Assert.DoesNotThrow(() => Invoke(owned.Resizer, "UpdateCollider"));
                Assert.That(owned.Collider.size, Is.EqualTo(new Vector3(5f, 6f, 7f)));
                Assert.That(owned.Collider.center, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            }
        }

        static void Invoke(object target, string name)
        {
            try { typeof(OriginalResizer).GetMethod(name, Declared).Invoke(target, null); }
            catch (TargetInvocationException e) { throw e.InnerException; }
        }

        // An inactive RectTransform owner with the resizer and its required BoxCollider.
        sealed class Owned : IDisposable
        {
            public readonly GameObject Owner;
            public readonly RectTransform Rect;
            public readonly BoxCollider Collider;
            public readonly OriginalResizer Resizer;

            public Owned()
            {
                Owner = new GameObject("OriginalResizer fixture", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    Owner.SetActive(false);
                    Rect = (RectTransform)Owner.transform;
                    Resizer = Owner.AddComponent<OriginalResizer>();
                    Collider = Owner.GetComponent<BoxCollider>();
                    Assert.That(Collider, Is.Not.Null);
                }
                catch { Object.DestroyImmediate(Owner); throw; }
            }

            public void Wire(RectTransform tracked, BoxCollider collider)
            {
                typeof(OriginalResizer).GetField("m_trackedTransform", Declared).SetValue(Resizer, tracked);
                typeof(OriginalResizer).GetField("m_boxCollider", Declared).SetValue(Resizer, collider);
            }

            public void Dispose() { if (Owner != null) Object.DestroyImmediate(Owner); }
        }
    }
}
