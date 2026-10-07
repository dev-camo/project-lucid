namespace Hardlight
{
    public interface IRibbonRuntimeHandle
    {
        int KnotCount { get; }
        int LUTCount { get; }
        LinearRatio GetLinearRatioFromKnotT(KnotT knotT, RibbonAlignment alignment);
        KnotT GetKnotTFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment);
        PositionAndTangent GetLocalPosAndTangentFromKnotT(KnotT knotT, RibbonAlignment alignment);
        ISplineKnotRuntimeHandle GetKnotRuntimeHandle(int knotIndex, RibbonAlignment alignment);
        ISplineRuntimeHandle GetSubSplineRuntimeHandle(RibbonAlignment alignment);
    }
}
