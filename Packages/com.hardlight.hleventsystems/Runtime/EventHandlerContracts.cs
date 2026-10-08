using UnityEngine.EventSystems;

// Six complete original HLEventSystems.Runtime contracts, each one actual abstract method and no fields.
public interface IScrollUpHandler : IEventSystemHandler
{
    void OnScrollUp(ScrollEventData eventData);
}
public interface IScrollDownHandler : IEventSystemHandler
{
    void OnScrollDown(ScrollEventData eventData);
}
public interface ITabNextHandler : IEventSystemHandler
{
    void OnTabNext(BaseEventData eventData);
}
public interface ITabPreviousHandler : IEventSystemHandler
{
    void OnTabPrevious(BaseEventData eventData);
}
public interface ITooltipToggleHandler : IEventSystemHandler
{
    void OnTooltipToggle(BaseEventData eventData);
}
public interface ILastInputTypeUpdateHandler : IEventSystemHandler
{
    void OnLastInputTypeUpdate(BaseEventData eventData);
}
