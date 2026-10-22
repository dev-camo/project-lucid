using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using UnityEngine;
using Object = UnityEngine.Object;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Shared setup for the trigger-condition fixtures. TriggerCondition and
    // TriggerCondition_CachedBase cache their CharacterManager reference in a static field when
    // the type is first used, and the second one is readonly, so a case never replaces either
    // field. It reads the cached SystemRef and fills it through its own InternalReplaceSystem
    // with a CharacterManager built by its real constructor (OwnedCharacterManagers), then revokes the
    // system and puts the startup observers back.
    internal static class TriggerConditionTestSupport
    {
        private const BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo ConditionManagerRef = typeof(TriggerCondition).GetField("m_characterManagerRef", Statics);
        private static readonly FieldInfo CachedManagerRef = typeof(TriggerCondition_CachedBase).GetField("s_characterManagerRef", Statics);
        private static readonly MethodInfo ReplaceSystem = typeof(SystemRef<CharacterManager>).GetMethod("Hardlight.ISystemRef.InternalReplaceSystem", Instance);
        private static readonly MethodInfo RevokeSystem = typeof(SystemRef<CharacterManager>).GetMethod("Hardlight.ISystemRef.InternalRevokeSystem", Instance);
        private static readonly FieldInfo StartupObservers = typeof(SystemRef<CharacterManager>).GetField("OnSystemStartup", Instance);

        // A character with a real FSM storage, current in a genuine CharacterManager.
        internal sealed class Scene
        {
            // Builds further managers, such as a later one a startup event passes.
            internal OwnedCharacterManagers Characters;
            internal Character Character;
            internal FSMStorage Storage;
            internal CharacterManager Manager;
            // What TriggerCondition and TriggerCondition_CachedBase read; one object when they share a row.
            internal SystemRef<CharacterManager> ConditionReference;
            internal SystemRef<CharacterManager> CachedReference;

            internal void SetCurrent(Character character, bool valid)
            {
                Set(Manager, "m_currentCharacter", character);
                Set(Manager, "<CharacterValid>k__BackingField", valid);
            }

            internal static void ReplaceWith(SystemRef<CharacterManager> reference, CharacterManager manager) =>
                ReplaceSystem.Invoke(reference, new object[] { manager });
        }

        // An invalid reference is not necessarily observer-free. Filling it runs its startup observers, emptying it runs its
        // shutdown observers and filling it consumes the deferred work waiting for a valid system, none of which belongs to a
        // fixture. Such a reference is refused before anything is touched.
        internal static void RequireOwnableReference<T>(SystemRef<T> reference) where T : class, ISystem
        {
            if (reference.IsValid()) throw new InvalidOperationException("Fixture requires an empty " + typeof(T).Name + " reference");
            foreach (string field in new[] { "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" })
                if (typeof(SystemRef<T>).GetField(field, Instance).GetValue(reference) != null)
                    throw new InvalidOperationException("Fixture requires a " + typeof(T).Name + " reference without observers or deferred work: " + field);
        }

        // Runs body with both condition types reading the scene's manager. The CharacterManager
        // row is leased first because reading either static can run the type's initialiser,
        // which creates that row.
        internal static void Run(Action<Owned, Scene> body) => Owned.Run(owned =>
        {
            // Taken first, so it is released last: the manager constructor's rows outlive the case's.
            var characters = owned.Lease(new OwnedCharacterManagers());
            owned.LeaseRow<CharacterManager>();
            owned.LeaseRow<GameplayIslandManager>();
            var scene = new Scene
            {
                Characters = characters,
                ConditionReference = (SystemRef<CharacterManager>)ConditionManagerRef.GetValue(null),
                CachedReference = (SystemRef<CharacterManager>)CachedManagerRef.GetValue(null),
            };
            var references = new List<SystemRef<CharacterManager>> { scene.ConditionReference };
            if (!ReferenceEquals(scene.CachedReference, scene.ConditionReference)) references.Add(scene.CachedReference);
            // Admission precedes the try, so a reference that already holds a system is never revoked below.
            foreach (SystemRef<CharacterManager> reference in references)
                RequireOwnableReference(reference);
            object[] priorObservers = references.Select(reference => StartupObservers.GetValue(reference)).ToArray();
            try
            {
                scene.Manager = characters.NewManager();
                scene.Storage = new FSMStorage(0);
                scene.Character = NewCharacter(owned, scene.Storage);
                scene.SetCurrent(scene.Character, true);
                foreach (SystemRef<CharacterManager> reference in references) Scene.ReplaceWith(reference, scene.Manager);
                body(owned, scene);
            }
            finally
            {
                for (int index = 0; index < references.Count; index++)
                {
                    // Only a manager this run built is revoked: the scene's, or a later one the body published.
                    if (characters.Built(references[index].GetSafe())) RevokeSystem.Invoke(references[index], null);
                    StartupObservers.SetValue(references[index], priorObservers[index]);
                }
            }
        });

        // AddComponent<Character> runs no Awake in Edit Mode; its field initialisers lease the
        // GameplayIslandManager row taken by Run.
        internal static Character NewCharacter(Owned owned, IGraphStorage storage)
        {
            GameObject host = owned.Host("Lucid trigger condition character", false);
            host.AddComponent<BoxCollider>();
            var character = host.AddComponent<Character>();
            Set(character, "<Storage>k__BackingField", storage);
            return character;
        }

        // Every comparison type of ComparisonTypeExtensions.Evaluate for value > criteria, then
        // value == criteria, then value < criteria, written out so a case does not recompute
        // the implementation: (type, value above, value equal, value below).
        internal static readonly (ComparisonType type, bool above, bool equal, bool below)[] ComparisonTable =
        {
            (ComparisonType.None, true, true, true),
            (ComparisonType.Equal, false, true, false),
            (ComparisonType.NotEqual, true, false, true),
            (ComparisonType.GreaterThan, true, false, false),
            (ComparisonType.GreaterThanEqual, true, true, false),
            (ComparisonType.LessThan, false, false, true),
            (ComparisonType.LessThanEqual, false, true, true),
        };
    }
}
