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
    // Original Game.Runtime type 0x02000a5d.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EntityActivationLogicTime : EntityActivationLogic<EntityActivationLogicTimeDefinition>
    {
        private bool m_timeReached;
        private Coroutine m_timerCoroutine;
        // Original 0x06003b95: start without stopping a previously stored coroutine.
        public override void OnStart()
        {
            base.OnStart();
            m_timerCoroutine = CoroutineUtils.RunCoroutine(ProcessTimer());
        }
        // Original 0x06003b96 with original natural d__3 methods 0x06003b9c..0x06003ba1.
        // Completion stores are scheduled differently by the two CPUs; this represents their common completed-state behavior.
        private IEnumerator ProcessTimer()
        {
            m_timeReached = false;
            yield return TimeScaledUtilities_SDT.WaitForFixedSeconds(m_definition.Time, m_definition.TimeCategory, null);
            m_timeReached = true;
            m_timerCoroutine = null;
        }
        // Original 0x06003b97: this query does not consume m_timeReached.
        public override bool CanTrigger() => HasStarted && m_timeReached;
        // Original 0x06003b98/0x06003b99.
        public override void OnEnd() { base.OnEnd(); Close(); }
        public override void Shutdown() { Close(); }
        // Original 0x06003b9a.
        private void Close() => CoroutineUtils.StopUtilCoroutine(ref m_timerCoroutine);
        // Original 0x06003b9b.
        public EntityActivationLogicTime() { }
    }
}
