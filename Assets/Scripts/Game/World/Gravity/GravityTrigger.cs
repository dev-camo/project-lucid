using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a7b. This abstract trigger owns no fields;
    // registration state and the authored priority belong to GravitySource.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(Collider))]
    public abstract class GravityTrigger : GravitySource
    {
        // 06003c44: call the base registration path directly, then the virtual
        // entry callback. Do not redirect the collider event to ManualTriggerEnter.
        private void OnTriggerEnter(Collider collider)
        {
            if (CanTrigger(collider, out GravityProvider gravityProvider))
            {
                Register(gravityProvider);
                OnGravityEnter(gravityProvider);
            }
        }

        // 06003c45: the callback runs even when Register already contains this
        // provider. Registration failures abort before the callback.
        public void ManualTriggerEnter(GravityProvider gravityProvider)
        {
            Register(gravityProvider);
            OnGravityEnter(gravityProvider);
        }

        // 06003c46: preserve the direct base removal followed by the virtual
        // exit callback. A derived ManualTriggerExit override is not called here.
        private void OnTriggerExit(Collider collider)
        {
            if (CanTrigger(collider, out GravityProvider gravityProvider))
            {
                Unregister(gravityProvider);
                OnGravityExit(gravityProvider);
            }
        }

        // 06003c47: implements the original abstract GravitySource slot 5.
        // The exit callback still runs when the provider was not registered.
        public override void ManualTriggerExit(GravityProvider gravityProvider)
        {
            Unregister(gravityProvider);
            OnGravityExit(gravityProvider);
        }

        // 06003c48: clear the output before reading collider.isTrigger. Only
        // the collider's own GameObject is searched; no parent lookup or tag gate.
        // Retain Unity's object inequality semantics for absent/destroyed providers.
        private static bool CanTrigger(Collider collider, out GravityProvider gravityProvider)
        {
            gravityProvider = null;
            if (collider.isTrigger)
                return false;
            gravityProvider = collider.gameObject.GetComponent<GravityProvider>();
            return gravityProvider != null;
        }

        // 06003c49: only inherited GravitySource initialization survives here;
        // its registered-provider list is allocated once by the base constructor.
        protected GravityTrigger() { }
    }
}
