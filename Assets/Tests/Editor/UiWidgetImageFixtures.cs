using System.Text.RegularExpressions;
using HardlightProject;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Character and archetype definitions whose image wrappers already hold a sprite, so the widgets that load them need no
    // Addressables load: ManagedAddressableAsset.Load answers from its cached asset (the state a finished load leaves).
    internal static class UiWidgetImageFixtures
    {
        // CreateInstance sends Awake, whose UpdateCachedValues calls Settings.UpdateCachedValues on the unassigned Settings with no
        // null test; the engine logs that exception.
        private static readonly Regex DefinitionAwakeFault = new Regex("^NullReferenceException: Object reference not set to an instance of an object$");

        // A wrapper over no reference whose load is already finished with a fresh sprite.
        internal static ManagedAddressableAsset<Sprite> LoadedWrapper(Owned owned, out Sprite sprite)
        {
            var wrapper = new ManagedAddressableAsset<Sprite>(null);
            sprite = NewSprite(owned);
            Cache(wrapper, sprite);
            return wrapper;
        }

        // A character definition (Image and Render are filled by the skin code in the game) with its id and archetype.
        internal static CharacterDefinition NewCharacterDefinition(Owned owned, CharacterId id, out Sprite image, out Sprite render,
            CharacterArchetype archetype = CharacterArchetype.None)
        {
            LogAssert.Expect(LogType.Exception, DefinitionAwakeFault);
            var definition = Create<CharacterDefinition>(owned, "{\"m_id\":" + (int)id + ",\"m_archetype\":" + (int)archetype + "}");
            definition.Image = LoadedWrapper(owned, out image);
            definition.Render = LoadedWrapper(owned, out render);
            return definition;
        }

        // An archetype definition whose three icon wrappers (OnEnable builds them over the unassigned references) are loaded.
        internal static CharacterArchetypeDefinition NewArchetypeDefinition(Owned owned, CharacterArchetype archetype,
            out Sprite normal, out Sprite alternate, out Sprite silhouette)
        {
            var definition = Create<CharacterArchetypeDefinition>(owned, "{\"m_characterArchetype\":" + (int)archetype + "}");
            Cache(definition.UIImage, normal = NewSprite(owned));
            Cache(definition.AlternateImage, alternate = NewSprite(owned));
            Cache(definition.SilhouetteImage, silhouette = NewSprite(owned));
            return definition;
        }
    }
}
