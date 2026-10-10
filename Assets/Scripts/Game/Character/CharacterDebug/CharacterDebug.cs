// Complete original Game.Runtime 02000323 native-derived source candidate.
// Whole source preserves shipping debug side effects; Engine/native equivalence remains unaccepted.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Hardlight;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterDebug : ActorDebug
    {
        private const string DebugMenuCharacterSelect = "Character Select"; // 0x04000b94
        private const string DebugMenuCharacterTraits = "Character Traits"; // 0x04000b95
        private const string DebugMenuAbilityPrefix = "Ability"; // 0x04000b96
        private const float AbilityValueDisplayTime = 10f; // 0x04000b97
        private const float TrailRendererWidth = 0.1f; // 0x04000b98
        private readonly Color TrailRendererColor = new Color(1f, 0f, 0f, 1f); // 0x04000b99
        private readonly Color LocationAxisXColor = new Color(1f, 0f, 0f, 1f); // 0x04000b9a
        private readonly Color LocationAxisYColor = new Color(0f, 1f, 0f, 1f); // 0x04000b9b
        private readonly Color LocationAxisZColor = new Color(0f, 0f, 1f, 1f); // 0x04000b9c
        private const float LocationAxisWidth = 0.05f; // 0x04000b9d
        private const float TurnVerticalOffset = 0.1f; // 0x04000b9e
        private const float TrackerMeshLength = 3f; // 0x04000b9f
        private const float TrackerMeshWidth = 1f; // 0x04000ba0
        private const float TrackerMeshHeightOffset = 0.5f; // 0x04000ba1
        private const float StickyControlsMeshLength = 3f; // 0x04000ba2
        private const float StickyControlsMeshWidth = 1f; // 0x04000ba3
        private const float StickyControlsMeshHeightOffset = 0.25f; // 0x04000ba4
        private const float TurnMeshLength = 3f; // 0x04000ba5
        private const float TurnMeshWidth = 0.07f; // 0x04000ba6
        private const float TurnMeshHeightOffset = 0.014f; // 0x04000ba7
        private const float RaycastHitSphereRadius = 2f; // 0x04000ba8
        private const float RaycastLineWidth = 5f; // 0x04000ba9
        private TrailRenderer m_trailRenderer; // 0x04000baa
        private GameObject m_root; // 0x04000bab
        private GameObject m_locationPrefab; // 0x04000bac
        private GameObject m_locationParent; // 0x04000bad
        private int m_locationCount; // 0x04000bae
        private readonly Dictionary<string,GameObject> m_trackedLocationObjects = new Dictionary<string, GameObject>(); // 0x04000baf
        private StackableDataHandle m_characterTakeDamageDebugHandle; // 0x04000bb0
        private Shader m_debugShaderUnlit; // 0x04000bb1
        private Shader m_debugShaderLit; // 0x04000bb2
        private Material m_materialAxisX; // 0x04000bb3
        private Material m_materialAxisY; // 0x04000bb4
        private Material m_materialAxisZ; // 0x04000bb5
        private GameObject m_trackerDebug; // 0x04000bb6
        private GameObject m_stickyControlsDebug; // 0x04000bb7
        private GameObject m_cameraTurnDebug; // 0x04000bb8
        private GameObject m_intendedTurnDebug; // 0x04000bb9
        private GameObject m_actualTurnDebug; // 0x04000bba
        private GameObject m_velocityDebug; // 0x04000bbb
        private StackableDataHandle m_pauseTimeOverride; // 0x04000bbc
        private TimeSetting m_pauseTimeSetting; // 0x04000bbd
        private Vector3 m_displayVelocity; // 0x04000bbe
        private DebugTrackRender m_trackRenderer; // 0x04000bbf
        private CharacterDebugImmobilise m_immobilise; // 0x04000bc0
        private bool m_cameraRotationLocked; // 0x04000bc1
        private string m_throttleDebugInfo; // 0x04000bc2
        private string m_movementDebugInfo; // 0x04000bc3
        private string m_turningDebugInfo; // 0x04000bc4
        private Character m_character; // 0x04000bc5
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>(null, true); // 0x04000bc6
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true); // 0x04000bc7
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>(null, true); // 0x04000bc8

        // 06001369: original UI container key.
        protected override string ContainerIdentifierName => "uicontaineridentifier_character_debug_info";

        // 0600136a. Erased menu drawing leaves the original enumerations and substring faults.
        public override void Init(Actor actor)
        {
            base.Init(actor);
            m_character = actor as Character;
            ControlMapping.Subscribe(GameInput.DebugRestart, OnRestart, InputTrigger.Down, -1);
            ControlMapping.Subscribe(GameInput.DebugRespawn, OnRespawn, InputTrigger.Down, -1);
            ControlMapping.Subscribe(GameInput.DebugInfo, OnCharacterInfo, InputTrigger.Down, -1);
            ControlMapping.Subscribe(GameInput.DebugGameSpeed, OnDebugGameSpeedHeld, InputTrigger.Held, -1);
            ControlMapping.Subscribe(GameInput.DebugGameSpeed, OnDebugGameSpeedReleased, InputTrigger.Up, -1);
            foreach (KeyValuePair<CharacterId, CharacterDefinition> pair in ProcessManager.GetSystem<DataManager>().Characters)
            {
                // Original enumeration survives the removed conditional menu calls.
            }
            foreach (AbilityTiersDefinition tiers in m_characterManagerRef.Get().GetCurrentCharacterUnsafe().Definition.Traits.AbilityTierDefinitions)
            {
                if (tiers.AbilityTiers.Count >= 2)
                {
                    for (int i = 1; i < tiers.AbilityTiers.Count; i++)
                    {
                        AbilityDefinition ability = tiers.AbilityTiers[i];
                        string abilityName = ability.name;
                        _ = abilityName.Substring(DebugMenuAbilityPrefix.Length, abilityName.Length - DebugMenuAbilityPrefix.Length);
                    }
                }
            }
            VisualAssetManager assets = ProcessManager.GetSystem<VisualAssetManager>();
            m_debugShaderUnlit = assets.Get("Universal Render Pipeline/Unlit");
            m_debugShaderLit = assets.Get("Universal Render Pipeline/Lit");
            Material trailMaterial = new Material(m_debugShaderUnlit);
            trailMaterial.color = TrailRendererColor;
            m_trailRenderer = gameObject.GetComponent<TrailRenderer>();
            if (m_trailRenderer == null) m_trailRenderer = gameObject.AddComponent<TrailRenderer>();
            m_trailRenderer.material = trailMaterial;
            m_trailRenderer.widthMultiplier = TrailRendererWidth;
            m_trailRenderer.time = 5f;
            Material materialX = new Material(m_debugShaderUnlit);
            materialX.color = LocationAxisXColor;
            m_materialAxisX = materialX;
            Material materialY = new Material(m_debugShaderUnlit);
            materialY.color = LocationAxisYColor;
            m_materialAxisY = materialY;
            Material materialZ = new Material(m_debugShaderUnlit);
            materialZ.color = LocationAxisZColor;
            m_materialAxisZ = materialZ;
            m_pauseTimeSetting = TimeSetting.GetDefault(0f);
            m_trackRenderer = new DebugTrackRender(m_character.TrackManager);
            m_immobilise = new CharacterDebugImmobilise(m_character);
            m_trailRenderer.enabled = IsEnabled;
            DebugMenu.OnDebugMenuToggle += OnDebugMenuToggle;
        }

        // 0600136b: resolve the camera manager before toggling the stored flag.
        private void DebugFreezeCameraRotation()
        {
            CinemachineCameraManager cameraManager = ProcessManager.GetSystem<CinemachineCameraManager>();
            m_cameraRotationLocked = !m_cameraRotationLocked;
            cameraManager.ProxyTarget.DisableRotation(m_cameraRotationLocked);
        }
        // 0600136c.
        public bool DebugInvulnerable() => m_characterTakeDamageDebugHandle != null &&
            m_character.GetModifierOverride<bool>(m_characterTakeDamageDebugHandle, (int)GameplayModifierType.Invulnerable);
        // 0600136d: capture the receiver and existing handle before reading the override.
        public void DebugToggleInvulnerable()
        {
            Character character = m_character;
            StackableDataHandle handle = m_characterTakeDamageDebugHandle;
            if (handle != null) character.AdjustModifierOverrides(handle, (int)GameplayModifierType.Invulnerable, !DebugInvulnerable());
            else m_characterTakeDamageDebugHandle = character.AddModifierOverride((int)GameplayModifierType.Invulnerable, true);
        }
        [Conditional("BUILD_DEVELOPMENT")]
        private void DebugCharacterSelect(CharacterId characterId) => m_characterManagerRef.Get().SetCharacter(characterId, true); // 0600136e
        [Conditional("BUILD_DEVELOPMENT")]
        private void DebugClearCharacterTraits() { _ = m_characterManagerRef.Get(); } // 0600136f, original lookup-only body.
        [Conditional("BUILD_DEVELOPMENT")]
        private void DebugCharacterTrait(AbilityDefinition abilityDefinition) // 06001370
        {
            foreach (AbilityTiersDefinition tiers in m_characterManagerRef.Get().GetCurrentCharacterUnsafe().Traits.AbilityTierDefinitions)
                if (tiers.AbilityTiers.Contains(abilityDefinition)) break;
        }
        private void OnDebugMenuToggle(bool debugMenuActive) // 06001371
        {
            if ((m_pauseTimeOverride != null) != debugMenuActive) OnDebugPause(0f); // amount is unused by the original callee.
        }
        // 06001372: original close leaves the two speed callbacks subscribed and does not call base.Close.
        public override void Close()
        {
            if (IsEnabled)
            {
                UIManager manager = ProcessManager.GetSystem<UIManager>();
                manager.Close(UIContainerIdentifier.FindByName(ContainerIdentifierName));
            }
            if (m_root != null) { UnityEngine.Object.Destroy(m_root); m_root = null; }
            if (m_trackerDebug != null) { UnityEngine.Object.Destroy(m_trackerDebug); m_trackerDebug = null; }
            if (m_stickyControlsDebug != null) { UnityEngine.Object.Destroy(m_stickyControlsDebug); m_stickyControlsDebug = null; }
            if (m_trackRenderer != null) { m_trackRenderer.Close(); m_trackRenderer = null; }
            m_trailRenderer = null;
            ControlMapping.Unsubscribe(GameInput.DebugRestart, OnRestart);
            ControlMapping.Unsubscribe(GameInput.DebugRespawn, OnRespawn);
            ControlMapping.Unsubscribe(GameInput.DebugInfo, OnCharacterInfo);
            m_immobilise.Close();
        }
        private static void OnRestart(float amount) => ProcessManager.GetSystem<LevelManager>().Restart(); // 06001373
        private void OnRespawn(float amount) => ProcessManager.GetSystem<CharacterManager>().GetCurrentCharacterUnsafe().Storage.SetValue(ActorFSMKeys.IsOutOfBounds, true); // 06001374
        private void OnCharacterInfo(float amount) => ToggleDebugInfo(); // 06001375
        private void OnDebugPause(float amount) // 06001376
        {
            TimeManager manager = ProcessManager.GetSystem<TimeManager>();
            if (m_pauseTimeOverride != null) { manager.RemoveTimeSetting(m_pauseTimeOverride); m_pauseTimeOverride = null; }
            else m_pauseTimeOverride = manager.ApplyTimeSetting(m_pauseTimeSetting);
        }
        private void OnDebugGameSpeedHeld(float amount) // 06001377
        {
            SystemRef<TimeManager> reference = ProcessManager.GetSystemRef<TimeManager>();
            if (reference.IsNull()) return;
            float scale = (1f - amount) * 0.5f;
            if (scale < 0.9999f) reference.Get().OverrideUnityTimescale(scale, true);
            else reference.Get().ClearUnityTimescaleOverride();
        }
        private void OnDebugGameSpeedReleased(float amount) // 06001378
        {
            SystemRef<TimeManager> reference = ProcessManager.GetSystemRef<TimeManager>();
            if (!reference.IsNull()) reference.Get().ClearUnityTimescaleOverride();
        }
        public override void OnFixedUpdate(float deltaTime) { if (IsEnabled) m_immobilise.OnFixedUpdate(deltaTime); } // 06001379
        private void UpdateDisplayVelocity() // 0600137a
        {
            SystemRef<TimeManager> reference = ProcessManager.GetSystemRef<TimeManager>();
            if (reference.IsNull() || m_pauseTimeOverride != null) return;
            if (reference.Get().GetUnityTimescale() == 0f) return;
            m_displayVelocity = m_character.IsPaused ? m_character.GetPausedWorldVelocity() : m_character.WorldVelocity;
        }
        public override Vector3 GetDisplayVelocity() => m_displayVelocity; // 0600137b
        [Conditional("BUILD_DEVELOPMENT")]
        private void Update() // 0600137c
        {
            if (!IsEnabled) return;
            Vector3 position = m_character.Tracker.TrackerPosition;
            Quaternion rotation = m_character.Tracker.TrackerRotation;
            m_trackerDebug.transform.SetPositionAndRotation(position, rotation);
            UpdateDisplayVelocity();
        }
        [Conditional("BUILD_DEVELOPMENT")]
        private void OnDestroy() => DebugMenu.OnDebugMenuToggle -= OnDebugMenuToggle; // 0600137d
        public override void ToggleDebugInfo() // 0600137e
        {
            if (!ProcessManager.GetSystem<CharacterManager>().IsCurrentCharacter(m_character)) return;
            base.ToggleDebugInfo();
            m_trailRenderer.enabled = IsEnabled;
        }
        [Conditional("BUILD_DEVELOPMENT")]
        private void CreateLocationObject(SurfaceLocation location, string locationName, Vector3 scale, float activeTime) // 0600137f
        {
            string[] segments = GetType().ToString().Split('.');
            m_root = new GameObject(segments[segments.Length - 1]);
            SceneManager.MoveGameObjectToScene(m_root, m_character.gameObject.scene);
            GameObject locationObject = new GameObject(locationName);
            locationObject.transform.parent = m_root.transform;
            locationObject.transform.position = location.m_worldPosition;
            locationObject.transform.rotation = location.m_worldRotation;
            m_locationPrefab = locationObject;
            m_locationPrefab.AddComponent<CharacterDebug_Metadata>();
            m_locationPrefab.AddComponent<CharacterDebug_Object>();
            m_locationPrefab.SetActive(false);
        }
        [Conditional("BUILD_DEVELOPMENT")]
        private void CreateLocationAxis(string axisName, Vector3 direction, GameObject parent, float scale, Material material) // 06001380
        {
            GameObject axis = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            axis.name = axisName;
            Transform transform = axis.transform;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, direction);
            Vector3 position = direction * scale;
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = rotation * (position + Vector3.one * LocationAxisWidth);
            transform.SetParent(parent.transform, false);
            axis.GetComponent<Renderer>().material = material;
        }
        private GameObject CreateDirectionalObject(string name, float width, float length, float heightOffset) // 06001381
        {
            GameObject directional = new GameObject(name);
            MeshFilter filter = directional.AddComponent<MeshFilter>();
            filter.mesh = MeshGenerationUtilities.CreateMeshPyramid(width, length, heightOffset);
            MeshRenderer renderer = directional.AddComponent<MeshRenderer>();
            renderer.material = new Material(m_debugShaderUnlit);
            SceneManager.MoveGameObjectToScene(directional, m_character.gameObject.scene);
            return directional;
        }
        [Conditional("BUILD_DEVELOPMENT")]
        private void SetupTrackerDebugObject() // 06001382
        {
            if (m_trackerDebug != null) return;
            m_trackerDebug = CreateDirectionalObject("TrackerDebug", TrackerMeshWidth, TrackerMeshLength, TrackerMeshHeightOffset);
        }
        [Conditional("BUILD_DEVELOPMENT")]
        private void SetupStickyControlsObject() // 06001383
        {
            if (m_stickyControlsDebug != null) return;
            m_stickyControlsDebug = CreateDirectionalObject("StickyControlsDebug", StickyControlsMeshWidth, StickyControlsMeshLength, StickyControlsMeshHeightOffset);
        }
        [Conditional("BUILD_DEVELOPMENT")]
        private void SetTrackerActive(bool value) // 06001384
        {
            if (m_trackerDebug == null) return;
            if (m_trackerDebug.activeSelf != value) m_trackerDebug.SetActive(value);
        }
        [Conditional("BUILD_DEVELOPMENT")]
        private void SetStickyControlsActive(bool value) // 06001385
        {
            if (m_stickyControlsDebug == null) return;
            if (m_stickyControlsDebug.activeSelf != value) m_stickyControlsDebug.SetActive(value);
        }
        [Conditional("BUILD_DEVELOPMENT")]
        private void SetupTurnDebugObject() // 06001386
        {
            if (m_cameraTurnDebug != null) return;
            m_cameraTurnDebug = new GameObject("CameraTurnDebug");
            MeshFilter cameraFilter = m_cameraTurnDebug.AddComponent<MeshFilter>();
            cameraFilter.mesh = MeshGenerationUtilities.CreateMeshPyramid(TurnMeshWidth, TurnMeshLength, TurnMeshHeightOffset);
            MeshRenderer cameraRenderer = m_cameraTurnDebug.AddComponent<MeshRenderer>();
            cameraRenderer.material = new Material(m_debugShaderUnlit);
            cameraRenderer.material.color = Color.green;
            SceneManager.MoveGameObjectToScene(m_cameraTurnDebug, m_character.gameObject.scene);
            m_cameraTurnDebug.transform.SetParent(m_character.transform);
            m_intendedTurnDebug = new GameObject("IntendedTurnDebug");
            MeshFilter intendedFilter = m_intendedTurnDebug.AddComponent<MeshFilter>();
            intendedFilter.mesh = MeshGenerationUtilities.CreateMeshPyramid(TurnMeshWidth, TurnMeshLength, TurnMeshHeightOffset);
            MeshRenderer intendedRenderer = m_intendedTurnDebug.AddComponent<MeshRenderer>();
            intendedRenderer.material = new Material(m_debugShaderUnlit);
            intendedRenderer.material.color = Color.magenta;
            SceneManager.MoveGameObjectToScene(m_intendedTurnDebug, m_character.gameObject.scene);
            m_intendedTurnDebug.transform.SetParent(m_character.transform);
            m_actualTurnDebug = new GameObject("ActualTurnDebug");
            MeshFilter actualFilter = m_actualTurnDebug.AddComponent<MeshFilter>();
            actualFilter.mesh = MeshGenerationUtilities.CreateMeshPyramid(TurnMeshWidth, TurnMeshLength, TurnMeshHeightOffset);
            MeshRenderer actualRenderer = m_actualTurnDebug.AddComponent<MeshRenderer>();
            actualRenderer.material = new Material(m_debugShaderUnlit);
            actualRenderer.material.color = Color.blue;
            SceneManager.MoveGameObjectToScene(m_actualTurnDebug, m_character.gameObject.scene);
            m_actualTurnDebug.transform.SetParent(m_character.transform);
            m_velocityDebug = new GameObject("VelocityDebug");
            MeshFilter velocityFilter = m_velocityDebug.AddComponent<MeshFilter>();
            velocityFilter.mesh = MeshGenerationUtilities.CreateMeshPyramid(TurnMeshWidth, TurnMeshLength, TurnMeshHeightOffset);
            MeshRenderer velocityRenderer = m_velocityDebug.AddComponent<MeshRenderer>();
            velocityRenderer.material = new Material(m_debugShaderUnlit);
            velocityRenderer.material.color = new Color(1f, 235f / 255f, 4f / 255f, 1f);
            SceneManager.MoveGameObjectToScene(m_velocityDebug, m_character.gameObject.scene);
            m_velocityDebug.transform.SetParent(m_character.transform);
        }
        [Conditional("BUILD_DEVELOPMENT")]
        private void SetTurnDebugActive(bool value) // 06001387
        {
            if (m_cameraTurnDebug == null || m_cameraTurnDebug.activeSelf == value) return;
            m_cameraTurnDebug.SetActive(value);
            m_intendedTurnDebug.SetActive(value);
            m_actualTurnDebug.SetActive(value);
            m_velocityDebug.SetActive(value);
        }
        [Conditional("BUILD_DEVELOPMENT")]
        private void UpdateTurnIndicators() // 06001388
        {
            if (!IsEnabled) return;
            Vector3 position = m_character.WorldPosition + m_character.UpDirection * TurnVerticalOffset;
            Vector3 direction = m_character.CameraForwardOnCharacterPlane;
            float intendedAngle = CharacterMovementUtilities.GetIntendedTurnAngle(m_character, m_character.ControllerMovement);
            if (!(direction.sqrMagnitude > 0f)) direction = m_character.ForwardDirection;
            Quaternion rotation = Quaternion.LookRotation(direction, m_character.UpDirection);
            m_intendedTurnDebug.transform.SetPositionAndRotation(position, rotation);
            m_intendedTurnDebug.transform.Rotate(0f, intendedAngle, 0f, Space.World);
            m_cameraTurnDebug.transform.SetPositionAndRotation(position, rotation);
            m_actualTurnDebug.transform.SetPositionAndRotation(position, m_character.WorldRotation);
            if (m_character.WorldVelocity.sqrMagnitude > m_character.Constants.MovementInputSpeedMinSqr)
            {
                Transform transform = m_velocityDebug.transform;
                transform.SetPositionAndRotation(position, Quaternion.LookRotation(m_character.WorldVelocity, m_character.UpDirection));
            }
        }
        [Conditional("BUILD_DEVELOPMENT")]
        private void UpdateStickyControls() // 06001389
        {
            if (!IsEnabled) return;
            bool active = m_character.Storage.GetValue(ActorFSMKeys.TurnCameraActive, false, true);
            m_stickyControlsDebug.SetActive(active);
            if (active)
            {
                Quaternion rotation = m_character.Storage.GetValue(ActorFSMKeys.TurnCameraRotation, default(Quaternion), true);
                Transform transform = m_stickyControlsDebug.transform;
                transform.SetPositionAndRotation(m_character.WorldPosition, rotation);
            }
        }
        public override void BeginLocationInstantiation(bool forceUpdate = false) // 0600138a
        {
            if (!IsEnabled && !forceUpdate) return;
            m_locationParent = new GameObject(string.Format("{0:00}_{1}", m_locationCount, m_locationPrefab.name));
            m_locationParent.transform.SetParent(m_root.transform);
        }
        public override void EndLocationInstantiation(bool forceUpdate = false) // 0600138b
        {
            if (!IsEnabled && !forceUpdate) return;
            m_locationParent = null;
            m_locationCount++;
        }
        // 0600138c: unique entries do not take the ordinary naming/parenting path.
        public override void AddLocationInstantiation(Vector3 position, Quaternion rotation, string namePostfix,
            Vector3? scale = null, GameObject parent = null, float lifetime = 5f, CharacterDebug_Metadata.Metadata data = null, bool unique = false)
        {
            if (!IsEnabled) return;
            GameObject instance;
            if (unique)
            {
                if (!m_trackedLocationObjects.TryGetValue(namePostfix, out instance) || instance == null)
                {
                    instance = UnityEngine.Object.Instantiate(m_locationPrefab, position, rotation);
                    m_trackedLocationObjects[namePostfix] = instance;
                }
                else
                {
                    instance.transform.position = position;
                    instance.transform.rotation = rotation;
                }
            }
            else
            {
                instance = UnityEngine.Object.Instantiate(m_locationPrefab, position, rotation);
                if (parent != null)
                {
                    instance.name = namePostfix;
                    instance.transform.SetParent(parent.transform);
                }
                else
                {
                    instance.name = m_locationParent.name + "_" + namePostfix;
                    instance.transform.SetParent(m_locationParent.transform);
                }
            }
            instance.SetActive(true);
            instance.GetComponent<CharacterDebug_Metadata>().Data = data;
            CharacterDebug_Object debugObject = instance.GetComponent<CharacterDebug_Object>();
            debugObject.SetLifetime(lifetime);
            if (scale.HasValue) debugObject.SetScale(scale.Value);
        }
        // 0600138d: retained transform lookup; removed conditional drawing calls perform no instantiation.
        public override void AddLocationObject(GameObject gameObject, string name, Vector3? scale = null,
            GameObject parent = null, float lifetime = 5f, CharacterDebug_Metadata.Metadata data = null, bool unique = false)
        { _ = gameObject.transform; }
        // 0600138e: retain length division and LookRotation even for zero/nonfinite lines.
        public override void AddLocationLine(Vector3 start, Vector3 end, string namePostfix,
            GameObject parent = null, float lifetime = 5f, CharacterDebug_Metadata.Metadata data = null, bool unique = false)
        {
            if (!IsEnabled) return;
            Vector3 direction = end - start;
            float length = direction.magnitude;
            _ = Quaternion.LookRotation(direction / length);
        }
        public override void AddLocationCube(Vector3 centre, Vector3 size, string namePostfix, CharacterDebug_Metadata.Metadata data = null) { } // 0600138f, genuine RET.
        public override void RemoveLocation(GameObject locationObject) // 06001390
        {
            if (IsEnabled) locationObject.GetComponent<CharacterDebug_Object>().SetLifetime(0f);
        }
        public override void RemoveLocation(GameObject gameObject, GameObject parent) // 06001391
        {
            if (!IsEnabled || parent == null) return;
            foreach (CharacterDebug_Metadata metadata in parent.transform.GetComponentsInChildren<CharacterDebug_Metadata>())
                if (metadata.Data != null && metadata.Data.GameObject == gameObject) break;
        }
        public override void RemoveLocationParent(GameObject locationParent) // 06001392
        { if (locationParent != null) UnityEngine.Object.Destroy(locationParent); }
        [Conditional("BUILD_DEVELOPMENT")]
        public static void RenderCone(ConeVolume.ConeDescription cone, Actor actor = null) // 06001393
        { _ = cone.Rotation * Vector3.forward; }
        [Conditional("BUILD_DEVELOPMENT")]
        private static void RenderConeSegment(ConeVolume.ConeDescription cone, Vector3 projectedPosition, float projectedDistance, Actor actor = null) // 06001394
        {
            Gizmos.color = Color.blue;
            Vector2 tangents = cone.Tangents;
            int count = ConeVolume.ConeSegmentAngles.Length;
            tangents *= projectedDistance;
            for (int i = 0; i < count; i++)
            {
                Vector2 point = ConeVolume.ConeSegmentAngles[i] * tangents;
                _ = cone.Rotation * new Vector3(point.x, point.y, 0f);
            }
            _ = cone.Rotation * new Vector3(tangents.x, 0f, 0f);
        }
        [Conditional("BUILD_DEVELOPMENT")]
        public static void RenderLine(Vector3 start, Vector3 end, Actor actor = null) { _ = actor != null; } // 06001395
        [Conditional("BUILD_DEVELOPMENT")]
        public static void RenderSphere(Vector3 hitPosition, Vector3 planeForward, Vector3 planeUp, Color colour, float scaleMultiplier, Actor actor = null) // 06001396
        { if (actor != null) _ = Quaternion.LookRotation(planeForward, planeUp); }
        [Conditional("BUILD_DEVELOPMENT")]
        public static void RenderBounds(Bounds bounds, Actor actor = null) { _ = actor != null; } // 06001397
        public override void GetActorInfo(FiniteStateMachine fsm, StringBuilder stringInfoBuilder) // 06001398
        {
            stringInfoBuilder.AppendLine(string.Format("Controller = {0:F2} | Raw = {1:F2}", m_character.ControllerMovement, m_character.RawControllerMovement));
            stringInfoBuilder.AppendLine(string.Format("Tracking Type = {0}", m_character.ActiveTracking.Type));
            stringInfoBuilder.AppendLine(m_turningDebugInfo);
            stringInfoBuilder.AppendLine(m_throttleDebugInfo);
            stringInfoBuilder.AppendLine(m_movementDebugInfo);
            stringInfoBuilder.Append("Form = " + HardlightEnumExtensions.GetString(m_character.FormType));
            GetRailInfo(stringInfoBuilder);
            GetLedgeBrakeInfo(stringInfoBuilder);
            stringInfoBuilder.AppendLine();
        }
        // 06001399: original native import is acosf, confirmed by Mach indirect symbol.
        private void GetRailInfo(StringBuilder stringInfoBuilder)
        {
            bool showRailInfo = m_character.Storage.GetValue(ActorFSMKeys.RailActive, false, false);
            if (!showRailInfo)
            {
                float lastRailTime = m_character.Storage.GetValue(ActorFSMKeys.RailActiveLastTime, 0f, false);
                showRailInfo = m_character.GetTotalFixedTime() - lastRailTime < AbilityValueDisplayTime;
            }
            if (showRailInfo)
            {
                float angle = Mathf.Acos(m_character.Storage.GetValue(ActorFSMKeys.RailToCamera, 0f, false)) * 57.29578f;
                stringInfoBuilder.Append(string.Format(", Rail To Camera = {0:F2}", angle));
            }
        }
        // 0600139a: this greater-than return preserves the original NaN append path.
        private void GetLedgeBrakeInfo(StringBuilder stringInfoBuilder)
        {
            float groundLedgeTime = m_character.Storage.GetValue(ActorFSMKeys.GroundLedgeTime, 0f, false);
            float elapsed = m_character.GetTotalFixedTime() - groundLedgeTime;
            if (elapsed > AbilityValueDisplayTime) return;
            stringInfoBuilder.Append(string.Format(", Ledge Brake = {0:F2}", elapsed));
        }
        public override void SetThrottleDebugInfo(float minSpeed, float maxSpeed, float acceleration, float deceleration) => // 0600139b
            m_throttleDebugInfo = string.Format("Min = {0:F2} | Max = {1:F2} | Acc = {2:F2} | Dec = {3:F2}", minSpeed, maxSpeed, acceleration, deceleration);
        public override void SetMovementDebugInfo(float targetSpeed, float scaledSpeed, float accelerationMultiplier, float effectiveInputMagnitude) => // 0600139c
            m_movementDebugInfo = string.Format("TaS = {0:F2} | ScS = {1:F2} | AcM = {2:F2} | EfI = {3:F2}", targetSpeed, scaledSpeed, accelerationMultiplier, effectiveInputMagnitude);
        public override void SetTurningDebugInfo(float intendedTurnDelta, float byVelocity, float byAngle, float rateOfTurn) => // 0600139d
            m_turningDebugInfo = string.Format("Int = {0:F2} | Vel = {1:F2} | Ang = {2:F2} | RTD = {3:F2}", intendedTurnDelta, byVelocity, byAngle, rateOfTurn);
        public override void DrawRay(Vector3 position, Vector3 direction, Color colour) // 0600139e
        { if (IsEnabled) UnityEngine.Debug.DrawRay(position, direction, colour, 5f); }
        public override GameObject GetLocationParent() => m_locationParent; // 0600139f
        public CharacterDebug() { } // 060013a0: readonly fields initialize before the ActorDebug base constructor.
    }
}
