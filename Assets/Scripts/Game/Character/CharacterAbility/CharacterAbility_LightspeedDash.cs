using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterAbility_LightspeedDash : CharacterAbility_MovementTracker<CharacterAbilityDefinition_LightspeedDash, LightspeedDashDefinition>
    {
        private SurfaceLocation m_lightspeedDashSurface;
        protected override LightspeedDashDefinition GetTrackerDefinition() => DataManager.LightspeedDashDefinitions[LightspeedDashType.Default];
        // Original 06001123 captures the manager before reading position/definition.
        public void FindClosestLightspeedDash()
        {
            TrackManager trackManager = m_character.TrackManager;
            UnityEngine.Vector3 position = m_character.WorldPosition;
            float proximityDistance = Definition.ProximityDistance;
            if (trackManager.TryGetClosestLightspeedDashSpline(position, proximityDistance, ref m_lightspeedDashSurface, false))
                m_character.Storage.SetValue(ActorFSMKeys.LightspeedDashTarget, m_lightspeedDashSurface.m_surface);
        }
        public CharacterAbility_LightspeedDash() { }
    }
}
