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
    // Original Game.Runtime type 0x02000a60.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EntityActivationLogicWaitForCutscene : EntityActivationLogic<EntityActivationLogicWaitForCutsceneDefinition>
    {
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);
        private bool m_hasLoaded;
        private bool m_canTrigger;
        private CutsceneManager m_cutsceneManager;
        // Original 0x06003ba4: the CutsceneManager system reference is transient, not an added field.
        public override void Initialise(EntityActivationLogicDefinition definition, IEntityActivatable entity)
        {
            base.Initialise(definition, entity);
            m_hasLoaded = false;
            ProcessManager.GetSystemRef<CutsceneManager>(null, true).InvokeOnValid(CutsceneManagerValid);
        }
        // Original 0x06003ba5 and natural 0x06003bab: store manager before registering the immediate level callback.
        private void CutsceneManagerValid(CutsceneManager manager)
        {
            m_cutsceneManager = manager;
            m_levelManagerRef.InvokeOnValid(levelManager => levelManager.InvokeOnLevelActivated(OnLevelActivated, true));
        }
        // Original 0x06003ba6: retain IsNull then a fresh Get, rather than folding these into TryGet.
        public override void Shutdown()
        {
            if (m_levelManagerRef.IsNull()) return;
            m_levelManagerRef.Get().RemoveLevelActivatedAction(OnLevelActivated);
        }
        // Original 0x06003ba7.
        public override void OnEnd() { base.OnEnd(); m_canTrigger = false; }
        // Original 0x06003ba8.
        public override bool CanTrigger()
        {
            if (!HasStarted || !m_canTrigger) return false;
            m_canTrigger = false;
            return true;
        }
        // Original 0x06003ba9: query IsCutscenePlaying before the first-notification and started checks.
        private void OnLevelActivated(LevelManagerLevel level)
        {
            if (!m_cutsceneManager.CutsceneIsLoading)
            {
                bool playing = m_cutsceneManager.IsCutscenePlaying;
                if (m_hasLoaded && !playing && HasStarted) m_canTrigger = true;
            }
            m_hasLoaded = true;
        }
        // Original 0x06003baa.
        public EntityActivationLogicWaitForCutscene() { }
    }
}
