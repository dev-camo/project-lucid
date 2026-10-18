using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using UnityEditor.Animations;
using UnityEngine;
using static ProjectLucid.Tests.TriggerConditionTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Shared setup for the character trigger fixtures. A CharacterManager is built by its real
    // constructor (OwnedCharacterManagers) and registered into its absent row before any component that
    // caches a reference to it is built; a Character is a real component on an owned, inactive host
    // (no Awake runs in Edit Mode) with a real FSM storage, so only members that need no live body run.
    internal static class CharacterTriggerTestSupport
    {
        // A registered manager and a character that is not yet current.
        internal static void WithManager(Action<Owned, CharacterManager, Character> body) => Owned.Run(owned =>
        {
            // Taken first, so it is released last: the manager constructor's rows outlive the case's.
            OwnedCharacterManagers characters = owned.Lease(new OwnedCharacterManagers());
            owned.LeaseRow<GameplayIslandManager>();
            owned.LeaseRow<LevelManager>();
            CharacterManager manager = owned.Register(characters.NewManager());
            body(owned, manager, NewCharacter(owned, new FSMStorage(0)));
        });

        // A character with its storage and the rows its constructor reads, but no manager registered.
        internal static void WithCharacter(Action<Owned, Character> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<GameplayIslandManager>();
            body(owned, NewCharacter(owned, new FSMStorage(0)));
        });

        // CharacterValid has a private setter; the case calls it as the manager's own code does.
        internal static void SetCurrent(CharacterManager manager, Character character, bool valid)
        {
            Set(manager, "m_currentCharacter", character);
            typeof(CharacterManager).GetProperty("CharacterValid").GetSetMethod(true).Invoke(manager, new object[] { valid });
        }

        // Actor state with a private or protected setter, set as the owning class's own code does.
        internal static void SetActorProperty(Actor actor, string name, object value) =>
            typeof(Actor).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .GetSetMethod(true).Invoke(actor, new[] { value });

        // A Character whose gravity direction is supplied (the shipped override reads a RigidbodyGravity component
        // that Edit Mode does not build), with a real FSM storage. The GameplayIslandManager row must be leased.
        internal static CharCoreShared.CharacterGravityProbe NewGravityCharacter(Owned owned, Vector3 down)
        {
            GameObject host = owned.Host("Lucid gravity character", false);
            host.AddComponent<BoxCollider>();
            var character = host.AddComponent<CharCoreShared.CharacterGravityProbe>();
            SetActorProperty(character, "Storage", new FSMStorage(0));
            character.Down = down;
            return character;
        }

        // TriggerCondition caches a typed CharacterManager reference in a static field when the type is first
        // used. Running the type initialiser here, with an empty row that is released at once, keeps that
        // reference from being built while a case has a manager registered.
        internal static void InitialiseTriggerConditions() => Owned.Run(owned =>
        {
            owned.LeaseRow<CharacterManager>();
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(TriggerCondition).TypeHandle);
        });

        internal static string[] HandlerNames(Delegate handlers) =>
            handlers == null ? new string[0] : handlers.GetInvocationList().Select(handler => handler.Method.Name).ToArray();

        // A real Animator driven by a controller with the given trigger and bool parameters, wrapped by the
        // genuine ActorAnimator constructor and installed as the character's animator.
        internal static Animator AttachAnimator(Owned owned, Character character, string[] triggers, string[] bools)
        {
            var animator = owned.Host("Lucid trigger animator", true).AddComponent<Animator>();
            var controller = owned.Track(new AnimatorController());
            controller.AddLayer("Base");
            foreach (string name in triggers) controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
            foreach (string name in bools) controller.AddParameter(name, AnimatorControllerParameterType.Bool);
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Update(0f);
            SetActorProperty(character, "Animator", new ActorAnimator(animator));
            return animator;
        }

        internal static AnimationParameterWrapper Parameter(string name)
        {
            var wrapper = new AnimationParameterWrapper();
            Set(wrapper, "m_parameterName", name);
            return wrapper;
        }

        // The modifier stack a Character holds for its overrides.
        internal static StackableData Modifiers(Character character) => Get<StackableData>(character, "m_modifiers");

        internal sealed class ReleaseAction : IDisposable
        {
            private readonly Action m_release;
            internal ReleaseAction(Action release) => m_release = release;
            public void Dispose() => m_release();
        }

        // A TimeManager that is a real component on an inactive host (Awake never runs, so it registers
        // nothing) holding one category at the given scale, with the category lookup TimeExtensions reads
        // leased for the case. The TimeManager row must already be leased by the caller. TimeManager.Reset
        // (1.10.1 0x1B168D0) reads m_timeCategoryConfiguration (+0xA8) without a null test, and AddComponent
        // sends the Editor Reset message while it is unassigned.
        internal static TimeManager NewTimeManager(Owned owned, TimeCategory category, float timeScale)
        {
            const BindingFlags statics = BindingFlags.Static | BindingFlags.NonPublic;
            FieldInfo lookupField = typeof(TimeExtensions).GetField("s_config_Internal", statics);
            object priorLookup = lookupField.GetValue(null);
            owned.Lease(new ReleaseAction(() => lookupField.SetValue(null, priorLookup)));
            var categoryObject = owned.Track(ScriptableObject.CreateInstance<TimeCategoryObject>());
            Set(categoryObject, "m_guid", "lucid-trigger-" + category);
            var lookup = owned.Track(ScriptableObject.CreateInstance<TimeCategoryLookup>());
            lookup.Dictionary.Add(category, categoryObject);
            lookupField.SetValue(null, lookup);
            var setting = new TimeSetting();
            setting.SetCategory(categoryObject, timeScale);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception,
                new System.Text.RegularExpressions.Regex("^NullReferenceException: Object reference not set to an instance of an object$"));
            var manager = owned.Host("Lucid trigger time manager", false).AddComponent<TimeManager>();
            Get<StackableData>(manager, "m_timeSettings")
                .SetBaseValue((int)typeof(TimeManager).GetField("SettingId", statics).GetRawConstantValue(), setting);
            return manager;
        }

        // Fills a component's own TimeManager reference through its InternalReplaceSystem, then revokes it
        // and puts the startup observers back, which leaves the registry row alone as an unregistered
        // system would.
        internal static void PublishTimeManager(Owned owned, SystemRef<TimeManager> reference, TimeManager manager)
            => owned.Lease(new SystemRefLease<TimeManager>(reference, manager));
    }
}
