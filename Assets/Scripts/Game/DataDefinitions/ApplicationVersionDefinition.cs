using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using GameVersion = Hardlight.Utils.Version;

namespace HardlightProject
{
    // Game.Runtime 02000478. This complete original definition is awaiting its
    // genuine zone/save dependency graph; no offline requirements are substituted.
    [CreateAssetMenu(fileName = "ApplicationVersionDefinition", menuName = "HardlightProject/DefinitionData/Definitions/ApplicationVersionDefinition")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class ApplicationVersionDefinition : ScriptableObject
    {
        [SerializeField] private GameVersion m_version;
        [SerializeField] private UIContainerIdentifier m_whatsNewContainerIdentifier;
        [Tooltip("Which zone must the player reach first before we can display the container?")]
        [Header("Requirements")]
        [SerializeField] private GameplayLevelCategory m_zoneRequired;

        // 060019f2 / ARM 514088; copy the complete value-type Version.
        public GameVersion Version => m_version;
        // 060019f3 / ARM 51409c.
        public UIContainerIdentifier WhatsNewContainerIdentifier => m_whatsNewContainerIdentifier;

        // 060019f4 / ARM 5140a4. Debug query is first; zone equality uses the
        // original ScriptableObjectWithGuid operator, then the actual requirements.
        public bool MeetsAllRequirements()
        {
            if (DebugUnlockLevels.AreAllLevelsUnlocked() || m_zoneRequired == null)
                return true;
            return m_zoneRequired.MeetsAllRequirements();
        }

        // 060019f5 / ARM 514158: no field initialization before the genuine base.
        public ApplicationVersionDefinition() { }
    }
}
