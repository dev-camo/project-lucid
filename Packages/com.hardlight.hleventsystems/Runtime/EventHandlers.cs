using UnityEngine.EventSystems;
using Unity.IL2CPP.CompilerServices;

// Whole original HLEventSystems.Runtime02000002,13 methods and6 fields.
[Il2CppSetOption(Option.NullChecks, false)]
[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
public static class EventHandlers
{
    private static readonly ExecuteEvents.EventFunction<IScrollUpHandler> s_scrollUpHandler;
    private static readonly ExecuteEvents.EventFunction<IScrollDownHandler> s_scrollDownHandler;
    private static readonly ExecuteEvents.EventFunction<ITabNextHandler> s_tabNextHandler;
    private static readonly ExecuteEvents.EventFunction<ITabPreviousHandler> s_tabPreviousHandler;
    private static readonly ExecuteEvents.EventFunction<ITooltipToggleHandler> s_tooltipToggleHandler;
    private static readonly ExecuteEvents.EventFunction<ILastInputTypeUpdateHandler> s_lastInputTypeUpdateHandler;
    // Original06000001.
    public static ExecuteEvents.EventFunction<IScrollUpHandler> ScrollUpHandler => s_scrollUpHandler;
    // Original06000002.
    public static ExecuteEvents.EventFunction<IScrollDownHandler> ScrollDownHandler => s_scrollDownHandler;
    // Original06000003.
    public static ExecuteEvents.EventFunction<ITabNextHandler> TabNextHandler => s_tabNextHandler;
    // Original06000004.
    public static ExecuteEvents.EventFunction<ITabPreviousHandler> TabPreviousHandler => s_tabPreviousHandler;
    // Original06000005.
    public static ExecuteEvents.EventFunction<ITooltipToggleHandler> TooltipToggleHandler => s_tooltipToggleHandler;
    // Original06000006.
    public static ExecuteEvents.EventFunction<ILastInputTypeUpdateHandler> LastInputTypeUpdateHandler => s_lastInputTypeUpdateHandler;
    // Original06000007; the original scroll routes validate their custom event data first.
    private static void Execute(IScrollUpHandler handler, BaseEventData data) { handler.OnScrollUp(ExecuteEvents.ValidateEventData<ScrollEventData>(data)); }
    // Original06000008; the original scroll routes validate their custom event data first.
    private static void Execute(IScrollDownHandler handler, BaseEventData data) { handler.OnScrollDown(ExecuteEvents.ValidateEventData<ScrollEventData>(data)); }
    // Original06000009; the original scroll routes validate their custom event data first.
    private static void Execute(ITabNextHandler handler, BaseEventData data) { handler.OnTabNext(data); }
    // Original0600000a; the original scroll routes validate their custom event data first.
    private static void Execute(ITabPreviousHandler handler, BaseEventData data) { handler.OnTabPrevious(data); }
    // Original0600000b; the original scroll routes validate their custom event data first.
    private static void Execute(ITooltipToggleHandler handler, BaseEventData data) { handler.OnTooltipToggle(data); }
    // Original0600000c; the original scroll routes validate their custom event data first.
    private static void Execute(ILastInputTypeUpdateHandler handler, BaseEventData data) { handler.OnLastInputTypeUpdate(data); }
    // Original0600000d is explicit, preserving the absence of BeforeFieldInit and the six allocation/store pairs.
    static EventHandlers()
    {
        s_scrollUpHandler = new ExecuteEvents.EventFunction<IScrollUpHandler>(Execute);
        s_scrollDownHandler = new ExecuteEvents.EventFunction<IScrollDownHandler>(Execute);
        s_tabNextHandler = new ExecuteEvents.EventFunction<ITabNextHandler>(Execute);
        s_tabPreviousHandler = new ExecuteEvents.EventFunction<ITabPreviousHandler>(Execute);
        s_tooltipToggleHandler = new ExecuteEvents.EventFunction<ITooltipToggleHandler>(Execute);
        s_lastInputTypeUpdateHandler = new ExecuteEvents.EventFunction<ILastInputTypeUpdateHandler>(Execute);
    }
}
