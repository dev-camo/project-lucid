using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original 020006d8, complete two methods and five readonly fields.
    // The real MessageExchangeWithCompletion<T> provider remains required.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class MessageManager : ISystem
    {
        public readonly MessageExchangeWithCompletion<Component> ComponentMessagesWithCompletion = new MessageExchangeWithCompletion<Component>(false);
        public readonly MessageExchangeWithCompletion<string> StringMessagesWithCompletion = new MessageExchangeWithCompletion<string>(false);
        public readonly MessageExchange<Component> ComponentMessagesWithoutCompletion = new MessageExchange<Component>();
        public readonly MessageExchange<string> StringMessagesWithoutCompletion = new MessageExchange<string>();
        public readonly MessageExchange<ScriptableObjectWithGuid> ScriptableObjectMessagesWithoutCompletion = new MessageExchange<ScriptableObjectWithGuid>();

        public MessageManager() // 060024f4; all five allocations precede Object ctor.
        {
            this.SubscribeToAction(SystemAction.Shutdown, Shutdown);
        }

        private void Shutdown(object context = null) // 060024f5, live fields, exact ordering.
        {
            ComponentMessagesWithCompletion.ProcessSystemAction(SystemAction.Shutdown, null);
            StringMessagesWithCompletion.ProcessSystemAction(SystemAction.Shutdown, null);
            ComponentMessagesWithoutCompletion.ProcessSystemAction(SystemAction.Shutdown, null);
            StringMessagesWithoutCompletion.ProcessSystemAction(SystemAction.Shutdown, null);
            ScriptableObjectMessagesWithoutCompletion.ProcessSystemAction(SystemAction.Shutdown, null);
        }
    }
}
