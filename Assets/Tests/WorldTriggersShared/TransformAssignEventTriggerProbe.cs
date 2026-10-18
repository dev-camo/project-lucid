using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.WorldTriggersShared
{
    // Test-only TransformAssignEventTrigger that overrides both virtuals, as the shipped
    // CameraProxyAssignTrigger and CharacterProxyAssignTrigger do, so a case can tell virtual
    // dispatch from reading m_setNullOnExit or other.transform directly. Editor-only
    // assemblies cannot supply attachable components.
    public sealed class TransformAssignEventTriggerProbe : TransformAssignEventTrigger
    {
        public bool NullOnExit;
        public Transform Proxy;
        public int TargetRequests;

        protected override bool SetNullOnExit => NullOnExit;

        protected override Transform GetTargetTransform(Collider other)
        {
            TargetRequests++;
            return Proxy;
        }
    }
}
