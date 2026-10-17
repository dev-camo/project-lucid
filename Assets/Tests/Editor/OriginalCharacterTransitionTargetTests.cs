using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Static checks on CharacterTransition_Target.DoUpdate (0600179e, 1.10.1 RVA 0x794770).
    // Running it needs a started Character (HomingPool and Constants come from OnStart and the
    // data manager), which a test cannot build without fakes. Expected references and order
    // come from the x86-64 body; only member references and their relative order are checked.
    // The runtime cases (no active target with a valid pending one, distinct active and pending
    // targets, a destroyed active Object.Target) are therefore covered only structurally.
    public sealed class OriginalCharacterTransitionTargetTests
    {
        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic;

        // 0x794803 loads Character.HomingPool (+0x398), 0x79480a loads the reference at pool+0x10
        // (<ActiveTarget>k__BackingField) and 0x79480e null-tests it. The pending PooledTarget is an
        // inline struct at pool+0x18 and is never read. Target (0x794817), Type (0x79484a) and
        // WorldPosition (0x794869) are all read through ActiveHomingTarget.Object (+0x18).
        [Test]
        public void DoUpdateReadsTheActiveHomingTargetAndNeverThePendingCandidate()
        {
            List<MemberInfo> refs = References();
            MethodInfo activeTarget = Getter(typeof(CharacterHomingPool), "ActiveTarget");
            MethodInfo pendingTarget = Getter(typeof(CharacterHomingPool), "PendingTarget");
            FieldInfo activeObject = typeof(ActiveHomingTarget).GetField("Object", Declared);
            FieldInfo pooledObject = typeof(CharacterHomingPool.PooledTarget).GetField("Object", Declared);

            Assert.That(refs.Count(m => Same(m, activeTarget)), Is.EqualTo(1), "one read of the active target");
            Assert.That(refs.Count(m => Same(m, pendingTarget)), Is.EqualTo(0), "the pending candidate is not read");
            Assert.That(refs.Count(m => Same(m, pooledObject)), Is.EqualTo(0), "no PooledTarget data is read");
            Assert.That(refs.Count(m => Same(m, activeObject)), Is.EqualTo(4),
                "Target, Type, WorldPosition and the GetComponent receiver all come from ActiveHomingTarget.Object");
            int firstObjectRead = refs.FindIndex(m => Same(m, activeObject));
            Assert.That(firstObjectRead, Is.GreaterThan(Index(refs, m => Same(m, activeTarget))));
        }

        // A null active target becomes a null GameObject (0x794952) that still goes through the one
        // UnityEngine.Object.op_Equality (0x79483d), so destroyed targets also fail. Then IsValidType
        // (slot 18, 0x79485b), the squared distance against Constants+0x70 (HomingTargetHitDistance,
        // 0x7948b3), GetComponent<ISurface> (0x7948cb), IsSystemNull<TrackManager> (0x7948ff),
        // Character.get_TrackManager (0x79490d) and the IsValidSurface tail call (0x79493a).
        [Test]
        public void DoUpdateTestsTheActiveTargetObjectThenTypeDistanceAndSurfaceInOriginalOrder()
        {
            List<MemberInfo> refs = References();
            MethodInfo unityEquality = typeof(UnityEngine.Object).GetMethod("op_Equality",
                new[] { typeof(UnityEngine.Object), typeof(UnityEngine.Object) });
            MethodInfo isValidType = typeof(CharacterTransition_Target).GetMethod("IsValidType", Declared);
            MethodInfo isValidSurface = typeof(CharacterTransition_Target).GetMethod("IsValidSurface", Declared);
            FieldInfo hitDistance = typeof(GlobalConstantDefinition_Character).GetField("HomingTargetHitDistance", Declared);

            Assert.That(refs.Count(m => Same(m, unityEquality)), Is.EqualTo(1), "one Unity null test of the target object");
            Assert.That(refs.Count(m => m.Name == "ReferenceEquals" || m.Name == "op_Inequality"), Is.EqualTo(0),
                "no reference-only or inverted object test");
            Assert.That(refs.Count(m => Same(m, isValidType)), Is.EqualTo(1));
            Assert.That(refs.Count(m => Same(m, isValidSurface)), Is.EqualTo(1));

            int[] order =
            {
                Index(refs, m => Same(m, Getter(typeof(Character), "HomingPool"))),
                Index(refs, m => Same(m, Getter(typeof(CharacterHomingPool), "ActiveTarget"))),
                Index(refs, m => Same(m, unityEquality)),
                Index(refs, m => Same(m, isValidType)),
                Index(refs, m => Same(m, hitDistance)),
                Index(refs, m => m is MethodInfo mi && mi.DeclaringType == typeof(GameObject) && mi.Name == "GetComponent" &&
                    mi.IsGenericMethod && mi.GetGenericArguments()[0] == typeof(ISurface)),
                Index(refs, m => m is MethodInfo mi && mi.DeclaringType == typeof(ProcessManager) && mi.Name == "IsSystemNull" &&
                    mi.IsGenericMethod && mi.GetGenericArguments()[0] == typeof(TrackManager)),
                Index(refs, m => Same(m, Getter(typeof(Character), "TrackManager"))),
                Index(refs, m => Same(m, isValidSurface)),
            };
            Assert.That(order, Is.Ordered.Ascending.And.Unique,
                "HomingPool, ActiveTarget, object test, type, distance, surface lookup, TrackManager guard, TrackManager, IsValidSurface");
        }

        // Every method and field operand of DoUpdate in IL order.
        private static List<MemberInfo> References()
        {
            MethodInfo doUpdate = typeof(CharacterTransition_Target).GetMethod("DoUpdate", Declared);
            Assert.That(doUpdate, Is.Not.Null);
            return OriginalMethodBodyReader.Instructions(doUpdate)
                .Select(instruction => instruction.Value as MemberInfo)
                .Where(member => member != null)
                .ToList();
        }

        private static bool Same(MemberInfo actual, MemberInfo expected)
        {
            Assert.That(expected, Is.Not.Null, "expected member exists");
            return actual.Module == expected.Module && actual.MetadataToken == expected.MetadataToken;
        }

        private static int Index(List<MemberInfo> refs, Predicate<MemberInfo> match)
        {
            int index = refs.FindIndex(match);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "expected member reference is present");
            return index;
        }

        private static MethodInfo Getter(Type owner, string name) => owner.GetProperty(name, Declared).GetGetMethod(true);
    }
}
