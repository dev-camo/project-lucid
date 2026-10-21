using Cinemachine;
using Hardlight;
using HardlightProject;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Shared setup for the camera proxy, camera trigger and target group assigner fixtures. Everything is a real
    // component on an owned inactive host, so no Awake runs unless a case calls it by name.
    internal static class CameraProxyAndTriggerTestSupport
    {
        // A real CameraYAxisProxyManager registered into its absent row. It has no layer definitions, so its target
        // stack accepts overrides without the player-character wiring that Awake would add. Its field initialisers
        // take the CinemachineCameraManager and CharacterManager rows, which the caller has registered or leased.
        internal static CameraYAxisProxyManager RegisterYAxisManager(Owned owned, CinemachineBrain brain)
        {
            var manager = EnemyAbilityTestSupport.AddTimeScaled<CameraYAxisProxyManager>(
                owned.Host("Lucid camera y axis proxy manager", false));
            Set(manager, "m_transformStackLayerDefinition", new CameraYAxisProxyManager.TransformStackLayerDefinition[0]);
            if (brain != null) Set(manager, "m_cinemachineBrain", brain);
            return owned.Register(manager);
        }

        // Properties the manager publishes from its own update; their setters are private.
        internal static void SetYAxisProperty(CameraYAxisProxyManager manager, string name, object value) =>
            typeof(CameraYAxisProxyManager).GetProperty(name).GetSetMethod(true).Invoke(manager, new[] { value });

        // A settings value as the serializer would fill it: the fields have no setters.
        internal static CameraProxyTargetSettings ProxySettings(int priority)
        {
            object boxed = new CameraProxyTargetSettings();
            Set(boxed, "m_priority", priority);
            return (CameraProxyTargetSettings)boxed;
        }

        // The manager's Initialise (not run here) gives the proxy setting override its latest-wins retrieval; without it
        // an override is stored but never read back.
        internal static void ConfigureProxySettingRetrieval(CinemachineCameraManager manager) =>
            Get<StackableData>(manager, "m_cameraSettings").SetRetrievalOperation<CameraProxyTargetSettings>(
                (int)CameraSettingType.ProxySettingOverride, StackableData.RetrievalOperation.Latest,
                default(CameraProxyTargetSettings));

        // CharacterTrigger requires a Collider, so a bare host would make AddComponent fail.
        internal static T AddCharacterTrigger<T>(Owned owned, string name) where T : CharacterTrigger
        {
            GameObject host = owned.Host(name, false);
            host.AddComponent<BoxCollider>();
            return host.AddComponent<T>();
        }
    }
}
