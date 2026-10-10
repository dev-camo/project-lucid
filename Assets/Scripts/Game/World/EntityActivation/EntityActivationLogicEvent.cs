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
    // Original Game.Runtime type 0x02000a59.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class EntityActivationLogicEvent<T> : EntityActivationLogic<T> where T : EntityActivationLogicDefinition
    {
        private EntityActivationMessage m_message;
        private bool m_canTrigger;
        // Original 0x06003b7a: reset after subscription, preserving subscription callbacks and faults before the reset.
        public override void OnStart()
        {
            base.OnStart();
            MessageExchange<EntityActivationMessage> exchange = ProcessManager.GetSystemAutoCreate<MessageExchange<EntityActivationMessage>>(null);
            m_message = new EntityActivationMessage(LogicType);
            exchange.SubscribeToMessage(in m_message, SetCanTrigger);
            m_canTrigger = false;
        }
        // Original 0x06003b7b: genuine abstract contract.
        protected abstract EntityActivationLogicType LogicType { get; }
        // Original 0x06003b7c.
        private void SetCanTrigger() => m_canTrigger = true;
        // Original 0x06003b7d.
        public override bool CanTrigger()
        {
            if (!HasStarted || !m_canTrigger) return false;
            m_canTrigger = false;
            return true;
        }
        // Original 0x06003b7e/0x06003b7f.
        public override void OnEnd() { base.OnEnd(); Close(); }
        public override void Shutdown() { base.Shutdown(); Close(); }
        // Original 0x06003b80: safe lookup and managed null test, retaining the latch value.
        private void Close()
        {
            MessageExchange<EntityActivationMessage> exchange = ProcessManager.GetSystemSafe<MessageExchange<EntityActivationMessage>>(null, true);
            if (exchange != null) exchange.UnsubscribeFromMessage(in m_message, SetCanTrigger);
        }
        // Original 0x06003b81.
        protected EntityActivationLogicEvent() { }
    }
}
