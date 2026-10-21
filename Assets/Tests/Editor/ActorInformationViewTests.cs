using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using static ProjectLucid.Tests.BossAbilityTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The boss debug views (Game.Runtime 02000602 ViewActorInformation, 02000603 ViewBossInformation, 02000851
    // ViewBossAbilityInfoParameters, 02000852 ViewBossAbilityInfo; x86-64 0x5633E0..0x563DC0 and 0x60DAC0..0x60DD10). The views are real
    // components on owned hosts; Awake, OnEnable and Update are called directly, as Edit Mode sends none of them. The debug inputs the
    // overlay binds are leased through ControlTableLease, which refuses an already bound input and removes the keys the view adds.
    // Not covered: the development-only Actor.DebugGet... calls that fill the actor, state, FSM, collision and animator texts are
    // compiled out of retail (Update fills those texts with the empty string and the animator columns from the empty split); the
    // Camera.current branch of ViewBossAbilityInfo.OnSetup only exercises its null result, as no camera is rendering in Edit Mode.
    public sealed class ActorInformationViewTests
    {
        private static readonly string NewLine = Environment.NewLine;
        // Everything a view fixture leases: the debug inputs.
        private static void WithView(Action<Owned> body) =>
            ControlTableLease.Run(() => Owned.Run(body), GameInput.DebugNextInfo, GameInput.DebugClearFsmInfo);

        private static T NewView<T>(Owned owned) where T : ViewActorInformation => owned.Host("Lucid actor information view", false).AddComponent<T>();

        private static List<GameObject> NewPanels(Owned owned, ViewActorInformation view, int count)
        {
            var panels = new List<GameObject>();
            for (int i = 0; i < count; i++)
            {
                GameObject panel = owned.Host("Lucid actor information panel " + i);
                panels.Add(panel);
                Get<List<GameObject>>(view, "m_infoList").Add(panel);
            }
            return panels;
        }

        private static bool[] Visible(List<GameObject> panels) => panels.ConvertAll(panel => panel.activeSelf).ToArray();

        // 0x5633E0: Awake takes the typed reference to the entity activation manager row.
        [Test]
        public void AwakeTakesTheEntityActivationManagerReference()
        {
            WithView(owned =>
            {
                owned.LeaseRow<EntityActivationManager>(); // the registry creates the row for the reference
                ViewActorInformation view = NewView<ViewActorInformation>(owned);
                Call(view, "Awake");
                var reference = Get<SystemRef<EntityActivationManager>>(view, "m_entityActivationManagerRef");
                Assert.That(reference, Is.Not.Null);
                Assert.That(reference.SystemName(), Is.EqualTo(ProcessManager.GetDefaultName<EntityActivationManager>()));
            });
        }

        // 0x563460 (Action<float>(OnDebugNextInfo) with 0x95A76B23, then Action<float>(OnDebugClearFsmInfo) with 0xA17EFB54, both Subscribe
        // with edx = 1, a Down trigger, and ecx = -1, any joystick) and the info-list pass that follows: only the panel at the index
        // (zero) is shown. Subscribe fills the down button pair and the shared swipe and scroll wheel tables.
        [Test]
        public void EnablingBindsBothDebugInputsAndShowsOnlyTheFirstPanel()
        {
            WithView(owned =>
            {
                ViewActorInformation view = NewView<ViewActorInformation>(owned);
                List<GameObject> panels = NewPanels(owned, view, 3);

                Call(view, "OnEnable");

                Assert.That(ControlTables.Subscriptions(GameInput.DebugNextInfo, view),
                    Is.EqualTo(ControlTables.Expected(InputTrigger.Down, "ViewActorInformation.OnDebugNextInfo")));
                Assert.That(ControlTables.Subscriptions(GameInput.DebugClearFsmInfo, view),
                    Is.EqualTo(ControlTables.Expected(InputTrigger.Down, "ViewActorInformation.OnDebugClearFsmInfo")));
                Assert.That(Visible(panels), Is.EqualTo(new[] { true, false, false }));
                Call(view, "OnDisable");
            });
        }

        // 0x563650: disabling removes only the next-info subscription (the clear-FSM-info one stays bound, as in the original) and
        // forgets the actor.
        [Test]
        public void DisablingUnbindsOnlyTheNextInfoInputAndForgetsTheActor()
        {
            WithView(owned =>
            {
                ViewActorInformation view = NewView<ViewActorInformation>(owned);
                NewPanels(owned, view, 2);
                Call(view, "OnEnable");
                Set(view, "m_actor", owned.Host("Lucid actor information boss", false).AddComponent<Boss>());

                Call(view, "OnDisable");

                Assert.That(ControlTables.Subscriptions(GameInput.DebugNextInfo, view), Is.Empty);
                Assert.That(ControlTables.Subscriptions(GameInput.DebugClearFsmInfo, view),
                    Is.EqualTo(ControlTables.Expected(InputTrigger.Down, "ViewActorInformation.OnDebugClearFsmInfo")));
                Assert.That(Get<Actor>(view, "m_actor"), Is.Null);
            });
        }

        // 0x563700: each next-info input advances the index around the list and shows only that panel; a single panel keeps showing.
        [Test]
        public void NextInfoCyclesThroughThePanels()
        {
            WithView(owned =>
            {
                ViewActorInformation view = NewView<ViewActorInformation>(owned);
                List<GameObject> panels = NewPanels(owned, view, 3);
                Call(view, "OnEnable");

                var seen = new List<bool[]>();
                for (int i = 0; i < 4; i++)
                {
                    Call(view, "OnDebugNextInfo", 1f);
                    seen.Add(Visible(panels));
                }

                Assert.That(seen[0], Is.EqualTo(new[] { false, true, false }));
                Assert.That(seen[1], Is.EqualTo(new[] { false, false, true }));
                Assert.That(seen[2], Is.EqualTo(new[] { true, false, false }), "it wraps around");
                Assert.That(seen[3], Is.EqualTo(new[] { false, true, false }));
                Call(view, "OnDisable");
            });

            WithView(owned =>
            {
                ViewActorInformation view = NewView<ViewActorInformation>(owned);
                List<GameObject> panels = NewPanels(owned, view, 1);
                Call(view, "OnDebugNextInfo", 1f);
                Assert.That(Visible(panels), Is.EqualTo(new[] { true }));
                Assert.That(Get<int>(view, "m_infoIndex"), Is.Zero);
            });
        }

        // 0x5637C0: the clear-FSM-info input only tests the actor; the Actor.DebugClearFsmInfo call after the test is compiled out of
        // retail, so with or without an actor nothing changes.
        [Test]
        public void ClearingFsmInfoChangesNothingInRetail()
        {
            WithView(owned =>
            {
                ViewActorInformation view = NewView<ViewActorInformation>(owned);
                Assert.DoesNotThrow(() => Call(view, "OnDebugClearFsmInfo", 1f));
                Boss boss = NewBoss(owned, Vector3.zero);
                Set(view, "m_actor", boss);
                Assert.DoesNotThrow(() => Call(view, "OnDebugClearFsmInfo", 1f));
                Assert.That(Get<Actor>(view, "m_actor"), Is.SameAs(boss));
            });
        }

        // 0x563865: without an actor the update returns before touching any text.
        [Test]
        public void UpdateWithoutAnActorLeavesTheTextsAlone()
        {
            WithView(owned =>
            {
                ViewActorInformation view = NewView<ViewActorInformation>(owned);
                view.ActorInfo.Value = "stale";
                view.EntityActivationInfo.Value = "stale";
                view.AnimatorColumn0Info.Value = "stale";
                Call(view, "Update");
                Assert.That(view.ActorInfo.Value, Is.EqualTo("stale"));
                Assert.That(view.EntityActivationInfo.Value, Is.EqualTo("stale"));
                Assert.That(view.AnimatorColumn0Info.Value, Is.EqualTo("stale"));
            });
        }

        // 0x563820..0x563B15: with an actor every text is republished. The retail build fills only the entity activation text, from the
        // manager's debug info; the other texts are the empty string (the builder is cleared before each is read). The animator text is
        // empty, so it splits into one empty line: the first column takes it with a line ending and the second is empty. The
        // transform text is never written.
        [Test]
        public void UpdateWithAnActorRepublishesEveryText()
        {
            WithView(owned =>
            {
                owned.LeaseActions(SystemAction.Update);
                owned.Register(new EntityActivationManager());
                ViewActorInformation view = NewView<ViewActorInformation>(owned);
                Call(view, "Awake");
                Set(view, "m_actor", owned.Host("Lucid actor information actor", false).AddComponent<Boss>());
                foreach (var text in new[] { view.ActorInfo, view.StateInfo, view.FsmInfo, view.CollisionInfo, view.EntityActivationInfo, view.TransformInfo,
                    view.AnimatorColumn0Info, view.AnimatorColumn1Info })
                    text.Value = "stale";

                Call(view, "Update");

                Assert.That(view.ActorInfo.Value, Is.Empty);
                Assert.That(view.StateInfo.Value, Is.Empty);
                Assert.That(view.FsmInfo.Value, Is.Empty);
                Assert.That(view.CollisionInfo.Value, Is.Empty);
                Assert.That(view.EntityActivationInfo.Value,
                    Is.EqualTo(NewLine + "Active objects (0): " + NewLine + NewLine + NewLine + "Inactive objects (0): " + NewLine));
                Assert.That(view.AnimatorColumn0Info.Value, Is.EqualTo(NewLine));
                Assert.That(view.AnimatorColumn1Info.Value, Is.Empty);
                Assert.That(view.TransformInfo.Value, Is.EqualTo("stale"), "never written");
            });
        }

        // ---- ViewBossInformation ----

        // 0x563D40: the boss overlay takes the boss found in the scene as its actor before the base binds the inputs; with no active
        // boss the actor is null and the base still runs.
        [Test]
        public void BossInformationTakesTheSceneBossBeforeBinding()
        {
            WithView(owned =>
            {
                ViewBossInformation view = NewView<ViewBossInformation>(owned);
                if (UnityEngine.Object.FindObjectsOfType<Boss>().Length != 0)
                    Assert.Inconclusive("a boss from outside this case is active in the scene");
                Call(view, "OnEnable");
                Assert.That(Get<Actor>(view, "m_actor"), Is.Null, "no active boss in the scene");
                Assert.That(ControlTables.Subscriptions(GameInput.DebugNextInfo, view), Is.Not.Empty, "the base ran");
                Call(view, "OnDisable");

                Boss boss = owned.Host("Lucid actor information scene boss").AddComponent<Boss>();
                Call(view, "OnEnable");
                Assert.That(Get<Actor>(view, "m_actor"), Is.SameAs(boss));
                Call(view, "OnDisable");
            });
        }

        // ---- ViewBossAbilityInfo ----

        // An ability whose UI text is under the case's control.
        private sealed class AbilityInfoTextProbe : ActorAbility
        {
            public bool HasText;
            public override AbilityDefinition DefinitionBase => null;

            public override bool GetUIText(StringBuilder stringInfoBuilder)
            {
                stringInfoBuilder.Append("probe ability text");
                return HasText;
            }
        }

        // 0x60DAC0: the parameters keep the boss and the ability.
        [Test]
        public void AbilityInfoParametersKeepTheBossAndTheAbility()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, Vector3.zero);
                var ability = new AbilityInfoTextProbe();
                var parameters = new ViewBossAbilityInfoParameters(boss, ability);
                Assert.That(parameters.Boss, Is.SameAs(boss));
                Assert.That(parameters.Ability, Is.SameAs(ability));
            });
        }

        private static ViewBossAbilityInfo NewAbilityInfoView(Owned owned, bool active, out Canvas canvas)
        {
            GameObject host = owned.Track(new GameObject("Lucid boss ability info view", typeof(RectTransform)));
            host.SetActive(active);
            canvas = host.AddComponent<Canvas>();
            return host.AddComponent<ViewBossAbilityInfo>();
        }

        // 0x60DB10: parameters of another type are ignored, so the view neither takes a boss nor changes its visibility.
        [Test]
        public void AbilityInfoSetupIgnoresOtherParameters()
        {
            Owned.Run(owned =>
            {
                ViewBossAbilityInfo view = NewAbilityInfoView(owned, true, out _);
                view.Setup(new ViewParameters());
                Assert.That(Get<Boss>(view, "m_boss"), Is.Null);
                Assert.That(view.gameObject.activeSelf, Is.True);
                view.Setup(null);
                Assert.That(Get<Boss>(view, "m_boss"), Is.Null);
            });
        }

        // 0x60DB10..0x60DC2D: the boss and ability are kept. A missing ability (a plain reference test) hides the view and does nothing
        // more; an ability shows it, parents it to the boss without keeping its world position (the local position stays) and gives
        // the canvas the current camera, which is none outside rendering.
        [Test]
        public void AbilityInfoSetupHidesWithoutAnAbilityAndOtherwiseParentsToTheBoss()
        {
            Owned.Run(owned =>
            {
                Boss boss = NewBoss(owned, Vector3.zero);
                boss.transform.position = new Vector3(10f, 0f, 0f);
                ViewBossAbilityInfo view = NewAbilityInfoView(owned, true, out Canvas canvas);

                view.Setup(new ViewBossAbilityInfoParameters(boss, null));
                Assert.That(Get<Boss>(view, "m_boss"), Is.SameAs(boss));
                Assert.That(view.gameObject.activeSelf, Is.False);
                Assert.That(view.transform.parent, Is.Null, "not parented");

                view.gameObject.SetActive(false);
                view.transform.localPosition = new Vector3(1f, 2f, 3f);
                canvas.worldCamera = owned.Host("Lucid boss ability info camera", false).AddComponent<Camera>();
                var ability = new AbilityInfoTextProbe();

                view.Setup(new ViewBossAbilityInfoParameters(boss, ability));

                Assert.That(Get<ActorAbility>(view, "m_ability"), Is.SameAs(ability));
                Assert.That(view.gameObject.activeSelf, Is.True);
                Assert.That(view.transform.parent, Is.SameAs(boss.transform));
                AssertNear(view.transform.localPosition, new Vector3(1f, 2f, 3f), "the local position is kept", 1e-5f);
                Assert.That(canvas.worldCamera, Is.Null, "Camera.current is null outside rendering");
            });
        }

        // SetText reads the default style sheet from TMP_Settings.instance (TMP_Text.GetStyle). The Resources "TMP Settings" asset of
        // the recovered project is not bound to this TextMesh Pro package's settings script, so no instance loads; an owned empty
        // settings object stands in for the case and the loaded one (none) is put back.
        private static void LeaseTextSettings(Owned owned)
        {
            FieldInfo instance = typeof(TMP_Settings).GetField("s_Instance", BindingFlags.Static | BindingFlags.NonPublic);
            object prior = instance.GetValue(null);
            owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => instance.SetValue(null, prior)));
            instance.SetValue(null, owned.Track(ScriptableObject.CreateInstance<TMP_Settings>()));
        }

        // 0x60DC40..0x60DD0F: each update asks the ability for its text. Without text the view is hidden and nothing else changes; with
        // text it is shown and placed one boss height (the collider bounds' size.y = 3) above the boss, and the label gets the text.
        [Test]
        public void AbilityInfoUpdateShowsTheAbilityTextAboveTheBoss()
        {
            Owned.Run(owned =>
            {
                LeaseTextSettings(owned);
                Boss boss = owned.Host("Lucid boss ability info boss").AddComponent<Boss>();
                BoxCollider collider = boss.gameObject.AddComponent<BoxCollider>();
                collider.size = new Vector3(2f, 3f, 4f);
                Set(boss, "m_colliderCollision", collider);
                ViewBossAbilityInfo view = NewAbilityInfoView(owned, true, out _);
                GameObject labelHost = owned.Track(new GameObject("Lucid boss ability info label", typeof(RectTransform)));
                labelHost.SetActive(false);
                TextMeshProUGUI label = labelHost.AddComponent<TextMeshProUGUI>();
                Set(view, "m_nameText", label); // serialized
                var ability = new AbilityInfoTextProbe { HasText = false };
                view.Setup(new ViewBossAbilityInfoParameters(boss, ability));
                view.transform.localPosition = new Vector3(5f, 5f, 5f);
                Physics.SyncTransforms();

                Call(view, "Update");
                Assert.That(view.gameObject.activeSelf, Is.False, "no text hides the view");
                AssertNear(view.transform.localPosition, new Vector3(5f, 5f, 5f), "and leaves it where it was", 1e-5f);
                Assert.That(label.text, Is.Null.Or.Empty);

                view.gameObject.SetActive(true);
                ability.HasText = true;
                Call(view, "Update");
                Assert.That(view.gameObject.activeSelf, Is.True);
                AssertNear(view.transform.localPosition, new Vector3(0f, 3f, 0f), "one boss height up", 1e-4f);
                Assert.That(label.text, Is.EqualTo("probe ability text"));
            });
        }
    }
}
