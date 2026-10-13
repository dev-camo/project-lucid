using System.Collections.Generic;
using System.Diagnostics;
using Hardlight;
using Hardlight.Localisation;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime.dll 02000321. Complete original owner has six
    // MethodDefs, including the natural instance callback <.ctor>b__0_0.
    // Preserve save registration before the language subscription and the
    // original dictionary order, partial effects and unguarded faults.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterCustomisationManager : ISystem, ISaveGameListener
    {
        // 06001359: real base construction precedes save registration, which
        // may invoke immediately, before the genuine string-table subscription.
        public CharacterCustomisationManager()
        {
            ProcessManager.GetSystemRef<SaveManager>().InvokeOnValid(saveManager =>
            {
                // Original instance callback 0600135e; no invented coowner.
                saveManager.AddDependantListener(this, true);
            });
            StringTable.StringsTableHandlers += OnLanguageChanged;
        }

        // 0600135a: both complete original CPU bodies return without work.
        private void OnLanguageChanged()
        {
        }

        // 0600135b: retain the real dictionary's order, Deconstruct call and
        // enumerator disposal. Do not null-guard the save, definition or record.
        public void OnSaveGameOpen(SaveDataGame saveDataGame)
        {
            foreach (KeyValuePair<CharacterId, CharacterDefinition> entry in
                ProcessManager.GetSystem<DataManager>().Characters)
            {
                entry.Deconstruct(out CharacterId characterId,
                    out CharacterDefinition characterDefinition);
                characterDefinition.SetCharacterSkin(
                    saveDataGame.GetOrCreateCharacterCustomisation(characterId).SkinGUID);
            }
        }

        // 0600135c: the saveDataGame parameter is retained although unused.
        public void OnSaveGameClose(SaveDataGame saveDataGame)
        {
        }

        // 0600135d: the original Conditional body still enumerates and
        // deconstructs every entry; it is not replaced with an empty method.
        [Conditional("BUILD_DEVELOPMENT")]
        public static void AddCharacterSkinDebug()
        {
            foreach (KeyValuePair<CharacterId, CharacterDefinition> entry in
                ProcessManager.GetSystem<DataManager>().Characters)
            {
                entry.Deconstruct(out CharacterId _, out CharacterDefinition _);
            }
        }
    }
}
