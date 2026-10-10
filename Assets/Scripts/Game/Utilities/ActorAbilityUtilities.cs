using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Game.Runtime 020009dd: all six declared original methods and its one
    // constant. These are ordinary static methods in the shipped metadata.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class ActorAbilityUtilities
    {
        // 04002991.
        private const float DefaultAbilityInUseGracePeriodSeconds = 0.2f;

        // 060038c3. Both outputs are initialised before the real Storage access.
        // The first active entry is returned in the live dictionary's order.
        public static bool TryGetAbilityInUse(Character character, out ActorAbilityType abilityType, out float timeSinceUsed)
        {
            abilityType = (ActorAbilityType)0;
            timeSinceUsed = 0f;
            if (!character.Storage.TryGetValue(ActorFSMKeys.AbilitiesInUse, out Dictionary<ActorAbilityType, ActorAbilityInUseData> abilities)) return false;
            foreach (KeyValuePair<ActorAbilityType, ActorAbilityInUseData> ability in abilities)
            {
                if (!ability.Value.InUse) continue;
                abilityType = ability.Key;
                timeSinceUsed = character.GetTotalFixedTime() - ability.Value.TimeLastUsedSeconds;
                return true;
            }
            return false;
        }

        // 060038c4. Ability zero negates the complete dictionary overload;
        // preserve that original recursion when zero itself is stored as a key.
        // ARM FCMP elapsed,grace/CSET LS and x86 UCOMISS grace,elapsed/SETAE
        // both reject unordered operands in the grace comparison.
        public static bool IsAbilityInUse(Character character, ActorAbilityType abilityType, float gracePeriodSeconds = DefaultAbilityInUseGracePeriodSeconds)
        {
            if (abilityType == (ActorAbilityType)0) return !AnyAbilityInUse(character, gracePeriodSeconds);
            if (!character.Storage.TryGetValue(ActorFSMKeys.AbilitiesInUse, out Dictionary<ActorAbilityType, ActorAbilityInUseData> abilities)) return false;
            if (!abilities.TryGetValue(abilityType, out ActorAbilityInUseData ability)) return false;
            if (ability.InUse) return true;
            return character.GetTotalFixedTime() - ability.TimeLastUsedSeconds <= gracePeriodSeconds;
        }

        // 060038c5. Empty lists return without touching character; the genuine
        // list enumerator and its disposal retain live mutation/null faults.
        public static bool AnyAbilityInUse(Character character, List<ActorAbilityType> abilityTypes, float gracePeriodSeconds = DefaultAbilityInUseGracePeriodSeconds)
        {
            foreach (ActorAbilityType abilityType in abilityTypes)
                if (IsAbilityInUse(character, abilityType, gracePeriodSeconds)) return true;
            return false;
        }

        // 060038c6. Read the actual nested definition before the character call.
        public static bool AnyAbilityInUse(Character character, List<CharacterAbilityTypeGroup.AbilityTypeDefinition> abilityTypeDefinitions)
        {
            foreach (CharacterAbilityTypeGroup.AbilityTypeDefinition abilityTypeDefinition in abilityTypeDefinitions)
                if (IsAbilityInUse(character, abilityTypeDefinition.Type, abilityTypeDefinition.GracePeriod)) return true;
            return false;
        }

        // 060038c7. Equal elapsed values replace the prior selection. Preserve
        // the greater-than skip: unordered elapsed values are selected on both
        // backends, unlike a rewritten less-than-or-equal selection predicate.
        // Fixed time is fetched separately for every inactive dictionary entry.
        public static bool TryGetAbilityLastUsed(Character character, out ActorAbilityType abilityType, out float timeSinceUsed)
        {
            abilityType = (ActorAbilityType)0;
            timeSinceUsed = float.MaxValue;
            if (!character.Storage.TryGetValue(ActorFSMKeys.AbilitiesInUse, out Dictionary<ActorAbilityType, ActorAbilityInUseData> abilities)) return false;
            foreach (KeyValuePair<ActorAbilityType, ActorAbilityInUseData> ability in abilities)
            {
                if (ability.Value.InUse) continue;
                float elapsed = character.GetTotalFixedTime() - ability.Value.TimeLastUsedSeconds;
                if (elapsed > timeSinceUsed) continue;
                abilityType = ability.Key;
                timeSinceUsed = elapsed;
            }
            return abilityType != (ActorAbilityType)0;
        }

        // 060038c8. Every live key goes through IsAbilityInUse; this is not a
        // direct state scan, and it retains the original zero-key recursion.
        public static bool AnyAbilityInUse(Character character, float gracePeriodSeconds = DefaultAbilityInUseGracePeriodSeconds)
        {
            if (!character.Storage.TryGetValue(ActorFSMKeys.AbilitiesInUse, out Dictionary<ActorAbilityType, ActorAbilityInUseData> abilities)) return false;
            foreach (KeyValuePair<ActorAbilityType, ActorAbilityInUseData> ability in abilities)
                if (IsAbilityInUse(character, ability.Key, gracePeriodSeconds)) return true;
            return false;
        }
    }
}
