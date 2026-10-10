namespace HardlightProject
{
    public static class Requirements
    {
        // Original 06001efb: authored order and short-circuit virtual slot4.
        // Null arrays/elements fault, while an empty array succeeds. No Unity
        // truth test or new null fallback precedes the original virtual call.
        public static bool AreMet(RequirementGameplayLevelBase[] requirements, GameplayLevelDefinition levelDefinition = null)
        {
            for (int index = 0; index < requirements.Length; ++index)
                if (!requirements[index].RequirementsMet(levelDefinition))
                    return false;
            return true;
        }
    }
}
