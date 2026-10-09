using UnityEngine;

namespace Hardlight
{
    // Original02000037/38 have no local methods; true generic interfaces are reused.
    public interface IInputAxisSource : IBaseInputAxisSource<string> { }
    public interface IInputKeySource : IBaseInputKeySource<KeyCode> { }
    // Original0200008e is genuinely stripped empty, with these exact constraints.
    public interface IBaseSourceProvider<TKeySource, TKey, TAxisSource, TAxis>
        where TKeySource : IBaseInputKeySource<TKey>
        where TAxisSource : IBaseInputAxisSource<TAxis> { }
    public interface ISourceProvider : IBaseSourceProvider<IInputKeySource, KeyCode, IInputAxisSource, string>
    {
        IInputTouchSource TouchSource { get; } // Original06000287, sole own abstract accessor.
    }
}
