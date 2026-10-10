using System;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class AppRatingRequesterBase : ScriptableObject
    {
        [Tooltip("How often should this requester be prompting the user? Defaults to one week.")]
        [SerializeField]
        private float m_requestCooldownDays = 7.0f;

        // Game.Runtime 0x060008af: real virtual validation precedes original review endpoint.
        public void TryRequest()
        {
            if (Validate()) AppRating.RequestReview();
        }

        // Game.Runtime 0x060008b0: genuine ObjectUtils lookup, no new provider or alias.
        public static AppRatingRequesterBase FindByName(string name) => ObjectUtils.FindByName<AppRatingRequesterBase>(name);

        // Game.Runtime 0x060008b1: captured save receiver/time/float values survive callbacks.
        protected virtual bool Validate()
        {
            var saveData = ProcessManager.GetSystem<SaveManager>(null, true).CurrentSave;
            long currentTime = TimeUtils.ToUnixTimeSeconds(DateTime.UtcNow);
            long lastRequestTime = saveData.LastRatingRequestTimestampSeconds;
            float cooldownSeconds = m_requestCooldownDays * 86400.0f;
            float elapsedSeconds = unchecked(currentTime - lastRequestTime);
            if (cooldownSeconds < elapsedSeconds)
            {
                saveData.LastRatingRequestTimestampSeconds = currentTime;
                saveData.RequestSave();
            }
            return cooldownSeconds < elapsedSeconds;
        }

        // Game.Runtime 0x060008b2: original seven-day initializer before ScriptableObject base.
        protected AppRatingRequesterBase() { }
    }
}
