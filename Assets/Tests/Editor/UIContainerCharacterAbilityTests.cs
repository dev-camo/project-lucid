using System;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CameraManagerClientSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The character ability UI containers: the stamina base (Game.Runtime 02000868, x86-64 0x611E20..0x612E80), its
    // generic form (02000867, 0xCA7690..0xCA7720) and the Air (02000862, 0x611D30), BoostStamina (02000863, 0x612030),
    // Chaos (02000864, 0x612490), ChaosStamina (02000865, 0x612940) and SpinDash (02000866, 0x612C30) containers.
    // Each is a real component on an owned inactive host. The Air container runs through a real Character, a real
    // ability and a real CinemachineCameraManager, whose camera is not ready here, so the camera is never oriented.
    // Not covered: BoostStamina and ChaosStamina Update (they read Character.BoostStamina and ChaosStamina, which only
    // Character.Initialise creates), SpinDash Update (Character.SpinDashRoll, the same), Chaos InitialiseTransforms and
    // LateUpdate (the aim ability's targeting), Chaos Setup for an aim ability (it schedules a frame callback on the
    // process-wide coroutine host), and the AirActive branch of Air Update (the ability's active flag has a private
    // setter reached only through its definition-driven Update).
    public sealed class UIContainerCharacterAbilityTests
    {
        // ---- declarations ----------------------------------------------------------------------------------------

        // dump.cs TypeDefIndex 2151/2150 and the DummyDll rows (fields 0x04002059..0x0400205d, 0x04002058).
        [Test]
        public void TheStaminaBasesMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerCharacterAbilityStamina), 0x00100081,
                new (string, int)[]
                {
                    ("m_staminaUIRemainTime", 0x0001),
                    ("ShouldShow", 0x0026),
                    ("StaminaValue", 0x0026),
                    ("TimeoutPercentageRemaining", 0x0026),
                    ("m_staminaUIRemainTimer", 0x0001),
                },
                new (string, int)[]
                {
                    ("Setup", 0x00c6),
                    ("ResetTimer", 0x0084),
                    ("UpdatePositionAndBinders", 0x0084),
                    (".ctor", 0x1884),
                });
            AssertIl2CppOptions(typeof(UIContainerCharacterAbilityStamina), 2, 1);
            Assert.That(typeof(UIContainerCharacterAbilityStamina).BaseType, Is.EqualTo(typeof(UIContainerCharacterAbility)));
            Assert.That(AttributeTypes(Field(typeof(UIContainerCharacterAbilityStamina), "m_staminaUIRemainTime")),
                Is.EqualTo(new[] { typeof(MinAttribute), typeof(TooltipAttribute), typeof(SerializeField) }));

            Type generic = typeof(UIContainerCharacterAbilityStamina<>);
            AssertMembers(generic, 0x00100081,
                new (string, int)[] { ("<Ability>k__BackingField", 0x0001) },
                new (string, int)[]
                {
                    ("get_Ability", 0x0884),
                    ("set_Ability", 0x0881),
                    ("Setup", 0x00c6),
                    (".ctor", 0x1884),
                });
            AssertIl2CppOptions(generic, 2, 1);
            Assert.That(generic.BaseType, Is.EqualTo(typeof(UIContainerCharacterAbilityStamina)));
            Type constraint = generic.GetGenericArguments()[0].GetGenericParameterConstraints()[0];
            Assert.That(constraint, Is.EqualTo(typeof(CharacterAbility)));
        }

        // dump.cs TypeDefIndex 2145..2149 and 2148 with the DummyDll rows (tokens 0x02000862..0x02000866).
        [Test]
        public void TheConcreteContainersMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerCharacterAbilityAir), 0x00100101,
                new (string, int)[0],
                new (string, int)[] { ("Update", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerCharacterAbilityAir), 1, 2);
            Assert.That(typeof(UIContainerCharacterAbilityAir).BaseType, Is.EqualTo(typeof(UIContainerCharacterAbilityStamina<CharacterAbility_Air>)));

            foreach (Type type in new[] { typeof(UIContainerCharacterAbilityBoostStamina), typeof(UIContainerCharacterAbilityChaosStamina) })
            {
                AssertMembers(type, 0x00100101,
                    new (string, int)[]
                    {
                        ("m_rechargeUIRemainTime", 0x0001),
                        ("CanTrigger", 0x0026),
                        ("RechargeActive", 0x0026),
                        ("m_rechargeUITimer", 0x0001),
                    },
                    new (string, int)[] { ("Update", 0x0081), (".ctor", 0x1886) });
                Assert.That(type.BaseType, Is.EqualTo(typeof(UIContainerCharacterAbilityStamina)));
                Assert.That(AttributeTypes(Field(type, "m_rechargeUIRemainTime")),
                    Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(MinAttribute), typeof(SerializeField) }));
            }
            AssertIl2CppOptions(typeof(UIContainerCharacterAbilityBoostStamina), 1, 2);
            AssertIl2CppOptions(typeof(UIContainerCharacterAbilityChaosStamina), 2, 1);

            AssertMembers(typeof(UIContainerCharacterAbilityChaos), 0x00100101,
                new (string, int)[] { ("m_transforms", 0x0001), ("m_ability", 0x0001), ("ShouldShow", 0x0026) },
                new (string, int)[]
                {
                    ("Setup", 0x00c6),
                    ("InitialiseTransforms", 0x0081),
                    ("LateUpdate", 0x0081),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerCharacterAbilityChaos), 2, 1);
            Assert.That(typeof(UIContainerCharacterAbilityChaos).BaseType, Is.EqualTo(typeof(UIContainerCharacterAbility)));

            AssertMembers(typeof(UIContainerCharacterAbilitySpinDash), 0x00100101,
                new (string, int)[]
                {
                    ("m_arrowUILocation", 0x0001),
                    ("m_arrowUIScaler", 0x0001),
                    ("m_scaleMin", 0x0001),
                    ("m_scaleMax", 0x0001),
                },
                new (string, int)[] { ("Update", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerCharacterAbilitySpinDash), 1, 2);
            Assert.That(typeof(UIContainerCharacterAbilitySpinDash).BaseType,
                Is.EqualTo(typeof(UIContainerCharacterAbilityStamina<CharacterAbility_MovementGroundSpinDashCharge>)));
        }

        // ---- constructors ----------------------------------------------------------------------------------------

        // The stamina constructor 0x612330 sets the remain time to 1.0 (0x3f800000) and creates the three bindables;
        // BoostStamina 0x612280 and ChaosStamina 0x612b80 set their recharge time to 0x3dcccccd and create two more;
        // SpinDash 0x612dd0 sets both arrow scales to Vector3.one; Chaos 0x612860 creates its bindable.
        [Test]
        public void ConstructorsSetTheAuthoredDefaults()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<CinemachineCameraManager>();
                var air = owned.Host("Lucid ability air", false).AddComponent<UIContainerCharacterAbilityAir>();
                Assert.That(Get<float>(air, "m_staminaUIRemainTime"), Is.EqualTo(1f));
                Assert.That(air.ShouldShow.Value, Is.False);
                Assert.That(air.StaminaValue.Value, Is.Zero);
                Assert.That(air.TimeoutPercentageRemaining.Value, Is.Zero);

                var boost = owned.Host("Lucid ability boost", false).AddComponent<UIContainerCharacterAbilityBoostStamina>();
                var chaosStamina = owned.Host("Lucid ability chaos stamina", false).AddComponent<UIContainerCharacterAbilityChaosStamina>();
                foreach (var stamina in new UIContainerCharacterAbilityStamina[] { boost, chaosStamina })
                    Assert.That(Get<float>(stamina, "m_staminaUIRemainTime"), Is.EqualTo(1f));
                Assert.That(Get<float>(boost, "m_rechargeUIRemainTime"), Is.EqualTo(0.1f));
                Assert.That(Get<float>(chaosStamina, "m_rechargeUIRemainTime"), Is.EqualTo(0.1f));
                Assert.That(boost.CanTrigger.Value, Is.False);
                Assert.That(boost.RechargeActive.Value, Is.False);
                Assert.That(chaosStamina.CanTrigger.Value, Is.False);
                Assert.That(chaosStamina.RechargeActive.Value, Is.False);

                var spinDash = owned.Host("Lucid ability spin dash", false).AddComponent<UIContainerCharacterAbilitySpinDash>();
                Assert.That(Get<Vector3>(spinDash, "m_scaleMin"), Is.EqualTo(Vector3.one));
                Assert.That(Get<Vector3>(spinDash, "m_scaleMax"), Is.EqualTo(Vector3.one));

                var chaos = owned.Host("Lucid ability chaos", false).AddComponent<UIContainerCharacterAbilityChaos>();
                Assert.That(chaos.ShouldShow, Is.Not.Null);
                Assert.That(chaos.ShouldShow.Value, Is.False);
            });
        }

        // ---- set-up and update through a real character ---------------------------------------------------------

        private sealed class AbilityRig
        {
            public Rig Camera;
            public Character Character;
            public CapsuleCollider Capsule;
        }

        // Character's field initialiser creates the empty GameplayIslandManager row and CharacterAbility's the empty
        // App row. The character's bounds come from its authored capsule collider, a real collider on an active host.
        private static void WithAbilityUi(Action<Owned, AbilityRig> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<GameplayIslandManager>();
            owned.LeaseRow<App>();
            var rig = new AbilityRig { Camera = NewRig(owned, true, true) };
            rig.Character = owned.Host("Lucid ability character", false).AddComponent<Character>();
            rig.Capsule = owned.Host("Lucid ability capsule", true).AddComponent<CapsuleCollider>();
            rig.Capsule.radius = 0.5f;
            rig.Capsule.height = 2f;
            Set(rig.Character, "m_capsuleColliderCollision", rig.Capsule); // serialized authored reference
            body(owned, rig);
        });

        private static UIContainerCharacterAbilityAir NewAir(Owned owned, AbilityRig rig, out CharacterAbility_Air ability)
        {
            ability = new CharacterAbility_Air();
            var container = owned.Host("Lucid ability air container", false).AddComponent<UIContainerCharacterAbilityAir>();
            container.Setup(new UIContainerCharacterAbilityParameters(rig.Character, ability));
            return container;
        }

        // UIContainerCharacterAbility.Setup 06003087 appends the camera callback to the manager when the main camera
        // is not ready (OnCinemachineCameraManagerValid 06003088), and a repeated Setup appends it again. The stamina
        // Setup 0x612e80 hides the bar and the generic Setup 0xCA76C0 publishes the typed ability.
        [Test]
        public void SetupSubscribesTheCameraCallbackAndPublishesTheAbility()
        {
            WithAbilityUi((owned, rig) =>
            {
                Delegate[] before = rig.Camera.Manager.OnMainCameraReady.GetInvocationList();
                var ability = new CharacterAbility_Air();
                var container = owned.Host("Lucid ability air container", false).AddComponent<UIContainerCharacterAbilityAir>();
                container.ShouldShow.Value = true;

                container.Setup(new UIContainerCharacterAbilityParameters(rig.Character, ability));

                Delegate[] after = rig.Camera.Manager.OnMainCameraReady.GetInvocationList();
                Assert.That(after.Length, Is.EqualTo(before.Length + 1));
                Assert.That(after[after.Length - 1].Target, Is.SameAs(container));
                Assert.That(after[after.Length - 1].Method.Name, Is.EqualTo("SetCamera"));
                Assert.That(container.ShouldShow.Value, Is.False);
                Assert.That(Call(container, "get_Ability"), Is.SameAs(ability));

                container.Setup(new UIContainerCharacterAbilityParameters(rig.Character, ability));
                Assert.That(rig.Camera.Manager.OnMainCameraReady.GetInvocationList().Length, Is.EqualTo(before.Length + 2));
            });
        }

        // TryGetAbility 0600308c is a type test: an ability of another type leaves the typed ability null, while
        // the stamina Setup still runs. The same ability serves the spin dash container when it is the right type.
        [Test]
        public void AnAbilityOfAnotherTypeIsPublishedAsNull()
        {
            WithAbilityUi((owned, rig) =>
            {
                var spinDash = owned.Host("Lucid ability spin dash", false).AddComponent<UIContainerCharacterAbilitySpinDash>();
                spinDash.ShouldShow.Value = true;
                spinDash.Setup(new UIContainerCharacterAbilityParameters(rig.Character, new CharacterAbility_Air()));
                Assert.That(Call(spinDash, "get_Ability"), Is.Null);
                Assert.That(spinDash.ShouldShow.Value, Is.False);

                var charge = new CharacterAbility_MovementGroundSpinDashCharge();
                spinDash.Setup(new UIContainerCharacterAbilityParameters(rig.Character, charge));
                Assert.That(Call(spinDash, "get_Ability"), Is.SameAs(charge));
            });
        }

        // Chaos Setup 0x612490 stores the TryGetAbility result field directly, so an unrelated ability leaves it null,
        // and only a found aim ability schedules the next-frame set-up (not exercised here).
        [Test]
        public void ChaosSetupLeavesTheAimAbilityNullForAnotherAbility()
        {
            WithAbilityUi((owned, rig) =>
            {
                var chaos = owned.Host("Lucid ability chaos", false).AddComponent<UIContainerCharacterAbilityChaos>();

                chaos.Setup(new UIContainerCharacterAbilityParameters(rig.Character, new CharacterAbility_Air()));

                Assert.That(Get<CharacterAbility_AirChaosAim>(chaos, "m_ability"), Is.Null);
            });
        }

        // Air Update 0x611d30 with a fresh ability: no stamina left (GetStaminaRemaining is 0), so the bar shows
        // (1.0 > 0 at 0x611de3), is placed at half the character's bounds height (0x611ed0..0x611ef9), the timeout
        // reads full (0x611f0c) and the stamina value is not written because the ability is not active (shouldUpdate).
        [Test]
        public void AirShowsTheBarAboveTheCharacterWhileStaminaIsBelowFull()
        {
            WithAbilityUi((owned, rig) =>
            {
                UIContainerCharacterAbilityAir container = NewAir(owned, rig, out CharacterAbility_Air ability);
                container.StaminaValue.Value = 0.5f;

                Call(container, "Update");

                Assert.That(container.ShouldShow.Value, Is.True);
                Assert.That(container.StaminaValue.Value, Is.EqualTo(0.5f), "not written while the ability is inactive");
                Assert.That(container.TimeoutPercentageRemaining.Value, Is.EqualTo(1f));
                Assume.That(rig.Capsule.bounds.size.y, Is.GreaterThan(0f), "the editor reports the capsule's bounds");
                Assert.That(container.transform.localPosition, Is.EqualTo(new Vector3(0f, rig.Capsule.bounds.size.y * 0.5f, 0f)));
            });
        }

        // Once the stamina is full and the ability inactive nothing keeps the bar shown: a zero remain time hides it
        // on the next update (timer >= remain time, 0x611fc5) without writing the stamina value.
        [Test]
        public void AirHidesTheBarWhenStaminaIsFullAndTheRemainTimeIsZero()
        {
            WithAbilityUi((owned, rig) =>
            {
                UIContainerCharacterAbilityAir container = NewAir(owned, rig, out CharacterAbility_Air ability);
                Call(container, "Update");
                Assert.That(container.ShouldShow.Value, Is.True, "set-up: shown while stamina is empty");
                // Full stamina needs the ability's definition-driven Initialise; its private stamina time is set directly.
                Set(ability, "m_staminaTimeSeconds", 2f);
                Set(container, "m_staminaUIRemainTime", 0f);
                container.StaminaValue.Value = 0.5f;

                Call(container, "Update");

                Assert.That(ability.GetStaminaRemaining(), Is.EqualTo(1f), "set-up: the ability reports full stamina");
                Assert.That(container.ShouldShow.Value, Is.False);
                Assert.That(container.StaminaValue.Value, Is.EqualTo(0.5f));
            });
        }

        // While the remain time has not elapsed the bar stays and the value reads full (0x611f82..0x611f93) with
        // the timeout counting down from one (1 - timer / remain time, 0x611f9e..0x611fbd).
        [Test]
        public void AirKeepsTheBarDuringTheRemainTime()
        {
            WithAbilityUi((owned, rig) =>
            {
                UIContainerCharacterAbilityAir container = NewAir(owned, rig, out CharacterAbility_Air ability);
                Call(container, "Update");
                Set(ability, "m_staminaTimeSeconds", 2f);
                Set(container, "m_staminaUIRemainTime", 1000f);
                container.StaminaValue.Value = 0.5f;

                Call(container, "Update");

                Assert.That(container.ShouldShow.Value, Is.True, "still shown");
                Assert.That(container.StaminaValue.Value, Is.EqualTo(1f));
                Assert.That(container.TimeoutPercentageRemaining.Value, Is.EqualTo(1f).Within(0.01f));
                Assert.That(container.TimeoutPercentageRemaining.Value, Is.LessThanOrEqualTo(1f));
            });
        }

        // The update is gated on the parameters' character being alive (op_Inequality at 0x611db0): a destroyed
        // character leaves the bindables and the position untouched.
        [Test]
        public void AirDoesNothingForADestroyedCharacter()
        {
            WithAbilityUi((owned, rig) =>
            {
                UIContainerCharacterAbilityAir container = NewAir(owned, rig, out CharacterAbility_Air ability);
                UnityEngine.Object.DestroyImmediate(rig.Character);
                container.ShouldShow.Value = true;
                container.TimeoutPercentageRemaining.Value = 0.25f;
                container.transform.localPosition = new Vector3(1f, 2f, 3f);

                Call(container, "Update");

                Assert.That(container.ShouldShow.Value, Is.True);
                Assert.That(container.TimeoutPercentageRemaining.Value, Is.EqualTo(0.25f));
                Assert.That(container.transform.localPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            });
        }
    }
}
