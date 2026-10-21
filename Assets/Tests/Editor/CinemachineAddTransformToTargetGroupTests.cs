using System;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CinemachineAddTransformToTargetGroup (TypeDef 0x02000268, x86-64 0x717C00..0x717EDF). The
    // component and a real CinemachineTargetGroup are on owned inactive hosts, so the Editor sends no OnEnable; the
    // private OnEnable and OnDisable are called by name. The serialized fields have no setter and are written the way
    // the serializer does.
    public sealed class CinemachineAddTransformToTargetGroupTests
    {
        private static void WithGroup(Action<Owned, CinemachineTargetGroup, CinemachineAddTransformToTargetGroup> body) =>
            Owned.Run(owned =>
            {
                var group = owned.Host("Lucid target group", false).AddComponent<CinemachineTargetGroup>();
                var member = owned.Host("Lucid group member", false).AddComponent<CinemachineAddTransformToTargetGroup>();
                Set(member, "m_targetGroup", group);
                body(owned, group, member);
            });

        private static Transform Other(Owned owned, string name) => owned.Host(name, false).transform;

        private static string[] Names(CinemachineTargetGroup group) => group.m_Targets.Select(target => target.target.name).ToArray();

        private static (string name, float weight, float radius)[] Rows(IEnumerable<CinemachineTargetGroup.Target> targets) =>
            targets.Select(target => (target.target.name, target.weight, target.radius)).ToArray();

        // The constructor 0x717EC0 stores 1.0f at +0x28 and +0x2C (one movsd of the pair at 0x29a6c30) and 0x0101 at
        // +0x30, so the member joins on enable and leaves on disable; the two clearing flags stay false.
        [Test]
        public void ANewComponentJoinsAndLeavesWithUnitWeightAndRadius()
        {
            WithGroup((owned, group, member) =>
            {
                Assert.That(Get<float>(member, "m_weight"), Is.EqualTo(1f));
                Assert.That(Get<float>(member, "m_radius"), Is.EqualTo(1f));
                Assert.That(Get<bool>(member, "m_addOnEnable"), Is.True);
                Assert.That(Get<bool>(member, "m_removeOnDisable"), Is.True);
                Assert.That(Get<bool>(member, "m_removeAllOthers"), Is.False);
                Assert.That(Get<bool>(member, "m_restoreAllOthersOnRemove"), Is.False);

                Call(member, "OnEnable");
                Assert.That(Rows(group.m_Targets), Is.EqualTo(new[] { (member.name, 1f, 1f) }));
                Call(member, "OnDisable");
                Assert.That(group.m_Targets, Is.Empty);
            });
        }

        // AddTransformToGroup 0x717C00 passes its own transform with the stored weight and radius (0x717d36..0x717d64);
        // without the clearing flag it never touches the other members or the parked list (0x717c45).
        [Test]
        public void AddingAppendsTheOwnTransformWithTheAuthoredWeightAndRadius()
        {
            WithGroup((owned, group, member) =>
            {
                group.AddMember(Other(owned, "first"), 3f, 4f);
                Set(member, "m_weight", 2.5f);
                Set(member, "m_radius", 7f);

                member.AddTransformToGroup();

                Assert.That(Rows(group.m_Targets), Is.EqualTo(new[] { ("first", 3f, 4f), (member.name, 2.5f, 7f) }));
                Assert.That(Get<List<CinemachineTargetGroup.Target>>(member, "m_clearedTargetGroup"), Is.Null);
            });
        }

        // With the clearing flag the group is walked from its last member to its first (r12 steps down by 0x10 from the
        // last element, 0x717c9a..0x717cdf), each member copied to the parked list and then removed, so the parked list
        // is the reverse of the group. Removal with the restore flag takes this transform out first and then adds the
        // parked members back in parked order (the loop from index 0, 0x717e10..0x717e3a) and clears the list.
        [Test]
        public void ClearingParksTheOtherMembersLastToFirstAndRestoringBringsThemBackInThatOrder()
        {
            WithGroup((owned, group, member) =>
            {
                group.AddMember(Other(owned, "A"), 1f, 2f);
                group.AddMember(Other(owned, "B"), 3f, 4f);
                group.AddMember(Other(owned, "C"), 5f, 6f);
                Set(member, "m_removeAllOthers", true);
                Set(member, "m_restoreAllOthersOnRemove", true);

                member.AddTransformToGroup();

                Assert.That(Names(group), Is.EqualTo(new[] { member.name }), "only this transform is left");
                var parked = Get<List<CinemachineTargetGroup.Target>>(member, "m_clearedTargetGroup");
                Assert.That(Rows(parked), Is.EqualTo(new[] { ("C", 5f, 6f), ("B", 3f, 4f), ("A", 1f, 2f) }));

                member.RemoveTransformFromGroup();

                Assert.That(Rows(group.m_Targets), Is.EqualTo(new[] { ("C", 5f, 6f), ("B", 3f, 4f), ("A", 1f, 2f) }));
                Assert.That(parked, Is.Empty, "the parked list is cleared after restoring");
            });
        }

        // RemoveTransformFromGroup 0x717D90 tests the restore flag (0x717dec) before it reads the parked list, so
        // without the flag the parked members stay parked.
        [Test]
        public void RemovingWithoutTheRestoreFlagLeavesTheParkedMembersParked()
        {
            WithGroup((owned, group, member) =>
            {
                group.AddMember(Other(owned, "A"), 1f, 1f);
                group.AddMember(Other(owned, "B"), 1f, 1f);
                Set(member, "m_removeAllOthers", true);

                member.AddTransformToGroup();
                member.RemoveTransformFromGroup();

                Assert.That(group.m_Targets, Is.Empty);
                Assert.That(Get<List<CinemachineTargetGroup.Target>>(member, "m_clearedTargetGroup").Select(target => target.target.name),
                    Is.EqualTo(new[] { "B", "A" }));
            });
        }

        // Every AddTransformToGroup with the clearing flag allocates a new parked list (the List constructor at
        // 0x717c6e is reached on each call), and the member's own earlier entry is one of the "others" the second call
        // parks. The first call's parked members are therefore dropped, and a restore brings back only the member itself.
        [Test]
        public void AddingTwiceReplacesTheParkedListWithTheFirstAddsOwnEntry()
        {
            WithGroup((owned, group, member) =>
            {
                group.AddMember(Other(owned, "A"), 1f, 1f);
                Set(member, "m_removeAllOthers", true);
                Set(member, "m_restoreAllOthersOnRemove", true);

                member.AddTransformToGroup();
                member.AddTransformToGroup();

                Assert.That(Names(group), Is.EqualTo(new[] { member.name }));
                Assert.That(Get<List<CinemachineTargetGroup.Target>>(member, "m_clearedTargetGroup").Select(target => target.target.name),
                    Is.EqualTo(new[] { member.name }));

                member.RemoveTransformFromGroup();

                Assert.That(Names(group), Is.EqualTo(new[] { member.name }), "the dropped member A never comes back");
            });
        }

        // OnEnable 0x717E80 and OnDisable 0x717EA0 each test one flag (+0x30, +0x31) and tail-call the matching method.
        [Test]
        public void EnableAndDisableFollowTheirOwnFlags()
        {
            WithGroup((owned, group, member) =>
            {
                Set(member, "m_addOnEnable", false);
                Call(member, "OnEnable");
                Assert.That(group.m_Targets, Is.Empty, "enable does not add without its flag");

                member.AddTransformToGroup();
                Set(member, "m_removeOnDisable", false);
                Call(member, "OnDisable");
                Assert.That(Names(group), Is.EqualTo(new[] { member.name }), "disable does not remove without its flag");

                Set(member, "m_removeOnDisable", true);
                Call(member, "OnDisable");
                Assert.That(group.m_Targets, Is.Empty);

                Set(member, "m_addOnEnable", true);
                Call(member, "OnEnable");
                Assert.That(Names(group), Is.EqualTo(new[] { member.name }));
            });
        }

        // OverrideTargetGroup 0x717D70 stores the group (a write barrier tail call); later adds go to the new group.
        [Test]
        public void OverridingTheTargetGroupRedirectsLaterAdds()
        {
            WithGroup((owned, group, member) =>
            {
                var replacement = owned.Host("Lucid replacement group", false).AddComponent<CinemachineTargetGroup>();
                member.OverrideTargetGroup(replacement);

                member.AddTransformToGroup();

                Assert.That(group.m_Targets, Is.Empty);
                Assert.That(Names(replacement), Is.EqualTo(new[] { member.name }));
            });
        }
    }
}
