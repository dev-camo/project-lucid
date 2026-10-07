namespace Hardlight
{
    // HLInput.Runtime 06000049: genuine invariant, unconstrained getter contract; no native body.
    public interface IAxisProvider<T>
    {
        T Axis { get; }
    }
}
