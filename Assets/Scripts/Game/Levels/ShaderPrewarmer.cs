using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Serialization;

namespace HardlightProject
{
    // Whole original owner candidate inferred from both shipping architectures.
    // Native review and compiler binding remain pending; no runtime acceptance.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ShaderPrewarmer : MonoBehaviour
    {
        [SerializeField, InspectorReadOnly] private ParticleSystem[] m_sceneParticleSystems;
        [InspectorReadOnly, SerializeField, FormerlySerializedAs("m_sceneHashedRendererGroups")]
        private HashedComponentGroup[] m_sceneHashedComponentGroups;
        private readonly SystemRef<ParticleEffectManager> m_particleEffectManagerRef = ProcessManager.GetSystemRef<ParticleEffectManager>();
        private List<ParticleEffectDefinition> m_effectDefinitions;
        private readonly List<HashedComponentGroup> m_levelHashedComponentGroups = new List<HashedComponentGroup>();
        private readonly List<AsyncOperationHandle> m_characterHandles = new List<AsyncOperationHandle>();
        private readonly List<GameObject> m_characterMeshes = new List<GameObject>();
        private const float PrerenderCameraHeight = 300f;

        //060024a7: play scene effects before testing the system reference. The
        //definition list is replaced only when that reference is valid.
        private void StartParticlePrewarm(GameObject prerenderCamera)
        {
            foreach (ParticleSystem particleSystem in m_sceneParticleSystems)
                particleSystem.Play();
            if (m_particleEffectManagerRef.IsNull()) return;
            m_effectDefinitions = new List<ParticleEffectDefinition>();
            ParticleEffectManager particleEffectManager = m_particleEffectManagerRef.Get();
            DataManager dataManager = ProcessManager.GetSystem<App>().DataManager;
            MissionManager missionManager = ProcessManager.GetSystem<MissionManager>();
            var characters = new List<CharacterId>();
            missionManager.GetCharactersForActiveMission(ref characters);
            foreach (CharacterId character in characters)
            {
                if (!dataManager.Characters.TryGetValue(character, out CharacterDefinition definition)) continue;
                foreach (KeyValuePair<ActorParticleTriggerType, ParticleEffectType> entry in definition.PFXDefinitionsDictionary)
                {
                    bool found = false;
                    foreach (ParticleEffectDefinition existing in m_effectDefinitions)
                    {
                        if (existing.PFXType != entry.Value) continue;
                        found = true;
                        break;
                    }
                    if (found) continue;
                    if (!dataManager.ParticleEffectDefinitions.TryGetValue(entry.Value, out ParticleEffectDefinition effectDefinition)) continue;
                    m_effectDefinitions.Add(effectDefinition);
                    particleEffectManager.RegisterInterest(effectDefinition, this);
                }
            }
            particleEffectManager.StartPrewarmParticleSystems(prerenderCamera.transform);
            FullscreenShaderManager fullscreenShaderManager = ProcessManager.GetSystem<FullscreenShaderManager>();
            if (fullscreenShaderManager != null)
                fullscreenShaderManager.StartEffectOverride(FullscreenShaderParametersType.BoostEnter, out _, float.MaxValue, false, true);
        }

        //060024a8: stop screen overrides first; scene effects precede the ref
        //check. Interest removal and list clearing happen after manager shutdown.
        private void StopParticlePrewarm()
        {
            FullscreenShaderManager fullscreenShaderManager = ProcessManager.GetSystem<FullscreenShaderManager>();
            if (fullscreenShaderManager != null) fullscreenShaderManager.StopAllEffectOverrides();
            foreach (ParticleSystem particleSystem in m_sceneParticleSystems)
                particleSystem.Stop();
            if (m_particleEffectManagerRef.IsNull()) return;
            ParticleEffectManager particleEffectManager = m_particleEffectManagerRef.Get();
            particleEffectManager.StopPrewarmParticleSystems();
            foreach (ParticleEffectDefinition definition in m_effectDefinitions)
                particleEffectManager.UnregisterInterest(definition, this);
            m_effectDefinitions.Clear();
        }

        //060024a9: ordinary managed list membership, followed by a separate Add.
        //No lifetime filtering or notification is added to this original method.
        public void RegisterHashedComponentGroup(HashedComponentGroup hashedComponentGroup)
        {
            if (m_levelHashedComponentGroups.Contains(hashedComponentGroup)) return;
            m_levelHashedComponentGroups.Add(hashedComponentGroup);
        }

        //060024aa: remove one matching entry and discard Remove's result.
        public void UnRegisterHashedComponentGroup(HashedComponentGroup hashedComponentGroup)
        {
            m_levelHashedComponentGroups.Remove(hashedComponentGroup);
        }

        //060024ab: the actual authored graph callback and user are carried by
        //the deferred iterator rather than invoked by this scheduling method.
        public void StartPrewarmHashedComponentGroups(Action<IGraphUser> onPrewarmComplete, IGraphUser user)
        {
            CoroutineUtils.RunCoroutine(PrewarmHashedComponentGroups(onPrewarmComplete, user));
        }

        //060024ac and original d__13: authored preparation spans eleven suspension
        //states, including a nested character-load iterator. Temporary outro and
        //camera lifetime, group toggles, and completion order follow the shipped
        //state table. No cancellation cleanup is added outside its enumerators.
        private IEnumerator PrewarmHashedComponentGroups(Action<IGraphUser> onPrewarmComplete, IGraphUser user)
        {
            var wait = new WaitForEndOfFrame();
            GameObject predenderCamera = CreatePrerenderCamera();
            yield return wait;
            FullscreenShaderManager fullscreenShaderManager = ProcessManager.GetSystem<FullscreenShaderManager>();
            if (fullscreenShaderManager != null)
                fullscreenShaderManager.StartEffectOverride(FullscreenShaderParametersType.BoostEnter, out _, float.MaxValue, false, true);
            var allRenderers = new List<HashedComponentGroup>(m_sceneHashedComponentGroups);
            allRenderers.AddRange(m_levelHashedComponentGroups);
            yield return wait;
            yield return LoadCharacters();
            yield return wait;
            StartParticlePrewarm(predenderCamera);
            yield return wait;
            GameObject outro = LoadOutroSequence();
            yield return wait;
            UnityEngine.Object.DestroyImmediate(outro);
            yield return wait;
            ReleaseCharacters();
            yield return wait;
            foreach (HashedComponentGroup renderGroup in allRenderers)
            {
                if (renderGroup == null) continue;
                MeshRenderer meshRenderer = renderGroup.GetComponentInChildren<MeshRenderer>();
                Vector3 position = meshRenderer != null ? meshRenderer.transform.position : Vector3.zero;
                predenderCamera.transform.position = new Vector3(position.x, position.y + PrerenderCameraHeight, position.z);
                renderGroup.SetPrerenderActive(true);
                yield return wait;
                renderGroup.SetPrerenderActive(false);
                yield return wait;
            }
            StopParticlePrewarm();
            if (fullscreenShaderManager != null) fullscreenShaderManager.StopAllEffectOverrides();
            UnityEngine.Object.Destroy(predenderCamera);
            m_levelHashedComponentGroups.Clear();
            yield return wait;
            onPrewarmComplete?.Invoke(user);
        }

        //060024ad: this original camera is placed at25, before the later300
        //render-group offset. The added Camera component's result is discarded.
        private GameObject CreatePrerenderCamera()
        {
            var cameraObject = new GameObject("Prerender Camera");
            cameraObject.transform.position = new Vector3(0f, 25f, 0f);
            cameraObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            cameraObject.AddComponent<Camera>();
            return cameraObject;
        }

        //060024ae and original d__15: enqueue every character load before waiting
        //on handles in list order. One frame token is reused by every wait.
        private IEnumerator LoadCharacters()
        {
            AddressableManager addressableManager = ProcessManager.GetSystem<AddressableManager>();
            var characters = new List<CharacterId>();
            ProcessManager.GetSystem<MissionManager>().GetCharactersForActiveMission(ref characters);
            var definitions = ProcessManager.GetSystem<DataManager>().Characters;
            foreach (CharacterId character in characters)
                m_characterHandles.Add(addressableManager.LoadAssetAsyncInstance(definitions[character].MeshPrefab, null, OnCharacterLoaded));
            var wait = new WaitForEndOfFrame();
            foreach (AsyncOperationHandle handle in m_characterHandles)
                while (!handle.IsDone) yield return wait;
        }

        //060024af: append the callback result verbatim, including null/duplicates.
        private void OnCharacterLoaded(GameObject character)
        {
            m_characterMeshes.Add(character);
        }

        //060024b0: destroy all meshes before obtaining the original asset manager.
        //Neither list is cleared until both enumerations have completed.
        private void ReleaseCharacters()
        {
            foreach (GameObject character in m_characterMeshes)
                UnityEngine.Object.Destroy(character);
            AddressableManager addressableManager = ProcessManager.GetSystem<AddressableManager>();
            foreach (AsyncOperationHandle handle in m_characterHandles)
                addressableManager.ReleaseHandle(handle);
            m_characterMeshes.Clear();
            m_characterHandles.Clear();
        }

        //060024b1: original immediate default-outro load, with no local handle
        //tracking or release added to this method.
        private GameObject LoadOutroSequence()
        {
            var sequence = ProcessManager.GetSystem<DataManager>()
                .OutroSequenceDefinitions[OutroSequenceIdentifier.Default].OutroSequence;
            GameObject prefab = ProcessManager.GetSystem<AddressableManager>()
                .LoadAssetImmediate<GameObject>(sequence).Result;
            return UnityEngine.Object.Instantiate(prefab);
        }

        //060024b2: the system ref and three lists initialize before the original
        //MonoBehaviour constructor. The effect-definition list stays null.
        public ShaderPrewarmer()
        {
        }
    }
}
