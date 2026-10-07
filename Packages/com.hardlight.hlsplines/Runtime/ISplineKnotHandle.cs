namespace Hardlight
{
    public interface ISplineKnotHandle
    {
        float[] TLUT { get; }
        float GetLUTValue(int index);
    }
}
