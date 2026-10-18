using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using Unity.Profiling;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 0200002a; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class ProceduralMesh_Placement : MonoBehaviour
	{
		[SerializeField]
		private ProceduralMesh_PlacementSetup m_setup;

		private static Camera m_camera;

		private static Plane[] m_cameraPlanes;

		private static int m_frameCount;

		// Retail only constructs these markers; their [Conditional] Begin/End samples are compiled out.
		private readonly ProfilerMarker m_profilerMarkerDespawnAllMeshes = new ProfilerMarker("ProceduralMeshPlacement.DespawnAllMeshes");

		private readonly ProfilerMarker m_profilerMarkerCheckForSpawnAndDespawn = new ProfilerMarker("ProceduralMeshPlacement.CheckForSpawnAndDespawn");

		protected int m_populatedCount;

		// 0600007f..0600008f (1.10.1 RVA 0x1A59160..0x1A59280): plain accessors.
		public float MaxRatio { get; private set; }

		protected float SpawnDistanceSquare { get; private set; }

		protected float DespawnDistanceSquare { get; private set; }

		protected float FadeInDistanceSquare { get; private set; }

		protected float FadeOutDistanceSquare { get; private set; }

		protected ProceduralMesh_PlacementSetup Setup => m_setup;

		public ProceduralMesh_SystemConfig SystemConfig { get; private set; }

		protected bool ValidSetup { get; set; }

		protected Bounds TotalBounds { get; set; }

		// 06000090 (1.10.1 RVA 0x1A592A0). Errors are reported while the previous
		// ValidSetup still holds; a null list counts as valid.
		public bool HasValidSetup()
		{
			List<string> errorMessages = GetErrorMessages();
			if (errorMessages != null)
			{
				OutputErrors(errorMessages);
				ValidSetup = errorMessages.Count == 0;
			}
			else
			{
				ValidSetup = true;
			}
			return ValidSetup;
		}

		protected abstract void CheckForSpawnAndDespawn(Camera camera, bool resetStorage);

		protected abstract void DespawnAllMeshes(bool onDestroy = false, bool resetStorage = false, bool canFade = false);

		public abstract void GenerateLocations(ProceduralMesh_StorageForSubSurface storage, ISurface surface, Transform surfaceTransform);

		public abstract Bounds CalculateBounds(ISurface surface, int startKnot, int endKnot);

		// 06000095 (1.10.1 RVA 0x1A59580)
		protected virtual IReadOnlyList<ProceduralMesh_Type> GetMeshTypes()
		{
			return null;
		}

		// 06000096 (1.10.1 RVA 0x1A59590)
		private void Start()
		{
			InitialiseSetup();
			ProcessManager.GetSystem<ProceduralMesh_PlacementManager>().RegisterInstance(this);
		}

		// 06000097 (1.10.1 RVA 0x1A596B0)
		internal virtual void InitialiseSetup()
		{
			ValidSetup = true;
			SystemConfig = SystemConfiguration.GetConfig<ProceduralMesh_SystemConfig>();
			MaxRatio = 0f;
			IReadOnlyList<ProceduralMesh_Type> meshTypes = GetMeshTypes();
			for (int i = 0; i < meshTypes.Count; ++i)
			{
				ProceduralMesh_Type meshType = meshTypes[i];
				meshType.PrimeForUse();
				MaxRatio += meshType.Ratio;
			}

			if (m_setup == null)
				m_setup = ScriptableObject.CreateInstance<ProceduralMesh_PlacementSetup>();
			m_setup.OnUpdated += SetupUpdated;

			ProceduralMesh_PlacementSetup.PerformanceSetup setup = m_setup.GetSetup();
			SpawnDistanceSquare = Mathf.Pow(setup.SpawnDistance, 2f);
			DespawnDistanceSquare = Mathf.Pow(setup.SpawnDistance + setup.DespawnExtraDistance, 2f);
			FadeInDistanceSquare = Mathf.Pow(setup.SpawnDistance - setup.FadeDistance, 2f);
			FadeOutDistanceSquare = Mathf.Pow(setup.SpawnDistance + setup.DespawnExtraDistance + setup.FadeDistance, 2f);
		}

		// 06000098 (1.10.1 RVA 0x1A59E20). The retail body only unsubscribes.
		private void SetupUpdated(ProceduralMesh_PlacementSetup setup)
		{
			m_setup.OnUpdated -= SetupUpdated;
		}

		// 06000099 (1.10.1 RVA 0x1A59FD0)
		private void OnDestroy()
		{
			if (m_setup != null)
				m_setup.OnUpdated -= SetupUpdated;
			DespawnAllMeshes(true, true);
			ProceduralMesh_PlacementManager manager = ProcessManager.GetSystemSafe<ProceduralMesh_PlacementManager>();
			if (manager != null)
				manager.UnregisterInstance(this);
		}

		// 0600009a (1.10.1 RVA 0x1A5A220). Camera.main and its frustum planes are
		// sampled once per frame and shared by every placement.
		private Camera GetCamera()
		{
			if (m_frameCount != Time.frameCount)
			{
				m_frameCount = Time.frameCount;
				m_camera = Camera.main;
				if (m_camera == null)
					return null;
				m_cameraPlanes = GeometryUtility.CalculateFrustumPlanes(m_camera);
			}
			return m_camera;
		}

		// 0600009b (1.10.1 RVA 0x1A5A330)
		private void PreUpdate()
		{
		}

		// 0600009c (1.10.1 RVA 0x1A5A340)
		public void DoUpdate()
		{
			if (!ValidSetup)
			{
				DespawnAllMeshes(false, true);
				return;
			}

			Camera camera = GetCamera();
			if (camera == null)
				return;

			bool resetStorage = SystemConfig.ResetMeshStorage;
			float sqrDistance = TotalBounds.SqrDistance(camera.transform.position);
			if (sqrDistance > DespawnDistanceSquare)
			{
				if (m_populatedCount > 0)
					DespawnAllMeshes(false, resetStorage, sqrDistance < FadeOutDistanceSquare);
			}
			else
			{
				CheckForSpawnAndDespawn(camera, resetStorage);
			}
		}

		// 0600009d (1.10.1 RVA 0x1A5A470)
		protected static List<string> AddErrorMessage(List<string> errors, string message)
		{
			if (errors == null)
				errors = new List<string>(1);
			errors.Add(message);
			return errors;
		}

		// 0600009e (1.10.1 RVA 0x1A5A550)
		protected virtual List<string> GetErrorMessages()
		{
			return null;
		}

		// 0600009f (1.10.1 RVA 0x1A5A560)
		protected static string GetPath(Transform current)
		{
			if (current.parent == null)
				return "/" + current.name;
			return GetPath(current.parent) + "/" + current.name;
		}

		// 060000a0 (1.10.1 RVA 0x1A59300)
		private void OutputErrors(IReadOnlyList<string> errorMessages)
		{
			if (!ValidSetup || errorMessages.Count <= 0)
				return;

			string message = "'" + GetPath(transform) + "' game object has invalid procedural mesh setup:";
			for (int i = 0; i < errorMessages.Count; ++i)
				message = message + "\n- " + errorMessages[i];
			HLOutput.LogError(message);
		}

		// 060000a1 (1.10.1 RVA 0x1A5A630). Extents become the sum of the absolute
		// transformed axis vectors, so rotation and scale stay conservative.
		public static Bounds TransformBounds(Transform transform, Bounds localBounds)
		{
			Vector3 center = transform.TransformPoint(localBounds.center);
			Vector3 extents = localBounds.extents;
			Vector3 axisX = transform.TransformVector(extents.x, 0f, 0f);
			Vector3 axisY = transform.TransformVector(0f, extents.y, 0f);
			Vector3 axisZ = transform.TransformVector(0f, 0f, extents.z);
			extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
			extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
			extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);
			return new Bounds { center = center, extents = extents };
		}

		// 060000a2 (1.10.1 RVA 0x1A5A720)
		public bool TestAgainstCameraFrustum(Transform transform, Bounds bounds)
		{
			if (!SystemConfig.UseCameraFrustum)
				return true;
			return GeometryUtility.TestPlanesAABB(m_cameraPlanes, TransformBounds(transform, bounds));
		}

		protected abstract Bounds CalculateTotalWorldSpaceBounds();
	}
}
