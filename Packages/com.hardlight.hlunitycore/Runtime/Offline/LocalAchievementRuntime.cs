using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;

namespace ProjectLucid.Offline
{
    // Local completion is durable in the existing genuine default save file.
    // Tracker values continue to come from original game progression/stat records.
    public static class LocalAchievementRuntime
    {
        private sealed class Context
        {
            internal readonly HLPropertyStore Store;
            internal readonly string[] Identifiers;
            internal readonly Func<bool> TrackingAllowed;
            internal readonly Dictionary<Achievement, string> Owners = new Dictionary<Achievement, string>();
            internal readonly List<Action> Evaluators = new List<Action>();
            internal Dictionary<string, Achievement> Entries;
            internal bool Ready;
            internal Context(HLPropertyStore store, string[] identifiers, Func<bool> trackingAllowed) { Store = store; Identifiers = identifiers; TrackingAllowed = trackingAllowed; }
        }
        private static readonly Dictionary<AchievementsManager, Context> Contexts = new Dictionary<AchievementsManager, Context>();
        private static readonly Dictionary<Achievement, Context> Owners = new Dictionary<Achievement, Context>();
        public const string SaveIdentifier = "HL_Default_Save_ID";
        private const string KeyPrefix = "ProjectLucid.Local.default.Achievements.";

        public static void Configure(AchievementsManager manager, HLPropertyStore store, IEnumerable<string> identifiers, Func<bool> trackingAllowed)
        {
            if (manager == null || store == null || trackingAllowed == null) throw new ArgumentNullException();
            if (!store.IsLoaded || !store.CanSave || store.GetSaveIdentifier() != SaveIdentifier)
                throw new InvalidOperationException("Local profile requires the loaded, idle default property store.");
            if (Contexts.ContainsKey(manager)) throw new InvalidOperationException("Local achievement manager is already configured.");
            var ids = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in identifiers)
            {
                if (string.IsNullOrEmpty(id) || id.Length > 128 || !unique.Add(id) || ids.Count >= 256)
                    throw new InvalidOperationException("Invalid or duplicate genuine achievement identifier.");
                ids.Add(id);
            }
            if (ids.Count == 0) throw new InvalidOperationException("Genuine achievement definitions are unavailable.");
            Contexts.Add(manager, new Context(store, ids.ToArray(), trackingAllowed));
        }

        public static void Initialise(AchievementsManager manager, Dictionary<string, Achievement> achievements, Action complete)
        {
            Context context = Contexts[manager];
            if (context.Ready || achievements.Count != 0) throw new InvalidOperationException("Local achievement initialization must occur once per manager lifecycle.");
            context.Entries = achievements;
            var prepared = new List<KeyValuePair<string, Achievement>>();
            foreach (string id in context.Identifiers)
            {
                string value = context.Store.GetPropertyValueFromSave(SaveIdentifier, KeyPrefix + id);
                if (value != HLPropertyStore.DefaultPropertyValue && value != "0" && value != "1")
                    throw new FormatException("Invalid local achievement completion value.");
                var achievement = new Achievement(id);
                achievement.SyncPlatformValues(0f, value == "1");
                prepared.Add(new KeyValuePair<string, Achievement>(id, achievement));
            }
            foreach (var row in prepared)
            {
                achievements.Add(row.Key, row.Value);
                context.Owners.Add(row.Value, row.Key);
                Owners.Add(row.Value, context);
            }
            context.Ready = true;
            complete();
        }

        public static bool IsReady(AchievementsManager manager) => manager != null && Contexts.TryGetValue(manager, out Context context) && context.Ready;

        public static void AddBehaviour(AchievementsManager manager, Dictionary<string, Achievement> achievements,
            string id, int target, ref Action evaluateOn, Func<int> tracker, int prerequisiteTarget)
        {
            if (!IsReady(manager)) throw new InvalidOperationException("Local achievement definitions are not ready.");
            if (!achievements.TryGetValue(id, out Achievement achievement)) throw new KeyNotFoundException(id);
            Context context = Contexts[manager];
            Func<int> guardedTracker = tracker == null ? null : (Func<int>)(() => context.Ready && context.TrackingAllowed() ? tracker() : 0);
            if (!achievement.ReportedComplete)
            {
                // The genuine AddBehaviour body appends exactly its Evaluate delegate.
                int priorCount = evaluateOn?.GetInvocationList().Length ?? 0;
                achievement.AddBehaviour(target, ref evaluateOn, guardedTracker, prerequisiteTarget);
                Delegate[] after = evaluateOn.GetInvocationList();
                if (after.Length != priorCount + 1) throw new InvalidOperationException("Unexpected genuine achievement subscription shape.");
                context.Evaluators.Add((Action)after[priorCount]);
            }
        }

        public static void DetachEvaluators(AchievementsManager manager, ref Action evaluateOn)
        {
            if (manager == null || !Contexts.TryGetValue(manager, out Context context)) return;
            foreach (Action evaluator in context.Evaluators) evaluateOn -= evaluator;
        }

        public static bool ReportCompletion(Achievement achievement, string id, bool complete)
        {
            if (!Owners.TryGetValue(achievement, out Context context)) return false;
            if (!context.Ready || context.Owners[achievement] != id) throw new InvalidOperationException("Foreign achievement owner.");
            if (!context.TrackingAllowed() || !complete) return false;
            if (!context.Store.CanSave) return false;
            return context.Store.TrySetPropertyValueOnSave(SaveIdentifier, KeyPrefix + id, "1");
        }

        // Abort uses the actual dictionary supplied by the genuine manager's
        // Initialise callback. It never replaces the registry or fabricates owners.
        public static void AbortManager(AchievementsManager manager)
        {
            if (manager == null || !Contexts.TryGetValue(manager, out Context context)) return;
            context.Ready = false;
            var errors = new List<Exception>();
            try
            {
                foreach (Achievement achievement in new List<Achievement>(context.Owners.Keys))
                {
                    try { achievement.OnDestroy(); }
                    catch (Exception error) { errors.Add(error); }
                    finally { Owners.Remove(achievement); }
                }
            }
            finally
            {
                context.Entries?.Clear();
                context.Owners.Clear();
                context.Evaluators.Clear();
                Contexts.Remove(manager);
            }
            if (errors.Count != 0) throw new AggregateException("Owned achievement cleanup failed.", errors);
        }

        public static void Shutdown(AchievementsManager manager, Dictionary<string, Achievement> achievements)
        {
            if (Contexts.TryGetValue(manager, out Context context) && context.Entries != null
                && !ReferenceEquals(context.Entries, achievements))
                throw new InvalidOperationException("Foreign achievement dictionary supplied to shutdown.");
            AbortManager(manager);
            achievements.Clear();
        }
    }
}
