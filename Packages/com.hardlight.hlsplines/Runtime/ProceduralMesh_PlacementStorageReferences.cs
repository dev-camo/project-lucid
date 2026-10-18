using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using Random = System.Random;

// Reference declarations of the splines-storage types the placement restore
// calls, with original names, signatures, field order and the constructor
// stores visible in the placement bodies. Their own bodies belong to the
// splines-storage restore, which supersedes this whole file; until then they
// throw rather than guess.
namespace Hardlight
{
	// Original HLSplines.Runtime 02000023 (reference declaration).
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public static class LocationGenerator
	{
		public enum BiasType
		{
			WidthRatio = 0,
			WorldUnit = 1
		}

		public enum BiasDensity
		{
			Even = 0,
			Left = 1,
			Right = 2,
			Edge = 3,
			Center = 4
		}

		public static List<LightweightTransform> RandomPositions(Random rand, IRibbon ribbon, int numberToGenerate, float biasMin = -1f, float biasMax = 1f, BiasType biasType = BiasType.WidthRatio, BiasDensity biasDensity = BiasDensity.Even, bool inverseBias = false, int startKnot = 0, int endKnot = -1) =>
			throw new NotImplementedException();

		public static List<LightweightTransform> RandomPositions(Random rand, ISpline spline, int numberToGenerate, int startKnot = 0, int endKnot = -1) =>
			throw new NotImplementedException();

		public static List<LightweightTransform> GridPositions(IRibbon ribbon, float strideZ, float strideX, float biasMin = -1f, float biasMax = 1f, BiasType biasType = BiasType.WidthRatio, int startKnot = 0, int endKnot = -1) =>
			throw new NotImplementedException();

		public static List<LightweightTransform> GridPositions(ISpline spline, float strideZ, int startKnot = 0, int endKnot = -1) =>
			throw new NotImplementedException();

		public static List<LightweightTransform> RandomPositions(Random rand, Mesh mesh, int numberToGenerate) =>
			throw new NotImplementedException();
	}

	// Original HLSplines.Runtime 0200004b (reference declaration). The seed and
	// ratio initializers are the stores of the constructor inlined into the
	// placement Reset bodies (x86-64 0x1A5D9D6).
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Serializable]
	public class ProceduralMesh_Type
	{
		[SerializeField]
		private PrefabPoolType m_meshPoolType;

		[SerializeField]
		private int m_seed = -1;

		[SerializeField]
		private float m_ratio = 1f;

		private SystemRef<PrefabPoolManager> m_manager;

		public ProceduralMesh_Pool MeshPool { get; set; }

		public PrefabPoolType MeshPoolType => m_meshPoolType;

		public int Seed
		{
			get => m_seed;
			set => m_seed = value;
		}

		public float Ratio => m_ratio;

		public virtual bool HasValidSetup() => throw new NotImplementedException();

		public virtual string GetErrorMessage() => throw new NotImplementedException();

		public virtual void PrimeForUse() => throw new NotImplementedException();
	}

	// Original HLSplines.Runtime 0200004c (reference declaration).
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Serializable]
	public class ProceduralMesh_TypeGrid : ProceduralMesh_Type
	{
		[SerializeField]
		private ProceduralMesh_SetupTypeGrid m_setup;

		public ProceduralMesh_SetupTypeGrid Setup => m_setup;

		public override void PrimeForUse() => throw new NotImplementedException();
	}

	// Original HLSplines.Runtime 0200004d (reference declaration).
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Serializable]
	public class ProceduralMesh_TypeRandom : ProceduralMesh_Type
	{
		[SerializeField]
		private ProceduralMesh_SetupTypeRandom m_setup;

		public ProceduralMesh_SetupTypeRandom Setup => m_setup;

		public override void PrimeForUse() => throw new NotImplementedException();
	}

	// Original HLSplines.Runtime 0200004e (reference declaration). The bias
	// initializers are the stores of the constructor inlined into
	// ProceduralMesh_PlacementOnRibbonRandom.Reset (x86-64 0x1A5E056).
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Serializable]
	public class ProceduralMesh_TypeRibbonRandom : ProceduralMesh_TypeRandom
	{
		[SerializeField]
		private float m_biasMin = -1f;

		[SerializeField]
		private float m_biasMax = 1f;

		[SerializeField]
		private LocationGenerator.BiasType m_biasType = LocationGenerator.BiasType.WidthRatio;

		[SerializeField]
		private LocationGenerator.BiasDensity m_biasDensity = LocationGenerator.BiasDensity.Even;

		[SerializeField]
		private bool m_inverseBias;

		public float BiasMin => m_biasMin;

		public float BiasMax => m_biasMax;

		public LocationGenerator.BiasType BiasType => m_biasType;

		public LocationGenerator.BiasDensity BiasDensity => m_biasDensity;

		public bool InverseBias => m_inverseBias;
	}

	// Original HLSplines.Runtime 02000041 (reference declaration).
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ProceduralMesh_StorageForSubSurface
	{
		public delegate List<LightweightTransform> GetRandomLocations<TMeshType>(Random random, int numMeshesToCreate, int startSubSurface, int endSubSurface, TMeshType meshType);

		public delegate List<LightweightTransform> GetGridLocations(int startSubSurface, int endSubSurface);

		public int StartSubSurface { get; set; }

		public int EndSubSurface { get; set; }

		public void GenerateRandomLocations<TMeshType>(Transform surfaceTransform, List<TMeshType> meshTypes, NumMeshesType numMeshesType, float numMeshes, bool scaleNumMeshes, ProceduralMesh_Placement owner, GetRandomLocations<TMeshType> getLocations) =>
			throw new NotImplementedException();

		public void GenerateGridLocations<TMeshType>(Transform surfaceTransform, List<TMeshType> meshTypes, float gapsRatio, ProceduralMesh_Placement owner, GetGridLocations getLocations) =>
			throw new NotImplementedException();
	}

	// Original HLSplines.Runtime 02000049 (reference declaration).
	public class ProceduralMesh_StorageForSurface
	{
		protected List<ProceduralMesh_StorageForSubSurface> m_meshStorage;

		private int m_populatedCount;

		protected ProceduralMesh_StorageForSurface(int startSubSurface, int endSubSurface, bool populatePerSubSurface, float numMeshesMultiplier, bool useRuntimeCallback)
		{
		}

		public int DespawnSurfaceMeshes(bool onDestroy = false, bool resetStorage = false, bool canFade = false, bool force = false) =>
			throw new NotImplementedException();
	}

	// Original HLSplines.Runtime 0200003f (reference declaration).
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ProceduralMesh_StorageForRibbon : ProceduralMesh_StorageForSurface
	{
		private readonly Ribbon m_ribbon;

		public ProceduralMesh_StorageForRibbon(Ribbon ribbon, int startKnot, int endKnot, bool populatePerKnot, float numMeshesMultiplier, bool useRuntimeCallback)
			: base(startKnot, endKnot, populatePerKnot, numMeshesMultiplier, useRuntimeCallback) =>
			throw new NotImplementedException();

		public void GenerateLocations(ProceduralMesh_Placement owner) => throw new NotImplementedException();

		public int CheckForSpawnAndDespawn(Camera camera, float spawnDistanceSquare, float despawnDistanceSquare, float fadeInDistanceSquare, float fadeOutDistanceSquare, bool resetMeshStorage, ProceduralMesh_Placement owner) =>
			throw new NotImplementedException();

		public Bounds CalculateTotalWorldSpaceBounds() => throw new NotImplementedException();
	}

	// Original HLSplines.Runtime 02000040 (reference declaration).
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ProceduralMesh_StorageForSpline : ProceduralMesh_StorageForSurface
	{
		private readonly Spline m_spline;

		public ProceduralMesh_StorageForSpline(Spline spline, int startKnot, int endKnot, bool populatePerKnot, float numMeshesMultiplier, bool useRuntimeCallback)
			: base(startKnot, endKnot, populatePerKnot, numMeshesMultiplier, useRuntimeCallback) =>
			throw new NotImplementedException();

		public void GenerateLocations(ProceduralMesh_Placement owner) => throw new NotImplementedException();

		public int CheckForSpawnAndDespawn(Camera camera, float spawnDistanceSquare, float despawnDistanceSquare, float fadeInDistanceSquare, float fadeOutDistanceSquare, bool resetMeshStorage, ProceduralMesh_Placement owner) =>
			throw new NotImplementedException();

		public Bounds CalculateTotalWorldSpaceBounds() => throw new NotImplementedException();
	}

	// Original HLSplines.Runtime 0200003e (reference declaration).
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ProceduralMesh_StorageForMesh : ProceduralMesh_StorageForSurface
	{
		private readonly MeshCollider m_meshCollider;

		private readonly MeshSurface m_meshSurface;

		public ProceduralMesh_StorageForMesh(MeshCollider meshCollider, float numMeshesMultiplier, bool useRuntimeCallback)
			: base(0, 0, false, numMeshesMultiplier, useRuntimeCallback) =>
			throw new NotImplementedException();

		public void GenerateLocations(ProceduralMesh_Placement owner) => throw new NotImplementedException();

		public int CheckForSpawnAndDespawn(Camera camera, float spawnDistanceSquare, float despawnDistanceSquare, float fadeInDistanceSquare, float fadeOutDistanceSquare, bool resetMeshStorage, ProceduralMesh_Placement owner) =>
			throw new NotImplementedException();

		public Bounds CalculateTotalWorldSpaceBounds() => throw new NotImplementedException();
	}
}
