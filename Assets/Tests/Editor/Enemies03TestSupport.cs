using System;
using Hardlight;
using HardlightProject;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Shared construction for the enemy brain, state, navigation and debug fixtures. Every object is built through its
    // shipped constructor on an owned inactive host; the rows the Enemy field initialisers cache typed references from
    // are leased and released idle.
    internal static class Enemies03TestSupport
    {
        internal static string Name() => "Lucid-enemy-" + Guid.NewGuid().ToString("N");

        internal static FiniteStateMachine NewFsm() => new FiniteStateMachine(new FSMIdentifier(Name()), skipAddToManager: true);

        internal static FSMUpdateContext Context(float deltaTime = 0.02f) => new FSMUpdateContext(deltaTime, FSMUpdateType.FixedUpdate);

        // An Enemy that never started: Edit Mode runs neither Awake nor Start, so it holds no definition.
        internal static Enemy NewEnemy(Owned owned)
        {
            owned.LeaseRow<CharacterManager>();
            owned.LeaseRow<EnemyManager>();
            owned.LeaseRow<GameplayIslandManager>();
            return owned.Host(Name(), false).AddComponent<Enemy>();
        }

        // A brain initialised on a new Enemy with an empty state machine, so it has its own storage and no target.
        internal static void WithBrain(Action<Owned, EnemyBrain, Enemy> body) => Owned.Run(owned =>
        {
            owned.Lease(new VerificationFixtureOwnership.ActionPoolLease());
            Enemy enemy = NewEnemy(owned);
            var brain = new EnemyBrain();
            brain.Initialise(enemy, NewFsm());
            body(owned, brain, enemy);
        });
    }
}
