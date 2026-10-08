using System;
using System.Reflection;
using UnityEngine.EventSystems;

namespace ProjectLucid.Verification
{
    // Only genuine managed event data and a fixture client of the real contracts.
    // No Unity object, event-system instance, renderer, native import or request.
    public static class OriginalEventSystemPreservationVerification
    {
        private const BindingFlags Own = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static void Check(ref int count, bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            count++;
        }
        private static Exception Failure(Action action)
        {
            try { action(); }
            catch (Exception error) { return error; }
            throw new InvalidOperationException("Expected original managed fault");
        }
        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        private static float Float(int value) => BitConverter.ToSingle(BitConverter.GetBytes(value), 0);
        private static Delegate[] Cached() => new Delegate[] { EventHandlers.ScrollUpHandler, EventHandlers.ScrollDownHandler, EventHandlers.TabNextHandler, EventHandlers.TabPreviousHandler, EventHandlers.TooltipToggleHandler, EventHandlers.LastInputTypeUpdateHandler };

        public static int OriginalDeclarationsCachesAndDataBits()
        {
            int count = 0;
            Type owner = typeof(EventHandlers);
            Check(ref count, owner.IsPublic && owner.IsAbstract && owner.IsSealed, "Original static event owner");
            Check(ref count, (owner.Attributes & TypeAttributes.BeforeFieldInit) == 0 && owner.TypeInitializer != null, "Original explicit initializer");
            Check(ref count, owner.GetFields(Own).Length == 6 && owner.GetMethods(Own).Length == 12, "Six caches, six getters and six private routes");
            string[] fields = { "s_scrollUpHandler", "s_scrollDownHandler", "s_tabNextHandler", "s_tabPreviousHandler", "s_tooltipToggleHandler", "s_lastInputTypeUpdateHandler" };
            string[] getters = { "ScrollUpHandler", "ScrollDownHandler", "TabNextHandler", "TabPreviousHandler", "TooltipToggleHandler", "LastInputTypeUpdateHandler" };
            string[] callbacks = { "OnScrollUp", "OnScrollDown", "OnTabNext", "OnTabPrevious", "OnTooltipToggle", "OnLastInputTypeUpdate" };
            Type[] contracts = { typeof(IScrollUpHandler), typeof(IScrollDownHandler), typeof(ITabNextHandler), typeof(ITabPreviousHandler), typeof(ITooltipToggleHandler), typeof(ILastInputTypeUpdateHandler) };
            Delegate[] first = Cached(), second = Cached();
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = owner.GetField(fields[i], Own);
                MethodInfo getter = owner.GetProperty(getters[i], Own).GetGetMethod();
                Type delegateType = typeof(ExecuteEvents.EventFunction<>).MakeGenericType(contracts[i]);
                Check(ref count, field.IsPrivate && field.IsStatic && field.IsInitOnly && field.FieldType == delegateType, "Original readonly typed cache " + fields[i]);
                Check(ref count, getter.IsPublic && getter.IsStatic && getter.ReturnType == delegateType && getter.GetParameters().Length == 0, "Original getter " + getters[i]);
                Check(ref count, ReferenceEquals(first[i], second[i]) && ReferenceEquals(first[i], field.GetValue(null)), "Stable original cached delegate identity");
                Check(ref count, first[i].Target == null && first[i].Method.DeclaringType == owner && first[i].Method.Name == "Execute", "Original static route target");
                ParameterInfo[] parameters = first[i].Method.GetParameters();
                Check(ref count, parameters.Length == 2 && parameters[0].ParameterType == contracts[i] && parameters[1].ParameterType == typeof(BaseEventData), "Exact original route parameters");
                Check(ref count, contracts[i].IsPublic && contracts[i].IsInterface && typeof(IEventSystemHandler).IsAssignableFrom(contracts[i]) && contracts[i].GetFields(Own).Length == 0, "Genuine fieldless event contract");
                MethodInfo[] declared = contracts[i].GetMethods(Own);
                Check(ref count, declared.Length == 1 && declared[0].Name == callbacks[i] && declared[0].IsAbstract && declared[0].ReturnType == typeof(void), "One original abstract callback");
                Check(ref count, declared[0].GetParameters().Length == 1 && declared[0].GetParameters()[0].ParameterType == (i < 2 ? typeof(ScrollEventData) : typeof(BaseEventData)), "Original callback data type");
                for (int j = 0; j < i; j++) Check(ref count, !ReferenceEquals(first[i], first[j]), "Distinct typed cache allocations");
            }
            Type flexible = typeof(IScrollFlexible);
            Check(ref count, flexible.IsPublic && flexible.IsInterface && typeof(IEventSystemHandler).IsAssignableFrom(flexible), "Authentic flexible marker");
            Check(ref count, flexible.GetMethods(Own).Length == 0 && flexible.GetFields(Own).Length == 0, "Marker has zero own members");
            FieldInfo delta = typeof(ScrollEventData).GetField("m_delta", Own);
            FieldInfo input = typeof(LastInputTypeUpdateEventData).GetField("m_newInputType", Own);
            FieldInfo baseSystem = typeof(BaseEventData).GetField("m_EventSystem", Own);
            Check(ref count, typeof(ScrollEventData).IsSealed && typeof(ScrollEventData).BaseType == typeof(BaseEventData) && delta.IsPrivate && delta.FieldType == typeof(float), "Original whole scroll data shape");
            Check(ref count, typeof(LastInputTypeUpdateEventData).IsSealed && typeof(LastInputTypeUpdateEventData).BaseType == typeof(BaseEventData) && input.IsPrivate && input.FieldType == typeof(int), "Original whole input data shape");
            int[] vectors = { 0, unchecked((int)0x80000000), 0x3f800000, unchecked((int)0xbf800000), 0x7f800000, unchecked((int)0xff800000), 0x7fc12345 };
            foreach (int bits in vectors)
            {
                var data = new ScrollEventData(null, Float(bits));
                Check(ref count, Bits((float)delta.GetValue(data)) == bits, "Original exact stored float bits");
                Check(ref count, baseSystem.GetValue(data) == null && !data.used, "Genuine null base event-system construction");
            }
            foreach (int value in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
            {
                var data = new LastInputTypeUpdateEventData(null, value);
                Check(ref count, (int)input.GetValue(data) == value && data.NewInputType == value, "Unclamped original Int32 field/getter");
                Check(ref count, baseSystem.GetValue(data) == null && !data.used, "Input genuine null base construction");
            }
            return count;
        }

        // This is an owned subscriber/client, not an original runtime provider.
        private sealed class Subscriber : IScrollUpHandler, IScrollDownHandler, ITabNextHandler, ITabPreviousHandler, ITooltipToggleHandler, ILastInputTypeUpdateHandler
        {
            internal int Calls, Kind;
            internal BaseEventData Last;
            internal Exception Throw;
            private void Receive(int kind, BaseEventData data) { Calls++; Kind = kind; Last = data; if (Throw != null) throw Throw; }
            public void OnScrollUp(ScrollEventData data) => Receive(0, data);
            public void OnScrollDown(ScrollEventData data) => Receive(1, data);
            public void OnTabNext(BaseEventData data) => Receive(2, data);
            public void OnTabPrevious(BaseEventData data) => Receive(3, data);
            public void OnTooltipToggle(BaseEventData data) => Receive(4, data);
            public void OnLastInputTypeUpdate(BaseEventData data) => Receive(5, data);
        }
        private static Action<Subscriber, BaseEventData>[] Routes() => new Action<Subscriber, BaseEventData>[] {
            (h,d) => EventHandlers.ScrollUpHandler(h,d), (h,d) => EventHandlers.ScrollDownHandler(h,d),
            (h,d) => EventHandlers.TabNextHandler(h,d), (h,d) => EventHandlers.TabPreviousHandler(h,d),
            (h,d) => EventHandlers.TooltipToggleHandler(h,d), (h,d) => EventHandlers.LastInputTypeUpdateHandler(h,d) };

        public static int OriginalValidationForwardingAndFaultOrder()
        {
            int count = 0;
            var handler = new Subscriber();
            var valid = new ScrollEventData(null, -0f);
            var plain = new BaseEventData(null);
            var input = new LastInputTypeUpdateEventData(null, int.MinValue);
            Action<Subscriber, BaseEventData>[] routes = Routes();
            Delegate[] prior = Cached();
            for (int i = 0; i < routes.Length; i++)
            {
                BaseEventData data = i < 2 ? (BaseEventData)valid : input;
                data.Use(); int before = handler.Calls;
                routes[i](handler, data);
                Check(ref count, handler.Calls == before + 1 && handler.Kind == i, "Correct typed original route, exactly once");
                Check(ref count, ReferenceEquals(handler.Last, data) && data.used, "Original forwards same data and does not reset it");
                var sentinel = new ApplicationException("owned callback fault"); handler.Throw = sentinel;
                Check(ref count, ReferenceEquals(Failure(() => routes[i](handler, data)), sentinel), "Original propagates exact callback fault");
                Check(ref count, handler.Calls == before + 2 && ReferenceEquals(handler.Last, data), "Callback prefix remains before fault");
                handler.Throw = null;
                if (i < 2)
                {
                    before = handler.Calls;
                    foreach (BaseEventData wrong in new BaseEventData[] { plain, input })
                    {
                        Check(ref count, Failure(() => routes[i](handler, wrong)).GetType() == typeof(ArgumentException), "Original ValidateEventData rejects wrong type");
                        Check(ref count, handler.Calls == before, "Validation failure precedes subscriber callback");
                        Check(ref count, Failure(() => routes[i](null, wrong)).GetType() == typeof(ArgumentException), "Validation precedes missing-handler call");
                    }
                    // Installed genuine validator formats data.GetType() while constructing
                    // its error. Null data therefore faults with NullReferenceException.
                    Check(ref count, Failure(() => routes[i](handler, null)).GetType() == typeof(NullReferenceException), "Genuine validator null-data failure");
                    Check(ref count, handler.Calls == before, "Null data does not call handler");
                    Check(ref count, Failure(() => routes[i](null, valid)).GetType() == typeof(NullReferenceException), "Valid scroll data reaches missing handler");
                }
                else
                {
                    before = handler.Calls; routes[i](handler, null);
                    Check(ref count, handler.Calls == before + 1 && handler.Last == null && handler.Kind == i, "Non-scroll forwards null data directly");
                    routes[i](handler, plain);
                    Check(ref count, ReferenceEquals(handler.Last, plain) && handler.Calls == before + 2, "Non-scroll accepts plain data directly");
                    Check(ref count, Failure(() => routes[i](null, plain)).GetType() == typeof(NullReferenceException), "Non-scroll missing handler fails directly");
                }
            }
            Delegate[] after = Cached();
            for (int i = 0; i < prior.Length; i++) Check(ref count, ReferenceEquals(prior[i], after[i]), "Every prior static readonly delegate identity retained");
            return count;
        }
    }
}
