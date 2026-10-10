// Preserves the original shipping declarations and inferred whole managed bodies.
// Compiler-generated identities and exceptional native fault ordering require separate qualification.
using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000a5f.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class EntityActivationLogicTrigger : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Logic type to be triggered.")]
        private EntityActivationLogicType m_logicType = EntityActivationLogicType.PlatformReset;
        // Original 0x06003ba2: the collider parameter is ignored; preserve real message publication.
        private void OnTriggerEnter(Collider other)
        {
            MessageExchange<EntityActivationMessage> exchange = ProcessManager.GetSystemAutoCreate<MessageExchange<EntityActivationMessage>>(null);
            EntityActivationMessage message = new EntityActivationMessage(m_logicType);
            exchange.PublishMessage(in message);
        }
        // Original 0x06003ba3.
        public EntityActivationLogicTrigger() { }
    }
}
