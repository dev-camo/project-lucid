using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game02000ab5: complete six owner declarations and real Side enum.
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SurfaceSplineSection
    {
        public enum Side { Any = 0, Left = 1, Right = 2 }
        public ObjectSerializable<ISpline> SourceSpline = new ObjectSerializable<ISpline>();
        public float SourceDistanceStart;
        public float SourceDistanceEnd;
        public ObjectSerializable<ISpline> TargetSpline = new ObjectSerializable<ISpline>();
        public float TargetDistanceStart;
        public float TargetDistanceEnd;
        public Side TargetSide;
        private const float SplineNeighbourDistanceMax = 50f;
        private const float SplineNeighbourDistanceMaxSqr = 2500f;
        private const float SplineDistanceStep = 1f;

        //06003d97: reference-equal wrappers are skipped. Distinct wrappers holding
        //the same spline remain candidates. Original local bounds and Expand(50)
        //are retained; there is no activity or distinct underlying-surface filter.
        public static void BuildTrackableNeighbours(IDictionary<ISurface, List<SurfaceSplineSection>> splineNeighbours,
            List<ObjectSerializable<ISurface>> trackableSplineList)
        {
            splineNeighbours.Clear();
            foreach (ObjectSerializable<ISurface> source in trackableSplineList)
            {
                ISpline sourceSpline = source.Value as ISpline;
                if (sourceSpline == null) continue;
                Bounds sourceBounds = sourceSpline.GetBoundingBox(false);
                sourceBounds.Expand(SplineNeighbourDistanceMax);
                foreach (ObjectSerializable<ISurface> target in trackableSplineList)
                {
                    if (source == target) continue;
                    ISpline targetSpline = target.Value as ISpline;
                    if (targetSpline == null) continue;
                    if (sourceBounds.Intersects(targetSpline.GetBoundingBox(false)))
                        AddNeighbourSections(sourceSpline, targetSpline, splineNeighbours);
                }
            }
        }

        private static void AddNeighbourSections(ISpline sourceSpline, ISpline targetSpline,
            IDictionary<ISurface, List<SurfaceSplineSection>> splineNeighbours)
        {
            float sourceDistance = 0f;
            float targetDistance = 0f;
            do
            {
                LightweightTransform sourceTransform = sourceSpline.GetWorldTransformFromDistance(sourceDistance);
                SurfaceLocation nearest = targetSpline.FindNearestSurfaceLocationFromWorld(sourceTransform.Location);
                if ((nearest.m_worldPosition - sourceTransform.Location).sqrMagnitude > SplineNeighbourDistanceMaxSqr)
                {
                    sourceDistance += SplineDistanceStep;
                    continue;
                }
                targetDistance = FindSplineDistanceFromWorld(targetSpline, nearest.m_worldPosition);
                SurfaceSplineSection section = CreateSplineSection(sourceSpline, sourceDistance,
                    targetSpline, targetDistance, splineNeighbours);
                sourceTransform = sourceSpline.GetWorldTransformFromDistance(sourceDistance);
                LightweightTransform targetTransform = targetSpline.GetWorldTransformFromDistance(targetDistance);
                CalculateSplineSectionSide(sourceTransform, targetTransform, out Side initialSide);
                do
                {
                    sourceDistance = Mathf.Min(sourceDistance + SplineDistanceStep, sourceSpline.Length);
                    sourceTransform = sourceSpline.GetWorldTransformFromDistance(sourceDistance);
                    targetDistance = FindSplineDistanceFromWorld(targetSpline, sourceTransform.Location);
                    targetTransform = targetSpline.GetWorldTransformFromDistance(targetDistance);
                    if (!CalculateSplineSectionSide(sourceTransform, targetTransform, out Side side) ||
                        side != initialSide || !((targetTransform.Location - sourceTransform.Location).sqrMagnitude <= SplineNeighbourDistanceMaxSqr))
                        break;
                } while (sourceDistance < sourceSpline.Length);
                if (section != null)
                {
                    section.SourceDistanceEnd = sourceDistance;
                    section.TargetDistanceEnd = targetDistance;
                }
            } while (sourceDistance < sourceSpline.Length && targetDistance < targetSpline.Length);
        }

        //06003d99: a new empty dictionary list is installed before the side test,
        //and remains if the test fails. Section construction initializes wrappers,
        //then replaces them with newly constructed wrappers for the supplied pair.
        private static SurfaceSplineSection CreateSplineSection(ISpline sourceSpline, float sourceDistance,
            ISpline targetSpline, float targetDistance, IDictionary<ISurface, List<SurfaceSplineSection>> splineNeighbours)
        {
            if (!splineNeighbours.TryGetValue(sourceSpline, out List<SurfaceSplineSection> sections))
            {
                sections = new List<SurfaceSplineSection>();
                splineNeighbours[sourceSpline] = sections;
            }
            LightweightTransform sourceTransform = sourceSpline.GetWorldTransformFromDistance(sourceDistance);
            LightweightTransform targetTransform = targetSpline.GetWorldTransformFromDistance(targetDistance);
            if (!CalculateSplineSectionSide(sourceTransform, targetTransform, out Side targetSide)) return null;
            var section = new SurfaceSplineSection
            {
                SourceSpline = new ObjectSerializable<ISpline>(sourceSpline),
                SourceDistanceStart = sourceDistance,
                SourceDistanceEnd = sourceDistance,
                TargetSpline = new ObjectSerializable<ISpline>(targetSpline),
                TargetDistanceStart = targetDistance,
                TargetDistanceEnd = targetDistance,
                TargetSide = targetSide
            };
            sections.Add(section);
            return section;
        }

        private static bool CalculateSplineSectionSide(LightweightTransform sourceTransform,
            LightweightTransform targetTransform, out Side targetSide)
        {
            float dot = Vector3.Dot(sourceTransform.Orientation * Vector3.right,
                (targetTransform.Location - sourceTransform.Location).normalized);
            targetSide = dot > 0f ? Side.Right : Side.Left;
            return Mathf.Abs(dot) > 0.0001f;
        }

        //06003d9b: one-unit samples exclude the endpoint; strict improvement
        //retains the earliest tied sample. Empty or nonpositive lengths return0.
        private static float FindSplineDistanceFromWorld(ISpline spline, Vector3 worldPosition)
        {
            float closestDistance = 0f;
            float closestDistanceSquared = float.MaxValue;
            for (float distance = 0f; distance < spline.Length; distance += SplineDistanceStep)
            {
                float distanceSquared = (spline.GetWorldTransformFromDistance(distance).Location - worldPosition).sqrMagnitude;
                if (distanceSquared < closestDistanceSquared)
                {
                    closestDistance = distance;
                    closestDistanceSquared = distanceSquared;
                }
            }
            return closestDistance;
        }

        public SurfaceSplineSection() { }
    }
}
