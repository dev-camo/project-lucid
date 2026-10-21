using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using ProjectLucid.Tests.CharCoreShared;
using ProjectLucid.Tests.WorldSurfaceShared;
using UnityEditor.Animations;
using UnityEngine;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.EnemyTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Shared construction for the enemy ability and movement state fixtures. The Enemy is a real component on an
    // owned inactive host (Edit Mode sends no Awake or Start) with a real brain, an authored definition, a simplified
    // body and the transform that Start would cache; an ability is built by its shipped constructor and given the enemy
    // and its definition through its own Initialise, as Actor.InitialiseAbilities does.
    internal static class EnemyAbilityTestSupport
    {
        // The value a parameter holds before an ability writes to it, so a write of zero is visible.
        internal const float Sentinel = 9f;

        private sealed class UnsubscribeLease : IDisposable
        {
            private readonly ISystem m_system;

            internal UnsubscribeLease(ISystem system) => m_system = system;

            public void Dispose() => ProcessManager.UnsubscribeFromAllActions(m_system);
        }

        internal sealed class Scene
        {
            internal Owned Owned;
            internal Enemy Enemy;
            internal EnemyBrain Brain;
            internal EnemyManager Manager;
            internal EnemyDefinitionProbe Definition;
            internal SimplifiedBody Body;
            internal Animator Animator;
            internal ActorAnimationDefinition Animation;

            internal EnemyTraits Traits => Definition.Traits;

            // The ability is built by its constructor and initialised with a fresh authored definition.
            internal TAbility Ability<TAbility, TDefinition>(Action<TDefinition> author = null)
                where TAbility : EnemyAbility, new()
                where TDefinition : AbilityDefinition
            {
                var definition = Owned.Track(ScriptableObject.CreateInstance<TDefinition>());
                author?.Invoke(definition);
                var ability = new TAbility();
                ability.Initialise(Enemy, definition);
                return ability;
            }

            // The brain's movement impulse as the enemy mirrors it each fixed update.
            internal void Impulse(float x, float y) => SetProperty(Enemy, "BrainMovement", new Vector2(x, y));

            internal void Flag(string property, bool value) => SetProperty(Enemy, property, value);

            // A proxy that records the rotation it is asked for, installed as the enemy's visual proxy.
            internal EnemyVisualProxyProbe AttachProxy()
            {
                var proxy = Owned.Host(Name(), false).AddComponent<EnemyVisualProxyProbe>();
                proxy.ToggleSmoothing(false);
                proxy.CacheTransform();
                SetActorProperty(Enemy, "VisualProxy", proxy);
                return proxy;
            }

            // A real animator with the parameters the movement animation writes, and an animation definition that
            // maps the enemy animation types onto them. The float parameters start at the sentinel.
            internal void AttachAnimator()
            {
                Animator = Owned.Host(Name(), true).AddComponent<Animator>();
                var controller = Owned.Track(new AnimatorController());
                controller.AddLayer("Base");
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
                controller.AddParameter("Lateral", AnimatorControllerParameterType.Float);
                controller.AddParameter("Turning", AnimatorControllerParameterType.Bool);
                controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
                controller.AddParameter("Spotted", AnimatorControllerParameterType.Bool);
                Animator.runtimeAnimatorController = controller;
                Animator.Rebind();
                Animator.Update(0f);
                Animator.SetFloat("Speed", Sentinel);
                Animator.SetFloat("Lateral", Sentinel);
                SetActorProperty(Enemy, "Animator", new ActorAnimator(Animator));
                Animation = Owned.Track(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
                Set(Animation, "m_parameters", new[]
                {
                    Mapping(ActorAnimationType.Enemy_Movement, "Speed"),
                    Mapping(ActorAnimationType.Enemy_Movement_Lateral, "Lateral"),
                    Mapping(ActorAnimationType.Enemy_Movement_Turning, "Turning"),
                    Mapping(ActorAnimationType.Enemy_Movement_BoolSet, "Moving"),
                    Mapping(ActorAnimationType.BoolSet, "Spotted"),
                });
                Call(Animation, "RebuildAnimationParameters");
            }

            private static ActorAnimationDefinition.AnimationParameter Mapping(ActorAnimationType type, string parameter) =>
                new ActorAnimationDefinition.AnimationParameter { AnimationType = type, ParameterName = Parameter(parameter) };

            // The attachment points of an actor, with one node of the given type standing at the given point (or none).
            internal ActorAttachPoints AttachPoints(Actor actor, ActorAttachPointType? nodeType, Vector3 point)
            {
                GameObject host = actor.gameObject;
                var points = host.AddComponent<ActorAttachPoints>();
                var nodes = new List<ActorAttachNode>();
                if (nodeType.HasValue)
                {
                    Transform stand = Owned.Host(Name(), false, host.transform).transform;
                    stand.position = point;
                    var form = new ActorAttachNode.AttachNodeForm();
                    Set(form, "m_transform", stand);
                    Set(form, "m_allForms", true);
                    var node = new ActorAttachNode();
                    Set(node, "m_attachPointType", nodeType.Value);
                    Set(node, "m_nodeForms", new List<ActorAttachNode.AttachNodeForm> { form });
                    node.UpdateForm(ActorFormType.None);
                    nodes.Add(node);
                }
                Set(points, "m_nodes", nodes.ToArray());
                points.Awake();
                SetActorProperty(actor, "ActorAttachPoints", points);
                return points;
            }

            // An actor the enemy can sense, whose aim point (its body attachment node) stands at the given position.
            // A target without a node has attachment points that hold no node at all.
            internal EnemyTargetActorProbe Target(Vector3 aimPoint, bool withNode = true)
            {
                var target = Owned.Host(Name(), false).AddComponent<EnemyTargetActorProbe>();
                AttachPoints(target, withNode ? ActorAttachPointType.Body : (ActorAttachPointType?)null, aimPoint);
                return target;
            }

            // Hands the targets to the manager's list, which the ability reads each update.
            internal void Sense(params Actor[] targets) => ((List<Actor>)Manager.GetTargetsForEnemies()).AddRange(targets);

            internal T BrainStore<T>(GraphStorageKey key, T fallback) => Brain.Storage.GetValue(key, fallback, false);

            internal T ActorStore<T>(GraphStorageKey key, T fallback) => Enemy.Storage.GetValue(key, fallback, false);
        }

        // An enemy at the origin facing +z with up +y, with its own manager (plain instance, subscriptions leased),
        // brain, storage, definition, simplified body and cached transform.
        internal static void WithScene(Action<Scene> body, Action<EnemyTraits> configure = null) => Owned.Run(owned =>
        {
            owned.Lease(new VerificationFixtureOwnership.ActionPoolLease());
            owned.LeaseActions(SystemAction.Initialise, SystemAction.Shutdown);
            var manager = new EnemyManager();
            owned.Lease(new UnsubscribeLease(manager));
            Enemy enemy = NewEnemy(owned);
            var brain = new EnemyBrain();
            brain.Initialise(enemy, NewFsm());
            Call(enemy, "RegisterToEnemyManager", manager);
            SetProperty(enemy, "Brain", brain);
            SetActorProperty(enemy, "Storage", new FSMStorage(0));
            var definition = owned.Track(ScriptableObject.CreateInstance<EnemyDefinitionProbe>());
            definition.Traits = new EnemyTraits();
            configure?.Invoke(definition.Traits);
            definition.Traits.UpdateCachedValues();
            Set(enemy, "m_definition", definition);
            var bodyComponent = enemy.gameObject.AddComponent<SimplifiedBody>();
            Set(enemy, "m_simplifiedBody", bodyComponent);
            // Start caches the transform; Enemy.SetWorldPosition writes through it.
            Set(enemy, "m_transform", enemy.transform);
            Set(enemy, "m_startRotation", Quaternion.identity);
            enemy.SetWorldRotation(Quaternion.identity);
            body(new Scene { Owned = owned, Enemy = enemy, Brain = brain, Manager = manager, Definition = definition, Body = bodyComponent });
        });

        internal static void AssertNear(Vector3 actual, Vector3 expected, string why) =>
            NUnit.Framework.Assert.That((actual - expected).magnitude, NUnit.Framework.Is.LessThan(1e-4f),
                why + ": expected " + expected + " but was " + actual);

        internal static void AssertRotation(Quaternion actual, Quaternion expected, string why) =>
            NUnit.Framework.Assert.That(Quaternion.Angle(actual, expected), NUnit.Framework.Is.LessThan(1e-3f),
                why + ": expected " + expected.eulerAngles + " but was " + actual.eulerAngles);
    }
}
