using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using UnityEngine;
using static ProjectLucid.Tests.CharacterSelectScene;
using static ProjectLucid.Tests.DreamPowerSlotRigs;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The services the dream power store screens read, over the character select scene: a real DreamPowerStoreManager over the
    // scene's open save (no unlock requirements, so the store is available), a real ProgressionManager whose cached totals are
    // authored, and a regular mission context for the active mission. Nothing here touches the UINavigationManager, whose
    // constructor reads the UI runtime configuration.
    internal static class DreamPowerStoreScenes
    {
        internal sealed class World
        {
            internal Scene Scene;
            internal DreamPowerStoreManager Store;
            internal ProgressionManager Progression;
            internal Dictionary<CollectableType, ProgressionCollectable> Totals;
        }

        internal static void Run(Action<World> body)
        {
            SettingsPageTestSupport.InitialiseAnalytics(); // the collector's type initialiser runs over owned rows here, not in whichever case reports first
            CharacterSelectScene.Run(scene =>
            {
                Owned owned = scene.Owned;
                OwnedChallengeServices.RegisterChallengeManager(owned); // The store's field initialiser caches this row, and the cached progress takes the manager
                var world = new World { Scene = scene };
                world.Store = owned.Register(owned.Host("Lucid dream power store manager", false).AddComponent<DreamPowerStoreManager>());
                Set(world.Store, "m_saveManager", scene.Save); // Reflection: the reference the store's start-up chain takes from the SaveManager
                Set(world.Store, "m_dataManager", scene.Data); // Reflection: the reference the store's start-up chain takes from the DataManager
                Set(world.Store, "m_unlockRequirements", new RequirementGameplayLevelBase[0]); // Reflection: authored serialized field
                Set(scene.Save, "m_saveSettings", new SaveDataSettings()); // Reflection: the settings the SaveManager holds after loading

                // The manager's constructor subscribes both actions; the scene leases those rows.
                world.Progression = new ProgressionManager();
                owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => ProcessManager.UnsubscribeFromAllActions(world.Progression)));
                Set(world.Progression, "m_saveManager", scene.Save); // Reflection: set by Initialise, which also caches every zone
                var handler = new ProgressionCollectableHandler();
                handler.CacheEmptyProgress(scene.Save.CurrentSaveIndex);
                Set(world.Progression, "m_collectables", handler); // Reflection: set by Initialise
                world.Totals = (Dictionary<CollectableType, ProgressionCollectable>)handler.ProgressPerSave[scene.Save.CurrentSaveIndex].Total;
                body(world);
            });
        }

        // The scene's active mission: a shipped regular context over a definition for the given archetype, published through the
        // manager's public SetActiveMissionContext (which loads the mission's state); it is cleared again before the scene is released.
        internal static void ActivateMission(Scene scene, CharacterArchetype archetype, bool dreamPowersEnabled)
        {
            var mission = scene.Owned.Track(ScriptableObject.CreateInstance<MissionDefinition>());
            Set(mission, "m_guid", "lucid-store-mission"); // Reflection: authored GUID of a serialized definition
            Set(mission, "m_characterArchetype", archetype); // Reflection: authored mission data
            Set(mission, "m_enableDreamPowers", dreamPowersEnabled); // Reflection: authored mission data
            Set(mission, "m_subMissions", new System.Collections.Generic.List<MissionDefinition>()); // Reflection: authored (empty) list, which LoadMission reads
            var level = scene.Owned.Track(ScriptableObject.CreateInstance<GameplayLevelDefinition>());
            Set(level, "m_guid", "lucid-store-level"); // Reflection: authored GUID of a serialized definition
            scene.Missions.SetActiveMissionContext(new MissionContextRegular(mission, level, false));
            scene.Owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => scene.Missions.SetActiveMissionContext(null)));
        }

        // A dream power with an authored purchase cost and band, listed in the scene's DataManager.
        internal static DreamPowerDefinition AddPower(World world, string guid, int cost, DreamPowerStoreBandType band = DreamPowerStoreBandType.None)
        {
            var power = ChallengeRewardTestSupport.Create<DreamPowerDefinition>(world.Scene.Owned,
                "{\"m_purchaseCost\":" + cost + ",\"m_band\":" + (int)band + "}");
            Set(power, "m_guid", guid); // Reflection: authored GUID of a serialized definition
            world.Scene.Data.DreamPowerDefinitions[guid] = power;
            return power;
        }

        // A store container on an owned inactive host with the services Awake would take from the process manager. Awake asks for a
        // UINavigationManager, whose constructor reads the UI runtime configuration, so it is not run.
        internal static T NewStore<T>(World world) where T : UIContainerDreamPowerStore
        {
            var store = world.Scene.Owned.Host("Lucid dream power store", false).AddComponent<T>();
            // Reflection: the references Awake takes from the process manager.
            Set(store, "m_dreamPowerStoreManager", world.Store);
            Set(store, "m_dataManager", world.Scene.Data);
            Set(store, "m_missionManager", world.Scene.Missions); // Reflection: Awake takes these from the registry; Edit Mode does not send Awake
            Set(store, "m_saveManager", world.Scene.Save); // Reflection: Awake takes these from the registry; Edit Mode does not send Awake
            Set(store, "m_progressionManager", world.Progression); // Reflection: Awake takes these from the registry; Edit Mode does not send Awake
            return store;
        }

        // The serialized parts OnEnable reaches without a navigation manager: the blue coin counter, two slot buttons and no rows.
        // The caller wraps the case in a string table, which the counter format needs.
        internal static void WireForEnable(World world, UIContainerDreamPowerStore store)
        {
            Owned owned = world.Scene.Owned;
            Set(store, "m_collectableCounter", AddBlueCoinCounter(world, out _)); // Reflection: serialized reference the editor assigns
            ButtonSlot first = NewButtonSlot(owned);
            ButtonSlot second = NewButtonSlot(owned, true, false);
            Set(store, "m_slotButtons", new[] { first.Widget, second.Widget }); // Reflection: serialized references the editor assigns
            Set(store, "m_dreamPowerRows", new RectTransform[0]); // Reflection: serialized references the editor assigns
        }

        internal sealed class ArchetypeImages
        {
            internal Sprite Alternate, Silhouette, Ui;
        }

        // An archetype definition in the DataManager whose three sprite wrappers already hold their sprites.
        internal static ArchetypeImages AddArchetype(World world, CharacterArchetype archetype)
        {
            Owned owned = world.Scene.Owned;
            var definition = ChallengeRewardTestSupport.Create<CharacterArchetypeDefinition>(owned);
            Call(definition, "OnEnable");
            var images = new ArchetypeImages
            {
                Alternate = ChallengeRewardTestSupport.NewSprite(owned),
                Silhouette = ChallengeRewardTestSupport.NewSprite(owned),
                Ui = ChallengeRewardTestSupport.NewSprite(owned),
            };
            ChallengeRewardTestSupport.Cache(definition.AlternateImage, images.Alternate);
            ChallengeRewardTestSupport.Cache(definition.SilhouetteImage, images.Silhouette);
            ChallengeRewardTestSupport.Cache(definition.UIImage, images.Ui);
            world.Scene.Data.CharacterArchetypes[archetype] = definition;
            return images;
        }

        // A collectable counter over the blue coin definition (whose icon is already loaded). The counter's field initialisers cache
        // the ProgressionManager row, so the world's progression manager is registered into it first.
        internal static UIContainerCollectableCounter AddBlueCoinCounter(World world, out Sprite icon)
        {
            Owned owned = world.Scene.Owned;
            owned.Register(world.Progression);
            var coins = ChallengeRewardTestSupport.Create<CollectableDefinition>(owned,
                "{\"m_type\":" + (int)CollectableType.BlueCoin + ",\"m_displayFormattedString\":\"{0}/{1}\"}");
            icon = ChallengeRewardTestSupport.NewSprite(owned);
            Set(coins, "m_loadedSprite", icon); // Reflection: the sprite an earlier Addressables load would hold
            world.Scene.Data.CollectableDefinitions[CollectableType.BlueCoin] = coins;
            return owned.Host("Lucid blue coin counter", false).AddComponent<UIContainerCollectableCounter>();
        }

        internal static void Buy(World world, DreamPowerDefinition power) =>
            world.Scene.Save.CurrentSave.DreamPowerStore.GetOrCreateDreamPowerData(power.GetGUID()).Bought = true;

        // The store's item list is filled from its by-GUID table only when the save serializes (OnBeforeSerialize 0x06002be1); the
        // availability test and the counter read the list. A case that bought a power through the table writes it out as the save does.
        internal static void WriteStoreItems(World world) =>
            world.Scene.Save.CurrentSave.DreamPowerStore.OnBeforeSerialize();

        // Two band definitions, listed in the DataManager's band table and, in this order, in its band group (the store manager
        // reads a band's tier from its position in the group). Each band's slot background already holds its sprite.
        internal static DreamPowerStoreBandDefinition[] AddBands(World world, out Sprite[] backgrounds, params (DreamPowerStoreBandType type, Strings name)[] bands)
        {
            Owned owned = world.Scene.Owned;
            var definitions = new DreamPowerStoreBandDefinition[bands.Length];
            backgrounds = new Sprite[bands.Length];
            for (int i = 0; i < bands.Length; i++)
            {
                definitions[i] = ChallengeRewardTestSupport.Create<DreamPowerStoreBandDefinition>(owned,
                    "{\"m_guid\":\"lucid-store-band-" + i + "\",\"m_type\":" + (int)bands[i].type + ",\"m_name\":" + (int)bands[i].name + "}");
                Call(definitions[i], "OnEnable");
                backgrounds[i] = ChallengeRewardTestSupport.NewSprite(owned);
                ChallengeRewardTestSupport.Cache(definitions[i].SlotLockedBackground, backgrounds[i]);
                world.Scene.Data.DreamPowerStoreBandDefinitions[bands[i].type] = definitions[i];
            }
            var group = ChallengeRewardTestSupport.Create<DreamPowerStoreBandDefinitionGroup>(owned);
            group.m_elements = Array.ConvertAll(definitions, definition =>
                new DefinitionDataType<DreamPowerStoreBandType, DreamPowerStoreBandDefinition>.DefinitionElement<DreamPowerStoreBandDefinition>(definition));
            var all = ChallengeRewardTestSupport.Create<DataDefinitions>(owned);
            all.DataDefinitionsArray = new DefinitionDataType[] { group };
            // Reflection: the group table is filled by the Addressables load of the definition data, which has no other route here.
            Set(world.Scene.Data, "m_dataDefinitions", all);
            return definitions;
        }

        // A class that keeps a typed system reference in a static readonly field runs its type initialiser once per process. A
        // reference left empty by an earlier case's row is filled through its own InternalReplaceSystem and revoked again.
        internal static void BindStaticReference<T>(Owned owned, Type owner, string field, T system) where T : class, ISystem
        {
            RuntimeHelpers.RunClassConstructor(owner.TypeHandle);
            Action restore = StaticSystemReference.Bind(owner, field, system);
            if (restore != null)
                owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(restore));
        }

        internal sealed class PowerWidget
        {
            internal UIWidgetToggleDreamPower Widget;
            internal UIToggle Toggle;
            internal UIToggleGroupElement GroupElement;
            internal UIWidgetNavigation Navigation;
            internal DreamPowerSlotRigs.Transitions Transitions;
            internal AnimationClip Locked, LockedByArchetype, Purchasable, Selected, ClearSelected, Available, Equipped;
        }

        // A dream power widget over a real toggle, group element, transition (one legacy clip for each animation the widget plays)
        // and navigation component. Its static store reference is pointed at the world's store manager for the case.
        internal static PowerWidget NewPowerWidget(World world)
        {
            Owned owned = world.Scene.Owned;
            BindStaticReference(owned, typeof(UIWidgetToggleDreamPower), "s_dreamPowerStoreManagerRef", world.Store);
            GameObject host = owned.Host("Lucid dream power widget", false);
            var rig = new PowerWidget { Widget = host.AddComponent<UIWidgetToggleDreamPower>() };
            rig.Toggle = host.AddComponent<UIToggle>();
            rig.GroupElement = host.AddComponent<UIToggleGroupElement>();
            rig.Navigation = owned.Host("Lucid dream power widget navigation", false).AddComponent<UIWidgetNavigation>();
            rig.Transitions = DreamPowerSlotRigs.AddTransitions(owned, host, rig.Toggle, new UITransitionState[0],
                new[] { "locked", "locked by archetype", "purchasable", "selected", "clear selected", "available", "equipped" });
            AnimationClip Clip(string name) => rig.Transitions.Clips[name];
            rig.Locked = Clip("locked");
            rig.LockedByArchetype = Clip("locked by archetype");
            rig.Purchasable = Clip("purchasable");
            rig.Selected = Clip("selected");
            rig.ClearSelected = Clip("clear selected");
            rig.Available = Clip("available");
            rig.Equipped = Clip("equipped");
            Set(rig.GroupElement, "m_toggle", rig.Toggle); // Reflection: serialized reference the editor fills in, which a toggle group registration reads
            Widget(rig, "m_toggle", rig.Toggle);
            Widget(rig, "m_groupElement", rig.GroupElement);
            Widget(rig, "m_transitionAnimation", rig.Transitions.Transition);
            Widget(rig, "m_uiWidgetNavigation", rig.Navigation);
            Widget(rig, "m_lockedByProgression", rig.Locked);
            Widget(rig, "m_lockedByArchetypeAnimation", rig.LockedByArchetype);
            Widget(rig, "m_purchasableAnimation", rig.Purchasable);
            Widget(rig, "m_selectedAnimation", rig.Selected);
            Widget(rig, "m_clearSelectedAnimation", rig.ClearSelected);
            Widget(rig, "m_availableAnimation", rig.Available);
            Widget(rig, "m_equippedAnimation", rig.Equipped);
            return rig;
        }

        // Reflection: serialized reference or authored field the editor assigns.
        private static void Widget(PowerWidget rig, string field, object value) => Set(rig.Widget, field, value);
    }
}
