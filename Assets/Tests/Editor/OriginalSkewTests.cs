using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.IL2CPP.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.UI;
using OriginalSkew = Hardlight.Skew;

namespace ProjectLucid.Tests
{
    // Behaviour specification for the original HLUnityUI.Runtime Hardlight.Skew
    // (TypeDef 0x02000064) and its iterator <LerpToSkew>d__7 (0x02000065), written
    // from the x86-64 bodies of the pinned 6c89 dylib before the type is restored.
    // Every expectation below is hand-derived from the cited native address; no
    // expectation is computed by re-running the formula under test except the one
    // case that must read the editor's own Time.deltaTime.
    public sealed class OriginalSkewTests
    {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const string IteratorName = "<LerpToSkew>d__7";

        // dump.cs TypeDefIndex 11925: two Il2CppSetOption rows in metadata order
        // (ArrayBoundsChecks before NullChecks, unlike Gradient), AddComponentMenu
        // ("Hardlight UI/Effects/Skew Sprite", 4), four private [SerializeField] fields
        // at +0x28 m_skewY, +0x2C m_skewX, +0x30 m_lerpFromZeroOnEnable, +0x34 m_lerpTime,
        // and methods 0x060002ec..f1 in declaration order. The member order fixes the
        // generated iterator name: LerpToSkew is member ordinal 7 (fields m_skewY..m_lerpTime
        // are 0..3, then ModifyMesh 4, ModifyVertices 5, OnEnable 6), which gives d__7.
        [Test]
        public void SkewRetainsWholeDeclarationAndOrderedAnnotations()
        {
            Type type = typeof(OriginalSkew);
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("HLUnityUI.Runtime"));
            Assert.That(type.Namespace, Is.EqualTo("Hardlight"));
            Assert.That(type.BaseType, Is.EqualTo(typeof(BaseMeshEffect)));
            Assert.That((int)type.Attributes, Is.EqualTo(1048577));
            Assert.That(type.IsGenericType, Is.False);
            Assert.That(type.GetEvents(Declared), Is.Empty);
            Assert.That(type.GetProperties(Declared), Is.Empty);
            Assert.That(type.GetInterfaces(), Is.EqualTo(typeof(BaseMeshEffect).GetInterfaces()));

            IList<CustomAttributeData> attributes = type.GetCustomAttributesData();
            Assert.That(attributes.Count, Is.EqualTo(3));
            int[] options = { 2, 1 };
            for (int i = 0; i < 2; ++i)
            {
                CustomAttributeData option = attributes[i];
                Assert.That(option.AttributeType, Is.EqualTo(typeof(Il2CppSetOptionAttribute)));
                Assert.That(option.Constructor.GetParameters().Select(p => p.ParameterType).ToArray(), Is.EqualTo(new[] { typeof(Option), typeof(object) }));
                Assert.That(option.ConstructorArguments.Count, Is.EqualTo(2));
                Assert.That(option.ConstructorArguments[0].ArgumentType, Is.EqualTo(typeof(Option)));
                Assert.That(Convert.ToInt32(option.ConstructorArguments[0].Value), Is.EqualTo(options[i]));
                Assert.That(option.ConstructorArguments[1].ArgumentType, Is.EqualTo(typeof(bool)));
                Assert.That(option.ConstructorArguments[1].Value, Is.EqualTo(false));
                Assert.That(option.NamedArguments, Is.Empty);
            }
            Assert.That(attributes[2].AttributeType, Is.EqualTo(typeof(AddComponentMenu)));
            Assert.That(attributes[2].Constructor.GetParameters().Select(p => p.ParameterType).ToArray(), Is.EqualTo(new[] { typeof(string), typeof(int) }));
            Assert.That(attributes[2].ConstructorArguments.Select(p => p.Value).ToArray(), Is.EqualTo(new object[] { "Hardlight UI/Effects/Skew Sprite", 4 }));
            Assert.That(attributes[2].NamedArguments, Is.Empty);

            FieldInfo[] fields = type.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "m_skewY", "m_skewX", "m_lerpFromZeroOnEnable", "m_lerpTime" }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[] { typeof(float), typeof(float), typeof(bool), typeof(float) }));
            foreach (FieldInfo field in fields)
            {
                Assert.That((int)field.Attributes, Is.EqualTo(1), field.Name);
                IList<CustomAttributeData> data = field.GetCustomAttributesData();
                Assert.That(data.Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(SerializeField) }), field.Name);
                Assert.That(data[0].ConstructorArguments, Is.Empty);
                Assert.That(data[0].NamedArguments, Is.Empty);
            }

            MethodInfo[] methods = type.GetMethods(Declared);
            Assert.That(methods.Select(m => m.Name).ToArray(), Is.EqualTo(new[] { "ModifyMesh", "ModifyVertices", "OnEnable", "LerpToSkew", "ChangeSkewAmount" }));
            CheckMethod("ModifyMesh", typeof(void), 198, new[] { typeof(VertexHelper) }, new[] { "vh" });
            CheckMethod("ModifyVertices", typeof(void), 134, new[] { typeof(List<UIVertex>) }, new[] { "verts" });
            CheckMethod("OnEnable", typeof(void), 196, Type.EmptyTypes, new string[0]);
            CheckMethod("ChangeSkewAmount", typeof(void), 134, new[] { typeof(float), typeof(float), typeof(bool) }, new[] { "X", "Y", "instant" });
            Assert.That(type.GetMethod("OnEnable", Declared).GetBaseDefinition().DeclaringType, Is.EqualTo(typeof(UnityEngine.EventSystems.UIBehaviour)));
            Assert.That(type.GetMethod("ModifyMesh", Declared).GetBaseDefinition().DeclaringType, Is.EqualTo(typeof(BaseMeshEffect)));

            // 0x060002ef: private, one optional bool defaulting to true, and only the iterator attribute.
            MethodInfo lerp = type.GetMethod("LerpToSkew", Declared);
            Assert.That(lerp.ReturnType, Is.EqualTo(typeof(IEnumerator)));
            Assert.That((int)lerp.Attributes, Is.EqualTo(129));
            Assert.That(lerp.GetMethodImplementationFlags(), Is.EqualTo(MethodImplAttributes.IL));
            ParameterInfo reset = lerp.GetParameters().Single();
            Assert.That(reset.Name, Is.EqualTo("resetToZeroFist"));
            Assert.That(reset.ParameterType, Is.EqualTo(typeof(bool)));
            Assert.That(reset.Attributes, Is.EqualTo(ParameterAttributes.Optional | ParameterAttributes.HasDefault));
            Assert.That(reset.HasDefaultValue, Is.True);
            Assert.That(reset.DefaultValue, Is.EqualTo(true));
            CustomAttributeData iteratorAttribute = lerp.GetCustomAttributesData().Single();
            Assert.That(iteratorAttribute.AttributeType, Is.EqualTo(typeof(IteratorStateMachineAttribute)));
            Assert.That(iteratorAttribute.ConstructorArguments.Single().Value, Is.EqualTo(type.GetNestedType(IteratorName, BindingFlags.NonPublic)));

            ConstructorInfo constructor = type.GetConstructors(Declared).Single();
            Assert.That((int)constructor.Attributes, Is.EqualTo(6278));
            Assert.That(constructor.GetParameters(), Is.Empty);
            Assert.That(constructor.GetCustomAttributesData(), Is.Empty);
            Assert.That(type.GetNestedTypes(Declared).Select(t => t.Name).ToArray(), Is.EqualTo(new[] { IteratorName }));
        }

        // dump.cs TypeDefIndex 11924: the natural compiler iterator with the original
        // hoisted names (+0x2C <cachedSkewX>5__2, +0x30 <cachedSkewY>5__3, +0x34 <timer>5__4),
        // [DebuggerHidden] on everything except MoveNext.
        [Test]
        public void LerpIteratorRetainsTheOriginalGeneratedShape()
        {
            Type iterator = typeof(OriginalSkew).GetNestedType(IteratorName, BindingFlags.NonPublic);
            Assert.That(iterator, Is.Not.Null);
            Assert.That((int)iterator.Attributes, Is.EqualTo(1048835));
            Assert.That(iterator.BaseType, Is.EqualTo(typeof(object)));
            Assert.That(iterator.GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(CompilerGeneratedAttribute) }));
            CollectionAssert.AreEquivalent(new[] { typeof(IEnumerator<object>), typeof(IEnumerator), typeof(IDisposable) }, iterator.GetInterfaces());

            FieldInfo[] fields = iterator.GetFields(Declared);
            Assert.That(fields.Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "<>1__state", "<>2__current", "<>4__this", "resetToZeroFist", "<cachedSkewX>5__2", "<cachedSkewY>5__3", "<timer>5__4" }));
            Assert.That(fields.Select(f => f.FieldType).ToArray(), Is.EqualTo(new[] { typeof(int), typeof(object), typeof(OriginalSkew), typeof(bool), typeof(float), typeof(float), typeof(float) }));
            Assert.That(fields.Select(f => (int)f.Attributes).ToArray(), Is.EqualTo(new[] { 1, 1, 6, 6, 1, 1, 1 }));

            ConstructorInfo constructor = iterator.GetConstructors(Declared).Single();
            Assert.That((int)constructor.Attributes, Is.EqualTo(6278));
            Assert.That(constructor.GetParameters().Single().Name, Is.EqualTo("<>1__state"));
            Assert.That(constructor.GetParameters().Single().ParameterType, Is.EqualTo(typeof(int)));
            Assert.That(constructor.GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(new[] { typeof(DebuggerHiddenAttribute) }));

            string[] names = { "System.IDisposable.Dispose", "MoveNext", "System.Collections.Generic.IEnumerator<System.Object>.get_Current", "System.Collections.IEnumerator.Reset", "System.Collections.IEnumerator.get_Current" };
            int[] flags = { 481, 481, 2529, 481, 2529 };
            Type[] returns = { typeof(void), typeof(bool), typeof(object), typeof(void), typeof(object) };
            MethodInfo[] methods = iterator.GetMethods(Declared);
            Assert.That(methods.Length, Is.EqualTo(names.Length));
            for (int i = 0; i < names.Length; ++i)
            {
                MethodInfo method = methods.Single(m => m.Name == names[i]);
                Assert.That((int)method.Attributes, Is.EqualTo(flags[i]), names[i]);
                Assert.That(method.ReturnType, Is.EqualTo(returns[i]), names[i]);
                Assert.That(method.GetParameters(), Is.Empty, names[i]);
                Type[] expected = names[i] == "MoveNext" ? Type.EmptyTypes : new[] { typeof(DebuggerHiddenAttribute) };
                Assert.That(method.GetCustomAttributesData().Select(a => a.AttributeType).ToArray(), Is.EqualTo(expected), names[i]);
            }
            Assert.That(iterator.GetProperties(Declared).Length, Is.EqualTo(2));
            Assert.That(iterator.GetNestedTypes(Declared), Is.Empty);
        }

        // .ctor 0x060002f1 @0x1B4FA10: stores 0x3e4ccccd (0.2f) into +0x34 m_lerpTime
        // before BaseMeshEffect..ctor; every other field keeps its zero default.
        // Fails for a wrong constant (0.25, 1) or a missing initializer.
        [Test]
        public void ConstructionDefaultsLerpTimeToTwoTenthsAndEverythingElseToZero()
        {
            using (var fixture = new Fixture("OriginalSkew-defaults", false))
            {
                Assert.That(LerpTime(fixture.Skew), Is.EqualTo(0.2f));
                Assert.That(BitConverter.ToInt32(BitConverter.GetBytes(LerpTime(fixture.Skew)), 0), Is.EqualTo(0x3e4ccccd));
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(0f));
                Assert.That(SkewY(fixture.Skew), Is.EqualTo(0f));
                Assert.That(LerpFlag(fixture.Skew), Is.False);
                using (var serialized = new SerializedObject(fixture.Skew))
                {
                    serialized.Update();
                    Assert.That(serialized.FindProperty("m_lerpTime").floatValue, Is.EqualTo(0.2f));
                    Assert.That(serialized.FindProperty("m_skewX").floatValue, Is.EqualTo(0f));
                    Assert.That(serialized.FindProperty("m_skewY").floatValue, Is.EqualTo(0f));
                    Assert.That(serialized.FindProperty("m_lerpFromZeroOnEnable").boolValue, Is.False);
                }
            }
        }

        // ChangeSkewAmount 0x060002f0 @0x1B4F980: X (xmm0) -> +0x2C m_skewX, Y (xmm1) ->
        // +0x28 m_skewY, then `instant` returns before any coroutine. Distinct signed
        // values catch an X/Y swap; the silent log catches a StartCoroutine on the
        // instant branch (the inactive owner would make Unity log an error).
        [Test]
        public void InstantChangeStoresXAndYWithoutStartingACoroutine()
        {
            using (var fixture = new Fixture("OriginalSkew-instant", false))
            {
                SetLerpTime(fixture.Skew, 1.5f);
                fixture.Skew.ChangeSkewAmount(3f, -2f, true);
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(3f));
                Assert.That(SkewY(fixture.Skew), Is.EqualTo(-2f));
                Assert.That(LerpTime(fixture.Skew), Is.EqualTo(1.5f));
                Assert.That(LerpFlag(fixture.Skew), Is.False);
                LogAssert.NoUnexpectedReceived();
            }
        }

        // 0x1B4F99A..0x1B4F9FE: the non-instant branch builds <LerpToSkew>d__7 with
        // resetToZeroFist = 0 and tail-calls MonoBehaviour.StartCoroutine after both stores.
        // On an inactive owner the genuine engine refuses and logs; the stores remain.
        [Test]
        public void NonInstantChangeStoresFirstAndThenAsksTheEngineForACoroutine()
        {
            using (var fixture = new Fixture("OriginalSkew-noninstant-inactive", false))
            {
                LogAssert.Expect(LogType.Error, new Regex("^Coroutine couldn't be started because .*'OriginalSkew-noninstant-inactive' is inactive"));
                fixture.Skew.ChangeSkewAmount(-4f, 6f, false);
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(-4f));
                Assert.That(SkewY(fixture.Skew), Is.EqualTo(6f));
            }
        }

        // Quirk from 0x1B4F9ED (resetToZeroFist = false) and MoveNext 0x1B4FA8C..0x1B4FB4C:
        // without the reset the cached X equals the live X, the `cached > live` loop guard
        // fails on the synchronous first step, and the values are restored at once. A
        // "non-instant" change is therefore instant. The infinite lerp time means a
        // reset-to-zero implementation would be left stuck at zero.
        [Test]
        public void NonInstantChangeOnAnActiveOwnerCompletesSynchronouslyWithoutLerping()
        {
            using (var fixture = new Fixture("OriginalSkew-noninstant-active", true))
            {
                Assert.That(fixture.Skew.IsActive(), Is.True);
                SetLerpTime(fixture.Skew, float.PositiveInfinity);
                SetSkew(fixture.Skew, 1f, 1f);
                fixture.Skew.ChangeSkewAmount(3f, -2f, false);
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(3f));
                Assert.That(SkewY(fixture.Skew), Is.EqualTo(-2f));
                LogAssert.NoUnexpectedReceived();
            }
        }

        // OnEnable 0x060002ee @0x1B4F850: BaseMeshEffect.OnEnable, then only when
        // +0x30 m_lerpFromZeroOnEnable is set, StartCoroutine(new d__7 { resetToZeroFist = 1 }).
        // The engine runs the first MoveNext synchronously: authored (2, 5) are cached,
        // zeroed, and with an infinite lerp time t = Clamp01(0 + dt / inf) = 0, so both
        // axes read 0 after activation. Re-enabling later caches the partial (0, 0), so the
        // authored values are not recovered (no Awake-time cache exists).
        [Test]
        public void EnableWithLerpFlagZeroesBothAxesAndReEnableCachesTheLiveValues()
        {
            using (var fixture = new Fixture("OriginalSkew-enable-lerp", false))
            {
                SetLerpFlag(fixture.Skew, true);
                SetLerpTime(fixture.Skew, float.PositiveInfinity);
                SetSkew(fixture.Skew, 2f, 5f);
                fixture.Owner.SetActive(true);
                Assert.That(fixture.Skew.IsActive(), Is.True);
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(0f));
                Assert.That(SkewY(fixture.Skew), Is.EqualTo(0f));

                fixture.Owner.SetActive(false);
                fixture.Owner.SetActive(true);
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(0f));
                Assert.That(SkewY(fixture.Skew), Is.EqualTo(0f));
                Assert.That(LerpFlag(fixture.Skew), Is.True);
                LogAssert.NoUnexpectedReceived();
            }
        }

        // 0x1B4F861: a clear flag skips the coroutine entirely; the authored values stay.
        [Test]
        public void EnableWithoutLerpFlagLeavesAuthoredSkew()
        {
            using (var fixture = new Fixture("OriginalSkew-enable-plain", false))
            {
                SetLerpTime(fixture.Skew, float.PositiveInfinity);
                SetSkew(fixture.Skew, 2f, 5f);
                fixture.Owner.SetActive(true);
                Assert.That(fixture.Skew.IsActive(), Is.True);
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(2f));
                Assert.That(SkewY(fixture.Skew), Is.EqualTo(5f));
                LogAssert.NoUnexpectedReceived();
            }
        }

        // MoveNext 0x1B4FAA7 `ucomiss cachedX, liveX; jbe exit`: the loop is gated on X
        // alone. With authored X <= 0 the reset value 0 is not below the cached X, so the
        // first step restores (X, Y) and finishes, even though Y = 5 was never lerped.
        // Fails for a guard on Y, on magnitude, or on "not equal".
        [Test]
        public void EnableWithLerpFlagRestoresAtOnceWhenAuthoredXIsNotPositive()
        {
            using (var negative = new Fixture("OriginalSkew-enable-negative-x", false))
            using (var zero = new Fixture("OriginalSkew-enable-zero-x", false))
            {
                foreach (Fixture fixture in new[] { negative, zero })
                {
                    SetLerpFlag(fixture.Skew, true);
                    SetLerpTime(fixture.Skew, float.PositiveInfinity);
                }
                SetSkew(negative.Skew, -1f, 5f);
                SetSkew(zero.Skew, 0f, 5f);
                negative.Owner.SetActive(true);
                zero.Owner.SetActive(true);
                Assert.That(SkewX(negative.Skew), Is.EqualTo(-1f));
                Assert.That(SkewY(negative.Skew), Is.EqualTo(5f));
                Assert.That(SkewX(zero.Skew), Is.EqualTo(0f));
                Assert.That(SkewY(zero.Skew), Is.EqualTo(5f));
                LogAssert.NoUnexpectedReceived();
            }
        }

        // LerpToSkew 0x060002ef @0x1B4F8E0: only constructs d__7 with state 0, this and
        // the argument; nothing runs before the first MoveNext.
        [Test]
        public void LerpFactoryCapturesOwnerAndArgumentLazily()
        {
            using (var fixture = new Fixture("OriginalSkew-factory", false))
            {
                SetSkew(fixture.Skew, 4f, -8f);
                foreach (bool reset in new[] { true, false })
                {
                    IEnumerator lerp = Lerp(fixture.Skew, reset);
                    Assert.That(lerp.GetType().Name, Is.EqualTo(IteratorName));
                    Assert.That(Get(lerp, "<>1__state"), Is.EqualTo(0));
                    Assert.That(Get(lerp, "<>4__this"), Is.SameAs(fixture.Skew));
                    Assert.That(Get(lerp, "resetToZeroFist"), Is.EqualTo(reset));
                    Assert.That(Get(lerp, "<timer>5__4"), Is.EqualTo(0f));
                    Assert.That(SkewX(fixture.Skew), Is.EqualTo(4f));
                    Assert.That(SkewY(fixture.Skew), Is.EqualTo(-8f));
                }
            }
        }

        // MoveNext 0x060002f4 @0x1B4FA40, state 0: cache X (+0x2C) and Y (+0x28), zero
        // both when resetting, timer = 0; while cachedX > liveX: timer += deltaTime /
        // m_lerpTime (stored unclamped, 0x1B4FACC), t = Clamp01(timer) (minss 1.0f at
        // 0x29a6be0, then the < 0 mask), X = cachedX * t, Y = cachedY * t, yield null.
        // The timer field is driven directly under an infinite lerp time (dt / inf = 0),
        // so each t below is exact. Catches an unclamped or one-sided clamp, a clamped
        // stored timer, swapped axes, a missing final yield at t = 1, and a restarted
        // iterator after completion.
        [Test]
        public void ResettingLerpScalesBothAxesByTheClampedTimerAndFinishesOnTheNextStep()
        {
            using (var fixture = new Fixture("OriginalSkew-movenext", false))
            {
                SetLerpTime(fixture.Skew, float.PositiveInfinity);
                SetSkew(fixture.Skew, 4f, -8f);
                IEnumerator lerp = Lerp(fixture.Skew, true);

                Assert.That(lerp.MoveNext(), Is.True);
                Assert.That(lerp.Current, Is.Null);
                Assert.That(Get(lerp, "<>1__state"), Is.EqualTo(1));
                Assert.That(Get(lerp, "<cachedSkewX>5__2"), Is.EqualTo(4f));
                Assert.That(Get(lerp, "<cachedSkewY>5__3"), Is.EqualTo(-8f));
                Assert.That(Get(lerp, "<timer>5__4"), Is.EqualTo(0f));
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(0f));
                Assert.That(SkewY(fixture.Skew), Is.EqualTo(0f));

                Set(lerp, "<timer>5__4", 0.25f);
                Assert.That(lerp.MoveNext(), Is.True);
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(1f));
                Assert.That(SkewY(fixture.Skew), Is.EqualTo(-2f));

                Set(lerp, "<timer>5__4", -3f);
                Assert.That(lerp.MoveNext(), Is.True);
                Assert.That(Get(lerp, "<timer>5__4"), Is.EqualTo(-3f));
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(0f));
                Assert.That(SkewY(fixture.Skew), Is.EqualTo(0f));

                Set(lerp, "<timer>5__4", 7f);
                Assert.That(lerp.MoveNext(), Is.True, "t = 1 still yields once before the guard sees X == cachedX.");
                Assert.That(Get(lerp, "<timer>5__4"), Is.EqualTo(7f), "The stored timer is the unclamped sum.");
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(4f));
                Assert.That(SkewY(fixture.Skew), Is.EqualTo(-8f));
                Assert.That(Get(lerp, "<>1__state"), Is.EqualTo(1));

                Assert.That(lerp.MoveNext(), Is.False);
                Assert.That(Get(lerp, "<>1__state"), Is.EqualTo(-1));
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(4f));
                Assert.That(SkewY(fixture.Skew), Is.EqualTo(-8f));

                // State -1 falls straight to `return false` (0x1B4FB52) without touching the owner.
                SetSkew(fixture.Skew, 1f, 1f);
                Assert.That(lerp.MoveNext(), Is.False);
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(1f));
                Assert.That(SkewY(fixture.Skew), Is.EqualTo(1f));
            }
        }

        // State 1 path 0x1B4FB2D re-reads the live +0x2C each step: an external X at or
        // above the cached X ends the lerp, and 0x1B4FB41 writes the cached pair back,
        // overriding the external write. Fails if the guard uses the timer or a local.
        [Test]
        public void ExternalXAtOrAboveTheCachedValueEndsTheLerpAndRestoresTheCachedPair()
        {
            using (var fixture = new Fixture("OriginalSkew-external", false))
            {
                SetLerpTime(fixture.Skew, float.PositiveInfinity);
                SetSkew(fixture.Skew, 4f, -8f);
                IEnumerator lerp = Lerp(fixture.Skew, true);
                Assert.That(lerp.MoveNext(), Is.True);
                SetSkew(fixture.Skew, 10f, 3f);
                Assert.That(lerp.MoveNext(), Is.False);
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(4f));
                Assert.That(SkewY(fixture.Skew), Is.EqualTo(-8f));
                Assert.That(Get(lerp, "<>1__state"), Is.EqualTo(-1));
            }
        }

        // 0x1B4FAA0 writes timer = 0 before the guard; without the reset the guard fails
        // immediately, 0x1B4FB41 rewrites the same pair and MoveNext returns false with
        // no yield. Fails if the non-reset path still zeroes or yields once.
        [Test]
        public void NonResettingLerpFinishesOnTheFirstStepWithoutYielding()
        {
            using (var fixture = new Fixture("OriginalSkew-noreset", false))
            {
                SetLerpTime(fixture.Skew, float.PositiveInfinity);
                SetSkew(fixture.Skew, 4f, -8f);
                IEnumerator lerp = Lerp(fixture.Skew, false);
                Set(lerp, "<timer>5__4", 9f);
                Assert.That(lerp.MoveNext(), Is.False);
                Assert.That(Get(lerp, "<>1__state"), Is.EqualTo(-1));
                Assert.That(Get(lerp, "<timer>5__4"), Is.EqualTo(0f));
                Assert.That(Get(lerp, "<cachedSkewX>5__2"), Is.EqualTo(4f));
                Assert.That(Get(lerp, "<cachedSkewY>5__3"), Is.EqualTo(-8f));
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(4f));
                Assert.That(SkewY(fixture.Skew), Is.EqualTo(-8f));
            }
        }

        // 0x1B4FABA..0x1B4FAC7: the increment is Time.deltaTime divided by +0x34 m_lerpTime
        // (divss), added to the stored timer. This is the one case that reads the editor's
        // deltaTime, so it assumes a positive step (lerp time 0.5 makes division and
        // multiplication differ for any dt > 0); it fails for deltaTime * lerpTime or a reset
        // timer. The second step reopens the X gate so it always runs.
        [Test]
        public void LerpTimerAdvancesByDeltaTimeOverLerpTime()
        {
            using (var fixture = new Fixture("OriginalSkew-deltatime", false))
            {
                SetLerpTime(fixture.Skew, 0.5f);
                SetSkew(fixture.Skew, 2f, 6f);
                IEnumerator lerp = Lerp(fixture.Skew, true);
                float delta = Time.deltaTime;
                Assume.That(delta, Is.GreaterThan(0f));
                Assert.That(lerp.MoveNext(), Is.True);
                float first = delta / 0.5f;
                Assert.That((float)Get(lerp, "<timer>5__4"), Is.EqualTo(first).Within(1e-6f));
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(2f * Mathf.Clamp01(first)).Within(1e-5f));
                Assert.That(SkewY(fixture.Skew), Is.EqualTo(6f * Mathf.Clamp01(first)).Within(1e-5f));
                Set(lerp, "<timer>5__4", 0.125f);
                SetSkew(fixture.Skew, 0f, 0f);
                Assert.That(lerp.MoveNext(), Is.True);
                Assert.That((float)Get(lerp, "<timer>5__4"), Is.EqualTo(0.125f + first).Within(1e-6f));
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(2f * Mathf.Clamp01(0.125f + first)).Within(1e-5f));
            }
        }

        // Reset 0x060002f6 @0x1B4FB80 throws NotSupportedException; both Current getters
        // (0x1B4FB70, 0x1B4FBC0) return +0x18, which every yield sets to null; Dispose
        // (0x1B4FA30) is empty, so a disposed iterator still continues.
        [Test]
        public void IteratorResetThrowsAndDisposeDoesNotStopTheLerp()
        {
            using (var fixture = new Fixture("OriginalSkew-iterator", false))
            {
                SetLerpTime(fixture.Skew, float.PositiveInfinity);
                SetSkew(fixture.Skew, 4f, -8f);
                IEnumerator lerp = Lerp(fixture.Skew, true);
                Assert.That(lerp.MoveNext(), Is.True);
                Assert.That(((IEnumerator<object>)lerp).Current, Is.Null);
                Assert.Throws<NotSupportedException>(() => lerp.Reset());
                ((IDisposable)lerp).Dispose();
                Assert.That(Get(lerp, "<>1__state"), Is.EqualTo(1));
                Set(lerp, "<timer>5__4", 0.5f);
                Assert.That(lerp.MoveNext(), Is.True);
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(2f));
                Assert.That(SkewY(fixture.Skew), Is.EqualTo(-4f));
            }
        }

        // ModifyVertices 0x060002ed @0x1B4F5D0: centre = sum over every list entry of
        // (Vector2)position / Count (duplicates weigh again); then each entry gets
        // x += (y - centre.y) * m_skewX and y += (x - centre.x) * m_skewY from the
        // pre-shear position (0x1B4F74B..0x1B4F771), z and every other channel kept.
        // The duplicated (0,0) moves the centre to (1, 1) instead of the unique (4/3, 4/3).
        // Catches swapped skew fields, a scale instead of a shear, an origin pivot, a
        // unique-vertex centre and centre updates inside the shear loop.
        [Test]
        public void ModifyVerticesShearsAboutTheStreamWeightedCentre()
        {
            using (var fixture = new Fixture("OriginalSkew-vertices", false))
            {
                SetSkew(fixture.Skew, 0.5f, 0.25f);
                var source = new[] { new Vector3(0f, 0f, 7f), new Vector3(4f, 0f, 8f), new Vector3(0f, 4f, 9f), new Vector3(0f, 0f, 10f) };
                List<UIVertex> verts = source.Select((p, i) => Vertex(p, i)).ToList();
                List<UIVertex> before = new List<UIVertex>(verts);
                fixture.Skew.ModifyVertices(verts);
                Vector2[] expected = { new Vector2(-0.5f, -0.25f), new Vector2(3.5f, 0.75f), new Vector2(1.5f, 3.75f), new Vector2(-0.5f, -0.25f) };
                Assert.That(verts.Count, Is.EqualTo(4));
                for (int i = 0; i < verts.Count; ++i)
                {
                    Assert.That(verts[i].position, Is.EqualTo(new Vector3(expected[i].x, expected[i].y, source[i].z)), "vertex " + i);
                    CheckOtherChannels(before[i], verts[i]);
                }

                // A single vertex is its own centre and stays put; an empty list is untouched.
                var single = new List<UIVertex> { Vertex(new Vector3(3f, -2f, 1f), 0) };
                fixture.Skew.ModifyVertices(single);
                Assert.That(single[0].position, Is.EqualTo(new Vector3(3f, -2f, 1f)));
                var empty = new List<UIVertex>();
                Assert.DoesNotThrow(() => fixture.Skew.ModifyVertices(empty));
                Assert.That(empty, Is.Empty);
            }
        }

        // ModifyMesh 0x060002ec @0x1B4F510: IsActive() (vcall slot 9) false returns
        // before the vertex helper is read; true runs GetUIVertexStream ->
        // ModifyVertices -> Clear -> AddUIVertexTriangleStream on a fresh list.
        // A disabled component leaves the indexed quad (4 vertices, 6 indices) intact.
        [Test]
        public void DisabledModifyMeshLeavesTheIndexedMeshUntouched()
        {
            using (var fixture = new Fixture("OriginalSkew-mesh-disabled", true))
            {
                fixture.Skew.enabled = false;
                Assert.That(fixture.Skew.IsActive(), Is.False);
                SetSkew(fixture.Skew, 0.75f, 0.5f);
                using (VertexHelper vh = Quad())
                {
                    List<UIVertex> before = Stream(vh);
                    fixture.Skew.ModifyMesh(vh);
                    Assert.That(vh.currentVertCount, Is.EqualTo(4));
                    Assert.That(vh.currentIndexCount, Is.EqualTo(6));
                    List<UIVertex> after = Stream(vh);
                    for (int i = 0; i < after.Count; ++i)
                    {
                        Assert.That(after[i].position, Is.EqualTo(before[i].position));
                        CheckOtherChannels(before[i], after[i]);
                    }
                }
            }
        }

        // Active path: the triangle stream a,b,c,a,b,d of a(0,0) b(4,0) c(0,4) d(4,4) has
        // centre (2, 4/3); with m_skewX 0.75 and m_skewY 0.5 the sheared stream is
        // (-1,-1) (3,1) (2,3) (-1,-1) (3,1) (6,5). The rebuilt helper is a flat stream
        // (6 vertices, 6 indices). Fails for a skipped Clear (10 vertices), an indexed
        // rewrite (4 vertices), or a centre over the 4 unique vertices (2, 2).
        [Test]
        public void ActiveModifyMeshRebuildsASkewedTriangleStream()
        {
            using (var fixture = new Fixture("OriginalSkew-mesh-active", true))
            {
                Assert.That(fixture.Skew.IsActive(), Is.True);
                SetSkew(fixture.Skew, 0.75f, 0.5f);
                using (VertexHelper vh = Quad())
                {
                    List<UIVertex> before = Stream(vh);
                    fixture.Skew.ModifyMesh(vh);
                    Assert.That(vh.currentVertCount, Is.EqualTo(6));
                    Assert.That(vh.currentIndexCount, Is.EqualTo(6));
                    List<UIVertex> after = Stream(vh);
                    Vector2[] expected = { new Vector2(-1f, -1f), new Vector2(3f, 1f), new Vector2(2f, 3f), new Vector2(-1f, -1f), new Vector2(3f, 1f), new Vector2(6f, 5f) };
                    Assert.That(after.Count, Is.EqualTo(6));
                    for (int i = 0; i < after.Count; ++i)
                    {
                        Assert.That(after[i].position.x, Is.EqualTo(expected[i].x).Within(1e-5f), "x " + i);
                        Assert.That(after[i].position.y, Is.EqualTo(expected[i].y).Within(1e-5f), "y " + i);
                        Assert.That(after[i].position.z, Is.EqualTo(before[i].position.z), "z " + i);
                        CheckOtherChannels(before[i], after[i]);
                    }
                }
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(0.75f));
                Assert.That(SkewY(fixture.Skew), Is.EqualTo(0.5f));
            }
        }

        // OnEnable 0x1B4F85C calls BaseMeshEffect.OnEnable first, which (ugui 1.0.0
        // BaseMeshEffect.OnEnable) runs graphic.SetVerticesDirty(); only then 0x1B4F861
        // tests +0x30 and starts the reset lerp, whose synchronous first step zeroes X.
        // The one dirty callback therefore sees the authored X = 2. Fails with [] for a
        // dropped base call and with [0] for a coroutine started before it.
        [Test]
        public void EnableDirtiesTheGraphicBeforeTheResetLerpZeroesTheSkew()
        {
            using (var fixture = new Fixture("OriginalSkew-enable-dirty", true))
            {
                SetLerpFlag(fixture.Skew, true);
                SetLerpTime(fixture.Skew, float.PositiveInfinity);
                SetSkew(fixture.Skew, 2f, 5f);
                fixture.Skew.enabled = false;
                Image image = fixture.Owner.GetComponent<Image>();
                var seen = new List<float>();
                UnityAction probe = () => seen.Add(SkewX(fixture.Skew));
                image.RegisterDirtyVerticesCallback(probe);
                try
                {
                    fixture.Skew.enabled = true;
                }
                finally
                {
                    image.UnregisterDirtyVerticesCallback(probe);
                }
                Assert.That(seen, Is.EqualTo(new[] { 2f }));
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(0f));
                Assert.That(SkewY(fixture.Skew), Is.EqualTo(0f));
            }
        }

        // 0x1B4F6A2..0x1B4F6C2: every entry is divided by the Count reloaded from +0x18
        // (divps) and the quotient is added to the running centre. For x = 1, 2, 4 that
        // gives centre.x 0x40155556, so with m_skewY 1 the new y values are exactly
        // 0xbfaaaaac, 0xbeaaaab0 and 0x3fd55554. Summing first and dividing once gives
        // centre.x 0x40155555 and y values 0xbfaaaaaa, 0xbeaaaaa8 and 0x3fd55556.
        [Test]
        public void ModifyVerticesAddsEachEntryOverCountIntoTheCentre()
        {
            using (var fixture = new Fixture("OriginalSkew-centre-order", false))
            {
                SetSkew(fixture.Skew, 0f, 1f);
                float[] xs = { 1f, 2f, 4f };
                List<UIVertex> verts = xs.Select((x, i) => Vertex(new Vector3(x, 0f, 0f), i)).ToList();
                fixture.Skew.ModifyVertices(verts);
                int[] expectedY = { unchecked((int)0xbfaaaaac), unchecked((int)0xbeaaaab0), 0x3fd55554 };
                for (int i = 0; i < xs.Length; ++i)
                {
                    Assert.That(Bits(verts[i].position.y), Is.EqualTo(expectedY[i]), "y " + i);
                    Assert.That(verts[i].position.x, Is.EqualTo(xs[i]), "x " + i);
                    Assert.That(verts[i].position.z, Is.EqualTo(0f), "z " + i);
                }
            }
        }

        // ChangeSkewAmount 0x1B4F980..0x1B4F9FE and MoveNext 0x1B4FA40..0x1B4FB5C only
        // store the skew fields and touch no graphic, so no step marks the vertices
        // dirty. Fails for a SetVerticesDirty added after the stores or per lerp step.
        [Test]
        public void SkewChangesAndLerpStepsDoNotDirtyTheGraphic()
        {
            using (var fixture = new Fixture("OriginalSkew-no-dirty", true))
            {
                Image image = fixture.Owner.GetComponent<Image>();
                int dirty = 0;
                UnityAction probe = () => ++dirty;
                image.RegisterDirtyVerticesCallback(probe);
                try
                {
                    SetLerpTime(fixture.Skew, float.PositiveInfinity);
                    fixture.Skew.ChangeSkewAmount(3f, -2f, true);
                    Assert.That(SkewX(fixture.Skew), Is.EqualTo(3f));
                    Assert.That(SkewY(fixture.Skew), Is.EqualTo(-2f));
                    fixture.Skew.ChangeSkewAmount(1f, 1f, false);
                    Assert.That(SkewX(fixture.Skew), Is.EqualTo(1f));
                    Assert.That(SkewY(fixture.Skew), Is.EqualTo(1f));

                    SetSkew(fixture.Skew, 4f, -8f);
                    IEnumerator lerp = Lerp(fixture.Skew, true);
                    Assert.That(lerp.MoveNext(), Is.True);
                    Assert.That(SkewX(fixture.Skew), Is.EqualTo(0f));
                    Assert.That(SkewY(fixture.Skew), Is.EqualTo(0f));
                    Set(lerp, "<timer>5__4", 0.5f);
                    Assert.That(lerp.MoveNext(), Is.True);
                    Assert.That(SkewX(fixture.Skew), Is.EqualTo(2f));
                    Assert.That(SkewY(fixture.Skew), Is.EqualTo(-4f));
                    Set(lerp, "<timer>5__4", 7f);
                    Assert.That(lerp.MoveNext(), Is.True);
                    Assert.That(SkewX(fixture.Skew), Is.EqualTo(4f));
                    Assert.That(SkewY(fixture.Skew), Is.EqualTo(-8f));
                }
                finally
                {
                    image.UnregisterDirtyVerticesCallback(probe);
                }
                Assert.That(dirty, Is.EqualTo(0));
                LogAssert.NoUnexpectedReceived();
            }
        }

        // 0x1B4F555 gates on the virtual UIBehaviour.IsActive (vcall slot 9), not on
        // `enabled`: an enabled Skew on an inactive GameObject still leaves the mesh.
        [Test]
        public void InactiveOwnerModifyMeshLeavesTheIndexedMeshUntouched()
        {
            using (var fixture = new Fixture("OriginalSkew-mesh-inactive", false))
            {
                Assert.That(fixture.Skew.enabled, Is.True);
                Assert.That(fixture.Skew.IsActive(), Is.False);
                SetSkew(fixture.Skew, 0.75f, 0.5f);
                using (VertexHelper vh = Quad())
                {
                    List<UIVertex> before = Stream(vh);
                    fixture.Skew.ModifyMesh(vh);
                    Assert.That(vh.currentVertCount, Is.EqualTo(4));
                    Assert.That(vh.currentIndexCount, Is.EqualTo(6));
                    List<UIVertex> after = Stream(vh);
                    Assert.That(after.Count, Is.EqualTo(before.Count));
                    for (int i = 0; i < after.Count; ++i)
                    {
                        Assert.That(after[i].position, Is.EqualTo(before[i].position));
                        CheckOtherChannels(before[i], after[i]);
                    }
                }
            }
        }

        // No lerp-time guard: divss 0x1B4FAC1 stores dt / 0 = +inf as the timer, and the
        // clamp 0x1B4FAD1..0x1B4FAE5 gives t = 1, so the first step yields with the full
        // pair and the next step finishes. Fails for an early restore on m_lerpTime <= 0.
        [Test]
        public void ZeroLerpTimeStoresAnInfiniteTimerAndFinishesAfterOneYield()
        {
            using (var fixture = new Fixture("OriginalSkew-zero-lerptime", false))
            {
                SetLerpTime(fixture.Skew, 0f);
                SetSkew(fixture.Skew, 4f, -8f);
                IEnumerator lerp = Lerp(fixture.Skew, true);
                Assume.That(Time.deltaTime, Is.GreaterThan(0f));
                Assert.That(lerp.MoveNext(), Is.True);
                Assert.That(Get(lerp, "<timer>5__4"), Is.EqualTo(float.PositiveInfinity));
                Assert.That(Get(lerp, "<>1__state"), Is.EqualTo(1));
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(4f));
                Assert.That(SkewY(fixture.Skew), Is.EqualTo(-8f));
                Assert.That(lerp.MoveNext(), Is.False);
                Assert.That(Get(lerp, "<>1__state"), Is.EqualTo(-1));
                Assert.That(SkewX(fixture.Skew), Is.EqualTo(4f));
                Assert.That(SkewY(fixture.Skew), Is.EqualTo(-8f));
            }
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Owner;
            public readonly OriginalSkew Skew;

            public Fixture(string name, bool active)
            {
                Owner = new GameObject(name, typeof(RectTransform));
                try
                {
                    Owner.SetActive(false);
                    Owner.AddComponent<Image>();
                    Skew = Owner.AddComponent<OriginalSkew>();
                    if (active) Owner.SetActive(true);
                }
                catch
                {
                    UnityEngine.Object.DestroyImmediate(Owner);
                    throw;
                }
            }

            public void Dispose()
            {
                if (Owner != null) UnityEngine.Object.DestroyImmediate(Owner);
            }
        }

        private static IEnumerator Lerp(OriginalSkew skew, bool reset)
        {
            // Only constructs the natural iterator; the test drives MoveNext itself.
            return (IEnumerator)typeof(OriginalSkew).GetMethod("LerpToSkew", Own).Invoke(skew, new object[] { reset });
        }

        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        private static object Get(object target, string name) => target.GetType().GetField(name, Own).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Own).SetValue(target, value);
        private static float SkewX(OriginalSkew skew) => (float)Get(skew, "m_skewX");
        private static float SkewY(OriginalSkew skew) => (float)Get(skew, "m_skewY");
        private static float LerpTime(OriginalSkew skew) => (float)Get(skew, "m_lerpTime");
        private static bool LerpFlag(OriginalSkew skew) => (bool)Get(skew, "m_lerpFromZeroOnEnable");
        private static void SetLerpTime(OriginalSkew skew, float value) => Set(skew, "m_lerpTime", value);
        private static void SetLerpFlag(OriginalSkew skew, bool value) => Set(skew, "m_lerpFromZeroOnEnable", value);

        private static void SetSkew(OriginalSkew skew, float x, float y)
        {
            Set(skew, "m_skewX", x);
            Set(skew, "m_skewY", y);
        }

        private static UIVertex Vertex(Vector3 position, int i)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = new Color32(7, 11, (byte)(13 + i), 17);
            vertex.normal = new Vector3(0f, 0f, -1f);
            vertex.tangent = new Vector4(1f, 0f, 0f, -1f);
            vertex.uv0 = new Vector4(i, 2f, 3f, 4f);
            vertex.uv1 = new Vector4(5f, 6f, 7f, 8f);
            vertex.uv2 = new Vector4(9f, i, 11f, 12f);
            vertex.uv3 = new Vector4(13f, 14f, i, 16f);
            return vertex;
        }

        private static VertexHelper Quad()
        {
            var vh = new VertexHelper();
            try
            {
                vh.AddVert(Vertex(new Vector3(0f, 0f, 1f), 0));
                vh.AddVert(Vertex(new Vector3(4f, 0f, 2f), 1));
                vh.AddVert(Vertex(new Vector3(0f, 4f, 3f), 2));
                vh.AddVert(Vertex(new Vector3(4f, 4f, 4f), 3));
                vh.AddTriangle(0, 1, 2);
                vh.AddTriangle(0, 1, 3);
                return vh;
            }
            catch { vh.Dispose(); throw; }
        }

        private static List<UIVertex> Stream(VertexHelper vh)
        {
            var result = new List<UIVertex>();
            vh.GetUIVertexStream(result);
            return result;
        }

        private static void CheckOtherChannels(UIVertex before, UIVertex after)
        {
            Assert.That(after.color, Is.EqualTo(before.color));
            Assert.That(after.normal, Is.EqualTo(before.normal));
            Assert.That(after.tangent, Is.EqualTo(before.tangent));
            Assert.That(after.uv0, Is.EqualTo(before.uv0));
            Assert.That(after.uv1, Is.EqualTo(before.uv1));
            Assert.That(after.uv2, Is.EqualTo(before.uv2));
            Assert.That(after.uv3, Is.EqualTo(before.uv3));
        }

        private static void CheckMethod(string name, Type result, int flags, Type[] parameters, string[] names)
        {
            MethodInfo method = typeof(OriginalSkew).GetMethods(Declared).Single(m => m.Name == name);
            Assert.That(method.ReturnType, Is.EqualTo(result), name);
            Assert.That((int)method.Attributes, Is.EqualTo(flags), name);
            Assert.That(method.GetMethodImplementationFlags(), Is.EqualTo(MethodImplAttributes.IL), name);
            Assert.That(method.IsGenericMethod, Is.False, name);
            Assert.That(method.GetCustomAttributesData(), Is.Empty, name);
            ParameterInfo[] actual = method.GetParameters();
            Assert.That(actual.Select(p => p.ParameterType).ToArray(), Is.EqualTo(parameters), name);
            Assert.That(actual.Select(p => p.Name).ToArray(), Is.EqualTo(names), name);
            foreach (ParameterInfo parameter in actual)
            {
                Assert.That(parameter.Attributes, Is.EqualTo(ParameterAttributes.None), name);
                Assert.That(parameter.GetCustomAttributesData(), Is.Empty, name);
            }
        }
    }
}
