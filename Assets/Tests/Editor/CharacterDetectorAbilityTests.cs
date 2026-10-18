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
    // The detector abilities: the refresh timer of the generic base, the archetype detector and the lightspeed
    // dash detector. Expected values come from the 1.10.1 x86-64 bodies cited per test. The bodies that need
    // the track manager, the level manager or a started Character (DoOnDetect, DoOnEnter, DoOnLeave) are
    // checked as call sequences read from the compiled IL.
    public sealed class CharacterDetectorAbilityTests
    {
        // The generic base is abstract; a concrete leaf that only counts detections observes its timer.
        private sealed class CountingDetector : CharacterAbility_Detector<CharacterAbilityDefinition_DetectorArchetype>
        {
            public int Detections;

            protected override void DoOnDetect() => Detections++;

            public void Tick(float deltaTime) => DoOnUpdate(null, deltaTime);
        }

        // ---- CharacterAbility_Detector (020002f2) ----

        private static void WithCountingDetector(float refreshInterval, Action<CountingDetector> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<App>();
            var definition = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_DetectorArchetype>());
            definition.RefreshInterval = refreshInterval;
            var ability = new CountingDetector();
            Set(ability, "m_definition", definition);
            body(ability);
        });

        private static float Timer(object ability) => Get<float>(ability, "m_refreshTimer");

        // 0xE60080: the character base update first, then a zero interval returns before the timer moves
        // (0xE600FD ucomiss / jne / jnp: equal and ordered), otherwise the timer takes the step and, once the
        // interval is no longer greater than it (0xE6017C jbe: equal passes), restarts at zero and detects.
        [Test]
        public void DetectsEveryIntervalAndRestartsTheTimer()
        {
            WithCountingDetector(1f, ability =>
            {
                ability.Tick(0.5f);
                Assert.That((ability.Detections, Timer(ability)), Is.EqualTo((0, 0.5f)));
                ability.Tick(0.5f);
                Assert.That((ability.Detections, Timer(ability)), Is.EqualTo((1, 0f)), "0.5 + 0.5 reaches the interval exactly");
                ability.Tick(0.25f);
                Assert.That((ability.Detections, Timer(ability)), Is.EqualTo((1, 0.25f)));
                ability.Tick(4f);
                Assert.That((ability.Detections, Timer(ability)), Is.EqualTo((2, 0f)), "one detection however far the timer overshoots");
            });
        }

        [Test]
        public void AZeroIntervalNeverDetectsAndNeverMovesTheTimer()
        {
            WithCountingDetector(0f, ability =>
            {
                ability.Tick(0.5f);
                ability.Tick(100f);
                Assert.That((ability.Detections, Timer(ability)), Is.EqualTo((0, 0f)));
            });
        }

        // Only an exactly zero interval is skipped: a negative interval, and an unordered NaN interval (neither
        // the zero test nor the greater-than test holds), detect on every update.
        [Test]
        public void ANegativeOrNaNIntervalDetectsOnEveryUpdate()
        {
            foreach (float interval in new[] { -1f, float.NaN })
                WithCountingDetector(interval, ability =>
                {
                    ability.Tick(0.1f);
                    ability.Tick(0.1f);
                    Assert.That((ability.Detections, Timer(ability)), Is.EqualTo((2, 0f)), "interval " + interval);
                });
        }

        // The character base update runs first: it copies the definition's gravity multiplier.
        [Test]
        public void DetectorUpdateRunsTheCharacterBaseFirst()
        {
            WithCountingDetector(1f, ability =>
            {
                Get<CharacterAbilityDefinition_DetectorArchetype>(ability, "m_definition").GravityMultiplier = 3f;
                ability.Tick(0.1f);
                Assert.That(ability.GravityMultiplier, Is.EqualTo(3f));
            });
        }

        // ---- CharacterAbility_DetectorArchetype (020002f3) ----

        // The constructors read these rows' typed references; none is registered here, so a message cannot be
        // published and nothing is deferred.
        private static void WithArchetypeDetector(CharacterArchetype detects, CharacterArchetype characterIs,
            Action<CharacterGravityVectorProbe, CharacterAbility_DetectorArchetype> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<GameplayIslandManager>();
            owned.LeaseRow<App>();
            owned.LeaseRow<MessageManager>();
            owned.LeaseRow<LevelManager>();
            GameObject host = owned.Host("Lucid detector character", false);
            host.AddComponent<BoxCollider>();
            var character = host.AddComponent<CharacterGravityVectorProbe>();
            var characterDefinition = owned.Track(ScriptableObject.CreateInstance<CharacterDefinition>());
            Set(characterDefinition, "m_archetype", characterIs);
            SetProperty(character, "Definition", characterDefinition);
            var definition = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_DetectorArchetype>());
            definition.Archetype = detects;
            var ability = new CharacterAbility_DetectorArchetype();
            Set(ability, "m_character", character);
            Set(ability, "m_definition", definition);
            body(character, ability);
        });

        private static DetectorArchetypeUpdateMessage ArchetypeMessage(object ability) => Get<DetectorArchetypeUpdateMessage>(ability, "m_archetypeUpdateMessage");

        // 0x74C8E0 compares the character definition's archetype (+0x5C) with the detector's (+0xD8) and passes the
        // result on; 0x74C940 stores the detector's archetype, never the character's, with that flag before it
        // looks for the message manager.
        [Test]
        public void LevelActivationRecordsWhetherTheCharacterIsTheDetectedArchetype()
        {
            foreach ((CharacterArchetype detects, CharacterArchetype character, bool valid) in new[]
            {
                (CharacterArchetype.Flying, CharacterArchetype.Flying, true),
                (CharacterArchetype.Flying, CharacterArchetype.Dashing, false),
                (CharacterArchetype.None, CharacterArchetype.None, true),
                (CharacterArchetype.Shadow, CharacterArchetype.None, false),
            })
                WithArchetypeDetector(detects, character, (probe, ability) =>
                {
                    Call(ability, "OnLevelActivated", (LevelManagerLevel)null);
                    Assert.That((ArchetypeMessage(ability).Archetype, ArchetypeMessage(ability).IsValid), Is.EqualTo((detects, valid)),
                        "detects " + detects + ", character " + character);
                });
        }

        [Test]
        public void PublishingWithoutAMessageManagerStillRecordsTheMessage()
        {
            WithArchetypeDetector(CharacterArchetype.Climbing, CharacterArchetype.Flying, (probe, ability) =>
            {
                Assert.DoesNotThrow(() => Call(ability, "PublishDetectorMessage", true));
                Assert.That((ArchetypeMessage(ability).Archetype, ArchetypeMessage(ability).IsValid), Is.EqualTo((CharacterArchetype.Climbing, true)));
                Call(ability, "PublishDetectorMessage", false);
                Assert.That(ArchetypeMessage(ability).IsValid, Is.False);
            });
        }

        // A registered message manager and the real App (the first leased fixture owns the App and the Shutdown
        // action rows, and is released last).
        private static void WithMessages(Action<Owned, MessageManager> body) => Owned.Run(owned =>
        {
            owned.Lease(new OwnedCharacterManagers());
            owned.LeaseRow<GameplayIslandManager>();
            owned.LeaseRow<LevelManager>();
            var row = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<MessageManager>());
            owned.Lease(row);
            var manager = new MessageManager();
            owned.Lease(new ReleaseAction(() =>
            {
                manager.ProcessSystemAction(SystemAction.Shutdown);
                ProcessManager.UnsubscribeFromAllActions(manager);
                ProcessManager.UnsubscribeFromAllActions(manager.ComponentMessagesWithCompletion);
                ProcessManager.UnsubscribeFromAllActions(manager.StringMessagesWithCompletion);
                ProcessManager.UnsubscribeFromAllActions(manager.ComponentMessagesWithoutCompletion);
                ProcessManager.UnsubscribeFromAllActions(manager.StringMessagesWithoutCompletion);
                ProcessManager.UnsubscribeFromAllActions(manager.ScriptableObjectMessagesWithoutCompletion);
            }));
            row.Register(manager);
            body(owned, manager);
        });

        private static List<T> Capture<T>(MessageManager manager, Component sender)
        {
            var received = new List<T>();
            manager.ComponentMessagesWithCompletion.SubscribeToMessage(sender, new MessageCallbackWithCompletion<T>(
                (in T message, Action completed) =>
                {
                    received.Add(message);
                    completed?.Invoke();
                }));
            return received;
        }

        // 0x74C940: the message is published once on the component exchange under the App (SystemRef<App>.Get,
        // 0x74CA06), not under the character, with the detector's archetype and the flag.
        [Test]
        public void ArchetypeMessagesAreSentUnderTheApp()
        {
            WithMessages((owned, manager) =>
            {
                GameObject host = owned.Host("Lucid detector character", false);
                host.AddComponent<BoxCollider>();
                var character = host.AddComponent<CharacterGravityVectorProbe>();
                var characterDefinition = owned.Track(ScriptableObject.CreateInstance<CharacterDefinition>());
                Set(characterDefinition, "m_archetype", CharacterArchetype.Dashing);
                SetProperty(character, "Definition", characterDefinition);
                var definition = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_DetectorArchetype>());
                definition.Archetype = CharacterArchetype.Dashing;
                var ability = new CharacterAbility_DetectorArchetype();
                Set(ability, "m_character", character);
                Set(ability, "m_definition", definition);
                Component app = ProcessManager.GetSystemRef<App>().Get();

                List<DetectorArchetypeUpdateMessage> underApp = Capture<DetectorArchetypeUpdateMessage>(manager, app);
                List<DetectorArchetypeUpdateMessage> underCharacter = Capture<DetectorArchetypeUpdateMessage>(manager, character);
                Call(ability, "OnLevelActivated", (LevelManagerLevel)null);
                Assert.That(underCharacter, Is.Empty);
                Assert.That(underApp.Select(m => (m.Archetype, m.IsValid)), Is.EqualTo(new[] { (CharacterArchetype.Dashing, true) }));

                definition.Archetype = CharacterArchetype.Climbing;
                Call(ability, "OnLevelActivated", (LevelManagerLevel)null);
                Assert.That(underApp.Select(m => (m.Archetype, m.IsValid)), Is.EqualTo(new[]
                {
                    (CharacterArchetype.Dashing, true), (CharacterArchetype.Climbing, false),
                }));
            });
        }

        // 0x74C8D0 is a bare RET: the archetype detector publishes on level activation, not on its refresh.
        [Test]
        public void ArchetypeDetectionIsAnEmptyBody()
        {
            MethodInfo method = typeof(CharacterAbility_DetectorArchetype).GetMethod("DoOnDetect", Instance | BindingFlags.DeclaredOnly);
            Assert.That(OriginalILCallSequence.Instructions(method).All(i => i.Code == System.Reflection.Emit.OpCodes.Nop || i.Code == System.Reflection.Emit.OpCodes.Ret), Is.True);
            Assert.That(method.GetBaseDefinition().DeclaringType.Name, Is.EqualTo("CharacterAbility_Detector`1"));
        }

        // 0x74C810: the character base entry (its gravity multiplier copy is inlined natively), then a deferred
        // subscription on the level manager reference; the callback 0x74CC70 registers OnLevelActivated and
        // runs it at once. 0x74CAE0: the base leave, then the level manager is asked to drop the same handler.
        [Test]
        public void ArchetypeEntrySubscribesToLevelActivationAndLeavingWithdraws()
        {
            List<MethodBase> enter = Calls(typeof(CharacterAbility_DetectorArchetype), "DoOnEnter");
            Assert.That(IndexOf(enter, "CharacterAbility", "DoOnEnter"), Is.LessThan(IndexOf(enter, "SystemRef", "InvokeOnValid")));

            MethodBase lambda = typeof(CharacterAbility_DetectorArchetype).GetMethods(DeclaredMembers).Single(m => m.Name == "<DoOnEnter>b__3_0");
            List<MethodBase> body = OriginalILCallSequence.Calls(lambda).Select(call => call.Target).ToList();
            Assert.That(body.Where(m => m.Name == "InvokeOnLevelActivated").Select(m => m.DeclaringType.Name), Is.EqualTo(new[] { "LevelManager" }));
            Assert.That(OriginalILCallSequence.Instructions(lambda).Any(i => i.Code == System.Reflection.Emit.OpCodes.Ldc_I4_1), Is.True, "the second argument is true");

            List<MethodBase> leave = Calls(typeof(CharacterAbility_DetectorArchetype), "DoOnLeave");
            int baseLeave = IndexOf(leave, "ActorAbility", "DoOnLeave");
            int get = IndexOf(leave, "SystemRef", "Get");
            int remove = IndexOf(leave, "LevelManager", "RemoveLevelActivatedAction");
            Assert.That(new[] { baseLeave, get, remove }, Is.Ordered.Ascending.And.Unique);
        }

        // ---- CharacterAbility_DetectorLightspeedDash (020002f5) ----

        // A scripted surface answers the two queries the endpoints make, local zero (start) and Length forward (end).
        private sealed class ScriptedSurface : ISurface
        {
            public SurfaceLocation Start;
            public SurfaceLocation End;

            public SurfaceLocation FindNearestSurfaceLocationFromLocal(Vector3 localPosition) => localPosition == Vector3.zero ? Start : End;
            public float Length => 8f;
            public SurfaceLocation FindNearestSurfaceLocationFromWorld(Vector3 worldPosition) => throw new NotSupportedException();
            public DateTime GetTimestamp() => throw new NotSupportedException();
            public MetadataGroups GetSurfaceMetadata() => throw new NotSupportedException();
            public float LengthInverse => throw new NotSupportedException();
            public Bounds GetBoundingBox(bool inWorldSpace = false) => throw new NotSupportedException();
            public Vector3 TransformPoint(Vector3 point) => throw new NotSupportedException();
            public Vector3 InverseTransformPoint(Vector3 point) => throw new NotSupportedException();
            public bool IsActive() => throw new NotSupportedException();
        }

        private static SurfaceLocation Location(ISurface surface, Vector3 world) => new SurfaceLocation
        {
            m_surface = surface, m_localPosition = new Vector3(7f, 7f, 7f), m_worldPosition = world,
        };

        private static void WithLightspeedDetector(Action<Owned, CharacterGravityVectorProbe, CharacterAbility_DetectorLightspeedDash, CharacterAbilityDefinition_DetectorLightspeedDash> body) =>
            Owned.Run(owned =>
            {
                owned.LeaseRow<GameplayIslandManager>();
                owned.LeaseRow<App>();
                owned.LeaseRow<MessageManager>();
                GameObject host = owned.Host("Lucid lightspeed detector character", false);
                host.AddComponent<BoxCollider>();
                var character = host.AddComponent<CharacterGravityVectorProbe>();
                var definition = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_DetectorLightspeedDash>());
                var ability = new CharacterAbility_DetectorLightspeedDash();
                Set(ability, "m_character", character);
                Set(ability, "m_definition", definition);
                body(owned, character, ability, definition);
            });

        // 0x74D4FD: the message field starts with the lightspeed dash type (0x910ca515) and the rest zero.
        [Test]
        public void LightspeedMessageStartsAsALightspeedDashWithNoSurface()
        {
            WithLightspeedDetector((owned, character, ability, definition) =>
            {
                SurfaceDetectorUpdateMessage message = Get<SurfaceDetectorUpdateMessage>(ability, "m_updateMessage");
                Assert.That(message.Type, Is.EqualTo(HomingTargetType.LightspeedDash));
                Assert.That(message.SurfaceLocation.m_surface, Is.Null);
                Assert.That(message.SurfaceLocation.m_worldPosition, Is.EqualTo(Vector3.zero));
            });
        }

        private static SurfaceLocation Chosen(object ability) => Get<SurfaceLocation>(ability, "m_surfaceLocation");

        // 0x74CF70: the cached surface's start and end are measured against the character's position as squared
        // distances (the dot of the difference with itself). The cached surface is cleared, then the start is kept
        // while it is strictly inside the proximity (0x74D188 jbe: equal fails), and the end replaces it only when
        // it is strictly nearer than the start (0x74D221) and strictly inside the proximity (0x74D241).
        [Test]
        public void TheNearerEndOfTheSplineWithinProximityIsKept()
        {
            // The character is at the origin and the proximity is 5 (squared 25); the start and end positions
            // give exact squared distances.
            foreach ((Vector3 startAt, Vector3 endAt, string expected) in new[]
            {
                (new Vector3(3f, 0f, 0f), new Vector3(0f, 10f, 0f), "start"),
                (new Vector3(10f, 0f, 0f), new Vector3(0f, 3f, 0f), "end"),
                (new Vector3(3f, 0f, 0f), new Vector3(0f, 4f, 0f), "start"),
                (new Vector3(4f, 0f, 0f), new Vector3(0f, 3f, 0f), "end"),
                (new Vector3(3f, 0f, 0f), new Vector3(0f, 0f, 3f), "start"),
                (new Vector3(10f, 0f, 0f), new Vector3(0f, 0f, 20f), "none"),
                (new Vector3(5f, 0f, 0f), new Vector3(0f, 5f, 0f), "none"),
                (new Vector3(5f, 0f, 0f), new Vector3(0f, 4f, 0f), "end"),
                (new Vector3(4f, 0f, 0f), new Vector3(0f, 5f, 0f), "start"),
            })
                WithLightspeedDetector((owned, character, ability, definition) =>
                {
                    definition.ProximityDistance = 5f;
                    definition.CalculateCachedValues();
                    SetActorProperty(character, "WorldPosition", Vector3.zero);
                    var surface = new ScriptedSurface { Start = Location(null, startAt), End = Location(null, endAt) };
                    surface.Start.m_surface = surface;
                    surface.End.m_surface = surface;
                    Set(ability, "m_surfaceLocation", new SurfaceLocation { m_surface = surface, m_localPosition = new Vector3(9f, 9f, 9f) });

                    Call(ability, "SetSurfaceLocation");

                    SurfaceLocation chosen = Chosen(ability);
                    string label = "start " + startAt + " end " + endAt;
                    switch (expected)
                    {
                        case "start":
                            Assert.That(chosen.m_surface, Is.SameAs(surface), label);
                            Assert.That(chosen.m_worldPosition, Is.EqualTo(startAt), label);
                            Assert.That(chosen.m_localPosition, Is.EqualTo(new Vector3(7f, 7f, 7f)), label + ": the whole location is copied");
                            break;
                        case "end":
                            Assert.That(chosen.m_surface, Is.SameAs(surface), label);
                            Assert.That(chosen.m_worldPosition, Is.EqualTo(endAt), label);
                            break;
                        default:
                            Assert.That(chosen.m_surface, Is.Null, label);
                            Assert.That(chosen.m_localPosition, Is.EqualTo(new Vector3(9f, 9f, 9f)), label + ": only the surface is cleared");
                            break;
                    }
                });
        }

        // 0x74D300: the message takes the cached location before the message manager is looked up, so it is
        // updated even with no manager registered (the type stays).
        [Test]
        public void LightspeedMessageTakesTheCachedLocationEvenWithoutAManager()
        {
            WithLightspeedDetector((owned, character, ability, definition) =>
            {
                var surface = new ScriptedSurface();
                Set(ability, "m_surfaceLocation", Location(surface, new Vector3(1f, 2f, 3f)));
                Assert.DoesNotThrow(() => Call(ability, "PublishDetectorMessage"));
                SurfaceDetectorUpdateMessage message = Get<SurfaceDetectorUpdateMessage>(ability, "m_updateMessage");
                Assert.That(message.SurfaceLocation.m_surface, Is.SameAs(surface));
                Assert.That(message.SurfaceLocation.m_worldPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(message.Type, Is.EqualTo(HomingTargetType.LightspeedDash));
            });
        }

        // 0x74D3F3: the message is published once under the character (not the App) with the cached location.
        [Test]
        public void LightspeedMessagesAreSentUnderTheCharacter()
        {
            WithMessages((owned, manager) =>
            {
                GameObject host = owned.Host("Lucid lightspeed detector character", false);
                host.AddComponent<BoxCollider>();
                var character = host.AddComponent<CharacterGravityVectorProbe>();
                var definition = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_DetectorLightspeedDash>());
                var ability = new CharacterAbility_DetectorLightspeedDash();
                Set(ability, "m_character", character);
                Set(ability, "m_definition", definition);
                var surface = new ScriptedSurface();
                Set(ability, "m_surfaceLocation", Location(surface, new Vector3(1f, 2f, 3f)));
                Component app = ProcessManager.GetSystemRef<App>().Get();

                List<SurfaceDetectorUpdateMessage> underCharacter = Capture<SurfaceDetectorUpdateMessage>(manager, character);
                List<SurfaceDetectorUpdateMessage> underApp = Capture<SurfaceDetectorUpdateMessage>(manager, app);
                Call(ability, "PublishDetectorMessage");
                Assert.That(underApp, Is.Empty);
                Assert.That(underCharacter.Count, Is.EqualTo(1));
                Assert.That(underCharacter[0].Type, Is.EqualTo(HomingTargetType.LightspeedDash));
                Assert.That(underCharacter[0].SurfaceLocation.m_surface, Is.SameAs(surface));
                Assert.That(underCharacter[0].SurfaceLocation.m_worldPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            });
        }

        // 0x74CD80: the nearest dash spline query (with the position, the definition's proximity and no inactive
        // surfaces), and only on a hit the camera-to-surface ray; a blocked ray clears the surface, a clear one
        // narrows it to the nearer end; the message is published on every path (the tail jump at 0x74CF6B).
        [Test]
        public void DetectionQueriesTheTrackThenTestsTheViewRayThenPublishes()
        {
            List<MethodBase> calls = Calls(typeof(CharacterAbility_DetectorLightspeedDash), "DoOnDetect");
            int track = IndexOf(calls, "Character", "get_TrackManager");
            int query = IndexOf(calls, "TrackManager", "TryGetClosestLightspeedDashSpline");
            int camera = IndexOf(calls, "Character", "get_CameraPosition");
            int mask = IndexOf(calls, "Character", "get_CollisionMask");
            int raycast = IndexOf(calls, "Physics", "Raycast");
            int narrow = IndexOf(calls, "CharacterAbility_DetectorLightspeedDash", "SetSurfaceLocation");
            int publish = IndexOf(calls, "CharacterAbility_DetectorLightspeedDash", "PublishDetectorMessage");
            Assert.That(new[] { track, query, camera, mask, raycast, narrow, publish }, Is.Ordered.Ascending.And.Unique);
            Assert.That(calls.Count(m => m.Name == "PublishDetectorMessage"), Is.EqualTo(1));
        }

        // Declaration shape (not behaviour): the detector leaves keep the generic base with the right definitions
        // and the member order that fixes the compiler-generated lambda names.
        [Test]
        public void DeclarationShapeDetectorsKeepTheirBasesFieldsAndLambdaNames()
        {
            Assert.That(typeof(CharacterAbility_DetectorArchetype).BaseType, Is.EqualTo(typeof(CharacterAbility_Detector<CharacterAbilityDefinition_DetectorArchetype>)));
            Assert.That(typeof(CharacterAbility_DetectorLightspeedDash).BaseType, Is.EqualTo(typeof(CharacterAbility_Detector<CharacterAbilityDefinition_DetectorLightspeedDash>)));
            Assert.That(typeof(CharacterAbility_Detector<>).IsAbstract, Is.True);
            Assert.That(typeof(CharacterAbility_Detector<>).GetGenericArguments()[0].GetGenericParameterConstraints(), Is.EqualTo(new[] { typeof(CharacterAbilityDefinition_Detector) }));
            Assert.That(typeof(CharacterAbility_Detector<>).GetFields(DeclaredMembers).Select(f => f.Name), Is.EqualTo(new[] { "m_refreshTimer" }));
            Assert.That(typeof(CharacterAbility_DetectorArchetype).GetFields(DeclaredMembers).OrderBy(f => f.MetadataToken).Select(f => f.Name),
                Is.EqualTo(new[] { "m_archetypeUpdateMessage", "m_messageManagerRef", "m_levelManagerRef" }));
            Assert.That(typeof(CharacterAbility_DetectorLightspeedDash).GetFields(DeclaredMembers).OrderBy(f => f.MetadataToken).Select(f => f.Name),
                Is.EqualTo(new[] { "m_surfaceLocation", "m_updateMessage", "m_messageManagerRef" }));
            Assert.That(typeof(CharacterAbility_DetectorArchetype).GetMethods(DeclaredMembers).Select(m => m.Name), Does.Contain("<DoOnEnter>b__3_0"));
            Type cached = typeof(CharacterAbility_DetectorArchetype).GetNestedTypes(BindingFlags.NonPublic).Single();
            Assert.That(cached.GetMethods(DeclaredMembers).Select(m => m.Name), Does.Contain("<PublishDetectorMessage>b__6_0"));
            Type dashCached = typeof(CharacterAbility_DetectorLightspeedDash).GetNestedTypes(BindingFlags.NonPublic).Single();
            Assert.That(dashCached.GetMethods(DeclaredMembers).Select(m => m.Name), Does.Contain("<PublishDetectorMessage>b__5_0"));
        }
    }
}
