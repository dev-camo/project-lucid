using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // Compiled field flags must match the original 1.10.1 Game.Runtime declaration.
    // This covers declaration identity only, not animation behavior or native layout.
    public sealed class OriginalActorAnimatorFieldTests
    {
        [Test]
        public void AnimatorFieldRetainsOriginalPrivateInitOnlyFlags() // field token 0x0400013e
        {
            FieldInfo field = typeof(ActorAnimator).GetField("m_animator", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.That(field, Is.Not.Null);
            Assert.That(field.DeclaringType.Assembly.GetName().Name, Is.EqualTo("Game.Runtime"));
            Assert.That(field.FieldType, Is.EqualTo(typeof(Animator)));
            // Original attributes 0x0021: Private | InitOnly.
            Assert.That(field.Attributes, Is.EqualTo(FieldAttributes.Private | FieldAttributes.InitOnly));
        }
    }
}
