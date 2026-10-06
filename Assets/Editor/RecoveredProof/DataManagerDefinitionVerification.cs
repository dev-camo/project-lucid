using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using UnityEngine;
using HardlightEnumComparers = HardlightProject.HardlightEnumComparers;

namespace ProjectLucid
{
    // Bounded complete original definition families feeding four DataManager dictionaries.
    // Managed raw values never invoke Unity object operations. Engine fixtures own concrete
    // original ScriptableObjects only; authored definitions/App loading are outside this proof.
    public static class DataManagerDefinitionVerification
    {
        private static int checks;
        private static void Check(bool value, string label) { if (!value) throw new Exception("Original definition bridge: " + label); checks++; }
        private static T Raw<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private static FieldInfo Field(Type owner, string name) => owner.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        private static void Set(object value, Type owner, string name, object data) => Field(owner, name).SetValue(value, data);
        private static object Comparer(object group) => group.GetType().GetMethod("GetKeyComparer", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Invoke(group, null);
        private static void Throws<T>(Action action, string label) where T : Exception { try { action(); throw new Exception("Expected fault absent: " + label); } catch (T) { Check(true, label); } }
        private static DefinitionDataType<TKey,TData>.DefinitionElement<TData> Row<TKey,TData>(TData data) where TData : ScriptableObject
        {
            var row = Raw<DefinitionDataType<TKey,TData>.DefinitionElement<TData>>(); row.Data = data; return row;
        }
        public static int RunManaged()
        {
            checks = 0;
            var time = Raw<TimeScaleDefinition>();
            Check((int)time.TimeCategory == 0 && ReferenceEquals(time.TimeScale, null), "time fields retain native zero/null defaults");
            time.TimeCategory = (TimeCategory)int.MinValue;
            Check((int)time.TimeCategory == int.MinValue, "public category keeps unknown signed value");
            var curve = Raw<AnimationCurve>();
            // This raw reference has no engine-owned pointer; prevent its native-only finalizer
            // in managed hosts. Actual curve lifecycle/serialization is tested only in Unity.
            GC.SuppressFinalize(curve); time.TimeScale = curve;
            Check(ReferenceEquals(time.TimeScale, curve), "public curve remains retained reference");
            var timeGroup = Raw<TimeScaleDefinitionGroup>();
            Check(ReferenceEquals(Comparer(timeGroup), null), "name-key time comparer is genuine null");
            Check(timeGroup.m_elements == null, "time inherited rows are not eagerly repaired");
            Throws<NullReferenceException>(() => timeGroup.GetData(), "null inherited time rows reaches original fault");

            var achievement = Raw<AchievementDefinition>();
            Check((int)achievement.AchievementId == 0, "achievement identifier native default zero");
            Check((int)achievement.Title == 0 && (int)achievement.Description == 0, "localized text fields native default zero");
            Set(achievement, typeof(AchievementDefinition), "m_achievementId", (AchievementIdentifier)int.MinValue);
            Set(achievement, typeof(AchievementDefinition), "m_title", (Strings)12345);
            Set(achievement, typeof(AchievementDefinition), "m_description", (Strings)(-12345));
            Check((int)achievement.AchievementId == int.MinValue, "identifier getter is direct field without validity normalization");
            Check((int)achievement.Title == 12345 && (int)achievement.Description == -12345, "distinct signed title and description are retained");
            var achievementGroup = Raw<AchievementDefinitionGroup>();
            Check(ReferenceEquals(Comparer(achievementGroup), HardlightEnumComparers.AchievementIdentifierComparer), "achievement group exposes exact original readonly registry comparer");
            Check(achievementGroup.m_elements == null, "achievement inherited rows native null");
            achievementGroup.m_elements = new[] { Row<AchievementIdentifier,AchievementDefinition>(achievement) };
            var achievementData = achievementGroup.GetData();
            Check(ReferenceEquals(achievementData[(AchievementIdentifier)int.MinValue], achievement), "unknown achievement key comes from direct identifier getter");
            Check(ReferenceEquals(achievementData.Comparer, HardlightEnumComparers.AchievementIdentifierComparer), "inherited dictionary receives captured exact enum comparer");
            var sameAchievement = Raw<AchievementDefinition>();
            Set(sameAchievement, typeof(AchievementDefinition), "m_achievementId", (AchievementIdentifier)int.MinValue);
            achievementGroup.m_elements = new[] { Row<AchievementIdentifier,AchievementDefinition>(achievement), Row<AchievementIdentifier,AchievementDefinition>(sameAchievement) };
            Throws<ArgumentException>(() => achievementGroup.GetData(), "duplicate achievement identifiers retain Dictionary.Add fault");
            achievementGroup.m_elements = new[] { Row<AchievementIdentifier,AchievementDefinition>(null) };
            Throws<NullReferenceException>(() => achievementGroup.GetData(), "null achievement data is not skipped");
            achievementGroup.m_elements = Array.Empty<DefinitionDataType<AchievementIdentifier,AchievementDefinition>.DefinitionElement<AchievementDefinition>>();
            Check(achievementGroup.GetData().Count == 0, "empty achievement rows return empty dictionary");

            var fade = Raw<FadeTransitionDefinition>();
            Check((int)fade.Type == 0 && ReferenceEquals(fade.ContainerIdentifier, null), "fade native defaults remain enum zero and null reference");
            Set(fade, typeof(FadeTransitionDefinition), "m_type", (FadeTransitionType)int.MaxValue);
            var container = Raw<UIContainerIdentifier>();
            Set(fade, typeof(FadeTransitionDefinition), "m_containerIdentifier", container);
            Check((int)fade.Type == int.MaxValue, "fade type keeps arbitrary authored signed value");
            Check(ReferenceEquals(fade.ContainerIdentifier, container), "container getter retains original reference without operator comparison");
            var fadeGroup = Raw<FadeTransitionDefinitionGroup>();
            Check(ReferenceEquals(Comparer(fadeGroup), HardlightEnumComparers.FadeTransitionTypeComparer), "fade exact readonly registry comparer");
            Check(fadeGroup.m_elements == null, "fade inherited rows native null");
            fadeGroup.m_elements = new[] { Row<FadeTransitionType,FadeTransitionDefinition>(fade) };
            var fadeData = fadeGroup.GetData();
            Check(ReferenceEquals(fadeData[(FadeTransitionType)int.MaxValue], fade), "fade key is own type field");
            Check(ReferenceEquals(fadeData.Comparer, HardlightEnumComparers.FadeTransitionTypeComparer), "inherited fade dictionary retains exact comparer");
            fadeGroup.m_elements = new[] { Row<FadeTransitionType,FadeTransitionDefinition>(fade), Row<FadeTransitionType,FadeTransitionDefinition>(fade) };
            Throws<ArgumentException>(() => fadeGroup.GetData(), "duplicate fade identifiers are not silently overwritten");
            fadeGroup.m_elements = new[] { Row<FadeTransitionType,FadeTransitionDefinition>(null) };
            Throws<NullReferenceException>(() => fadeGroup.GetData(), "null fade data keeps original getter fault");
            fadeGroup.m_elements = Array.Empty<DefinitionDataType<FadeTransitionType,FadeTransitionDefinition>.DefinitionElement<FadeTransitionDefinition>>();
            Check(fadeGroup.GetData().Count == 0, "empty fade rows supported by genuine base");

            var eventGroup = Raw<ApplicationStateEventDefinitionGroup>();
            Check(ReferenceEquals(Comparer(eventGroup), null), "event name comparer is genuine null");
            Check(eventGroup.m_elements == null, "event inherited rows native null");
            Throws<NullReferenceException>(() => eventGroup.GetData(), "null event rows original fault");
            Check(ReferenceEquals(ApplicationStateEvent.FindByName(null), null), "real ObjectUtils null-name guard avoids engine discovery");
            Check(ReferenceEquals(ApplicationStateEvent.FindByName(""), null), "real ObjectUtils empty-name guard avoids engine discovery");
            return checks;
        }
        public static int RunEngine()
        {
            checks = 0; var owned = new List<UnityEngine.Object>();
            try
            {
                var time = ScriptableObject.CreateInstance<TimeScaleDefinition>(); owned.Add(time);
                var time2 = ScriptableObject.CreateInstance<TimeScaleDefinition>(); owned.Add(time2);
                var timeGroup = ScriptableObject.CreateInstance<TimeScaleDefinitionGroup>(); owned.Add(timeGroup);
                var fade = ScriptableObject.CreateInstance<FadeTransitionDefinition>(); owned.Add(fade);
                var fadeGroup = ScriptableObject.CreateInstance<FadeTransitionDefinitionGroup>(); owned.Add(fadeGroup);
                var achievement = ScriptableObject.CreateInstance<AchievementDefinition>(); owned.Add(achievement);
                var achievementGroup = ScriptableObject.CreateInstance<AchievementDefinitionGroup>(); owned.Add(achievementGroup);
                var first = ScriptableObject.CreateInstance<ApplicationStateEvent>(); owned.Add(first);
                var second = ScriptableObject.CreateInstance<ApplicationStateEvent>(); owned.Add(second);
                var eventGroup = ScriptableObject.CreateInstance<ApplicationStateEventDefinitionGroup>(); owned.Add(eventGroup);
                var container = ScriptableObject.CreateInstance<UIContainerIdentifier>(); owned.Add(container);
                Check(time != null && time2 != null && timeGroup != null && fade != null && fadeGroup != null && achievement != null && achievementGroup != null && first != null && second != null && eventGroup != null && container != null, "all genuine concrete definition and dependency types create in Unity");
                Check((int)time.TimeCategory == 0 && ReferenceEquals(time.TimeScale, null), "actual native-base time initialization retains zero/null");
                Check((int)achievement.AchievementId == 0 && (int)achievement.Title == 0 && (int)achievement.Description == 0, "actual achievement defaults zero");
                Check((int)fade.Type == 0 && ReferenceEquals(fade.ContainerIdentifier, null), "actual fade defaults zero/null");
                Check(timeGroup.m_elements == null && fadeGroup.m_elements == null && achievementGroup.m_elements == null && eventGroup.m_elements == null, "all concrete group constructors retain null inherited rows");
                Check(first.GetGUID() == "" && second.GetGUID() == "", "actual event consumes original GUID-base empty field initializer");
                time.name = "Time"; time2.name = "time";
                timeGroup.m_elements = new[] { new DefinitionDataType<string,TimeScaleDefinition>.DefinitionElement<TimeScaleDefinition>(time), new DefinitionDataType<string,TimeScaleDefinition>.DefinitionElement<TimeScaleDefinition>(time2) };
                var times = timeGroup.GetData();
                Check(times.Count == 2 && ReferenceEquals(times["Time"], time) && ReferenceEquals(times["time"], time2), "time dictionary uses actual object names and case-sensitive default comparer");
                time2.name = "Time";
                Throws<ArgumentException>(() => timeGroup.GetData(), "name collision remains inherited Dictionary.Add fault");
                time.TimeCategory = (TimeCategory)(-123); time.TimeScale = new AnimationCurve(new Keyframe(0f, -2f), new Keyframe(1f, 3f));
                var timeJson = JsonUtility.ToJson(time); JsonUtility.FromJsonOverwrite(timeJson, time2);
                Check((int)time2.TimeCategory == -123, "actual JSON serializes public unknown time category");
                Check(time2.TimeScale != null && time2.TimeScale.length == 2 && time2.TimeScale.keys[0].value == -2f && time2.TimeScale.keys[1].value == 3f, "actual JSON roundtrips complete authored AnimationCurve");

                Set(achievement, typeof(AchievementDefinition), "m_achievementId", (AchievementIdentifier)(-123));
                Set(achievement, typeof(AchievementDefinition), "m_title", (Strings)456);
                Set(achievement, typeof(AchievementDefinition), "m_description", (Strings)(-789));
                var achievementCopy = ScriptableObject.CreateInstance<AchievementDefinition>(); owned.Add(achievementCopy);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(achievement), achievementCopy);
                Check((int)achievementCopy.AchievementId == -123 && (int)achievementCopy.Title == 456 && (int)achievementCopy.Description == -789, "actual JSON serializes all private achievement fields distinctly");
                achievementGroup.m_elements = new[] { new DefinitionDataType<AchievementIdentifier,AchievementDefinition>.DefinitionElement<AchievementDefinition>(achievement) };
                var achievements = achievementGroup.GetData();
                Check(ReferenceEquals(achievements[(AchievementIdentifier)(-123)], achievement) && ReferenceEquals(achievements.Comparer, HardlightEnumComparers.AchievementIdentifierComparer), "actual achievement group uses exact original key and comparer");

                Set(fade, typeof(FadeTransitionDefinition), "m_type", (FadeTransitionType)321);
                Set(fade, typeof(FadeTransitionDefinition), "m_containerIdentifier", container);
                var fadeCopy = ScriptableObject.CreateInstance<FadeTransitionDefinition>(); owned.Add(fadeCopy);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fade), fadeCopy);
                Check((int)fadeCopy.Type == 321 && ReferenceEquals(fadeCopy.ContainerIdentifier, container), "actual JSON retains private fade type and Unity object reference");
                fadeGroup.m_elements = new[] { new DefinitionDataType<FadeTransitionType,FadeTransitionDefinition>.DefinitionElement<FadeTransitionDefinition>(fade) };
                var fades = fadeGroup.GetData();
                Check(ReferenceEquals(fades[(FadeTransitionType)321], fade) && ReferenceEquals(fades.Comparer, HardlightEnumComparers.FadeTransitionTypeComparer), "actual fade group uses exact original key and comparer");
                UnityEngine.Object.DestroyImmediate(container);
                Check(ReferenceEquals(fade.ContainerIdentifier, container), "destroyed container reference is returned directly without null normalization");

                string nonce = "ProjectLucid_EventFixture_" + Guid.NewGuid().ToString("N"); first.name = nonce; second.name = nonce + "_other";
                eventGroup.m_elements = new[] { new DefinitionDataType<string,ApplicationStateEvent>.DefinitionElement<ApplicationStateEvent>(first), new DefinitionDataType<string,ApplicationStateEvent>.DefinitionElement<ApplicationStateEvent>(second) };
                var events = eventGroup.GetData();
                Check(events.Count == 2 && ReferenceEquals(events[nonce], first) && ReferenceEquals(events[nonce + "_other"], second), "same empty GUID events are keyed by actual names");
                Check(ReferenceEquals(ApplicationStateEvent.FindByName(nonce), first), "real Resources discovery finds first exact event name");
                Check(ReferenceEquals(ApplicationStateEvent.FindByName(nonce.ToLowerInvariant()), null), "real name lookup retains ordinal casing");
                Check(ReferenceEquals(ApplicationStateEvent.FindByName(nonce + "_absent"), null), "real Resources missing name returns null");
                second.name = nonce;
                var current = Resources.FindObjectsOfTypeAll<ApplicationStateEvent>(); ApplicationStateEvent expected = null;
                foreach (var value in current) if (value.name == nonce) { expected = value; break; }
                Check(expected != null && ReferenceEquals(ApplicationStateEvent.FindByName(nonce), expected), "duplicate names keep current real Resources order");
                Throws<ArgumentException>(() => eventGroup.GetData(), "event name collision remains base Dictionary.Add fault");
                return checks;
            }
            finally
            {
                for (int i = owned.Count - 1; i >= 0; --i) if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
            }
        }
        public static void Run()
        {
            int managed = RunManaged(); int engine = RunEngine();
            Debug.Log("PASS original DataManager definition bridge: " + managed + " managed + " + engine + " engine checks; supplied assets/App loading unproven.");
        }
    }
}
