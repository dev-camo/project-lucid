using System;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.WorldTriggersShared
{
    // Test-only EventCollisionTrigger whose FilterCollision answer a case chooses, so the
    // original rejecting branches can run; the base filter always passes. Editor-only
    // assemblies cannot supply attachable components.
    public sealed class FilteredEventCollisionTriggerProbe : EventCollisionTrigger
    {
        public Func<Collider, bool> Filter;

        protected override bool FilterCollision(Collider other) => Filter == null ? base.FilterCollision(other) : Filter(other);
    }
}
