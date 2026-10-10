using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using HardlightProject;

namespace ProjectLucid.Offline
{
    internal static class LocalGameAchievements
    {
        internal static LocalAchievementManagerLease Prepare(App app, Action complete, Action rollback, Func<bool> bindingIsOpen)
        {
            DataManager data = ProcessManager.GetSystem<DataManager>(null, false);
            HLPropertyStore store = ProcessManager.GetSystem<HLPropertyStore>(null, false);
            var ids = new List<string>();
            foreach (AchievementIdentifier id in Enum.GetValues(typeof(AchievementIdentifier)))
            {
                if (id == AchievementIdentifier.None) continue;
                if (!data.AchievementDefinitions.TryGetValue(id, out AchievementDefinition definition)
                    || definition == null || definition.AchievementId != id)
                    throw new InvalidOperationException("Missing genuine achievement definition: " + id);
                ids.Add(id.GetString());
                if (id == AchievementIdentifier.Day_Dreamer2)
                    ids.Add(Achievements.AppleFTUEPrefix + id.GetString());
            }
            return LocalAchievementManagerLease.Create(store, ids, () =>
            {
                SaveManager saves = ProcessManager.GetSystemSafe<SaveManager>(null, false);
                return bindingIsOpen() && saves != null && saves.IsAnySaveOpen && saves.CurrentSave != null;
            }, complete, rollback);
        }
    }
}
