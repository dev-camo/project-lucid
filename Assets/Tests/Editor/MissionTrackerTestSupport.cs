using System;
using System.Runtime.CompilerServices;
using Hardlight;
using HardlightProject;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Shared construction for the mission tracker and objective fixtures. Every object is built through its shipped
    // constructor on an owned inactive host (Edit Mode sends no Awake or Start), and the services a tracker reaches in
    // Initialise and when it completes (level, track, collectable and mission managers, and the save a mission state
    // records into) are real instances registered into their absent rows for the length of a case.
    internal static class MissionTrackerTestSupport
    {
        internal sealed class World
        {
            internal Owned Owned;
            internal LevelManager Levels;
            internal TrackManager Tracks;
            internal CollectableManager Collectables;
            internal MissionManager Missions;

            // A mission definition with its own save record, set up in a state the way MissionState.SetUpData does.
            internal Mission NewMission(int totalObjectives = 1, int targetObjectives = -1)
            {
                string guid = "lucid-mission-" + Guid.NewGuid().ToString("N");
                var definition = Owned.Track(ScriptableObject.CreateInstance<MissionDefinition>());
                Set(definition, "m_guid", guid); // Reflection: authored GUID of a serialized definition
                Set(definition, "m_totalObjectivesCount", totalObjectives); // Reflection: authored mission data
                Set(definition, "m_targetObjectivesCount", targetObjectives); // Reflection: authored mission data
                var save = new SaveDataLevelMission(guid);
                save.DisableSaving(); // saving is off, so completing the mission sends the SaveManager no request
                var state = new MissionState();
                state.SetUpData(definition, save, "lucid-level");
                return new Mission { Definition = definition, Save = save, State = state };
            }

            // A tracker component on an owned inactive host, with its authored events present.
            internal T NewTracker<T>() where T : MissionTracker
            {
                T tracker = Owned.Host("Lucid mission tracker", false).AddComponent<T>();
                Author(tracker, "m_onCompleted"); // Reflection: authored events, which the constructor leaves null
                Author(tracker, "m_onPreviouslyCompleted");
                Author(tracker, "m_onRestart");
                return tracker;
            }

            // An objective of the given kind on an owned inactive host, with its authored events present.
            internal T NewObjective<T>(Transform parent = null) where T : MissionObjective
            {
                GameObject host = Owned.Host("Lucid mission objective", false, parent);
                T objective = EnemyAbilityTestSupport.AddTimeScaled<T>(host);
                Author(objective, "m_onActivated"); // Reflection: authored events, which the constructor leaves null
                Author(objective, "m_onPreviouslyCompleted");
                Author(objective, "m_onReset");
                Author(objective, "m_onComplete");
                return objective;
            }
        }

        internal sealed class Mission
        {
            internal MissionDefinition Definition;
            internal SaveDataLevelMission Save;
            internal MissionState State;
        }

        // Gives a serialized UnityEvent field its instance; the returned event records its invocations.
        internal static CountingEvent Author(object target, string field)
        {
            var recorded = new CountingEvent();
            Set(target, field, recorded.Event);
            return recorded;
        }

        internal sealed class CountingEvent
        {
            internal readonly UnityEvent Event = new UnityEvent();
            internal int Count;

            internal CountingEvent() => Event.AddListener(() => Count++);
        }

        // Collectable caches typed references to these rows in static fields when it is first used; running its type
        // initialiser against empty rows that are released at once keeps those references from outliving a case's rows.
        internal static void InitialiseCollectable() => Owned.Run(owned =>
        {
            owned.LeaseRow<CollectableManager>();
            owned.LeaseRow<LevelManager>();
            owned.LeaseRow<CharacterManager>();
            RuntimeHelpers.RunClassConstructor(typeof(Collectable).TypeHandle);
        });

        internal static void WithWorld(Action<World> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<CharacterManager>();
            owned.LeaseRow<SaveManager>();
            Build(owned, body);
        });

        // As WithWorld, with a registered CharacterManager and a character that is not yet current.
        internal static void WithCharacterWorld(Action<World, CharacterManager, Character> body) =>
            CharacterTriggerTestSupport.WithManager((owned, manager, character) => Build(owned, world => body(world, manager, character)));

        private static void Build(Owned owned, Action<World> body)
        {
            owned.LeaseActions(SystemAction.Initialise, SystemAction.Shutdown);
            // Rows that the manager, mission definition, mission state and objective field initialisers cache.
            owned.LeaseRow<AudioManager>();
            owned.LeaseRow<TimeTrialLeaderboardManager>();
            owned.LeaseRow<GameplayIslandManager>();
            owned.LeaseRow<UIManager>();
            var world = new World { Owned = owned };
            world.Levels = owned.Register(owned.Host("Lucid level manager", false).AddComponent<LevelManager>());
            world.Tracks = owned.Register(owned.Host("Lucid track manager", false).AddComponent<TrackManager>());
            world.Collectables = owned.Register(new CollectableManager());
            world.Missions = owned.Register(new MissionManager());
            owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => ProcessManager.UnsubscribeFromAllActions(world.Missions)));
            body(world);
        }
    }
}
