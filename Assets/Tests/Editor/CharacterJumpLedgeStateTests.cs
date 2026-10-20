using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The six character states of the jump, ledge and lightspeed dash cluster: half pipe transfer, jump (with its shared
    // base and the one shot), knockback recovery, ledge mount and lightspeed dash. Expected values come from the 1.10.1
    // x86-64 bodies cited per test. The enter, update and leave bodies run their bases first, and those reach the
    // camera, form, stamina and animation graphs of a started Character, so they are checked as call sequences read from
    // the compiled IL; the bodies that can run on their own (the jump impulse, the dash proxy and target helpers) run.
    public sealed class CharacterJumpLedgeStateTests
    {
        private static FiniteStateMachine Machine() => new FiniteStateMachine("Lucid-jump-" + Guid.NewGuid().ToString("N"), skipAddToManager: true);

        private static IFSMState NewState(string node, string json) =>
            FSMClassFactory.ConstructState(Machine(), node, new FSMIdentifier("Lucid-state-" + Guid.NewGuid().ToString("N")), json);

        private static void WithCharacter(Action<Owned, CharacterGravityVectorProbe> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<GameplayIslandManager>();
            body(owned, NewCharacter(owned));
        });

        private static Type[] GenericArguments(List<MethodBase> calls, string name) =>
            calls.Where(call => call.Name == name).SelectMany(call => ((MethodInfo)call).GetGenericArguments()).ToArray();

        // ---- Construction from JSON ----

        // 0x77ACF0 and 0x77AE60 build the state from its JSON; the shared jump base (0xE74010) keeps the air movement ability
        // type (+0x3C) and hashes the optional animation bool name (+0x40) only when it is not null or white space.
        [Test]
        public void JumpStatesReadTheirArgumentsFromJson()
        {
            foreach (string node in new[] { nameof(CharacterState_Jump), nameof(CharacterState_JumpOneShot) })
            {
                IFSMState state = NewState(node, "{\"AbilityType\":" + (int)ActorAbilityType.Character_Jump + ",\"AirMovementAbilityType\":"
                    + (int)ActorAbilityType.Character_MovementAir + ",\"BallToBallAnimationBool\":\"BallToBall\"}");
                Assert.That(state.GetType().Name, Is.EqualTo(node));
                Assert.That(Get<ActorAbilityType>(state, "m_airMovementAbilityType"), Is.EqualTo(ActorAbilityType.Character_MovementAir), node);
                Assert.That(Get<int>(state, "m_ballToBallAnimationBoolId"), Is.EqualTo(Animator.StringToHash("BallToBall")), node);
                Assert.That(typeof(ActorState_Ability).GetProperty("m_abilityType", Instance).GetValue(state), Is.EqualTo(ActorAbilityType.Character_Jump), node);

                foreach (string json in new[] { "{}", "{\"BallToBallAnimationBool\":\"\"}", "{\"BallToBallAnimationBool\":\"  \"}" })
                {
                    IFSMState bare = NewState(node, json);
                    Assert.That(Get<int>(bare, "m_ballToBallAnimationBoolId"), Is.EqualTo(0), node + " " + json);
                    Assert.That(Get<ActorAbilityType>(bare, "m_airMovementAbilityType"), Is.EqualTo((ActorAbilityType)0), node + " " + json);
                }
            }
        }

        // The other four states take the plain ability arguments (0x778D90, 0x77B260, 0x77B540, 0x77BFE0).
        [Test]
        public void TheOtherStatesTakeTheAbilityTypeFromTheirJson()
        {
            foreach (Type type in new[]
            {
                typeof(CharacterState_HalfPipeTransfer), typeof(CharacterState_KnockbackRecovery), typeof(CharacterState_LedgeMount), typeof(CharacterState_LightspeedDash),
            })
            {
                IFSMState state = NewState(type.Name, "{\"AbilityType\":" + (int)ActorAbilityType.Character_LedgeMount + "}");
                Assert.That(state.GetType(), Is.EqualTo(type));
                Assert.That(typeof(ActorState_Ability).GetProperty("m_abilityType", Instance).GetValue(state), Is.EqualTo(ActorAbilityType.Character_LedgeMount), type.Name);
            }
        }

        // ---- CharacterState_JumpBase.Jump (0xE75110) ----

        private static void WithJumpState(Action<Owned, CharacterGravityVectorProbe, IFSMState, CharacterAbilityDefinition_Jump> body) => WithCharacter((owned, character) =>
        {
            var definition = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_Jump>());
            body(owned, character, NewState(nameof(CharacterState_Jump), "{}"), definition);
        });

        private static Vector3 Jump(Character character, IFSMState state, CharacterAbilityDefinition_Jump definition, float timer, float maxTime)
        {
            SetActorProperty(character, "WorldVelocity", new Vector3(3f, 0f, 0f));
            character.Storage.SetValue(ActorFSMKeys.JumpDirection, new Vector3(0f, 1f, 0f));
            Call(state, "Jump", character, definition, 0.02f, timer, maxTime);
            return character.WorldVelocity;
        }

        // 0xE75110: a jump timer below zero (ucomiss 0 / jbe, 0xE75166) applies nothing; otherwise the velocity gains the
        // stored jump direction scaled by the power curve at timer over the maximum time (0xE75253 divss, 0xE75261
        // AnimationCurve.Evaluate), times the power (+0xE8), times the step.
        [Test]
        public void JumpAddsThePowerCurveAlongTheJumpDirection()
        {
            WithJumpState((owned, character, state, definition) =>
            {
                definition.JumpPower = 10f;
                definition.JumpPowerCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                Assert.That(Jump(character, state, definition, -0.1f, 0.6f), Is.EqualTo(new Vector3(3f, 0f, 0f)), "a negative timer applies nothing");
                Assert.That(Vector3.Distance(Jump(character, state, definition, 0f, 0.6f), new Vector3(3f, 0f, 0f)), Is.LessThan(1e-6f), "the curve starts at zero");
                Assert.That(Vector3.Distance(Jump(character, state, definition, 0.3f, 0.6f), new Vector3(3f, 0.1f, 0f)), Is.LessThan(1e-6f), "half way: 0.5 * 10 * 0.02");
                Assert.That(Vector3.Distance(Jump(character, state, definition, 0.6f, 0.6f), new Vector3(3f, 0.2f, 0f)), Is.LessThan(1e-6f), "the end of the curve: 1 * 10 * 0.02");
                definition.JumpPowerCurve = AnimationCurve.Constant(0f, 1f, 0.5f);
                Assert.That(Vector3.Distance(Jump(character, state, definition, 0f, 0.6f), new Vector3(3f, 0.1f, 0f)), Is.LessThan(1e-6f), "a zero timer is applied");
            });
        }

        // ---- CharacterState_LightspeedDash helpers ----

        private static IFSMState NewDashState() => NewState(nameof(CharacterState_LightspeedDash), "{}");

        // 0x77D680: the dash target is removed from the character's storage (as an ISpline, but the key alone decides).
        [Test]
        public void ClearingTheTargetSplineRemovesTheDashTarget()
        {
            WithCharacter((owned, character) =>
            {
                InertSpline spline = owned.Host("Lucid dash spline", false).AddComponent<InertSpline>();
                character.Storage.SetValue<ISpline>(ActorFSMKeys.LightspeedDashTarget, spline);
                character.Storage.SetValue(ActorFSMKeys.LightspeedDashInvulnerable, true);
                Call(NewDashState(), "ClearTargetSpline", character.Storage);
                Assert.That(character.Storage.HasValue(ActorFSMKeys.LightspeedDashTarget), Is.False);
                Assert.That(character.Storage.HasValue(ActorFSMKeys.LightspeedDashInvulnerable), Is.True, "nothing else is removed");
                Call(NewDashState(), "ClearTargetSpline", character.Storage);
            });
        }

        // 0x77D550 and 0x77D9E0: the visual proxy is made to follow the character's rotation followed by a quarter turn about
        // the right axis (0x77D5A6 AngleAxis 90 about Vector3.right; the multiplication keeps the character rotation first),
        // and releasing the proxy stops overriding it. A character turned 90 degrees about up and the quarter turn about
        // right give a proxy whose forward is down (0, -1, 0) and whose up is the character's right (1, 0, 0).
        [Test]
        public void TheDashProxyIsTippedAQuarterTurnAndLaterReleased()
        {
            WithCharacter((owned, character) =>
            {
                var proxy = owned.Host("Lucid dash proxy").AddComponent<SmoothedTransformProxy>();
                Call(proxy, "Awake"); // Edit Mode does not run Awake, which caches the transform the proxy rotates
                SetActorProperty(character, "VisualProxy", proxy);
                SetActorProperty(character, "WorldRotation", Quaternion.Euler(0f, 90f, 0f));
                IFSMState state = NewDashState();

                Call(state, "OverrideProxy", character);
                Quaternion rotation = proxy.transform.rotation;
                Assert.That(Get<bool>(proxy, "m_rotationOverriden"), Is.True);
                Assert.That(Vector3.Distance(rotation * Vector3.forward, new Vector3(0f, -1f, 0f)), Is.LessThan(1e-5f));
                Assert.That(Vector3.Distance(rotation * Vector3.up, new Vector3(1f, 0f, 0f)), Is.LessThan(1e-5f));

                Call(state, "ResetProxy", character);
                Assert.That(Get<bool>(proxy, "m_rotationOverriden"), Is.False);
                Assert.That(proxy.transform.rotation, Is.EqualTo(rotation), "the pose is left as it was");
            });
        }

        // ---- IL witnesses ----

        // 0xE74070: the form is read before the base entry and again after it, and the ball to ball bool uses both reads; the
        // character leaves the ground, jumping is flagged, the direction is read, and only then is the velocity written; a face
        // direction request projects the velocity onto the up plane and turns the character.
        [Test]
        public void JumpEntryFollowsTheNativeOrder()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_JumpBase<,>), "DoOnEnter");
            int firstForm = IndexOf(calls, "Actor", "get_FormType");
            int baseEnter = IndexOf(calls, "CharacterState_Ability", "DoOnEnter");
            int secondForm = IndexOf(calls, "Actor", "get_FormType", firstForm + 1);
            int animation = IndexOf(calls, "ActorAnimator", "SetAnimation");
            int away = IndexOf(calls, "Character", "OnMoveAwayFromGround");
            int jumping = IndexOf(calls, "IGraphStorage", "SetValue");
            int ability = IndexOf(calls, "ActorState_Ability", "GetAbility");
            int velocity = IndexOf(calls, "Character", "SetWorldVelocity");
            int project = IndexOf(calls, "Vector3", "ProjectOnPlane");
            int orientate = IndexOf(calls, "Actor", "OrientateToPlaneWithLookDirection");
            Assert.That(new[] { firstForm, baseEnter, secondForm, animation, away, jumping, ability, velocity, project, orientate }, Is.Ordered.Ascending.And.Unique);
            Assert.That(Count(calls, "Actor", "get_FormType"), Is.EqualTo(2));
            Assert.That(Count(calls, "Vector3", "Dot"), Is.EqualTo(2), "the connection velocity and the upward speed");
            Assert.That(Count(calls, "Vector3", "op_Equality") + Count(calls, "Vector3", "op_Inequality"), Is.EqualTo(1), "the planar velocity is compared with zero");
            Assert.That(GenericArguments(calls, "SetValue"), Is.EqualTo(new[] { typeof(bool) }));
            Assert.That(GenericArguments(calls, "GetValue"), Is.EqualTo(new[] { typeof(Vector3), typeof(bool) }));
        }

        // 0xE74720: the ability and the air movement ability are fetched, the air control is processed, the tracker is clamped,
        // the jump timer advances, and either the jump continues (the impulse, then the rail jump tracker or the off tracker
        // update, then the ground check that raises the exited ground flag) or jumping is cleared.
        [Test]
        public void JumpUpdateFollowsTheNativeOrder()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_JumpBase<,>), "DoUpdate");
            int baseUpdate = IndexOf(calls, "CharacterState_Ability", "DoUpdate");
            int ability = IndexOf(calls, "ActorState_Ability", "GetAbility");
            int air = IndexOf(calls, "Actor", "GetAbility");
            int heading = IndexOf(calls, "CharacterState_Ability", "MaintainHeading");
            int deceleration = IndexOf(calls, "IGraphStorage", "GetValue");
            int control = IndexOf(calls, "CharacterMovementUtilities", "ProcessAirControl");
            int clamp = IndexOf(calls, "CharacterTracker", "ClampToLateralBoundsViaSurfaceRaycast");
            int timer = IndexOf(calls, "IGraphStorageExtensions", "AdjustValue");
            int jump = IndexOf(calls, "CharacterState_JumpBase", "Jump");
            int rail = IndexOf(calls, "Character", "UpdateJumpOnRailTracker");
            int off = IndexOf(calls, "CharacterTracker", "UpdateOffTracker");
            int ground = IndexOf(calls, "CharacterTracking", "IsOnGround");
            Assert.That(new[] { baseUpdate, ability, air, heading, deceleration, control, clamp, timer, jump, rail, off, ground }, Is.Ordered.Ascending.And.Unique);
            Assert.That(Count(calls, "IGraphStorage", "SetValue"), Is.EqualTo(2), "jumping cleared, exited ground raised");
            Assert.That(GenericArguments(calls, "SetValue"), Is.EqualTo(new[] { typeof(bool), typeof(bool) }));
        }

        // 0xE74C20: the base leave, the ability, the animation bool cleared, the jump ended (EndJump inlined at 0xE74CF9), the
        // jump and air impulses ended, and the jump keys cleaned in the native order.
        [Test]
        public void JumpLeaveFollowsTheNativeOrder()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_JumpBase<,>), "DoOnLeave");
            int baseLeave = IndexOf(calls, "CharacterState_Ability", "DoOnLeave");
            int ability = IndexOf(calls, "ActorState_Ability", "GetAbility");
            int animation = IndexOf(calls, "ActorAnimator", "SetAnimation");
            int end = IndexOf(calls, "CharacterAbility_MovementJumpBase", "EndJump");
            int jumpImpulse = IndexOf(calls, "Character", "EndJumpImpulses");
            int airImpulse = IndexOf(calls, "Character", "EndAirActiveImpulse");
            int store = IndexOf(calls, "IGraphStorage", "SetValue");
            int remove = IndexOf(calls, "IGraphStorage", "RemoveValue");
            Assert.That(new[] { baseLeave, ability, animation, end, jumpImpulse, airImpulse, store, remove }, Is.Ordered.Ascending.And.Unique);
            Assert.That(GenericArguments(calls, "SetValue"), Is.EqualTo(new[] { typeof(float), typeof(bool) }), "elapsed time zeroed, jumping cleared");
            Assert.That(GenericArguments(calls, "RemoveValue"), Is.EqualTo(new[] { typeof(Vector3), typeof(bool), typeof(bool), typeof(bool) }),
                "direction, exited ground, deceleration disabled, face velocity direction");
        }

        // 0x778E50, 0x779070 and 0x779540: the transfer enters with the up direction and a zero rotation time, rotates the
        // character from the entry normal to the hit normal by two look rotations and a slerp once the transfer is active, and
        // leaves by removing the normal as a Quaternion (the entry stored a Vector3) and the rotation time and clearing the flag.
        [Test]
        public void HalfPipeTransferStateFollowsTheNativeOrder()
        {
            List<MethodBase> enter = Calls(typeof(CharacterState_HalfPipeTransfer), "DoOnEnter");
            Assert.That(IndexOf(enter, "CharacterState_HalfPipe", "DoOnEnter"), Is.LessThan(IndexOf(enter, "Actor", "get_UpDirection")));
            Assert.That(GenericArguments(enter, "SetValue"), Is.EqualTo(new[] { typeof(Vector3), typeof(float) }));

            List<MethodBase> update = Calls(typeof(CharacterState_HalfPipeTransfer), "DoUpdate");
            int baseUpdate = IndexOf(update, "CharacterState_HalfPipe", "DoUpdate");
            int active = IndexOf(update, "IGraphStorage", "GetValue");
            int ability = IndexOf(update, "ActorState_Ability", "GetAbility");
            int min = IndexOf(update, "Mathf", "Min");
            int store = IndexOf(update, "IGraphStorage", "SetValue");
            int look = IndexOf(update, "Quaternion", "LookRotation");
            int slerp = IndexOf(update, "Quaternion", "Slerp");
            int rotation = IndexOf(update, "Character", "SetWorldRotation");
            Assert.That(new[] { baseUpdate, active, ability, min, store, look, slerp, rotation }, Is.Ordered.Ascending.And.Unique);
            Assert.That(Count(update, "Quaternion", "LookRotation"), Is.EqualTo(2));
            Assert.That(Count(update, "Character", "get_WorldVelocity"), Is.EqualTo(1), "read once for both look rotations");
            Assert.That(GenericArguments(update, "GetValue"), Is.EqualTo(new[] { typeof(bool), typeof(float), typeof(Vector3) }));

            List<MethodBase> leave = Calls(typeof(CharacterState_HalfPipeTransfer), "DoOnLeave");
            Assert.That(IndexOf(leave, "CharacterState_HalfPipe", "DoOnLeave"), Is.LessThan(IndexOf(leave, "IGraphStorage", "RemoveValue")));
            Assert.That(GenericArguments(leave, "RemoveValue"), Is.EqualTo(new[] { typeof(Quaternion), typeof(float) }));
            Assert.That(GenericArguments(leave, "SetValue"), Is.EqualTo(new[] { typeof(bool) }));
        }

        // 0x77B320 and 0x77B3C0: recovery starts on entering; the update fetches the ability before it reads the local velocity,
        // and only then clamps the backward speed with Mathf.Min and writes the local velocity back.
        [Test]
        public void KnockbackRecoveryStateFollowsTheNativeOrder()
        {
            List<MethodBase> enter = Calls(typeof(CharacterState_KnockbackRecovery), "DoOnEnter");
            int baseEnter = IndexOf(enter, "CharacterState_Ability", "DoOnEnter");
            int ability = IndexOf(enter, "ActorState_Ability", "GetAbility");
            int recovery = IndexOf(enter, "CharacterAbility_Knockback", "EnterRecovery");
            Assert.That(new[] { baseEnter, ability, recovery }, Is.Ordered.Ascending.And.Unique);

            List<MethodBase> update = Calls(typeof(CharacterState_KnockbackRecovery), "DoUpdate");
            int baseUpdate = IndexOf(update, "CharacterState_Ability", "DoUpdate");
            int fetched = IndexOf(update, "ActorState_Ability", "GetAbility");
            int velocity = IndexOf(update, "Actor", "get_LocalVelocity");
            int definition = IndexOf(update, "CharacterAbility", "get_Definition");
            int min = IndexOf(update, "Mathf", "Min");
            int set = IndexOf(update, "Actor", "SetLocalVelocity");
            Assert.That(new[] { baseUpdate, fetched, velocity, definition, min, set }, Is.Ordered.Ascending.And.Unique);
        }

        // 0x77B600, 0x77B890 and 0x77BC20: the mount orients the character to the ledge surface, places it on the mount point
        // and resets the elapsed and finished keys; the update holds the character still, advances the elapsed time and raises
        // the finished flag; the leave applies the mount velocity, invalidates the last ground hit and removes both keys
        // through the user's own storage.
        [Test]
        public void LedgeMountStateFollowsTheNativeOrder()
        {
            List<MethodBase> enter = Calls(typeof(CharacterState_LedgeMount), "DoOnEnter");
            int baseEnter = IndexOf(enter, "CharacterState_Ability", "DoOnEnter");
            int ability = IndexOf(enter, "ActorState_Ability", "GetAbility");
            int orientate = IndexOf(enter, "Actor", "OrientateToPlaneWithLookDirection");
            int position = IndexOf(enter, "Character", "SetWorldPosition");
            int store = IndexOf(enter, "IGraphStorage", "SetValue");
            Assert.That(new[] { baseEnter, ability, orientate, position, store }, Is.Ordered.Ascending.And.Unique);
            Assert.That(GenericArguments(enter, "SetValue"), Is.EqualTo(new[] { typeof(float), typeof(bool) }));

            List<MethodBase> update = Calls(typeof(CharacterState_LedgeMount), "DoUpdate");
            int baseUpdate = IndexOf(update, "CharacterState_Ability", "DoUpdate");
            int still = IndexOf(update, "Character", "SetWorldVelocity");
            int read = IndexOf(update, "IGraphStorage", "GetValue");
            int advance = IndexOf(update, "IGraphStorage", "SetValue");
            int pause = IndexOf(update, "CharacterAbility", "get_Definition");
            int finish = IndexOf(update, "IGraphStorage", "SetValue", advance + 1);
            Assert.That(new[] { baseUpdate, still, read, advance, pause, finish }, Is.Ordered.Ascending.And.Unique);
            Assert.That(GenericArguments(update, "SetValue"), Is.EqualTo(new[] { typeof(float), typeof(bool) }));

            List<MethodBase> leave = Calls(typeof(CharacterState_LedgeMount), "DoOnLeave");
            int baseLeave = IndexOf(leave, "CharacterState_Ability", "DoOnLeave");
            int velocity = IndexOf(leave, "Actor", "SetLocalVelocity");
            int invalidate = IndexOf(leave, "RaycastColliderHit", "Invalidate");
            int remove = IndexOf(leave, "IGraphStorage", "RemoveValue");
            Assert.That(new[] { baseLeave, velocity, invalidate, remove }, Is.Ordered.Ascending.And.Unique);
            Assert.That(Count(leave, "IGraphUser", "get_Storage"), Is.EqualTo(2));
            Assert.That(GenericArguments(leave, "RemoveValue"), Is.EqualTo(new[] { typeof(float), typeof(bool) }));
        }

        // 0x77C0A0: the character is put on the surface tracker, teleported to its position, subscribed to the surface
        // changes, turned from its velocity, given the tracker's velocity at its current speed, and told about the surface it is
        // on before the invulnerability is recorded. The proxy is not touched on entry.
        [Test]
        public void LightspeedDashEntryFollowsTheNativeOrder()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_LightspeedDash), "DoOnEnter");
            int baseEnter = IndexOf(calls, "CharacterState_Ability", "DoOnEnter");
            int tracking = IndexOf(calls, "Character", "SetTrackingType");
            int teleport = IndexOf(calls, "CharacterTracker", "OnTeleport");
            int subscribe = IndexOf(calls, "Delegate", "Combine");
            int turn = IndexOf(calls, "Character", "DetectTurnAroundFromVelocity");
            int rotation = IndexOf(calls, "CharacterTracker", "get_TrackerRotation");
            int velocity = IndexOf(calls, "Character", "SetWorldVelocity");
            int ability = IndexOf(calls, "ActorState_Ability", "GetAbility");
            int surface = IndexOf(calls, "CharacterAbility_MovementTracker", "OnSurfaceChanged");
            int invulnerable = IndexOf(calls, "Character", "IsInvulnerable");
            int store = IndexOf(calls, "IGraphStorage", "SetValue");
            Assert.That(new[] { baseEnter, tracking, teleport, subscribe, turn, rotation, velocity, ability, surface, invulnerable, store }, Is.Ordered.Ascending.And.Unique);
            Assert.That(Count(calls, "CharacterState_LightspeedDash", "OverrideProxy"), Is.EqualTo(0));
        }

        // 0x77C9D0: the tracker follows the spline, the forward speed is advanced along the tracker rotation (deceleration is
        // applied unless the auto rail modifier is on), the character takes the velocity and the proxy is overridden; the target
        // spline is cleared when the look ahead leaves the tracker or invulnerability began.
        [Test]
        public void LightspeedDashUpdateFollowsTheNativeOrder()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_LightspeedDash), "DoUpdate");
            int baseUpdate = IndexOf(calls, "CharacterState_Ability", "DoUpdate");
            int ability = IndexOf(calls, "ActorState_Ability", "GetAbility");
            int spline = IndexOf(calls, "CharacterTracker", "UpdateOnTrackerSpline");
            int rotation = IndexOf(calls, "CharacterTracker", "get_TrackerRotation");
            int modifier = IndexOf(calls, "Character", "GetModifierValue");
            int speed = IndexOf(calls, "CharacterAbility_Movement", "UpdateForwardSpeed");
            int velocity = IndexOf(calls, "Character", "SetWorldVelocity");
            int proxy = IndexOf(calls, "CharacterState_LightspeedDash", "OverrideProxy");
            int dash = IndexOf(calls, "IGraphStorage", "GetValue");
            int invulnerable = IndexOf(calls, "Character", "IsInvulnerable");
            int record = IndexOf(calls, "IGraphStorage", "SetValue");
            int lookAhead = IndexOf(calls, "CharacterTracker", "LookAheadIsOnTracker");
            int clear = IndexOf(calls, "CharacterState_LightspeedDash", "ClearTargetSpline");
            Assert.That(new[] { baseUpdate, ability, spline, rotation, modifier, speed, velocity, proxy, dash, invulnerable, record, lookAhead, clear },
                Is.Ordered.Ascending.And.Unique);
            Assert.That(GenericArguments(calls, "GetModifierValue"), Is.EqualTo(new[] { typeof(bool) }));
        }

        // 0x77D780: the ability is told there is no surface, the subscription is withdrawn, tracking is detected again, the
        // proxy released, the target cleared and the invulnerability record removed.
        [Test]
        public void LightspeedDashLeaveFollowsTheNativeOrder()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_LightspeedDash), "DoOnLeave");
            int baseLeave = IndexOf(calls, "CharacterState_Ability", "DoOnLeave");
            int ability = IndexOf(calls, "ActorState_Ability", "GetAbility");
            int surface = IndexOf(calls, "CharacterAbility_MovementTracker", "OnSurfaceChanged");
            int unsubscribe = IndexOf(calls, "Delegate", "Remove");
            int detect = IndexOf(calls, "Character", "DetectTracking");
            int proxy = IndexOf(calls, "CharacterState_LightspeedDash", "ResetProxy");
            int clear = IndexOf(calls, "CharacterState_LightspeedDash", "ClearTargetSpline");
            int remove = IndexOf(calls, "IGraphStorage", "RemoveValue");
            Assert.That(new[] { baseLeave, ability, surface, unsubscribe, detect, proxy, clear, remove }, Is.Ordered.Ascending.And.Unique);
            Assert.That(GenericArguments(calls, "RemoveValue"), Is.EqualTo(new[] { typeof(bool) }));

            List<MethodBase> target = Calls(typeof(CharacterState_LightspeedDash), "ClearTargetSpline");
            Assert.That(GenericArguments(target, "RemoveValue"), Is.EqualTo(new[] { typeof(ISpline) }));
        }

        // 0x77D550: the proxy is fetched, then the character rotation is multiplied by the quarter turn about right.
        [Test]
        public void TheProxyOverrideMultipliesTheCharacterRotationByTheQuarterTurn()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_LightspeedDash), "OverrideProxy");
            int proxy = IndexOf(calls, "Actor", "get_VisualProxy");
            int rotation = IndexOf(calls, "Actor", "get_WorldRotation");
            int axis = IndexOf(calls, "Quaternion", "AngleAxis");
            int multiply = IndexOf(calls, "Quaternion", "op_Multiply");
            int apply = IndexOf(calls, "SmoothedTransformRotator", "OverrideRotation");
            Assert.That(new[] { proxy, rotation, axis, multiply, apply }, Is.Ordered.Ascending.And.Unique);
        }

        // ---- Registration and graph authoring metadata ----

        private static readonly (Type type, string name)[] States =
        {
            (typeof(CharacterState_HalfPipeTransfer), "HalfPipeTransfer"), (typeof(CharacterState_Jump), "Jump"), (typeof(CharacterState_JumpOneShot), "JumpOneShot"),
            (typeof(CharacterState_KnockbackRecovery), "KnockbackRecovery"), (typeof(CharacterState_LedgeMount), "LedgeMount"), (typeof(CharacterState_LightspeedDash), "LightspeedDash"),
        };

        // CAD blobs of the six states: default graph names and the Character menu; the abstract jump base carries neither.
        [Test]
        public void NodesCarryTheirOriginalGraphAuthoringAttributes()
        {
            foreach ((Type type, string name) in States)
            {
                Assert.That(type.GetCustomAttribute<GraphNodeDefaultNameAttribute>(false).DefaultName, Is.EqualTo(name), type.Name);
                Assert.That(type.GetCustomAttribute<GraphNodeMenuFormatAttribute>(false).Format, Is.EqualTo("Character/{0}"), type.Name);
            }
            Assert.That(typeof(CharacterState_JumpBase<,>).GetCustomAttribute<GraphNodeDefaultNameAttribute>(false), Is.Null);
            Assert.That(typeof(CharacterState_JumpBase<,>).GetCustomAttribute<GraphNodeMenuFormatAttribute>(false), Is.Null);
        }

        // The jump states have their own attributed argument classes derived from the generic base's, the other four share the
        // ability argument class; the abstract jump base is not registered.
        [Test]
        public void ClassFactoryRegistersTheStatesWithTheirArgumentTypes()
        {
            foreach ((Type type, string argsName, Type owner) in new[]
            {
                (typeof(CharacterState_HalfPipeTransfer), "AbilityJSONCtorArgs", typeof(ActorState_Ability)),
                (typeof(CharacterState_Jump), "JumpJSONCtorArgs", typeof(CharacterState_Jump)),
                (typeof(CharacterState_JumpOneShot), "JumpOneShotJSONCtorArgs", typeof(CharacterState_JumpOneShot)),
                (typeof(CharacterState_KnockbackRecovery), "AbilityJSONCtorArgs", typeof(ActorState_Ability)),
                (typeof(CharacterState_LedgeMount), "AbilityJSONCtorArgs", typeof(ActorState_Ability)),
                (typeof(CharacterState_LightspeedDash), "AbilityJSONCtorArgs", typeof(ActorState_Ability)),
            })
            {
                Assert.That(FSMClassFactory.States.TryGetValue(type.Name, out JSONFactoryClass factory), Is.True, type.Name);
                Assert.That(factory.ClassType, Is.EqualTo(type), type.Name);
                Assert.That(factory.ConstructInstanceMethod.DeclaringType, Is.EqualTo(type), type.Name);
                Assert.That(factory.JSONCtorArgsType.Name, Is.EqualTo(argsName), type.Name);
                Assert.That(factory.JSONCtorArgsType.DeclaringType, Is.EqualTo(owner), type.Name);
            }
            Assert.That(FSMClassFactory.States.Keys.Any(key => key.StartsWith("CharacterState_JumpBase", StringComparison.Ordinal)), Is.False);
        }

        // Field order and the shape of the generic jump base: two readonly fields, the constraints on its two type
        // arguments, and a nested argument class that adds the air ability type and the animation bool name to the ability
        // argument class.
        [Test]
        public void DeclarationShapeStatesKeepTheirFieldsBasesAndArgumentClasses()
        {
            const BindingFlags declared = Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            FieldInfo[] fields = typeof(CharacterState_JumpBase<,>).GetFields(declared).OrderBy(f => f.MetadataToken).ToArray();
            Assert.That(fields.Select(f => f.Name), Is.EqualTo(new[] { "m_airMovementAbilityType", "m_ballToBallAnimationBoolId" }));
            Assert.That(fields.All(f => f.IsInitOnly && f.IsPrivate), Is.True);
            Assert.That(typeof(CharacterState_JumpBase<,>).IsAbstract, Is.True);
            Type[] constraints = typeof(CharacterState_JumpBase<,>).GetGenericArguments().Select(argument => argument.GetGenericParameterConstraints().Single()).ToArray();
            Assert.That(constraints[0].GetGenericTypeDefinition(), Is.EqualTo(typeof(CharacterAbility_MovementJumpBase<>)));
            Assert.That(constraints[1], Is.EqualTo(typeof(CharacterAbilityDefinition_Jump)));
            Assert.That(typeof(CharacterState_JumpBase<,>).BaseType.GetGenericTypeDefinition(), Is.EqualTo(typeof(CharacterState_Ability<>)));

            Type args = typeof(CharacterState_JumpBase<,>).GetNestedType("JumpBaseJSONCtorArgs", BindingFlags.NonPublic);
            Assert.That(args.IsNestedFamily && args.IsSerializable, Is.True);
            Assert.That(args.GetFields(declared).OrderBy(f => f.MetadataToken).Select(f => f.Name), Is.EqualTo(new[] { "AirMovementAbilityType", "BallToBallAnimationBool" }));
            Assert.That(args.BaseType, Is.EqualTo(typeof(ActorState_Ability).GetNestedType("AbilityJSONCtorArgs", BindingFlags.NonPublic | BindingFlags.Public)));

            Assert.That(typeof(CharacterState_Jump).BaseType, Is.EqualTo(typeof(CharacterState_JumpBase<CharacterAbility_MovementJump, CharacterAbilityDefinition_Jump>)));
            Assert.That(typeof(CharacterState_JumpOneShot).BaseType,
                Is.EqualTo(typeof(CharacterState_JumpBase<CharacterAbility_MovementJumpOneShot, CharacterAbilityDefinition_JumpOneShot>)));
            Assert.That(typeof(CharacterState_HalfPipeTransfer).BaseType,
                Is.EqualTo(typeof(CharacterState_HalfPipe<CharacterAbilityDefinition_HalfPipeTransfer, CharacterAbility_HalfPipeTransfer>)));
            Assert.That(typeof(CharacterState_KnockbackRecovery).BaseType, Is.EqualTo(typeof(CharacterState_Ability<CharacterAbility_Knockback>)));
            Assert.That(typeof(CharacterState_LedgeMount).BaseType, Is.EqualTo(typeof(CharacterState_Ability<CharacterAbility_LedgeMount>)));
            Assert.That(typeof(CharacterState_LightspeedDash).BaseType, Is.EqualTo(typeof(CharacterState_Ability<CharacterAbility_LightspeedDash>)));
            Assert.That(typeof(CharacterState_Jump).GetNestedType("JumpJSONCtorArgs", BindingFlags.NonPublic).BaseType,
                Is.EqualTo(typeof(CharacterState_JumpBase<CharacterAbility_MovementJump, CharacterAbilityDefinition_Jump>).GetNestedType("JumpBaseJSONCtorArgs", BindingFlags.NonPublic)));
        }
    }
}
