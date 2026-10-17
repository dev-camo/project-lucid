using System;
using System.Collections.Concurrent;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x0200027d. Actions queued from any thread
    // run on the next ProcessManager Update action.
    public class MainThreadDispatcher : ISystem
    {
        // 0x06000f98; 0x1B1FA70. The typed reference is created (and the
        // registry row auto-registered) by the static initializer.
        private static readonly SystemRef<MainThreadDispatcher> s_dispatcherRef = ProcessManager.GetSystemRef<MainThreadDispatcher>();
        private readonly ConcurrentQueue<Action> m_actionQueue = new ConcurrentQueue<Action>();

        // 0x06000f95; 0x1B1F7E0. The queue initializer precedes Object's constructor.
        private MainThreadDispatcher()
        {
            this.SubscribeToAction(SystemAction.Update, Update);
        }

        // 0x06000f96; 0x1B1F8D0. Drains until empty, including actions queued
        // by the actions themselves; a null action faults mid-drain.
        private void Update(object context)
        {
            while (m_actionQueue.TryDequeue(out Action action)) action();
        }

        // 0x06000f97; 0x1B1F960. The first call creates and registers the
        // dispatcher under its default name, which revives s_dispatcherRef.
        public static void AddAction(Action action)
        {
            if (!s_dispatcherRef.TryGet(out MainThreadDispatcher dispatcher))
            {
                dispatcher = new MainThreadDispatcher();
                ProcessManager.RegisterSystem(dispatcher);
            }
            dispatcher.m_actionQueue.Enqueue(action);
        }
    }
}
