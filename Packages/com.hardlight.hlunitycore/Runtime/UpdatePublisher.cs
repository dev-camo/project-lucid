using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000256. Each subscription is keyed by
    // its subscriber identity and advanced by the derived publisher's clock.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class UpdatePublisher<TUpdateType> : ISystem
    {
        // Original 0x02000257. Equality and hashing use only the subscriber,
        // so the update type never distinguishes two handles.
        public readonly struct UpdateHandle
        {
            public readonly TUpdateType UpdateType;
            private readonly IUpdateSubscriber m_subscriber;

            // 0x06000f06; Int32Enum instance 0xCB35C0.
            public UpdateHandle(TUpdateType updateType, IUpdateSubscriber subscriber)
            {
                UpdateType = updateType;
                m_subscriber = subscriber;
            }

            // 0x06000f07; 0xCB35E0. A null subscriber faults here.
            public override int GetHashCode() => m_subscriber.GetHashCode();

            // 0x06000f08; 0xCB3610. The boxed handle is the receiver and this
            // handle the argument, so the boxed subscriber's Equals decides.
            public override bool Equals(object obj)
            {
                if (!(obj is UpdateHandle)) return false;
                return ((UpdateHandle)obj).Equals(this);
            }

            // 0x06000f09; 0xCB3740.
            public bool Equals(UpdateHandle updateHandle) => m_subscriber.Equals(updateHandle.m_subscriber);
        }

        // Original 0x02000258.
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        protected class UpdateInfo
        {
            private readonly float m_updateRate;
            private readonly IUpdateSubscriber m_subscriber;
            private float m_updateTimer;

            // 0x06000f0a; 0xCB3CA0. The timer reset follows Object's constructor.
            public UpdateInfo(float updateRate, IUpdateSubscriber subscriber)
            {
                m_updateRate = updateRate;
                m_subscriber = subscriber;
                m_updateTimer = 0f;
            }

            // 0x06000f0b; 0xCB3CF0. Catch up with one callback per elapsed
            // period; each callback receives the rate, not the frame delta.
            public void OnUpdate(float deltaTime)
            {
                m_updateTimer += deltaTime;
                while (m_updateTimer >= m_updateRate)
                {
                    m_updateTimer -= m_updateRate;
                    m_subscriber.OnUpdate(m_updateRate);
                }
            }
        }

        private readonly Dictionary<UpdateHandle, UpdateInfo> m_updateLookup = new Dictionary<UpdateHandle, UpdateInfo>();

        // 0x06000f01; 0xCB3F40. The dictionary initializer precedes Object's
        // constructor; the virtual OnUpdate is bound for the Update action.
        protected UpdatePublisher()
        {
            this.SubscribeToAction(SystemAction.Update, OnUpdate);
        }

        // 0x06000f02; shared 0x7C2F60. Dictionary.Add throws for a second
        // subscription of the same subscriber; the immediate call passes zero.
        public UpdateHandle SubscribeToUpdate<T>(float updateRate, TUpdateType updateType, T subscriber, bool updateImmediately = false)
            where T : IUpdateSubscriber, new()
        {
            UpdateHandle updateHandle = new UpdateHandle(updateType, subscriber);
            m_updateLookup.Add(updateHandle, new UpdateInfo(updateRate, subscriber));
            if (updateImmediately) subscriber.OnUpdate(0f);
            return updateHandle;
        }

        // 0x06000f03; 0xCB4030.
        public bool UnsubscribeFromUpdate(UpdateHandle updateHandle) => m_updateLookup.Remove(updateHandle);

        // 0x06000f04.
        protected abstract void AdvanceUpdateInfo(TUpdateType updateType, UpdateInfo updateInfo);

        // 0x06000f05; 0xCB4050. Live enumeration: a callback that subscribes
        // or unsubscribes invalidates the enumerator.
        protected virtual void OnUpdate(object context)
        {
            foreach (KeyValuePair<UpdateHandle, UpdateInfo> update in m_updateLookup)
                AdvanceUpdateInfo(update.Key.UpdateType, update.Value);
        }
    }
}
