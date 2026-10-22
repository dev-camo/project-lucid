using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The real services the character select screens read: a registered App, DataManager and SaveManager over an open save,
    // a CharacterManager and a MissionManager with no active mission (OwnedCharacterManagers through
    // MissionTrackerTestSupport.WithCharacterWorld). The typed references UIContainerUtilities caches in static fields are
    // pointed at this scene's systems for the case; the scene holds no meta game selection, so a selected level is absent.
    internal static class CharacterSelectScene
    {
        private const BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic;

        internal sealed class Scene
        {
            internal Owned Owned;
            internal App App;
            internal DataManager Data;
            internal SaveManager Save;
            internal CharacterManager Characters;
            internal MissionManager Missions;
            internal Character Character;
        }

        // UIContainerUtilities builds its typed references from the rows it finds when it is first used, so its type initialiser
        // runs here against empty rows that are released at once, and not while a case has systems registered.
        internal static void InitialiseUtilities() => Owned.Run(owned =>
        {
            owned.LeaseRow<App>();
            owned.LeaseRow<LevelManager>();
            owned.LeaseRow<MissionManager>();
            RuntimeHelpers.RunClassConstructor(typeof(UIContainerUtilities).TypeHandle);
        });

        private static void PointAt<T>(Owned owned, string field, T system) where T : class, ISystem
        {
            FieldInfo target = typeof(UIContainerUtilities).GetField(field, Statics);
            object prior = target.GetValue(null);
            target.SetValue(null, new SystemRef<T>(ProcessManager.GetDefaultName<T>(), system)); // Reflection: the class keeps its references in static readonly fields
            owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => target.SetValue(null, prior)));
        }

        internal static void Run(Action<Scene> body)
        {
            InitialiseUtilities();
            MissionTrackerTestSupport.WithCharacterWorld((world, characters, character) =>
            {
                var scene = new Scene
                {
                    Owned = world.Owned,
                    App = ProcessManager.GetSystem<App>(),
                    Data = ProcessManager.GetSystem<DataManager>(),
                    Save = ProcessManager.GetSystem<SaveManager>(),
                    Characters = characters,
                    Missions = world.Missions,
                    Character = character,
                };
                // The App creates this list when it wakes; Edit Mode never sends Awake.
                scene.App.Storage.SetValue(AppFSMKeys.UIModernEvents, new System.Collections.Generic.List<UIModernEvent>());
                PointAt(scene.Owned, "s_appRef", scene.App);
                PointAt(scene.Owned, "s_levelManagerRef", world.Levels);
                PointAt(scene.Owned, "s_missionManagerRef", world.Missions);
                body(scene);
            });
        }

        private static readonly Regex DefinitionAwakeFault = new Regex("^NullReferenceException: Object reference not set to an instance of an object$");

        // A character definition as the engine serializer would deliver it. CreateInstance sends Awake, whose
        // ActorDefinition.UpdateCachedValues (0x7A8060) calls Settings.UpdateCachedValues on the unassigned Settings (+0x28) with no
        // null test: one logged fault per definition. The authored settings and lookup lists are then written, so that wearing a
        // skin (CharacterDefinition.SetCharacterSkin 0x5470B0 refreshes the same caches) has what it reads.
        internal static CharacterDefinition CreateCharacterDefinition(Owned owned)
        {
            LogAssert.Expect(LogType.Exception, DefinitionAwakeFault);
            var definition = owned.Track(ScriptableObject.CreateInstance<CharacterDefinition>());
            JsonUtility.FromJsonOverwrite("{\"Settings\":{\"Collider\":{}},\"PFXLookups\":[],\"AudioDefinitions\":[]}", definition);
            return definition;
        }

        // A character definition under the given id, listed in the scene's DataManager.
        internal static CharacterDefinition AddCharacter(Scene scene, CharacterId id, CharacterArchetype archetype = CharacterArchetype.None)
        {
            var definition = CreateCharacterDefinition(scene.Owned);
            Set(definition, "m_id", id); // Reflection: authored serialized field
            Set(definition, "m_archetype", archetype); // Reflection: authored serialized field
            scene.Data.Characters[id] = definition;
            return definition;
        }

        // CharacterValid and the current character have no public setter; the case sets them as the manager's own code does.
        internal static void MakeCurrent(Scene scene, CharacterId id)
        {
            typeof(Character).GetProperty("IdType").GetSetMethod(true).Invoke(scene.Character, new object[] { id });
            CharacterTriggerTestSupport.SetCurrent(scene.Characters, scene.Character, true);
        }
    }
}
