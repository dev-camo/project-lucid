using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // CharacterAbility_AirChaosDash (TypeDef 0x020002b6, 0x73EB40..0x742540). The character is a real component on an
    // owned inactive host with the fixed pass marked active, so the world position, rotation and velocity setters
    // store on the Actor as in the native fixed pass. The air state itself (SetAirActive) needs the save data
    // settings of a started character, so Activate is run through a probe that records the virtual instead of
    // running the air base; everything Activate does around that call is native. Not run here: Initialise, DoOnUpdate,
    // DoOnLeave and the snap-on-exit part of SetAirActive (each needs the visual proxy or the air ability base of a
    // started character).
    public sealed class CharacterAirChaosDashAbilityTests
    {
        private const float Tolerance = 1e-3f;
        private static readonly Vector3EqualityComparer Vec = new Vector3EqualityComparer(Tolerance);

        private sealed class DashProbe : CharacterAbility_AirChaosDash
        {
            public readonly List<bool> AirActiveCalls = new List<bool>();

            protected override void SetAirActive(bool active) => AirActiveCalls.Add(active);
        }

        private static void WithDash(Action<Owned, CharacterGravityVectorProbe, DashProbe, CharacterAbilityDefinition_AirChaosDash> body) =>
            WithAbility<DashProbe, CharacterAbilityDefinition_AirChaosDash>(owned => owned.LeaseRow<TimeManager>(), (owned, character, ability, definition) =>
            {
                SetProperty(ability, "DefinitionAirChaos", definition);
                body(owned, character, ability, definition);
            });

        private static List<CharacterAbilityDefinition_Air.AirComponent> Components(CharacterAbility_AirChaosDash ability, int count)
        {
            var components = Get<List<CharacterAbilityDefinition_Air.AirComponent>>(ability, "m_airComponents");
            for (int i = 0; i < count; i++) components.Add(new CharacterAbilityDefinition_Air.AirComponent { Direction = new Vector3(9f, 9f, 9f) });
            return components;
        }

        private static CameraAbilityDefinition CameraDefinitionOverride(CharacterAbility ability) =>
            (CameraAbilityDefinition)typeof(CharacterAbility).GetProperty("CameraDefinitionOverride", Instance).GetValue(ability);

        private static void Face(Character character, float yaw) => character.SetWorldRotation(Quaternion.Euler(0f, yaw, 0f));

        // ---- Activate (0600105f, 0x73EB40) ----

        // 0x73ee4c..0x73ef38: the target is stored relative to nothing, the start rotation is the character's, the
        // total time is the distance over DashSpeed (0x73eca3), and every air component receives the world
        // displacement over that time rotated into character space (the world-to-local quaternion at +0xbc). The
        // character faces +x, so a +z world dash is -x locally. The air state is switched on once (slot 35), the
        // ability is stored under AirAbilityActive and the camera definition is kept (+0x80).
        [Test]
        public void ActivateStoresTheTargetAndAimsEveryAirComponentAtItInCharacterSpace()
        {
            WithDash((owned, character, ability, definition) =>
            {
                character.SetWorldPosition(new Vector3(0f, 0f, 4f));
                Face(character, 90f);
                List<CharacterAbilityDefinition_Air.AirComponent> components = Components(ability, 2);
                var camera = owned.Track(ScriptableObject.CreateInstance<CameraAbilityDefinition>());
                Quaternion targetRotation = Quaternion.Euler(10f, 20f, 30f);
                ability.Activate(AbilityAimTargetType.Air, null, new Vector3(0f, 0f, 14f), targetRotation, Vector3.back, camera, true);
                Assert.That(Get<float>(ability, "m_totalTimeSeconds"), Is.EqualTo(0.1f).Within(1e-6f));
                Assert.That(Get<Vector3>(ability, "m_targetPosition"), Is.EqualTo(new Vector3(0f, 0f, 14f)).Using(Vec));
                Assert.That(Quaternion.Angle(Get<Quaternion>(ability, "m_targetRotation"), targetRotation), Is.LessThan(0.01f));
                Assert.That(Quaternion.Angle(Get<Quaternion>(ability, "m_startRotation"), Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.01f));
                Assert.That(Get<bool>(ability, "m_snapToTarget"), Is.True);
                Assert.That(components[0].Direction, Is.EqualTo(new Vector3(-100f, 0f, 0f)).Using(Vec));
                Assert.That(components[1].Direction, Is.EqualTo(new Vector3(-100f, 0f, 0f)).Using(Vec));
                Assert.That(ability.AirActiveCalls, Is.EqualTo(new[] { true }));
                Assert.That(character.Storage.TryGetValue(ActorFSMKeys.AirAbilityActive, out CharacterAbility_AirChaosDash stored) && ReferenceEquals(stored, ability), Is.True);
                Assert.That(CameraDefinitionOverride(ability), Is.SameAs(camera));
            });
        }

        // 0x73ed94..0x73edc3: a target transform makes the stored position relative to it, and the snap flag is the
        // argument (r15b at 0x73ee5c).
        [Test]
        public void ActivateStoresThePositionRelativeToATargetTransform()
        {
            WithDash((owned, character, ability, definition) =>
            {
                Transform anchor = owned.Host("Lucid chaos dash anchor").transform;
                anchor.position = new Vector3(3f, 0f, 0f);
                ability.Activate(AbilityAimTargetType.Air, anchor, new Vector3(0f, 0f, 14f), Quaternion.identity, Vector3.back, null, false);
                Assert.That(Get<Transform>(ability, "m_targetTransform"), Is.SameAs(anchor));
                Assert.That(Get<Vector3>(ability, "m_targetPosition"), Is.EqualTo(new Vector3(-3f, 0f, 14f)).Using(Vec));
                Assert.That(Get<bool>(ability, "m_snapToTarget"), Is.False);
            });
        }

        // 0x73ecb4..0x73ecca: a wall target needing less than MinimumTime is refused before anything is stored: the
        // total time is zeroed, the air state is not switched on and the target fields keep their values.
        [Test]
        public void ActivateRefusesAWallReachedInLessThanTheMinimumTime()
        {
            WithDash((owned, character, ability, definition) =>
            {
                ability.Activate(AbilityAimTargetType.Wall, null, new Vector3(0f, 0f, 5f), Quaternion.identity, Vector3.back, null, true);
                Assert.That(Get<float>(ability, "m_totalTimeSeconds"), Is.EqualTo(0f));
                Assert.That(ability.AirActiveCalls, Is.Empty);
                Assert.That(Get<Vector3>(ability, "m_targetPosition"), Is.EqualTo(Vector3.zero));
                Assert.That(Get<bool>(ability, "m_snapToTarget"), Is.False);
                Assert.That(CameraDefinitionOverride(ability) == null, Is.True);
            });
        }

        // The comparison at 0x73ecc5 is ucomiss/jbe on MinimumTime against the time, so a wall reached in exactly
        // MinimumTime (10 / 100 = 0.1) is accepted, as is the same short dash to anything that is not a wall.
        [Test]
        public void ActivateAcceptsAWallReachedInExactlyTheMinimumTimeAndAnyOtherShortDash()
        {
            WithDash((owned, character, ability, definition) =>
            {
                ability.Activate(AbilityAimTargetType.Wall, null, new Vector3(0f, 0f, 10f), Quaternion.identity, Vector3.back, null, true);
                Assert.That(ability.AirActiveCalls, Is.EqualTo(new[] { true }));
                ability.Activate(AbilityAimTargetType.Track, null, new Vector3(0f, 0f, 5f), Quaternion.identity, Vector3.back, null, true);
                Assert.That(ability.AirActiveCalls, Is.EqualTo(new[] { true, true }));
                Assert.That(Get<float>(ability, "m_totalTimeSeconds"), Is.EqualTo(0.05f).Within(1e-6f));
            });
        }

        // 0x73ed03..0x73ed74: a wall whose normal is not beyond the cosine of OffsetAngleToWorldUp is pulled back along
        // the dash by OffsetDistance, while the time keeps the full distance (10 / 50), so the speed is 9.5 / 0.2.
        [Test]
        public void ActivateOffsetsAWallTargetBackAlongTheDash()
        {
            WithDash((owned, character, ability, definition) =>
            {
                definition.DashSpeed = 50f;
                definition.CalculateCachedValues();
                Face(character, 90f);
                List<CharacterAbilityDefinition_Air.AirComponent> components = Components(ability, 1);
                character.SetWorldPosition(new Vector3(0f, 0f, 4f));
                ability.Activate(AbilityAimTargetType.Wall, null, new Vector3(0f, 0f, 14f), Quaternion.identity, new Vector3(0f, 0f, -1f), null, true);
                Assert.That(Get<float>(ability, "m_totalTimeSeconds"), Is.EqualTo(0.2f).Within(1e-6f));
                Assert.That(Get<Vector3>(ability, "m_targetPosition"), Is.EqualTo(new Vector3(0f, 0f, 13.5f)).Using(Vec));
                Assert.That(components[0].Direction, Is.EqualTo(new Vector3(-47.5f, 0f, 0f)).Using(Vec));
            });
        }

        // 0x73ed3f: when the cosine of OffsetAngleToWorldUp exceeds the normal's dot with world up (a ceiling) the offset
        // runs along world up instead, moving the target down.
        [Test]
        public void ActivateOffsetsACeilingWallTargetAlongWorldUp()
        {
            WithDash((owned, character, ability, definition) =>
            {
                definition.DashSpeed = 50f;
                definition.CalculateCachedValues();
                Face(character, 90f);
                List<CharacterAbilityDefinition_Air.AirComponent> components = Components(ability, 1);
                character.SetWorldPosition(new Vector3(0f, 0f, 4f));
                ability.Activate(AbilityAimTargetType.Wall, null, new Vector3(0f, 0f, 14f), Quaternion.identity, new Vector3(0f, -1f, 0f), null, true);
                Assert.That(Get<Vector3>(ability, "m_targetPosition"), Is.EqualTo(new Vector3(0f, -0.5f, 14f)).Using(Vec));
                Assert.That(components[0].Direction, Is.EqualTo(new Vector3(-50f, -2.5f, 0f)).Using(Vec));
            });
        }

        // ---- GetTargetPositionAndRotation (06001060, 0x741760) ----

        [Test]
        public void TargetPositionIsRelativeToTheTargetTransformWhenThereIsOne()
        {
            WithDash((owned, character, ability, definition) =>
            {
                Quaternion rotation = Quaternion.Euler(0f, 45f, 0f);
                Set(ability, "m_targetPosition", new Vector3(10f, 0f, 0f));
                Set(ability, "m_targetRotation", rotation);
                var arguments = new object[] { null, null };
                Call(ability, "GetTargetPositionAndRotation", arguments);
                Assert.That((Vector3)arguments[0], Is.EqualTo(new Vector3(10f, 0f, 0f)));
                Assert.That((Quaternion)arguments[1], Is.EqualTo(rotation));
                Transform anchor = owned.Host("Lucid chaos dash anchor").transform;
                anchor.position = new Vector3(1f, 2f, 3f);
                Set(ability, "m_targetTransform", anchor);
                arguments = new object[] { null, null };
                Call(ability, "GetTargetPositionAndRotation", arguments);
                Assert.That((Vector3)arguments[0], Is.EqualTo(new Vector3(11f, 2f, 3f)).Using(Vec));
                Assert.That((Quaternion)arguments[1], Is.EqualTo(rotation));
            });
        }

        // ---- UpdateDirection (06001061, 0x741830) ----

        // 0x7418c4..0x741a85: without a target transform nothing changes; with one the components are aimed along
        // the normalised vector from the character to (transform position + stored position) at DashSpeed, in
        // character space. The character is off the origin and the stored offset is non-zero so that each term shows.
        [Test]
        public void UpdateDirectionAimsTheComponentsAtTheMovingTarget()
        {
            WithDash((owned, character, ability, definition) =>
            {
                character.SetWorldPosition(new Vector3(0f, 0f, 4f));
                Face(character, 90f);
                List<CharacterAbilityDefinition_Air.AirComponent> components = Components(ability, 2);
                Call(ability, "UpdateDirection");
                Assert.That(components[0].Direction, Is.EqualTo(new Vector3(9f, 9f, 9f)), "no target transform: untouched");
                Transform anchor = owned.Host("Lucid chaos dash anchor").transform;
                anchor.position = new Vector3(0f, 0f, 4f);
                Set(ability, "m_targetTransform", anchor);
                Set(ability, "m_targetPosition", new Vector3(2f, 0f, 2f));
                Call(ability, "UpdateDirection");
                Vector3 world = new Vector3(2f, 0f, 2f).normalized * 100f;
                Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, 90f, 0f)) * world;
                Assert.That(local, Is.EqualTo(new Vector3(-70.71f, 0f, 70.71f)).Using(new Vector3EqualityComparer(0.05f)), "hand check of the expectation");
                Assert.That(components[0].Direction, Is.EqualTo(local).Using(Vec));
                Assert.That(components[1].Direction, Is.EqualTo(local).Using(Vec));
            });
        }

        // ---- OnCharacterPositionSnap (06001063, 0x741C60) ----

        // 0x741c72..0x741d62: the character is turned to look along its forward projected onto the plane of world up,
        // with world up as the up direction. A forward tilted 45 degrees up ends horizontal.
        [Test]
        public void PositionSnapLevelsTheCharacterAlongItsForward()
        {
            WithDash((owned, character, ability, definition) =>
            {
                character.SetWorldRotation(Quaternion.LookRotation(new Vector3(1f, 1f, 0f).normalized, Vector3.up));
                Call(ability, "OnCharacterPositionSnap");
                Assert.That(character.ForwardDirection, Is.EqualTo(new Vector3(1f, 0f, 0f)).Using(Vec));
                Assert.That(character.UpDirection, Is.EqualTo(Vector3.up).Using(Vec));
            });
        }

        // ---- DoOnEnter (06001065, 0x741F90) ----

        // 0x742041..0x7420d9: the ability subscribes its position snap to the static trigger event, so raising the
        // ability's handler levels the character. Its subscription is removed afterwards; other subscribers are left alone.
        [Test]
        public void DoOnEnterSubscribesThePositionSnapToTheTriggerEvent()
        {
            WithDash((owned, character, ability, definition) =>
            {
                FieldInfo snap = typeof(RigidbodyForceTrigger).GetField("OnCharacterPositionSnap", BindingFlags.Static | BindingFlags.Public);
                // Only the ability's own subscription is removed afterwards, and only it is raised below: any other
                // subscriber of the static event is borrowed, and its callbacks have side effects the test cannot undo.
                owned.Lease(new ReleaseAction(() =>
                {
                    var now = (Action)snap.GetValue(null);
                    foreach (Delegate handler in now?.GetInvocationList() ?? new Delegate[0])
                        if (ReferenceEquals(handler.Target, ability)) now -= (Action)handler;
                    snap.SetValue(null, now);
                }));
                character.SetWorldRotation(Quaternion.LookRotation(new Vector3(1f, 1f, 0f).normalized, Vector3.up));
                Call(ability, "DoOnEnter");
                var current = (Action)snap.GetValue(null);
                Assert.That(current, Is.Not.Null);
                Assert.That(current.GetInvocationList(), Has.Exactly(1).Matches<Delegate>(d => ReferenceEquals(d.Target, ability) && d.Method.Name == "OnCharacterPositionSnap"));
                foreach (Delegate handler in current.GetInvocationList())
                    if (ReferenceEquals(handler.Target, ability)) ((Action)handler)();
                Assert.That(character.ForwardDirection, Is.EqualTo(new Vector3(1f, 0f, 0f)).Using(Vec));
            });
        }

        // ---- SetCameraOverrides (06001062, 0x741B00) ----

        // 0x741b3d..0x741c4c: after the base override the camera target transform is placed at the stored position
        // and rotation; a missing camera definition or target transform leaves it alone.
        [Test]
        public void CameraOverridePlacesTheCameraTargetAtTheDashTarget()
        {
            WithDash((owned, character, ability, definition) =>
            {
                var camera = owned.Track(ScriptableObject.CreateInstance<CameraAbilityDefinition>());
                Set(camera, "m_type", HardlightProject.CameraType.Default);
                Transform cameraTarget = owned.Host("Lucid chaos dash camera target").transform;
                Quaternion rotation = Quaternion.Euler(0f, 45f, 0f);
                Set(ability, "m_targetPosition", new Vector3(1f, 2f, 3f));
                Set(ability, "m_targetRotation", rotation);
                Call(ability, "SetCameraOverrides", null, cameraTarget);
                Assert.That(cameraTarget.position, Is.EqualTo(Vector3.zero));
                Call(ability, "SetCameraOverrides", camera, null);
                Assert.That(cameraTarget.position, Is.EqualTo(Vector3.zero));
                Call(ability, "SetCameraOverrides", camera, cameraTarget);
                Assert.That(cameraTarget.position, Is.EqualTo(new Vector3(1f, 2f, 3f)).Using(Vec));
                Assert.That(Quaternion.Angle(cameraTarget.rotation, rotation), Is.LessThan(0.01f));
                Assert.That(ability.CameraTypeOverride, Is.EqualTo(HardlightProject.CameraType.Default), "the base override ran with the definition's type");
            });
        }
    }
}
