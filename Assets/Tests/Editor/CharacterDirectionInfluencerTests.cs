using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.TriggerConditionTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CharacterDirectionInfluencer (TypeDef 0x02000738, x86-64 0x5C9F50..0x5CA95F, with
    // its private DirectionType enum 0x02000739) and CharacterDirectionInfluencerFalloff (0x0200073a,
    // 0x5CA970..0x5CAB2F). Components are real, on owned hosts that never wake. The TimeManager the
    // callbacks read is a real component that only holds its time settings, published through the
    // component's own reference. Characters have no body, so the Constant velocity modifier (the type's
    // default) is never used: it would read the body velocity.
    public sealed class CharacterDirectionInfluencerTests
    {
        private static readonly Type DirectionTypeEnum = typeof(CharacterDirectionInfluencer).GetNestedType("DirectionType", BindingFlags.NonPublic);

        private static T Add<T>(Owned owned, string name) where T : Component
        {
            GameObject host = owned.Host(name, false);
            host.AddComponent<BoxCollider>();
            return host.AddComponent<T>();
        }

        private static void WithInfluencer<T>(Action<Owned, Character, T> body) where T : CharacterDirectionInfluencer => Owned.Run(owned =>
        {
            // The constructor takes a typed TimeManager reference, which creates this row.
            owned.LeaseRow<TimeManager>();
            owned.LeaseRow<GameplayIslandManager>();
            Character character = NewCharacter(owned, new FSMStorage(0));
            body(owned, character, Add<T>(owned, "Lucid direction influencer"));
        });

        private static void AssertVector(Vector3 expected, Vector3 actual, float tolerance = 1e-5f, string message = null)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance), message + " x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance), message + " y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(tolerance), message + " z");
        }

        private static void AssertType(Type type, int typeFlags, Type baseType, int[] options,
            (string name, int flags)[] fields, (string name, int flags)[] methods)
        {
            AssertMembers(type, typeFlags, fields, methods);
            AssertIl2CppOptions(type, options);
            Assert.That(type.BaseType, Is.EqualTo(baseType), type.FullName);
        }

        // The declarations come from the original DummyDll metadata.
        [Test]
        public void DeclarationsKeepTheOriginalFlagsFieldsMethodsAndAttributes()
        {
            AssertType(typeof(CharacterDirectionInfluencer), 0x00100001, typeof(CharacterTrigger), new[] { 1, 2 },
                new[]
                {
                    ("m_timeCategory", 0x0001), ("m_speed", 0x0001), ("m_directionType", 0x0001),
                    ("m_smoothDampTimeSeconds", 0x0001), ("m_directionModiferType", 0x0001), ("m_speedBlendCurve", 0x0001),
                    ("m_maxResultantVelocity", 0x0001), ("m_spline", 0x0001), ("m_ribbon", 0x0001),
                    ("m_influencingCharacters", 0x0021), ("m_timeManagerRef", 0x0021), ("m_dampVelocity", 0x0001)
                },
                new[]
                {
                    ("DoTriggerEnter", 0x00c4), ("DoTriggerStay", 0x00c4), ("DoTriggerExit", 0x00c4),
                    ("GetDirectionInfluence", 0x0081), ("OnDrawGizmos", 0x0081), ("GetDirection", 0x0081),
                    ("GetSpeed", 0x01c4), ("GetForwardFromSurface", 0x0081), ("OnCollisionEnter", 0x0081),
                    ("OnCollisionStay", 0x0081), ("OnCollisionExit", 0x0081), (".ctor", 0x1886)
                });
            AssertMembers(DirectionTypeEnum, 0x00000103,
                new[]
                {
                    ("value__", 0x0606), ("SelfUp", 0x8056), ("SelfDown", 0x8056), ("SelfForward", 0x8056), ("SelfRight", 0x8056),
                    ("SelfBack", 0x8056), ("SelfLeft", 0x8056), ("Spline", 0x8056), ("Ribbon", 0x8056)
                },
                new (string, int)[0]);
            Assert.That(AttributeTypes(Field(typeof(CharacterDirectionInfluencer), "m_timeCategory")),
                Is.EqualTo(new[] { typeof(SerializeField), typeof(TooltipAttribute) }));
            Assert.That(AttributeTypes(Field(typeof(CharacterDirectionInfluencer), "m_speed")),
                Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(CharacterDirectionInfluencer), "m_directionType")),
                Is.EqualTo(new[] { typeof(SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(CharacterDirectionInfluencer), "m_spline")),
                Is.EqualTo(new[] { typeof(ShowIfAttribute), typeof(SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(CharacterDirectionInfluencer), "m_ribbon")),
                Is.EqualTo(new[] { typeof(SerializeField), typeof(ShowIfAttribute) }));
            Assert.That(Tooltip(Field(typeof(CharacterDirectionInfluencer), "m_directionModiferType")), Is.EqualTo(
                "Constant - Direction will be applied as a baseline addition to the character velocity without affecting rotation.\n"
                + "Acceleration - Direction will be continuously applied to character while in contact.\n"
                + "Override - Character velocity will be overriden by this direction."));
            Assert.That(Tooltip(Field(typeof(CharacterDirectionInfluencer), "m_speedBlendCurve")),
                Is.EqualTo("X Axis = time in contact\nY axis = Value multiplied by speed."));

            AssertType(typeof(CharacterDirectionInfluencerFalloff), 0x00100001, typeof(CharacterDirectionInfluencer), new[] { 1, 2 },
                new[] { ("m_normalisedSpeedOverDistance", 0x0001), ("m_distanceCheckSnap", 0x0001) },
                new[] { ("GetSpeed", 0x00c4), (".ctor", 0x1886) });
            Assert.That(Tooltip(Field(typeof(CharacterDirectionInfluencerFalloff), "m_normalisedSpeedOverDistance")),
                Is.EqualTo("X Axis = distance of character position to this position\nY axis = Value multiplied by speed."));
        }

        // The constructor 0x5CA860 writes Environment (0x34dab335) to the time category, SelfForward (2) to the
        // direction type and Constant (0xf2a6b66b) to the modifier type, then a constant-1 curve (0 to 1, value 1)
        // and the character dictionary. The falloff constructor 0x5CAAE0 adds its own constant-1 curve.
        [Test]
        public void ConstructorsWriteTheNativeDefaults()
        {
            WithInfluencer<CharacterDirectionInfluencerFalloff>((owned, character, influencer) =>
            {
                Assert.That((int)Get<TimeCategory>(influencer, "m_timeCategory"), Is.EqualTo(0x34dab335));
                Assert.That(Convert.ToInt32(Get<object>(influencer, "m_directionType")), Is.EqualTo(2));
                Assert.That((int)Get<DirectionModifierType>(influencer, "m_directionModiferType"), Is.EqualTo(unchecked((int)0xf2a6b66b)));
                Assert.That(Get<float>(influencer, "m_speed"), Is.EqualTo(0f));
                Assert.That(Get<float>(influencer, "m_smoothDampTimeSeconds"), Is.EqualTo(0f));
                foreach (string curve in new[] { "m_speedBlendCurve", "m_normalisedSpeedOverDistance" })
                {
                    var animationCurve = Get<AnimationCurve>(influencer, curve);
                    Assert.That(animationCurve.keys.Length, Is.EqualTo(2), curve);
                    Assert.That(animationCurve.Evaluate(0f), Is.EqualTo(1f), curve);
                    Assert.That(animationCurve.Evaluate(1f), Is.EqualTo(1f), curve);
                    Assert.That(animationCurve.Evaluate(0.37f), Is.EqualTo(1f), curve);
                }
                Assert.That(Get<Dictionary<Character, DirectionInfluenceData>>(influencer, "m_influencingCharacters"), Is.Empty);
                Assert.That(Get<SystemRef<TimeManager>>(influencer, "m_timeManagerRef"), Is.Not.Null);
            });
        }

        // GetDirection 0x5CA220 switches on the type through a jump table (0x5ca350): up, down (negated up),
        // forward, right, back (negated forward) and left (negated right) of this transform, read as the
        // transform's own axes. A transform turned 90 degrees about Y has forward = +X and right = -Z, so
        // every pair of types differs.
        [Test]
        public void SelfDirectionsFollowTheTransformAxes()
        {
            WithInfluencer<CharacterDirectionInfluencer>((owned, character, influencer) =>
            {
                influencer.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                (int type, Vector3 expected, string name)[] table =
                {
                    (0, new Vector3(0f, 1f, 0f), "SelfUp"),
                    (1, new Vector3(0f, -1f, 0f), "SelfDown"),
                    (2, new Vector3(1f, 0f, 0f), "SelfForward"),
                    (3, new Vector3(0f, 0f, -1f), "SelfRight"),
                    (4, new Vector3(-1f, 0f, 0f), "SelfBack"),
                    (5, new Vector3(0f, 0f, 1f), "SelfLeft"),
                };
                foreach ((int type, Vector3 expected, string name) in table)
                {
                    Set(influencer, "m_directionType", Enum.ToObject(DirectionTypeEnum, type));
                    var direction = (Vector3)Call(influencer, "GetDirection", new Vector3(7f, 8f, 9f));
                    AssertVector(expected, direction, 1e-6f, name);
                }
            });
        }

        // The jump table's range check (cmp rax, 7; ja 0x5ca317) sends any other value to a parameterless
        // ArgumentOutOfRangeException (0x5ca333).
        [Test]
        public void AnUndeclaredDirectionTypeRaisesArgumentOutOfRange()
        {
            WithInfluencer<CharacterDirectionInfluencer>((owned, character, influencer) =>
            {
                Set(influencer, "m_directionType", Enum.ToObject(DirectionTypeEnum, 8));
                var failure = Assert.Throws<ArgumentOutOfRangeException>(() => Call(influencer, "GetDirection", Vector3.zero));
                Assert.That(failure.ParamName, Is.Null);
            });
        }

        private sealed class ScriptedSurface : ISurface
        {
            public SurfaceLocation Location;
            public readonly List<Vector3> Queries = new List<Vector3>();

            public SurfaceLocation FindNearestSurfaceLocationFromWorld(Vector3 worldPosition)
            {
                Queries.Add(worldPosition);
                return Location;
            }

            public DateTime GetTimestamp() => throw new NotSupportedException();
            public MetadataGroups GetSurfaceMetadata() => throw new NotSupportedException();
            public SurfaceLocation FindNearestSurfaceLocationFromLocal(Vector3 localPosition) => throw new NotSupportedException();
            public float Length => throw new NotSupportedException();
            public float LengthInverse => throw new NotSupportedException();
            public Bounds GetBoundingBox(bool inWorldSpace = false) => throw new NotSupportedException();
            public Vector3 TransformPoint(Vector3 point) => throw new NotSupportedException();
            public Vector3 InverseTransformPoint(Vector3 point) => throw new NotSupportedException();
            public bool IsActive() => throw new NotSupportedException();
        }

        // GetForwardFromSurface 0x5CA620 asks the surface for its nearest location to the target position
        // (interface slot 2, FindNearestSurfaceLocationFromWorld, 0x5ca6cc) and rotates Vector3.forward by the
        // location's world rotation (the quaternion at +0x20 of the location, 0x5ca6ce, Quaternion.op_Multiply
        // 0x5ca724). A rotation of -90 degrees about X turns forward up.
        [Test]
        public void SurfaceForwardIsTheNearestLocationRotationAppliedToForward()
        {
            WithInfluencer<CharacterDirectionInfluencer>((owned, character, influencer) =>
            {
                var surface = new ScriptedSurface
                {
                    Location = new SurfaceLocation
                    {
                        m_localPosition = new Vector3(9f, 9f, 9f),
                        m_worldPosition = new Vector3(8f, 8f, 8f),
                        m_worldRotation = Quaternion.Euler(-90f, 0f, 0f),
                    },
                };

                var forward = (Vector3)Call(influencer, "GetForwardFromSurface", new Vector3(1f, 2f, 3f), surface);

                AssertVector(new Vector3(0f, 1f, 0f), forward, 1e-6f, "forward");
                Assert.That(surface.Queries, Is.EqualTo(new[] { new Vector3(1f, 2f, 3f) }), "queried once with the target position");
            });
        }

        // The spline and ribbon types read the spline or ribbon facade's surface (Spline.GetSpline 0x5ca2e5,
        // Ribbon.GetRibbon 0x5ca2fb) and jump to GetForwardFromSurface with the target position still in the
        // vector registers; the other six types read the transform and never reach it.
        [Test]
        public void SplineAndRibbonDirectionsGoThroughTheSurfaceForward()
        {
            List<MethodBase> calls = OriginalILCallSequence.Calls(typeof(CharacterDirectionInfluencer).GetMethod("GetDirection", Declared))
                .Select(call => call.Target).ToList();
            string[] names = calls.Select(call => call.DeclaringType.Name + "." + call.Name).ToArray();

            Assert.That(names.Count(name => name == "Spline.GetSpline"), Is.EqualTo(1));
            Assert.That(names.Count(name => name == "Ribbon.GetRibbon"), Is.EqualTo(1));
            Assert.That(names.Count(name => name == "CharacterDirectionInfluencer.GetForwardFromSurface"), Is.EqualTo(2));
            Assert.That(Array.IndexOf(names, "Spline.GetSpline"), Is.LessThan(Array.IndexOf(names, "Ribbon.GetRibbon")));
            Assert.That(names.Count(name => name == "ArgumentOutOfRangeException..ctor"), Is.EqualTo(1));
        }

        // The position that reaches GetDirection (and so the surface query of the spline and ribbon types) is the
        // character's WorldPosition, never this transform's: movsd [rbx+0x48] at 0x5ca028 and 0x5ca0f0 in DoTriggerEnter,
        // [rsi+0x48] at 0x5ca17f in GetDirectionInfluence. The surface types are the only ones that use the argument and
        // need a live spline or ribbon, so the order of the calls is witnessed on the compiled bodies.
        [Test]
        public void TheCharacterWorldPositionIsTheOnlyPositionPassedToGetDirection()
        {
            string[] Names(string method) => OriginalILCallSequence.Names(
                OriginalILCallSequence.Calls(typeof(CharacterDirectionInfluencer).GetMethod(method, Declared)));
            string[] Filter(string[] names, params string[] keep) => names.Where(keep.Contains).ToArray();

            string[] influence = Names("GetDirectionInfluence");
            Assert.That(Filter(influence, "Actor.get_WorldPosition", "CharacterDirectionInfluencer.GetDirection",
                    "CharacterDirectionInfluencer.GetSpeed", "Vector3.SmoothDamp"),
                Is.EqualTo(new[] { "Actor.get_WorldPosition", "CharacterDirectionInfluencer.GetDirection",
                    "CharacterDirectionInfluencer.GetSpeed", "Vector3.SmoothDamp" }));
            Assert.That(Array.IndexOf(influence, "CharacterDirectionInfluencer.GetDirection"),
                Is.EqualTo(Array.IndexOf(influence, "Actor.get_WorldPosition") + 1));

            string[] enter = Names("DoTriggerEnter");
            Assert.That(Filter(enter, "Actor.get_WorldPosition", "CharacterDirectionInfluencer.GetDirection"),
                Is.EqualTo(new[] { "Actor.get_WorldPosition", "CharacterDirectionInfluencer.GetDirection" }), "the velocity cap");
            Assert.That(Array.IndexOf(enter, "CharacterDirectionInfluencer.GetDirection"),
                Is.EqualTo(Array.IndexOf(enter, "Actor.get_WorldPosition") + 1));

            foreach (string name in new[] { "GetDirectionInfluence", "DoTriggerEnter", "DoTriggerStay", "GetDirection", "GetForwardFromSurface" })
                Assert.That(Names(name), Does.Not.Contain("Transform.get_position"), name);
        }

        // GetSpeed 0x5CA740 evaluates the blend curve (+0x60) at the influence data's time in contact (+0x28)
        // and multiplies by the speed (+0x50, mulss 0x5ca759). Other data fields are not read.
        [Test]
        public void SpeedIsTheBlendCurveAtTheTimeInContactTimesTheSpeed()
        {
            WithInfluencer<CharacterDirectionInfluencer>((owned, character, influencer) =>
            {
                Set(influencer, "m_speed", 4f);
                Set(influencer, "m_speedBlendCurve", AnimationCurve.Linear(0f, 0f, 2f, 1f));
                var data = new DirectionInfluenceData
                {
                    CurrentBlendTimeSeconds = 1f,
                    TargetVelocity = new Vector3(9f, 9f, 9f),
                    CurrentVelocity = new Vector3(8f, 8f, 8f),
                };

                Assert.That((float)Call(influencer, "GetSpeed", character, data), Is.EqualTo(2f).Within(1e-6f));
                data.CurrentBlendTimeSeconds = 2f;
                Assert.That((float)Call(influencer, "GetSpeed", character, data), Is.EqualTo(4f).Within(1e-6f));
                Set(influencer, "m_speed", -1f);
                Assert.That((float)Call(influencer, "GetSpeed", character, data), Is.EqualTo(-1f).Within(1e-6f), "a negative speed is kept");
            });
        }

        // The falloff GetSpeed 0x5CA970 snaps the character position to this transform on the enabled axes
        // (SnapToAxis.Snap, 0x5ca9c2), takes its distance to this transform's position, and multiplies the base
        // speed (blend curve times speed, evaluated first, 0x5caa4f..0x5caa54) by the distance curve at that
        // distance (0x5caa6c, mulss 0x5caa71).
        [Test]
        public void FalloffSpeedScalesTheBaseSpeedByTheCurveAtTheSnappedDistance()
        {
            WithInfluencer<CharacterDirectionInfluencerFalloff>((owned, character, influencer) =>
            {
                Set(influencer, "m_speed", 5f);
                Set(influencer, "m_normalisedSpeedOverDistance", AnimationCurve.Linear(0f, 1f, 10f, 0f));
                SetActorProperty(character, "WorldPosition", new Vector3(3f, 4f, 6f));
                var data = new DirectionInfluenceData();
                float Speed() => (float)Call(influencer, "GetSpeed", character, data);

                // No axis snapped: the distance is sqrt(9 + 16 + 36) from the origin.
                Assert.That(Speed(), Is.EqualTo(5f * (1f - Mathf.Sqrt(61f) / 10f)).Within(1e-4f), "unsnapped");

                Set(influencer, "m_distanceCheckSnap", new SnapToAxis { SnapToX = true, SnapToY = true });
                Assert.That(Speed(), Is.EqualTo(2f).Within(1e-4f), "x and y snapped: distance 6");

                influencer.transform.position = new Vector3(1f, 2f, 3f);
                Assert.That(Speed(), Is.EqualTo(3.5f).Within(1e-4f), "distance 3 from the moved transform");

                Set(influencer, "m_speedBlendCurve", AnimationCurve.Linear(0f, 0f, 1f, 2f));
                data.CurrentBlendTimeSeconds = 0.25f;
                Assert.That(Speed(), Is.EqualTo(5f * 0.5f * 0.7f).Within(1e-4f), "the base speed follows the blend curve");
            });
        }

        // The standard SmoothDamp critically damped spring (Unity's published algorithm) for one axis with
        // no speed limit, used as the oracle for the callbacks below.
        private static float SmoothDamp(float current, float target, ref float velocity, float smoothTime, float deltaTime)
        {
            smoothTime = Mathf.Max(0.0001f, smoothTime);
            float omega = 2f / smoothTime;
            float x = omega * deltaTime;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = current - target;
            float temp = (velocity + omega * change) * deltaTime;
            velocity = (velocity - omega * temp) * exp;
            float output = target + (change + temp) * exp;
            if (target - current > 0f == output > target)
            {
                output = target;
                velocity = (output - target) / deltaTime;
            }
            return output;
        }

        private static int OverrideKey => (int)DirectionModifierType.Override;
        private static int MaxVelocityKey => (int)GameplayModifierType.MaxVelocityFromInfluence;

        // A character with a registered TimeManager (Environment at twice the fixed step) and an influencer
        // pushing forward at speed 3, 0.1 s of smoothing, the Override modifier type and a velocity cap of 10.
        private static void WithTimedInfluencer(Action<Owned, Character, CharacterDirectionInfluencer, float> body) =>
            WithInfluencer<CharacterDirectionInfluencer>((owned, character, influencer) =>
            {
                PublishTimeManager(owned, Get<SystemRef<TimeManager>>(influencer, "m_timeManagerRef"),
                    NewTimeManager(owned, TimeCategory.Environment, 2f));
                Set(influencer, "m_speed", 3f);
                Set(influencer, "m_smoothDampTimeSeconds", 0.1f);
                Set(influencer, "m_directionModiferType", DirectionModifierType.Override);
                Set(influencer, "m_maxResultantVelocity", 10f);
                body(owned, character, influencer, Time.fixedDeltaTime * 2f);
            });

        // DoTriggerEnter 0x5C9F50: registers a new data entry for the character, computes the influence
        // (direction forward, target = direction * speed, SmoothDamp from rest towards it with the influencer's
        // own damp velocity, then times the category time scale 2), adds it under the configured modifier type
        // (0x5ca0d1) and a second override of direction * max velocity under MaxVelocityFromInfluence
        // (esi=0xcbf3202f, 0x5ca118), keeping both handles in the data.
        [Test]
        public void EnterRegistersTheCharacterAndAddsTheInfluenceAndTheVelocityCap()
        {
            WithTimedInfluencer((owned, character, influencer, deltaTime) =>
            {
                Call(influencer, "DoTriggerEnter", character);

                var entries = Get<Dictionary<Character, DirectionInfluenceData>>(influencer, "m_influencingCharacters");
                Assert.That(entries.Keys, Is.EqualTo(new[] { character }));
                DirectionInfluenceData data = entries[character];
                float velocity = 0f;
                float current = SmoothDamp(0f, 3f, ref velocity, 0.1f, deltaTime);
                AssertVector(new Vector3(0f, 0f, 3f), data.TargetVelocity, 1e-6f, "target");
                AssertVector(new Vector3(0f, 0f, current), data.CurrentVelocity, 1e-4f, "eased velocity");
                AssertVector(new Vector3(0f, 0f, velocity), Get<Vector3>(influencer, "m_dampVelocity"), 1e-3f, "damp velocity");
                Assert.That(data.CurrentBlendTimeSeconds, Is.EqualTo(0f));
                Assert.That(data.InfluenceModifierHandle, Is.Not.Null);
                Assert.That(data.MaxVelocityModifierHandle, Is.Not.Null);
                Assert.That(data.MaxVelocityModifierHandle, Is.Not.SameAs(data.InfluenceModifierHandle));

                StackableData modifiers = Modifiers(character);
                Assert.That(modifiers.GetStack<Vector3>(OverrideKey, new List<Vector3>()).Count, Is.EqualTo(1));
                AssertVector(new Vector3(0f, 0f, current * 2f), modifiers.Get<Vector3>(OverrideKey, true, default), 1e-4f, "influence");
                Assert.That(modifiers.GetStack<Vector3>(MaxVelocityKey, new List<Vector3>()), Is.EqualTo(new[] { new Vector3(0f, 0f, 10f) }));
                Assert.That(modifiers.GetOverride<Vector3>(data.InfluenceModifierHandle, OverrideKey).z, Is.EqualTo(current * 2f).Within(1e-4f));
            });
        }

        // The dictionary Add 0x5ca023 faults for a character that is already registered, before any modifier is added.
        [Test]
        public void EnteringTwiceFaultsOnTheDuplicateKeyBeforeAddingModifiers()
        {
            WithTimedInfluencer((owned, character, influencer, deltaTime) =>
            {
                Call(influencer, "DoTriggerEnter", character);
                Assert.Throws<ArgumentException>(() => Call(influencer, "DoTriggerEnter", character));
                Assert.That(Modifiers(character).GetStack<Vector3>(OverrideKey, new List<Vector3>()).Count, Is.EqualTo(1));
            });
        }

        // DoTriggerStay 0x5CA370 adds the fixed step to the time in contact (addss 0x5ca423) before the speed is
        // read, eases from the stored velocity with the same damp velocity, and replaces the held influence
        // override in place (AdjustModifierOverrides with the stored handle, 0x5ca4d6); the velocity cap is
        // not touched.
        [Test]
        public void StayAdvancesTheTimeInContactAndUpdatesTheHeldInfluenceInPlace()
        {
            WithTimedInfluencer((owned, character, influencer, deltaTime) =>
            {
                Set(influencer, "m_speedBlendCurve", AnimationCurve.Linear(0f, 1f, 10f, 11f));
                Call(influencer, "DoTriggerEnter", character);
                DirectionInfluenceData data = Get<Dictionary<Character, DirectionInfluenceData>>(influencer, "m_influencingCharacters")[character];
                StackableDataHandle influenceHandle = data.InfluenceModifierHandle;
                StackableDataHandle capHandle = data.MaxVelocityModifierHandle;
                float velocity = 0f;
                float afterEnter = SmoothDamp(0f, 3f * 1f, ref velocity, 0.1f, deltaTime);

                Call(influencer, "DoTriggerStay", character);

                Assert.That(data.CurrentBlendTimeSeconds, Is.EqualTo(deltaTime).Within(1e-6f));
                // The curve (1 + t) at the advanced time gives the speed 3 * (1 + dt); a speed read before the
                // advance would give 3.
                float target = 3f * (1f + deltaTime);
                AssertVector(new Vector3(0f, 0f, target), data.TargetVelocity, 1e-4f, "target");
                float afterStay = SmoothDamp(afterEnter, target, ref velocity, 0.1f, deltaTime);
                AssertVector(new Vector3(0f, 0f, afterStay), data.CurrentVelocity, 1e-3f, "eased velocity");
                Assert.That(data.InfluenceModifierHandle, Is.SameAs(influenceHandle));
                Assert.That(data.MaxVelocityModifierHandle, Is.SameAs(capHandle));
                StackableData modifiers = Modifiers(character);
                Assert.That(modifiers.GetStack<Vector3>(OverrideKey, new List<Vector3>()).Count, Is.EqualTo(1), "replaced, not stacked");
                Assert.That(modifiers.GetOverride<Vector3>(influenceHandle, OverrideKey).z, Is.EqualTo(afterStay * 2f).Within(2e-3f));
                Assert.That(modifiers.GetStack<Vector3>(MaxVelocityKey, new List<Vector3>()), Is.EqualTo(new[] { new Vector3(0f, 0f, 10f) }));

                var other = NewCharacter(owned, new FSMStorage(0));
                Assert.Throws<KeyNotFoundException>(() => Call(influencer, "DoTriggerStay", other), "an unregistered character");
            });
        }

        // DoTriggerExit 0x5CA4F0 removes both overrides the entry holds (RemoveModifierOverrides 0x5ca55a,
        // 0x5ca568) and then the entry (Dictionary.Remove, 0x5ca58b).
        [Test]
        public void ExitReleasesBothOverridesAndForgetsTheCharacter()
        {
            WithTimedInfluencer((owned, character, influencer, deltaTime) =>
            {
                Call(influencer, "DoTriggerEnter", character);
                StackableData modifiers = Modifiers(character);
                Assert.That(modifiers.HasData(OverrideKey), Is.True);
                Assert.That(modifiers.HasData(MaxVelocityKey), Is.True);

                Call(influencer, "DoTriggerExit", character);

                Assert.That(modifiers.HasData(OverrideKey), Is.False);
                Assert.That(modifiers.HasData(MaxVelocityKey), Is.False);
                Assert.That(Get<Dictionary<Character, DirectionInfluenceData>>(influencer, "m_influencingCharacters"), Is.Empty);
                Assert.Throws<KeyNotFoundException>(() => Call(influencer, "DoTriggerExit", character), "no entry any more");
            });
        }

        // The collision callbacks 0x5CA770, 0x5CA7C0 and 0x5CA810 resolve the character from the collision's collider
        // (CharacterTrigger.TryGetCharacter 0x060017b2) and forward it to the matching overridable trigger callback
        // (vtable slots 6, 7 and 8) only when one is found. A Collision cannot be built here, so the order of
        // the calls is witnessed on the compiled bodies.
        [Test]
        public void CollisionCallbacksForwardTheFoundCharacterToTheTriggerCallbacks()
        {
            (string callback, string forwarded)[] table =
            {
                ("OnCollisionEnter", "DoTriggerEnter"),
                ("OnCollisionStay", "DoTriggerStay"),
                ("OnCollisionExit", "DoTriggerExit"),
            };
            foreach ((string callback, string forwarded) in table)
            {
                string[] names = OriginalILCallSequence.Names(
                    OriginalILCallSequence.Calls(typeof(CharacterDirectionInfluencer).GetMethod(callback, Declared)));
                Assert.That(names, Is.EqualTo(new[] { "Collision.get_collider", "CharacterTrigger.TryGetCharacter", "CharacterTrigger." + forwarded }), callback);
            }
        }
    }
}
