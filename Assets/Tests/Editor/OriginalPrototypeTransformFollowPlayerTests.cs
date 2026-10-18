using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // HardlightProject.PrototypeTransformFollowPlayer (TypeDef 0x020007a8, x86-64 0x5DDA80..0x5DDCBA).
    // The character comes from AddComponent<Character> and the CharacterManager is built by its
    // constructor (OwnedCharacterManagers) and reached through a reference made for the case. Time.deltaTime is
    // read as the Editor reports it; the follower starts rolled, so the rotation rebuilt from
    // LookRotation shows as a dropped roll.
    public sealed class OriginalPrototypeTransformFollowPlayerTests
    {
        [Test]
        public void DeclarationsAndConstructorDefaults()
        {
            AssertMembers(typeof(PrototypeTransformFollowPlayer), 0x00100001,
                new[] { ("m_speed", 0x0001), ("m_rotateSpeed", 0x0001), ("m_characterManagerRef", 0x0001) },
                new[] { ("Update", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(PrototypeTransformFollowPlayer), 1, 2);
            // .ctor 0x5DDC40 caches GetSystemRef<CharacterManager>(null, true).
            Owned.Run(owned =>
            {
                owned.LeaseRow<CharacterManager>();
                var follower = owned.Host("Lucid prototype follower").AddComponent<PrototypeTransformFollowPlayer>();
                Assert.That(Get<SystemRef<CharacterManager>>(follower, "m_characterManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<CharacterManager>()));
                Assert.That((Get<float>(follower, "m_speed"), Get<float>(follower, "m_rotateSpeed")), Is.EqualTo((0f, 0f)));
            });
        }

        // Update 0x5DDA80 needs SystemRef.TryGet and TryGetCurrentCharacter. It then sets
        // rotation = LookRotation(RotateTowards(forward, characterPosition - position,
        // m_rotateSpeed * deltaTime, 0)) and afterwards position += forward * m_speed * deltaTime,
        // reading the forward again after the rotation (0x5DDBDD). Time.deltaTime cannot be set in a batch
        // Edit Mode run (it read 0.003..0.005), so the speeds are scaled by the observed delta: the turn is
        // 0.5 rad and the move 5 units whatever the delta, and moving along the old forward lands about 2.5 away.
        [Test]
        public void UpdateTurnsTowardsTheCharacterAndMovesAlongTheNewForward()
        {
            float deltaTime = Time.deltaTime;
            Assume.That(deltaTime, Is.GreaterThan(0f), "the turn and the move need a nonzero frame delta");
            float rotateSpeed = 0.5f / deltaTime, speed = 5f / deltaTime;
            Owned.Run(owned =>
            {
                // First, so it is released last: the manager constructor's rows outlive the case's.
                var characters = owned.Lease(new OwnedCharacterManagers());
                owned.LeaseRow<GameplayIslandManager>();
                owned.LeaseRow<CharacterManager>();
                Character character = owned.Host("Lucid followed character").AddComponent<Character>();
                character.transform.position = new Vector3(10f, 0f, 10f);
                CharacterManager manager = characters.NewManager();
                Set(manager, "m_currentCharacter", character);

                var follower = owned.Host("Lucid prototype follower").AddComponent<PrototypeTransformFollowPlayer>();
                Set(follower, "m_speed", speed);
                Set(follower, "m_rotateSpeed", rotateSpeed);
                Transform transform = follower.transform;
                var startPosition = new Vector3(1f, 2f, 3f);
                Quaternion startRotation = Quaternion.Euler(0f, 0f, 40f);

                transform.SetPositionAndRotation(startPosition, startRotation);
                var before = (transform.position, transform.rotation);
                Call(follower, "Update");
                Assert.That((transform.position, transform.rotation), Is.EqualTo(before), "the leased reference is empty");

                Set(follower, "m_characterManagerRef", new SystemRef<CharacterManager>(ProcessManager.GetDefaultName<CharacterManager>(), manager));
                Call(follower, "Update");
                Assert.That((transform.position, transform.rotation), Is.EqualTo(before), "no valid current character");

                Set(manager, "<CharacterValid>k__BackingField", true);
                Vector3 forward = startRotation * Vector3.forward;
                Quaternion rotation = Quaternion.LookRotation(Vector3.RotateTowards(forward, character.transform.position - startPosition, rotateSpeed * deltaTime, 0f));
                Vector3 position = startPosition + rotation * Vector3.forward * speed * deltaTime;
                Vector3 alongOldForward = startPosition + forward * speed * deltaTime;
                Call(follower, "Update");
                Assert.That(Quaternion.Angle(rotation, transform.rotation), Is.LessThan(1e-3f), "rotation");
                Assert.That(Quaternion.Angle(startRotation, transform.rotation), Is.GreaterThan(1f), "the roll is dropped");
                WorldMemberAccess.AreClose(position, transform.position, "position");
                Assert.That(Vector3.Distance(alongOldForward, transform.position), Is.GreaterThan(1f), "not along the old forward");
            });
        }
    }
}
