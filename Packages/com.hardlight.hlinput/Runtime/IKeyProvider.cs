namespace Hardlight
{
    // HLInput.Runtime 0600004a: genuine invariant, unconstrained getter contract; no native body.
    public interface IKeyProvider<T>
    {
        T Key { get; }
    }
}
