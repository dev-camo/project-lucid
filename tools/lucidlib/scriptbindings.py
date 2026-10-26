"""Restore approved script pointers in generated asset staging only."""

import hashlib
import json
import math
import struct
import sys
from pathlib import Path
import re

from . import inputvariants
from .bindings import run_layout_inventory
from .verification import artifact_fingerprint


# These are maintained implementations, not exported declarations. New types
# require native behavior recovery and a real Unity serialization proof before
# joining this list. Each supplied release still gets a strict layout comparison.
# Every rule's behaviour and layout evidence was recovered from this one declared
# input variant; another variant needs its own evidence, not a shared release string.
BINDING_VARIANT = "6c89-acb"
BINDING_INPUT = inputvariants.variant(BINDING_VARIANT, inputvariants.SCRIPT_BINDINGS)
RULES = ({
    "assembly": "Game.Runtime",
    "full_name": "HardlightProject.LevelStartPositionDefinition",
    "export_path": "Assets/Scripts/Game.Runtime/HardlightProject/LevelStartPositionDefinition.cs",
    "maintained_path": "Assets/Scripts/Definitions/LevelStartPositionDefinition.cs",
    "binary_sha256": BINDING_INPUT["binary_sha256"],
    "metadata_sha256": BINDING_INPUT["metadata_sha256"],
    "runtime_sources": {
        "Assets/Scripts/Definitions/LevelStartPositionDefinition.cs": "13283ab5c73e5d97032f1be3b2be1478708fa187999f5943b14c9295dbf7a92f",
        "Packages/com.hardlight.hlunitycore/Runtime/ScriptableObjectWithGuid.cs": "befb87c3cc9068308cfa3e8965ceb82602974ffcb6380a894cd68d03e364629a",
        "Packages/com.hardlight.hlunitycore/Runtime/InspectorReadOnlyAttribute.cs": "d6e24d4d94dfd222ae920e83600d518ed8fd17286532771ae9570fdefe52feed",
    },
}, {
    "assembly": "Game.Runtime",
    "full_name": "HardlightProject.LevelDefinition",
    "export_path": "Assets/Scripts/Game.Runtime/HardlightProject/LevelDefinition.cs",
    "maintained_path": "Assets/Scripts/Definitions/LevelDefinition.cs",
    "binary_sha256": BINDING_INPUT["binary_sha256"],
    "metadata_sha256": BINDING_INPUT["metadata_sha256"],
    "runtime_sources": {
        "Assets/Scripts/Definitions/LevelDefinition.cs": "e1789c615fe0c7d6156d6ea84a355afb270706e274647fda8d9737c4668d3774",
        "Assets/Scripts/Definitions/ILevelDefinition.cs": "a3fb7c57bad19ec9224b97ba969c03e72cd72b84adfc1e92e6b9b9ec1ac8aec9",
        "Packages/com.hardlight.hlunitycore/Runtime/ScriptableObjectWithGuid.cs": "befb87c3cc9068308cfa3e8965ceb82602974ffcb6380a894cd68d03e364629a",
        "Packages/com.hardlight.hlunitycore/Runtime/InspectorReadOnlyAttribute.cs": "d6e24d4d94dfd222ae920e83600d518ed8fd17286532771ae9570fdefe52feed",
    },
},)

# These three upstream components retain the shipped scalar layout. Pin the
# actual builtin package sources as well as its version: different Editor
# releases can supply different code under the same builtin version.
UGUI_RUNTIME_SOURCES = {
    "Packages/com.unity.ugui/Runtime/UnityEngine.UI.asmdef": "5e1a5809db8304714763c1eab0c2086b4816b806902da5aac2aa46b18addffde",
    "Packages/com.unity.ugui/Runtime/EventSystem/UIBehaviour.cs": "2a145c6aa56cfd949bad90853cce3bb5c20272c3cf1583e72d48fde786a6c1fb",
    "Packages/com.unity.ugui/Runtime/UI/Core/SetPropertyUtility.cs": "763d54578999cf89f9d7102f181caeb973cfcb82f81cd812d3f0b7f0780b68e4",
    "Packages/com.unity.ugui/Runtime/UI/Core/Layout/LayoutElement.cs": "c6e88f6d086a6dfc4111a03024ea1279d6d2c6b755e0a5df50f64dfa06edd732",
    "Packages/com.unity.ugui/Runtime/UI/Core/Layout/ContentSizeFitter.cs": "a6b30a3b6e697524ce16f07e1807e870faf8d0c3e8c43004474eb5b8bb3c887b",
    "Packages/com.unity.ugui/Runtime/UI/Core/Layout/AspectRatioFitter.cs": "c433f85be46913c31817c56abbbfbd4869337bc86929c6ad6f45b4861f16c555",
    "Packages/com.unity.ugui/Runtime/UI/Core/Layout/LayoutUtility.cs": "1a5d931224f626980f509af94acdc17836139e47333ca271e7df18430c37710d",
    "Packages/com.unity.ugui/Runtime/UI/Core/Layout/LayoutRebuilder.cs": "a716457d2bd3539145a02a9ef2184582db066bf437648cbd2a6d42b7c60b42fe"
}

PACKAGE_RULES = (
    {
        "assembly": "UnityEngine.UI",
        "full_name": "UnityEngine.UI.LayoutElement",
        "export_path": "Assets/Scripts/UnityEngine.UI/UnityEngine/UI/LayoutElement.cs",
        "maintained_path": "Packages/com.unity.ugui/Runtime/UI/Core/Layout/LayoutElement.cs",
        "binary_sha256": BINDING_INPUT["binary_sha256"],
        "metadata_sha256": BINDING_INPUT["metadata_sha256"],
        "package": "com.unity.ugui",
        "runtime_sources": UGUI_RUNTIME_SOURCES
    },
    {
        "assembly": "UnityEngine.UI",
        "full_name": "UnityEngine.UI.ContentSizeFitter",
        "export_path": "Assets/Scripts/UnityEngine.UI/UnityEngine/UI/ContentSizeFitter.cs",
        "maintained_path": "Packages/com.unity.ugui/Runtime/UI/Core/Layout/ContentSizeFitter.cs",
        "binary_sha256": BINDING_INPUT["binary_sha256"],
        "metadata_sha256": BINDING_INPUT["metadata_sha256"],
        "package": "com.unity.ugui",
        "runtime_sources": UGUI_RUNTIME_SOURCES
    },
    {
        "assembly": "UnityEngine.UI",
        "full_name": "UnityEngine.UI.AspectRatioFitter",
        "export_path": "Assets/Scripts/UnityEngine.UI/UnityEngine/UI/AspectRatioFitter.cs",
        "maintained_path": "Packages/com.unity.ugui/Runtime/UI/Core/Layout/AspectRatioFitter.cs",
        "binary_sha256": BINDING_INPUT["binary_sha256"],
        "metadata_sha256": BINDING_INPUT["metadata_sha256"],
        "package": "com.unity.ugui",
        "runtime_sources": UGUI_RUNTIME_SOURCES
    },
)
RULES += PACKAGE_RULES

# Separate authored Image domain; the three scalar package rules stay unchanged.
# Original Image serialization 06000162@27D1C20 and inherited destruction
# 06000101@27CC650 retain callbacks even for an inactive owned prefab.
IMAGE_RUNTIME_SOURCES = dict(UGUI_RUNTIME_SOURCES, **{
    'Packages/com.unity.ugui/Runtime/UI/Core/Image.cs': '351dd72516d240f5722c55f72f8e95a0a92cf71e83358bbc922eee47bdb78502',
    'Packages/com.unity.ugui/Runtime/UI/Core/MaskableGraphic.cs': '96ad95f99e13503587432204eaaaf3110f6b73795715798162c60379ee46408b',
    'Packages/com.unity.ugui/Runtime/UI/Core/Graphic.cs': 'c23b303effecdb6693f791cbbe703f0c368fd92b1443934ae90d4d97c21dd9b0',
    'Packages/com.unity.ugui/Runtime/UI/Animation/CoroutineTween.cs': 'efce6000f556688a1bbc76ed448bf3a884c6cd25070f2b8abf5948fb36ffb504',
    'Packages/com.unity.ugui/Runtime/UI/Core/GraphicRegistry.cs': 'd708b194821a8dd95ee2a0eb77b246229b15da0e82bafe76ce06f4bbebb20b85',
    'Packages/com.unity.ugui/Runtime/UI/Core/CanvasUpdateRegistry.cs': '6d74c8cfa3500ffc2f35e1dd6ebc991178e2227194e8994dffe9db74575171cb',
    'Packages/com.unity.ugui/Runtime/UI/Core/GraphicRebuildTracker.cs': 'acd4b05911ce9763c5ef27eda7e41e9850d877fb875c653e15c816b2871cc159',
    'Packages/com.unity.ugui/Runtime/UI/Core/SpecializedCollections/IndexedSet.cs': '67e82737a015c50084766be7bffd284f6950ecfd91e395e6c67d8f64a19aea8b',
    'Packages/com.unity.ugui/Runtime/UI/Core/Utility/VertexHelper.cs': '1623d631b78018c8171c9b13cd5372fdbc0ca53aac4a36a65b10027a0df37dd1',
    'Packages/com.unity.ugui/Editor/UI/PrefabLayoutRebuilder.cs': '7296d7c7420360e453ea84e5a5e410d03c984cf6496093e86b17749e60783356',
    'Packages/com.unity.ugui/Editor/UnityEditor.UI.asmdef': '5e77894ab373d3c4a9ac030bf88aea2e51bce612bd529dcceebe5301b93fe575',
})
IMAGE_PACKAGE_RULES = ({
    "assembly": "UnityEngine.UI",
    "full_name": "UnityEngine.UI.Image",
    "export_path": "Assets/Scripts/UnityEngine.UI/UnityEngine/UI/Image.cs",
    "maintained_path": "Packages/com.unity.ugui/Runtime/UI/Core/Image.cs",
    "binary_sha256": BINDING_INPUT["binary_sha256"],
    "metadata_sha256": BINDING_INPUT["metadata_sha256"],
    "package": "com.unity.ugui",
    "runtime_sources": IMAGE_RUNTIME_SOURCES,
},)
RULES += IMAGE_PACKAGE_RULES
UGUI_PACKAGE_SHA256 = "6830b8501f301344de9feab76c65d1d9339b496c55cb16f9827658e78234ee78"
UGUI_LOCK = {"version": "1.0.0", "depth": 0, "source": "builtin",
             "dependencies": {"com.unity.modules.ui": "1.0.0", "com.unity.modules.imgui": "1.0.0"}}

# Configuration assets have qualified ordered arrays and authored override names.
# Keep their complete source closure pinned; the resolver still requires the
# exact supplied release and a fresh compatible loaded MonoScript layout.
CONFIGURATION_RUNTIME_SOURCES = {
    "Assets/Scripts/Definitions/GameplayLevelUIEntry.cs": "a5e2245b1950df317b94aef300d63b818082ddd001c0d409f8b9b1a8b2e1ff51",
    "Assets/Scripts/Definitions/LevelDefinition.cs": "e1789c615fe0c7d6156d6ea84a355afb270706e274647fda8d9737c4668d3774",
    "Assets/Scripts/Game/Configuration/GameLevels.cs": "55a168c5e6a679435ed2d7235b9fc239a7603e4e07f251cdd2c8ba38594f4386",
    "Assets/Scripts/Game/Configuration/GameplayLevelCategory.cs": "f57167732a9d48844f1654cea80e3f93707ae7cc71161ac8930cfdbc283e51e2",
    "Assets/Scripts/Game/Configuration/GameplayLevelDefinition.cs": "1de517a38abdf1b8077111a18aa7fdc02c15d895aa08ffd666b0c72c32f80f15",
    "Assets/Scripts/RecoveredProof/Game.Runtime.asmdef": "b234684d1289ae1bc0a8bfc843f941b8bcf5f578f614a6e01c15d1348e9271a0",
    "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/Dependencies/Configuration/ActiveRuntimeConfiguration.cs": "45529ea0e694140c3ae5df3fc1a883e02ad0cf0706ff19fcf65d2539bcf086c5",
    "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/Dependencies/Configuration/IActiveConfiguration.cs": "ba5a828b8a3dc13f21060e92ebee6f452e40be3e82706e0e77957efeb9f6f534",
    "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/Dependencies/Configuration/ISystemConfigurationAssetCollection.cs": "e5dcacd89071d947bf65de29b729d768461f75fd5b6a6dd83b7c2d99ebec281e",
    "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/Dependencies/Configuration/RuntimeConfiguration.cs": "88474b08663263a5f872627e80c14a70e2b1e0975e5c412a128f049908b915e6",
    "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/Dependencies/Configuration/SingleScriptableObject.cs": "a05fe680aef655cbb22c836db8ded4d5314217f4d514f81f2aa5a77915635dd5",
    "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/Dependencies/Configuration/SystemConfigurationAsset.cs": "3f5bf61bfcf1952ce2aa3cc44ad258b839b1fdb0ed3d2ad81983c907d05f4904",
    "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/Dependencies/ListExtensions.cs": "ced7f28b5b7ad774fb81a8e36ab9c32ccf9c54f781c5292e7a000de8832ccf63",
    "Packages/com.hardlight.hlunitycore/Runtime/HLUnityCore.Runtime.asmdef": "b9f3210d5cca9f7b7cc5bc458defe0e14e8df1486a17b29e517332c476347973",
    "Packages/com.hardlight.hlunitycore/Runtime/ScriptableObjectWithGuid.cs": "befb87c3cc9068308cfa3e8965ceb82602974ffcb6380a894cd68d03e364629a"
}

CONFIGURATION_RULES = (
    {
        "assembly": "HLUnityCore.Runtime",
        "full_name": "Hardlight.ActiveRuntimeConfiguration",
        "export_path": "Assets/Scripts/HLUnityCore.Runtime/Hardlight/ActiveRuntimeConfiguration.cs",
        "maintained_path": "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/Dependencies/Configuration/ActiveRuntimeConfiguration.cs",
        "binary_sha256": BINDING_INPUT["binary_sha256"],
        "metadata_sha256": BINDING_INPUT["metadata_sha256"],
        "runtime_sources": CONFIGURATION_RUNTIME_SOURCES,
    },
    {
        "assembly": "HLUnityCore.Runtime",
        "full_name": "Hardlight.RuntimeConfiguration",
        "export_path": "Assets/Scripts/HLUnityCore.Runtime/Hardlight/RuntimeConfiguration.cs",
        "maintained_path": "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/Dependencies/Configuration/RuntimeConfiguration.cs",
        "binary_sha256": BINDING_INPUT["binary_sha256"],
        "metadata_sha256": BINDING_INPUT["metadata_sha256"],
        "runtime_sources": CONFIGURATION_RUNTIME_SOURCES,
    },
    {
        "assembly": "Game.Runtime",
        "full_name": "HardlightProject.GameLevels",
        "export_path": "Assets/Scripts/Game.Runtime/HardlightProject/GameLevels.cs",
        "maintained_path": "Assets/Scripts/Game/Configuration/GameLevels.cs",
        "binary_sha256": BINDING_INPUT["binary_sha256"],
        "metadata_sha256": BINDING_INPUT["metadata_sha256"],
        "runtime_sources": CONFIGURATION_RUNTIME_SOURCES,
    },
)
RULES += CONFIGURATION_RULES

# Core, localisation and fieldless hashed settings retain the original authored
# base/override graph. These rules require separate source/ABI/owned-serialization
# qualification before admission; the resolver keeps its release and layout gates.
MENU_CONFIGURATION_RUNTIME_SOURCES = {
    'Assets/Scripts/Game/Configuration/CoreGameConfiguration.cs': '200f3e5b474a7993aec3b7223d410482b403e8d9a2b236f6f8fa57462bce541f',
    'Assets/Scripts/RecoveredProof/HashedGroupConfiguration.cs': 'f3c8fa6b6167bd54a263724c788dd04e71ebe80e4f8d63c0e167453eb13514ed',
    'Packages/com.hardlight.hllocalisation/Runtime/HLLocalisationConfigurationAsset.cs': '6855d8696a47497d5a84d82539bc05583dd0f961e78488b175ab4452d72c54b7',
    'Assets/Scripts/HLAutoGenerated/ReplayMode.cs': '189ff04bb49cf49cb13c031f8e2c276c21a727052e26bc680f003f014c89c9d0',
    'Assets/Scripts/Game/Missions/Challenges/ChallengeManager.cs': '3f378cf97b6c0f4f1828d69e2ae07c8036465a7fb5c2a79284b98d350a41b770',
    'Assets/Scripts/RecoveredProof/RuntimeIssueProfile.cs': '1a3eaee02e93d41831d4a11ab84df9e59afe1ccec81998eb33c71e8bcb7047b5',
    'Assets/Scripts/RecoveredProof/RuntimeIssueAttribute.cs': '7f8a72256d6da432775c6a592599ec1019a4ab13b8a860d5d8c83d267c0a045d',
    'Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/Dependencies/Configuration/SystemConfigurationAsset.cs': '3f5bf61bfcf1952ce2aa3cc44ad258b839b1fdb0ed3d2ad81983c907d05f4904',
    'Packages/com.hardlight.hlunitycore/Runtime/ScriptableObjectWithGuid.cs': 'befb87c3cc9068308cfa3e8965ceb82602974ffcb6380a894cd68d03e364629a',
    'Packages/com.hardlight.hlunitycore/Runtime/InspectorReadOnlyAttribute.cs': 'd6e24d4d94dfd222ae920e83600d518ed8fd17286532771ae9570fdefe52feed',
    'Packages/com.hardlight.hlunitycore/Runtime/Il2CppSetOptionAttribute.cs': '138cbbdeccb4c6c6cdf5d08e248e06dc8419cff250de803582194df39893e945',
    'Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/Dependencies/HLOutput.cs': 'f0a390a051cdc3d604db7f7e484be19bf0e42badbb57591839f242a813f28f3a',
    'Assets/Scripts/RecoveredProof/Game.Runtime.asmdef': 'b234684d1289ae1bc0a8bfc843f941b8bcf5f578f614a6e01c15d1348e9271a0',
    'Assets/Scripts/HLAutoGenerated/HLAutoGenerated.asmdef': '1e054c56612e318e251e6417a47fe46c4564234ed6336cc0f3fb83cd988b6a82',
    'Packages/com.hardlight.hllocalisation/Runtime/HLLocalisation.Runtime.asmdef': '90eb3e09b235146c824f4ae6dcea9d8c379e8e07ee4160c4037accbdc7b1315b',
    'Packages/com.hardlight.hlunitycore/Runtime/HLUnityCore.Runtime.asmdef': 'b9f3210d5cca9f7b7cc5bc458defe0e14e8df1486a17b29e517332c476347973',
}

MENU_CONFIGURATION_RULES = (
    {
        'assembly': 'Game.Runtime',
        'full_name': 'HardlightProject.CoreGameConfiguration',
        'export_path': 'Assets/Scripts/Game.Runtime/HardlightProject/CoreGameConfiguration.cs',
        'maintained_path': 'Assets/Scripts/Game/Configuration/CoreGameConfiguration.cs',
        'binary_sha256': BINDING_INPUT["binary_sha256"],
        'metadata_sha256': BINDING_INPUT["metadata_sha256"],
        "runtime_sources": MENU_CONFIGURATION_RUNTIME_SOURCES,
    },
    {
        'assembly': 'HLLocalisation.Runtime',
        'full_name': 'Hardlight.HLLocalisationConfigurationAsset',
        'export_path': 'Assets/Scripts/HLLocalisation.Runtime/Hardlight/HLLocalisationConfigurationAsset.cs',
        'maintained_path': 'Packages/com.hardlight.hllocalisation/Runtime/HLLocalisationConfigurationAsset.cs',
        'binary_sha256': BINDING_INPUT["binary_sha256"],
        'metadata_sha256': BINDING_INPUT["metadata_sha256"],
        "runtime_sources": MENU_CONFIGURATION_RUNTIME_SOURCES,
    },
    {
        'assembly': 'Game.Runtime',
        'full_name': 'HardlightProject.HashedGroupConfiguration',
        'export_path': 'Assets/Scripts/Game.Runtime/HardlightProject/HashedGroupConfiguration.cs',
        'maintained_path': 'Assets/Scripts/RecoveredProof/HashedGroupConfiguration.cs',
        'binary_sha256': BINDING_INPUT["binary_sha256"],
        'metadata_sha256': BINDING_INPUT["metadata_sha256"],
        "runtime_sources": MENU_CONFIGURATION_RUNTIME_SOURCES,
    },
)
RULES += MENU_CONFIGURATION_RULES

# The definitions root stores ordered asset pointers. Referenced group payloads
# remain separate; this rule does not admit their generic serialized layouts.
DATA_DEFINITIONS_RUNTIME_SOURCES = {
    "Assets/Scripts/Definitions/DataDefinitions.cs": "a755b84d81a1db2a2fcf01a6cfef2581903bbc504ccbe7ca9a186e5d5871ec69",
    "Assets/Scripts/Definitions/DefinitionDataType.cs": "396b278ba573f3aace1a56b7db03f6807095108ebe2a1c198cc5da2247f0e768",
    "Packages/com.hardlight.hlunitycore/Runtime/CollectionExtensions.cs": "c12ebc871205508af466aff3730fdb0b6673294dfc3eb818807c1e0e4013a234",
    "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/Dependencies/HLOutput.cs": "f0a390a051cdc3d604db7f7e484be19bf0e42badbb57591839f242a813f28f3a",
    "Packages/com.hardlight.hlunitycore/Runtime/Il2CppSetOptionAttribute.cs": "138cbbdeccb4c6c6cdf5d08e248e06dc8419cff250de803582194df39893e945",
    "Assets/Scripts/RecoveredProof/Game.Runtime.asmdef": "b234684d1289ae1bc0a8bfc843f941b8bcf5f578f614a6e01c15d1348e9271a0",
    "Packages/com.hardlight.hlunitycore/Runtime/HLUnityCore.Runtime.asmdef": "b9f3210d5cca9f7b7cc5bc458defe0e14e8df1486a17b29e517332c476347973",
}
DATA_DEFINITIONS_RULES = ({
    "assembly": "Game.Runtime",
    "full_name": "HardlightProject.DataDefinitions",
    "export_path": "Assets/Scripts/Game.Runtime/HardlightProject/DataDefinitions.cs",
    "maintained_path": "Assets/Scripts/Definitions/DataDefinitions.cs",
    "binary_sha256": BINDING_INPUT["binary_sha256"],
    "metadata_sha256": BINDING_INPUT["metadata_sha256"],
    "runtime_sources": DATA_DEFINITIONS_RUNTIME_SOURCES,
},)
RULES += DATA_DEFINITIONS_RULES

# s_boot's Bootstrapper state machine components (FiniteStateMachineBehaviour is
# also rebound in the uicontainer_player_progression_stateful prefab) and the
# FSM definition assets they load. Pin each declaration with its serialized dependencies.
FSM_RUNTIME_SOURCES = {
    "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/FiniteStateMachineBehaviour.cs": "1236d601117642b3d2560cd10a717a55440908230f7f37b5696bca602849eaf8",
    "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/FSMUserBehaviour.cs": "3f8e6ed9a3fc04b24b2342d44dcac8f912b32af73b9f527978ed154d72c934f1",
    "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/FiniteStateMachineScriptableObject.cs": "c511ac0007064d0317284efb0c8df9290fe3465a2481389057260430200857d7",
    "Packages/com.hardlight.hlunitycore/Runtime/Il2CppSetOptionAttribute.cs": "138cbbdeccb4c6c6cdf5d08e248e06dc8419cff250de803582194df39893e945",
    "Packages/com.hardlight.hlunitycore/Runtime/HLUnityCore.Runtime.asmdef": "b9f3210d5cca9f7b7cc5bc458defe0e14e8df1486a17b29e517332c476347973",
}
FSM_RULES = tuple({
    "assembly": "HLUnityCore.Runtime",
    "full_name": "Hardlight." + name,
    "export_path": "Assets/Scripts/HLUnityCore.Runtime/Hardlight/" + name + ".cs",
    "maintained_path": "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/" + name + ".cs",
    "binary_sha256": BINDING_INPUT["binary_sha256"],
    "metadata_sha256": BINDING_INPUT["metadata_sha256"],
    "runtime_sources": FSM_RUNTIME_SOURCES,
} for name in ("FiniteStateMachineBehaviour", "FSMUserBehaviour", "FiniteStateMachineScriptableObject"))
RULES += FSM_RULES

# s_boot's other two components. GuiInitializer's only base field is static
# (MonoSingleton<T>.Instance); VisualAssetManager points at a DevicePerformanceGroup,
# whose ancestry passes through DefinitionDataType<TKey, TValue>.
BOOT_RUNTIME_SOURCES = {
    "Packages/com.hardlight.hlunityui/Runtime/GuiInitializer.cs": "da32053131571edc5f1695519604fd1baca467c609c6f776f1a73c11adf8d8e1",
    "Packages/com.hardlight.hlunityui/Runtime/HLUnityUI.Runtime.asmdef": "7396ad9420187103fa37eb2b6ef5a699dab45b094621b29dfe8c9084c49f16bd",
    "Packages/com.hardlight.hlunitycore/Runtime/MonoSingleton.cs": "6ac7379f530a9333a558d4e1bb0b678e23b66a868a00c84ec587a209dfac0ceb",
    # Pinned only for MenuPageIdentifier, DialogPageIdentifier and OverlayPageIdentifier
    # (GuiInitializer's page fields), which this shared file declares beside six other enums.
    "Assets/Scripts/HLAutoGenerated/CoreEnumPrerequisites.cs": "a8bdf745b924edd467ccaf58364e17867f830960f3fcef2ff382c651eb66879d",
    "Assets/Scripts/HLAutoGenerated/HLAutoGenerated.asmdef": "1e054c56612e318e251e6417a47fe46c4564234ed6336cc0f3fb83cd988b6a82",
    "Assets/Scripts/Game/Materials/VisualAssetManager.cs": "036ba1290350d36e6c10a896e5d145c31b5a77849fc3f7ad4276697be2b51cd1",
    "Packages/com.hardlight.hlunitycore/Runtime/Performance/PerformanceAttribute.cs": "287215f479cef4ad6eabd19cfd4eeabbae03791a657c3d7f3e6178a915a08c7c",
    "Packages/com.hardlight.hlunitycore/Runtime/Performance/PerformanceProfile.cs": "6910e45f664ef4e67896041acde3dfe0c6cf07f93b04a4e3dacf0d74588dc056",
    "Packages/com.hardlight.hlunitycore/Runtime/Performance/ScalableFeature.cs": "d0635ab9900049fbbc0379c5cff8d4c9b456583e02cec80138de4eb7529ce34b",
    "Assets/Scripts/Definitions/DefinitionDataType.cs": "396b278ba573f3aace1a56b7db03f6807095108ebe2a1c198cc5da2247f0e768",
    "Assets/Scripts/RecoveredProof/DevicePerformance.cs": "127273ce6c858efd2f42c627702b0e923cbbd1dce51a5472ad9c7cd45601f761",
    "Assets/Scripts/RecoveredProof/DevicePerformanceGroup.cs": "9528d6fd449b40ba244c600e178d0ce557c16b4b89cf829696ef134373600fea",
    "Packages/com.hardlight.hlunitycore/Runtime/ScriptableObjectWithGuid.cs": "befb87c3cc9068308cfa3e8965ceb82602974ffcb6380a894cd68d03e364629a",
    "Packages/com.hardlight.hlunitycore/Runtime/InspectorReadOnlyAttribute.cs": "d6e24d4d94dfd222ae920e83600d518ed8fd17286532771ae9570fdefe52feed",
    "Packages/com.hardlight.hlunitycore/Runtime/Il2CppSetOptionAttribute.cs": "138cbbdeccb4c6c6cdf5d08e248e06dc8419cff250de803582194df39893e945",
    "Assets/Scripts/RecoveredProof/Game.Runtime.asmdef": "b234684d1289ae1bc0a8bfc843f941b8bcf5f578f614a6e01c15d1348e9271a0",
    "Packages/com.hardlight.hlunitycore/Runtime/HLUnityCore.Runtime.asmdef": "b9f3210d5cca9f7b7cc5bc458defe0e14e8df1486a17b29e517332c476347973",
}
BOOT_RULES = ({
    "assembly": "HLUnityUI.Runtime",
    "full_name": "Hardlight.GuiInitializer",
    "export_path": "Assets/Scripts/HLUnityUI.Runtime/Hardlight/GuiInitializer.cs",
    "maintained_path": "Packages/com.hardlight.hlunityui/Runtime/GuiInitializer.cs",
    "binary_sha256": BINDING_INPUT["binary_sha256"],
    "metadata_sha256": BINDING_INPUT["metadata_sha256"],
    "runtime_sources": BOOT_RUNTIME_SOURCES,
}, {
    "assembly": "Game.Runtime",
    "full_name": "HardlightProject.VisualAssetManager",
    "export_path": "Assets/Scripts/Game.Runtime/HardlightProject/VisualAssetManager.cs",
    "maintained_path": "Assets/Scripts/Game/Materials/VisualAssetManager.cs",
    "binary_sha256": BINDING_INPUT["binary_sha256"],
    "metadata_sha256": BINDING_INPUT["metadata_sha256"],
    "runtime_sources": BOOT_RUNTIME_SOURCES,
})
RULES += BOOT_RULES

# Runtime configuration assets listed by ProdRuntimeConfiguration, and the
# TimeCategoryObject assets that TimeCategoryConfig points at. Pin each owner
# with its serialized dependencies, including pointed-to declarations.
RUNTIME_CONFIGURATION_SOURCES = {
    "Packages/com.hardlight.hlunitycore/Runtime/Scheduling/TimeCategoryConfiguration.cs": "6dc2660822f12494f335cb650964e24b21e6a9584cc3cdf7c30df6f4244a6814",
    "Packages/com.hardlight.hlunitycore/Runtime/Scheduling/TimeCategoryObject.cs": "f8bb6c44f6ed95b4f4131438e5baff39b7270bd2b6f2173a6a697ff7c9dcc9af",
    "Packages/com.hardlight.hlunitycore/Runtime/Configuration/HLUnityCoreConfigurationAsset.cs": "5db0137ff78f4c8e4875a7fd55a19d61165332806557dfa265e987bd79353cf4",
    "Packages/com.hardlight.hlunitycore/Runtime/Configuration/AndroidConfigurationAsset.cs": "b90e10b7ea8bf3317e39f15f581950aeb866eb725cff1d814032d6a56a95aa9b",
    "Packages/com.hardlight.hlunitycore/Runtime/Configuration/iOSConfigurationAsset.cs": "d2b58f050656b0609b83aa885fc92fe8cddfb147221375db5718387987b63298",
    "Packages/com.hardlight.hlunitycore/Runtime/Configuration/MacOSConfigurationAsset.cs": "331601c0d0f78f7cc749949cd20fc2d6004b953cc05f842da60928da2e9e21a5",
    "Packages/com.hardlight.hlmodernui/Runtime/UINavigationConfiguration.cs": "4a77abcd5f4c51237b9e0578c18cd99d8eaf12766d140e93d9b307d7efdbf8be",
    "Packages/com.hardlight.hlmodernui/Runtime/UIHighlightDefinition.cs": "fd92426131c8550eedeb46d9ef8cb87ea6ac77094ce6421316a1cc3210e56768",
    "Packages/com.hardlight.hlmodernui/Runtime/UIHighlight.cs": "cc4b08fd88f77c649d96d5b440173d63b5f5eba1e9f02faebca1eced1315bf77",
    "Packages/com.hardlight.hlmodernui/Runtime/HLModernUI.Runtime.asmdef": "433d7a8f6bf62758056fc8bb9f2734d09351e43710284c41754ed1c274966e60",
    "Packages/com.hardlight.hlunitycore/Runtime/HashEnumAttribute.cs": "bdb39c952bfaae93fd3fa60eda5f6669238b6914e4446e28a9bbd13bf70ef4aa",
    # Pinned only for GameInput (m_selection*Input), which this shared file declares.
    "Assets/Scripts/HLAutoGenerated/CoreEnumPrerequisites.cs": "a8bdf745b924edd467ccaf58364e17867f830960f3fcef2ff382c651eb66879d",
    "Assets/Scripts/HLAutoGenerated/HLAutoGenerated.asmdef": "1e054c56612e318e251e6417a47fe46c4564234ed6336cc0f3fb83cd988b6a82",
    "Assets/Scripts/Game/Analytics/Analytics_SystemConfig.cs": "30b131942bc87d8983fc21a1a45487f19283353074acfe8caa3611650a3bafdb",
    "Assets/Scripts/RecoveredProof/Game.Runtime.asmdef": "b234684d1289ae1bc0a8bfc843f941b8bcf5f578f614a6e01c15d1348e9271a0",
    "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/Dependencies/Configuration/SystemConfigurationAsset.cs": "3f5bf61bfcf1952ce2aa3cc44ad258b839b1fdb0ed3d2ad81983c907d05f4904",
    "Packages/com.hardlight.hlunitycore/Runtime/ScriptableObjectWithGuid.cs": "befb87c3cc9068308cfa3e8965ceb82602974ffcb6380a894cd68d03e364629a",
    "Packages/com.hardlight.hlunitycore/Runtime/InspectorReadOnlyAttribute.cs": "d6e24d4d94dfd222ae920e83600d518ed8fd17286532771ae9570fdefe52feed",
    "Packages/com.hardlight.hlunitycore/Runtime/Il2CppSetOptionAttribute.cs": "138cbbdeccb4c6c6cdf5d08e248e06dc8419cff250de803582194df39893e945",
    "Packages/com.hardlight.hlunitycore/Runtime/HLUnityCore.Runtime.asmdef": "b9f3210d5cca9f7b7cc5bc458defe0e14e8df1486a17b29e517332c476347973",
}
RUNTIME_CONFIGURATION_RULES = tuple({
    "assembly": assembly,
    "full_name": full_name,
    "export_path": export_path,
    "maintained_path": maintained_path,
    "binary_sha256": BINDING_INPUT["binary_sha256"],
    "metadata_sha256": BINDING_INPUT["metadata_sha256"],
    "runtime_sources": RUNTIME_CONFIGURATION_SOURCES,
} for assembly, full_name, export_path, maintained_path in (
    ("HLUnityCore.Runtime", "Hardlight.TimeCategoryConfiguration",
     "Assets/Scripts/HLUnityCore.Runtime/Hardlight/TimeCategoryConfiguration.cs",
     "Packages/com.hardlight.hlunitycore/Runtime/Scheduling/TimeCategoryConfiguration.cs"),
    ("HLUnityCore.Runtime", "Hardlight.TimeCategoryObject",
     "Assets/Scripts/HLUnityCore.Runtime/Hardlight/TimeCategoryObject.cs",
     "Packages/com.hardlight.hlunitycore/Runtime/Scheduling/TimeCategoryObject.cs"),
    ("HLUnityCore.Runtime", "Hardlight.HLUnityCoreConfigurationAsset",
     "Assets/Scripts/HLUnityCore.Runtime/Hardlight/HLUnityCoreConfigurationAsset.cs",
     "Packages/com.hardlight.hlunitycore/Runtime/Configuration/HLUnityCoreConfigurationAsset.cs"),
    ("HLModernUI.Runtime", "Hardlight.UINavigationConfiguration",
     "Assets/Scripts/HLModernUI.Runtime/Hardlight/UINavigationConfiguration.cs",
     "Packages/com.hardlight.hlmodernui/Runtime/UINavigationConfiguration.cs"),
    ("Game.Runtime", "Hardlight.Analytics.Analytics_SystemConfig",
     "Assets/Scripts/Game.Runtime/Hardlight/Analytics/Analytics_SystemConfig.cs",
     "Assets/Scripts/Game/Analytics/Analytics_SystemConfig.cs"),
))
RULES += RUNTIME_CONFIGURATION_RULES

# Boot-path data. TimeManager reads TimeCategoryLookup, a SerializableDictionary
# of TimeCategory keys and TimeCategoryObject pointers stored as its backing
# list. VisualAssetManager.Awake reads DevicePerformanceGroup, whose elements
# point at the per-platform DevicePerformance assets and their device matches.
# UINavigationConfiguration's default highlight is a UIHighlightDefinition
# pointing at the UIHighlight component of a uihighlight_* prefab. Pin each
# owner with its serialized dependencies, including pointed-to declarations.
BOOT_DATA_SOURCES = {
    "Assets/Scripts/RecoveredProof/Scheduling/TimeCategoryLookup.cs": "3de65e56e838d282879584e2d0712d9c944798728bf6140ebe7359c642b16040",
    "Assets/Scripts/HLAutoGenerated/TimeCategory.cs": "8cb6db981617bc1806f18730d9e7d28f0250d48e13d66d831ac6325d68cf5e21",
    "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/Dependencies/SerializableDictionary.cs": "64335ecb04645f60a4484e30597366c85537a0b042156a741a46c029b259f9a9",
    "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/Dependencies/SerializableDictionaryBase.cs": "325ac95f58f99e07b6851d9bf0c3bfe272e2e0eb4e3cc4042f586536ba8fabc1",
    "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/Dependencies/SerializableKeyValuePair.cs": "f00e06f63806beddd514fdde1ed8974d58c4ded1a123c56f025f1d3322922f59",
    "Packages/com.hardlight.hlunitycore/Runtime/Scheduling/TimeCategoryObject.cs": "f8bb6c44f6ed95b4f4131438e5baff39b7270bd2b6f2173a6a697ff7c9dcc9af",
    "Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/Dependencies/Configuration/SystemConfigurationAsset.cs": "3f5bf61bfcf1952ce2aa3cc44ad258b839b1fdb0ed3d2ad81983c907d05f4904",
    "Assets/Scripts/RecoveredProof/DevicePerformanceGroup.cs": "9528d6fd449b40ba244c600e178d0ce557c16b4b89cf829696ef134373600fea",
    "Assets/Scripts/Definitions/DefinitionDataType.cs": "396b278ba573f3aace1a56b7db03f6807095108ebe2a1c198cc5da2247f0e768",
    "Assets/Scripts/RecoveredProof/DevicePerformance.cs": "127273ce6c858efd2f42c627702b0e923cbbd1dce51a5472ad9c7cd45601f761",
    "Assets/Scripts/RecoveredProof/DevicePerformance_iOS.cs": "56b74a012b7940884e2d9aabeb084fae331f4bbd44a3318056ef73054602f515",
    "Assets/Scripts/RecoveredProof/DevicePerformance_macOS.cs": "d06a151997b86296f22263b590c3eb525696192bfb110bbf7abc83644558f151",
    "Assets/Scripts/RecoveredProof/DevicePerformance_tvOS.cs": "08948aa078815ea45299fb189d9e44b6f15608e868ac77631794df79fcd1f1ee",
    "Assets/Scripts/RecoveredProof/DevicePerformanceMatch.cs": "c3b187ef7eb60eda9e566b85201736a98f997f436e4e61d8bd9abfcd8bf7d812",
    "Assets/Scripts/RecoveredProof/DevicePerformanceMatch_iOS.cs": "ae1b2415aeea89a070bb5817720402e22fd4e491bec00e28a44ee95741e9b1d8",
    "Assets/Scripts/RecoveredProof/DevicePerformanceMatch_macOS.cs": "6ecb759fdb6618b0ceb3d35ad6aaabbcce5798c563b4eb63efe5ec9ed8a817de",
    "Assets/Scripts/RecoveredProof/DevicePerformanceMatch_tvOS.cs": "a3239b8567fdc42dbf24f7d4fce3a579aeeab91fb5fc310f5130c8eaf391c560",
    "Assets/Scripts/HLAutoGenerated/MacDeviceGeneration.cs": "9e893fe1980d40ee7ff4e36d77b30ea4b36fcd8f63c55cfd0ee573ca5135de5d",
    "Packages/com.hardlight.hlunitycore/Runtime/Performance/PerformanceProfile.cs": "6910e45f664ef4e67896041acde3dfe0c6cf07f93b04a4e3dacf0d74588dc056",
    "Packages/com.hardlight.hlunitycore/Runtime/Performance/PerformanceAttribute.cs": "287215f479cef4ad6eabd19cfd4eeabbae03791a657c3d7f3e6178a915a08c7c",
    "Packages/com.hardlight.hlunitycore/Runtime/Performance/ScalableFeature.cs": "d0635ab9900049fbbc0379c5cff8d4c9b456583e02cec80138de4eb7529ce34b",
    "Packages/com.hardlight.hlmodernui/Runtime/UIHighlightDefinition.cs": "fd92426131c8550eedeb46d9ef8cb87ea6ac77094ce6421316a1cc3210e56768",
    "Packages/com.hardlight.hlmodernui/Runtime/UIHighlight.cs": "cc4b08fd88f77c649d96d5b440173d63b5f5eba1e9f02faebca1eced1315bf77",
    "Packages/com.hardlight.hlmodernui/Runtime/HLModernUI.Runtime.asmdef": "433d7a8f6bf62758056fc8bb9f2734d09351e43710284c41754ed1c274966e60",
    "Packages/com.hardlight.hlunitycore/Runtime/ScriptableObjectWithGuid.cs": "befb87c3cc9068308cfa3e8965ceb82602974ffcb6380a894cd68d03e364629a",
    "Packages/com.hardlight.hlunitycore/Runtime/InspectorReadOnlyAttribute.cs": "d6e24d4d94dfd222ae920e83600d518ed8fd17286532771ae9570fdefe52feed",
    "Packages/com.hardlight.hlunitycore/Runtime/Il2CppSetOptionAttribute.cs": "138cbbdeccb4c6c6cdf5d08e248e06dc8419cff250de803582194df39893e945",
    "Assets/Scripts/HLAutoGenerated/HLAutoGenerated.asmdef": "1e054c56612e318e251e6417a47fe46c4564234ed6336cc0f3fb83cd988b6a82",
    "Assets/Scripts/RecoveredProof/Game.Runtime.asmdef": "b234684d1289ae1bc0a8bfc843f941b8bcf5f578f614a6e01c15d1348e9271a0",
    "Packages/com.hardlight.hlunitycore/Runtime/HLUnityCore.Runtime.asmdef": "b9f3210d5cca9f7b7cc5bc458defe0e14e8df1486a17b29e517332c476347973",
}
BOOT_DATA_RULES = tuple({
    "assembly": assembly,
    "full_name": full_name,
    "export_path": "Assets/Scripts/" + assembly + "/" + full_name.replace(".", "/") + ".cs",
    "maintained_path": maintained_path,
    "binary_sha256": BINDING_INPUT["binary_sha256"],
    "metadata_sha256": BINDING_INPUT["metadata_sha256"],
    "runtime_sources": BOOT_DATA_SOURCES,
} for assembly, full_name, maintained_path in (
    ("Game.Runtime", "HardlightProject.TimeCategoryLookup", "Assets/Scripts/RecoveredProof/Scheduling/TimeCategoryLookup.cs"),
    ("Game.Runtime", "HardlightProject.DevicePerformanceGroup", "Assets/Scripts/RecoveredProof/DevicePerformanceGroup.cs"),
    ("Game.Runtime", "HardlightProject.DevicePerformance_iOS", "Assets/Scripts/RecoveredProof/DevicePerformance_iOS.cs"),
    ("Game.Runtime", "HardlightProject.DevicePerformance_macOS", "Assets/Scripts/RecoveredProof/DevicePerformance_macOS.cs"),
    ("Game.Runtime", "HardlightProject.DevicePerformance_tvOS", "Assets/Scripts/RecoveredProof/DevicePerformance_tvOS.cs"),
    ("HLModernUI.Runtime", "Hardlight.UIHighlightDefinition", "Packages/com.hardlight.hlmodernui/Runtime/UIHighlightDefinition.cs"),
    ("HLModernUI.Runtime", "Hardlight.UIHighlight", "Packages/com.hardlight.hlmodernui/Runtime/UIHighlight.cs"),
))
RULES += BOOT_DATA_RULES

# TextMeshPro Resources assets. The manifest pins the registry package
# 3.2.0-pre.4; the resolver's fresh layout comparison must show each bound
# type's serialized layout equal to the shipped schema. Pin the registry
# identity, descriptor and each type's serialized closure. TMP_FontAsset covers
# every font asset of the release (LiberationSans, Noto and Proxima Nova).
TMP_VERSION = "3.2.0-pre.4"
TMP_LOCK = {"version": TMP_VERSION, "depth": 0, "source": "registry",
            "dependencies": {"com.unity.ugui": "1.0.0"}, "url": "https://packages.unity.com"}
TMP_PACKAGE_SHA256 = "6c735fb7670896a29f16cceb247f5c551d7c63a903fb52725a3961ea70273010"
TMP_RUNTIME_SOURCES = {
    "Packages/com.unity.textmeshpro/Scripts/Runtime/Unity.TextMeshPro.asmdef": "3b8e1b7597ef124b1ab919f7483fd79ddf8b9a9a3ad0d89343f3b8e9a74379a6",
    "Packages/com.unity.textmeshpro/Scripts/Runtime/TMP_Asset.cs": "e6cb2e6a443f546144bb133031e0a73e86a44e543438db26844be9bd30f1546a",
    "Packages/com.unity.textmeshpro/Scripts/Runtime/TMP_StyleSheet.cs": "f4fa00a40d01561847c4e22d874f711130b25df4b8b4b67dbb99488b0ead8515",
    "Packages/com.unity.textmeshpro/Scripts/Runtime/TMP_Style.cs": "c5c048cc47caa8c1bcf403e83fc2d2e725f2e9dfce1a8ec6528bb7c02865d07b",
    "Packages/com.unity.textmeshpro/Scripts/Runtime/TMP_SpriteAsset.cs": "9f7db52d3910b77d38603a401869c72f89aab2d7a9f547ace4943001d616ee23",
    "Packages/com.unity.textmeshpro/Scripts/Runtime/TMP_SpriteCharacter.cs": "2b431c801eeeb6b484ddfe8790acb688995f4dd08aea39b3c005b019dc538cbe",
    "Packages/com.unity.textmeshpro/Scripts/Runtime/TMP_SpriteGlyph.cs": "6939dd28017047d8b4efed3e3577813b87add5d5e3dc645a01b7668e772caf0c",
    "Packages/com.unity.textmeshpro/Scripts/Runtime/TMP_TextElement.cs": "493e18e7d05b988751a2e0a48d46d3eb18cdc95a240084bc818c52911bf4f04a",
    "Packages/com.unity.textmeshpro/Scripts/Runtime/TMP_Sprite.cs": "1666269274289fe6f41ab37218318cf81fcbfd228df4daa413c4f2885512e614",
    "Packages/com.unity.textmeshpro/Scripts/Runtime/TMP_TextElement_Legacy.cs": "53c9b2a15f289727029c10199a38dd0433415e699b2d45809f780efea61d9bb9",
    "Packages/com.unity.textmeshpro/Scripts/Runtime/TMP_Settings.cs": "0464ea977abbe847e2df3f38cbe7e3de322c719e69c5014d5c9f765ff2f73d38",
    "Packages/com.unity.textmeshpro/Scripts/Runtime/TMP_FontAsset.cs": "e6bf1994edf8c0972f3453958605f3d9c527ae58c8866c777f5c8c9cc7c1a846",
    "Packages/com.unity.textmeshpro/Scripts/Runtime/TMP_FontAssetCommon.cs": "6735d2eaccf3e035ecb42a3bb6487eb45997399219ccd8a00890b382172d2503",
    "Packages/com.unity.textmeshpro/Scripts/Runtime/TMP_FontFeatureTable.cs": "ea13f58bf8d7ef230edcf6369f9fe374af1836cfccc6330b68e4caa5f41d62ad",
    "Packages/com.unity.textmeshpro/Scripts/Runtime/FontFeatureCommon.cs": "b9912589f458ba3fc8b2df5e1ca72f66b0cfca188904e909b105486f8690dd65",
    "Packages/com.unity.textmeshpro/Scripts/Runtime/FontFeatureCommonGPOS.cs": "fc5fa9ca6c62bde04a409cd0d1c0d70aa2efd75f685c9bbed92198d7756d5869",
    "Packages/com.unity.textmeshpro/Scripts/Runtime/FontFeatureCommonGSUB.cs": "e616ccd75df5acd3c5a959d5b73ef5e19f0fe4d57b0f9ce70db340b8fb63d38d",
    "Packages/com.unity.textmeshpro/Scripts/Runtime/TMP_Character.cs": "238a4ee040c542469ce3f9ba24b156d2caae463923d230af65e439677082de03",
    # TextWrappingModes, the type of TMP_Settings.m_TextWrappingMode, is declared here.
    "Packages/com.unity.textmeshpro/Scripts/Runtime/TMP_Text.cs": "8747a12e8d0c555088f24c09557c2404eb6c3ef64e9cd738a04b7b93a53374f4",
}
TMP_PACKAGE_RULES = tuple({
    "assembly": "Unity.TextMeshPro",
    "full_name": "TMPro." + name,
    "export_path": "Assets/Scripts/Unity.TextMeshPro/TMPro/" + name + ".cs",
    "maintained_path": "Packages/com.unity.textmeshpro/Scripts/Runtime/" + name + ".cs",
    "binary_sha256": BINDING_INPUT["binary_sha256"],
    "metadata_sha256": BINDING_INPUT["metadata_sha256"],
    "package": "com.unity.textmeshpro",
    "runtime_sources": TMP_RUNTIME_SOURCES,
} for name in ("TMP_StyleSheet", "TMP_SpriteAsset", "TMP_Settings", "TMP_FontAsset"))
RULES += TMP_PACKAGE_RULES

# The render pipeline the shipped QualitySettings and GraphicsSettings select.
# URP 14.0.11 is a builtin package locked to the Editor; pin its lock entry,
# descriptor and the serialized closure of each bound type, plus
# Documentation.cs, whose [Conditional("UNITY_EDITOR")] URPHelpURL is one of
# the Editor-only differences the player plane accounts for.
URP_VERSION = "14.0.11"
URP_LOCK = {"version": URP_VERSION, "depth": 0, "source": "builtin",
            "dependencies": {"com.unity.mathematics": "1.2.1", "com.unity.burst": "1.8.9",
                             "com.unity.render-pipelines.core": "14.0.11", "com.unity.shadergraph": "14.0.11",
                             "com.unity.render-pipelines.universal-config": "14.0.9"}}
URP_PACKAGE_SHA256 = "b3e4967f5cadc53cefd0cc3de1aa4aef1a7225be0b6996e98ca174a9f0197bb2"
URP_RUNTIME_SOURCES = {
    "Packages/com.unity.render-pipelines.universal/Runtime/Unity.RenderPipelines.Universal.Runtime.asmdef": "36c3b5793d189f6781073e279af8fdfdb8ce4bbaa33df3a94170a0bcbadca063",
    "Packages/com.unity.render-pipelines.universal/Runtime/Documentation.cs": "6b0c1733156454dc371d3e0f6489e78063155956d1a9a6986be38943b1631246",
    "Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRenderPipelineAsset.cs": "53c371693601b65a3351e234870ecb025fdcb6633b3e356289da4a5ab97cb659",
    "Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRenderPipelineAssetPrefiltering.cs": "4ceb2dda0fceaca914fad0dbcae34b391f050502ab80931a14e54a79cdf48575",
    "Packages/com.unity.render-pipelines.universal/Runtime/Deprecated.cs": "09c88fe7fde2ae534ff6e87b32a73a3ddb5d9c9cbc1d710b136276718baa6953",
    "Packages/com.unity.render-pipelines.universal/Runtime/ScriptableRendererData.cs": "734945757162e043129506cb396cacba9b86a2a299a22bb19928b3638fdb74c1",
    "Packages/com.unity.render-pipelines.universal/Runtime/ScriptableRendererFeature.cs": "ac767233a46bfff3550d630ea10da835209b6c0d240e242ca0d88e5ef55c24cb",
    "Packages/com.unity.render-pipelines.universal/Runtime/UniversalRendererData.cs": "2117fed5c05af311aaa7198b249b3fb5bfbe732b7a44f1fe902283cfbf604800",
    "Packages/com.unity.render-pipelines.universal/Runtime/UniversalRenderer.cs": "fdd7dfcd1b11b818af52518e210710b68b9b88b89c161ae79a6fd08e1316a4b6",
    "Packages/com.unity.render-pipelines.universal/Runtime/IntermediateTextureMode.cs": "6004710b91d8618fb2651c10bbec6dd473d23a43a3e45c6590d2029dad352957",
    "Packages/com.unity.render-pipelines.universal/Runtime/Data/RenderStateData.cs": "af36c9c33b8cee09732e4e2010cdff9cfa34e54c10e8b91221c294e4f2402f2d",
    "Packages/com.unity.render-pipelines.universal/Runtime/Passes/ScriptableRenderPass.cs": "95229745e1be43c23a5137a0e27bb152f5f0ead9ef9f04d794cfc5afd2a7b175",
    "Packages/com.unity.render-pipelines.universal/Runtime/RendererFeatures/FullScreenPassRendererFeature.cs": "6983fa8a5a3af3a4e2fbc3595cec8fe7460dc009c343d61e249dd196f3208c6a",
    "Packages/com.unity.render-pipelines.universal/Runtime/RendererFeatures/FullScreenPassRendererFeature.migration.cs": "2f5758a7217e7ce5e0d30a4b76bee8b5cc5b441a18be5a9f21527f7d6d86618b",
    "Packages/com.unity.render-pipelines.universal/Runtime/RendererFeatures/DecalRendererFeature.cs": "00c4a7ed10729d43ded2cedfe508731164f7f31dedbbc161d09311990efbc758",
    "Packages/com.unity.render-pipelines.universal/Runtime/RendererFeatures/DisallowMultipleRendererFeature.cs": "28f887e438ce0057b07e7e041534408305639a185d254a999ae2a18c93cd4c66",
    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.cs": "8c9e48d2170198bb6fb7b283a36f9f99c5fcff969bc5cc278ead48f76ab43ff0",
    "Packages/com.unity.render-pipelines.universal/Runtime/Data/XRSystemData.cs": "55105c9db6925e635dd2053551702f684e77d5f42e5cb259ed1e2dd892d9071b",
    "Packages/com.unity.render-pipelines.universal/Runtime/UniversalRenderPipelineGlobalSettings.cs": "9d02ae2f4a820a8c2f770fda78d6d0d448c78fc3ffec908e2930c3d1d3a7309b",
    "Packages/com.unity.render-pipelines.universal/Runtime/UniversalRenderPipelineGlobalSettings.ShaderVariantSettings.cs": "015a54794fe6f599ae3e86e67855444f12bf8695b560a47dd49d7b50247ea046",
}
_URP_EXPORT = "Assets/Scripts/Unity.RenderPipelines.Universal.Runtime/"
_URP_PACKAGE = "Packages/com.unity.render-pipelines.universal/Runtime/"
# layout_plane "player": the Editor's declarations carry UNITY_EDITOR-only
# members, so the verdict comes from the genuine player compilation (see
# playerlayouts); the Editor candidate still supplies the loaded MonoScript.
# FullScreenPassRendererFeature binds to the package script itself:
# FullscreenShaderManager looks its feature up by exact type.
URP_PACKAGE_RULES = tuple({
    "assembly": "Unity.RenderPipelines.Universal.Runtime",
    "full_name": full_name,
    "export_path": _URP_EXPORT + export,
    "maintained_path": _URP_PACKAGE + maintained,
    "binary_sha256": BINDING_INPUT["binary_sha256"],
    "metadata_sha256": BINDING_INPUT["metadata_sha256"],
    "package": "com.unity.render-pipelines.universal",
    "runtime_sources": URP_RUNTIME_SOURCES,
    **({"layout_plane": "player"} if player else {}),
} for full_name, export, maintained, player in (
    ("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset", "UnityEngine/Rendering/Universal/UniversalRenderPipelineAsset.cs",
     "Data/UniversalRenderPipelineAsset.cs", True),
    ("UnityEngine.Rendering.Universal.UniversalRendererData", "UnityEngine/Rendering/Universal/UniversalRendererData.cs",
     "UniversalRendererData.cs", True),
    ("UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings",
     "UnityEngine/Rendering/Universal/UniversalRenderPipelineGlobalSettings.cs", "UniversalRenderPipelineGlobalSettings.cs", True),
    ("FullScreenPassRendererFeature", "FullScreenPassRendererFeature.cs", "RendererFeatures/FullScreenPassRendererFeature.cs", True),
    ("UnityEngine.Rendering.Universal.DecalRendererFeature", "UnityEngine/Rendering/Universal/DecalRendererFeature.cs",
     "RendererFeatures/DecalRendererFeature.cs", True),
    ("UnityEngine.Rendering.Universal.PostProcessData", "UnityEngine/Rendering/Universal/PostProcessData.cs",
     "Data/PostProcessData.cs", False),
    ("UnityEngine.Rendering.Universal.XRSystemData", "UnityEngine/Rendering/Universal/XRSystemData.cs", "Data/XRSystemData.cs", False),
))
RULES += URP_PACKAGE_RULES

# The game's renderer feature (renderer index 1, Renderer_UI) and the scalable
# performance features that name the renderer-feature and render-scale tiers.
RENDER_FEATURE_SOURCES = {
    "Assets/Scripts/Game/RenderFeature/UIBlurRenderFeature.cs": "3a09c1198274bd025d26b531d8575e88c10b6536841fbc71d88a17dc07922f3d",
    "Assets/Scripts/RecoveredProof/Game.Runtime.asmdef": "b234684d1289ae1bc0a8bfc843f941b8bcf5f578f614a6e01c15d1348e9271a0",
    "Packages/com.hardlight.hlunitycore/Runtime/Il2CppSetOptionAttribute.cs": "138cbbdeccb4c6c6cdf5d08e248e06dc8419cff250de803582194df39893e945",
}
SCALABLE_FEATURE_SOURCES = {
    "Packages/com.hardlight.hlunitycore/Runtime/Performance/ScalableFeature.cs": "d0635ab9900049fbbc0379c5cff8d4c9b456583e02cec80138de4eb7529ce34b",
    "Packages/com.hardlight.hlunitycore/Runtime/HLUnityCore.Runtime.asmdef": "b9f3210d5cca9f7b7cc5bc458defe0e14e8df1486a17b29e517332c476347973",
}
RENDER_GAME_RULES = ({
    "assembly": "Game.Runtime",
    "full_name": "HardlightProject.UIBlurRenderFeature",
    "export_path": "Assets/Scripts/Game.Runtime/HardlightProject/UIBlurRenderFeature.cs",
    "maintained_path": "Assets/Scripts/Game/RenderFeature/UIBlurRenderFeature.cs",
    "binary_sha256": BINDING_INPUT["binary_sha256"],
    "metadata_sha256": BINDING_INPUT["metadata_sha256"],
    "runtime_sources": RENDER_FEATURE_SOURCES,
}, {
    "assembly": "HLUnityCore.Runtime",
    "full_name": "Hardlight.ScalableFeature",
    "export_path": "Assets/Scripts/HLUnityCore.Runtime/Hardlight/ScalableFeature.cs",
    "maintained_path": "Packages/com.hardlight.hlunitycore/Runtime/Performance/ScalableFeature.cs",
    "binary_sha256": BINDING_INPUT["binary_sha256"],
    "metadata_sha256": BINDING_INPUT["metadata_sha256"],
    "runtime_sources": SCALABLE_FEATURE_SOURCES,
})
RULES += RENDER_GAME_RULES

GUID = re.compile(r"^guid:\s*([0-9a-f]{32})\s*$", re.MULTILINE)
DOCUMENT = re.compile(r"^--- !u!(?P<class>\d+) &-?\d+\r?\n(?P<label>\w+):\r?\n(?P<fields>.*?)(?=^--- !u!|\Z)", re.MULTILINE | re.DOTALL)
POINTER = re.compile(r"^(?P<prefix>  m_Script: *)\{(?P<body>[^}\r\n]*)\}(?P<trailer> *\r?$)", re.MULTILINE)
EXACT_POINTER = re.compile(r"\s*fileID:\s*(?P<id>-?\d+)\s*,\s*guid:\s*(?P<guid>[0-9a-fA-F]{32})\s*,\s*type:\s*(?P<type>\d+)\s*")


def _owned_file(root, relative):
    path = root / relative
    if path.is_symlink() or any(p.is_symlink() for p in path.parents):
        raise ValueError("Refusing symlinked script evidence: " + str(path))
    if not path.is_file():
        raise ValueError("Script evidence is missing: " + str(path))
    return path


def _package_json(data):
    def object_pairs(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("Duplicate key in approved package JSON: " + key)
            result[key] = value
        return result
    value = json.loads(data, object_pairs_hook=object_pairs)
    if not isinstance(value, dict) or not isinstance(value.get("dependencies"), dict):
        raise ValueError("Approved package JSON must contain a dependency object")
    return value


def _package_identity(name):
    """Return (version, lock entry, descriptor SHA-256, builtin) of a reviewed package."""
    if name == "com.unity.ugui":
        return "1.0.0", UGUI_LOCK, UGUI_PACKAGE_SHA256, True
    if name == "com.unity.textmeshpro":
        return TMP_VERSION, TMP_LOCK, TMP_PACKAGE_SHA256, False
    if name == "com.unity.render-pipelines.universal":
        return URP_VERSION, URP_LOCK, URP_PACKAGE_SHA256, True
    raise ValueError("Unsupported approved script package identity")


def _script_file(root, relative, rule):
    """Resolve only the reviewed UI, TextMeshPro and URP packages; never arbitrary packages."""
    if "package" not in rule:
        return _owned_file(root, relative)
    name = rule["package"]
    version, lock_entry, descriptor_sha256, _ = _package_identity(name)
    prefix = "Packages/" + name + "/"
    if not relative.startswith(prefix):
        raise ValueError("Unsupported approved script package identity")
    manifest = _package_json(_owned_file(root, "Packages/manifest.json").read_bytes())
    lock = _package_json(_owned_file(root, "Packages/packages-lock.json").read_bytes())
    if manifest.get("dependencies", {}).get(name) != version or lock.get("dependencies", {}).get(name) != lock_entry:
        raise ValueError("Approved package resolution changed: " + name)
    package = root / "Library/PackageCache" / (name + "@" + version)
    descriptor = _owned_file(package, "package.json").read_bytes()
    if hashlib.sha256(descriptor).hexdigest() != descriptor_sha256:
        raise ValueError("Approved package descriptor changed: " + name)
    suffix = relative[len(prefix):]
    if not suffix or any(part in ("", ".", "..") for part in suffix.split("/")):
        raise ValueError("Unsupported approved script package path")
    return _owned_file(package, suffix)


def _host_player_target():
    """The standalone player target this Editor host builds by default."""
    return {"darwin": "macos", "win32": "windows"}.get(sys.platform, "linux")


def _player_plane(root, work, current, schema, schema_digest, summary):
    """Compile the current source as a player and compare its layouts with the original.

    The player compile and its metadata inventory are fresh for exactly this
    source; the Editor inventory is the one this resolution just made.
    """
    from . import playercode, playerlayouts, playerschema
    target = _host_player_target()
    playercode.run_player_code(root, work, target)
    published = playerschema.run_player_schema(root, work, target, playerschema.URP_PROFILE)
    report_path = Path(published["report_path"])
    if work.resolve() not in report_path.resolve().parents or report_path.is_symlink():
        raise ValueError("Player schema lies outside the generated work directory")
    report_bytes = report_path.read_bytes()
    report = json.loads(report_bytes)
    if (report.get("status") != "metadata-ready" or report.get("identity_status") != "complete" or
            report.get("profile") != playerschema.URP_PROFILE or report.get("target") != target or
            report.get("source_fingerprint") != current or artifact_fingerprint(root) != current):
        raise ValueError("Player schema is stale or incomplete for the maintained source")
    inventory_path = Path(summary["inventory_path"])
    if work.resolve() not in inventory_path.resolve().parents or inventory_path.is_symlink():
        raise ValueError("Editor inventory lies outside the generated work directory")
    inventory_bytes = inventory_path.read_bytes()
    inventory = json.loads(inventory_bytes)
    if inventory.get("source_fingerprint") != current or inventory.get("original_schema_sha256") != schema_digest:
        raise ValueError("Editor inventory is stale for the player-plane comparison")
    result = playerlayouts.compare_player_plane(schema, report, inventory, playerschema.URP_ROOTS)
    playerlayouts.require_editor_only(result["editor_only"])
    return dict(result, report_path=str(report_path), report_sha256=hashlib.sha256(report_bytes).hexdigest(),
                inventory_sha256=hashlib.sha256(inventory_bytes).hexdigest(), target=target)


def resolve_bindings(repo_root, work_dir, exported_project, asset_map, *, input_fingerprint=None):
    """Prove exact current source/layout/asset identities before staging changes."""
    root = Path(repo_root).expanduser()
    if root.is_symlink() or any(parent.is_symlink() for parent in root.parents):
        raise ValueError("Refusing symlinked project root for script evidence")
    root = root.resolve(strict=True)
    work, export = map(Path, (work_dir, exported_project))
    rows = asset_map.get("assets", [])
    selected = []
    for rule in RULES:
        originals = [row for row in rows if row.get("path") == rule["export_path"]]
        if not originals:
            continue
        if len(originals) != 1 or originals[0].get("code_quarantined") is not True:
            raise ValueError("Exported script identity is ambiguous: " + rule["full_name"])
        if sum(row.get("guid") == originals[0].get("guid") for row in rows) != 1:
            raise ValueError("Exported script GUID has multiple asset owners")
        source = _script_file(root, rule["maintained_path"], rule)
        raw = _owned_file(export, rule["export_path"])
        meta = _owned_file(export, rule["export_path"] + ".meta")
        identities = GUID.findall(meta.read_text(encoding="utf-8"))
        if len(identities) != 1 or identities[0] != originals[0].get("guid"):
            raise ValueError("Exported script metadata differs from its asset map")
        selected.append((rule, originals[0], source, raw))
    if not selected:
        return {"status": "not_applicable", "bindings": [], "references_modified": False}

    critical = (input_fingerprint or {}).get("critical_files", {})
    for rule, _, _, _ in selected:
        for relative, expected in rule["runtime_sources"].items():
            if hashlib.sha256(_script_file(root, relative, rule).read_bytes()).hexdigest() != expected:
                raise ValueError("Maintained script behavior changed; review before binding: " + relative)
        if critical.get("Contents/Frameworks/GameAssembly.dylib") != rule["binary_sha256"] or critical.get(
                "Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat") != rule["metadata_sha256"]:
            raise ValueError("Script behavior has not been verified for this supplied release: GameAssembly.dylib " +
                             str(critical.get(inputvariants.GAME_ASSEMBLY)) + ", global-metadata.dat " +
                             str(critical.get(inputvariants.METADATA)) + "; " + rule["full_name"] + " requires " +
                             rule["binary_sha256"] + "/" + rule["metadata_sha256"])

    summary = run_layout_inventory(root, work)
    if summary.get("status") != "compared" or summary.get("source_freshness_verified") is not True:
        raise ValueError("Current Unity script layout proof failed; previous assets remain unchanged")
    comparison_path = Path(summary["comparison_path"])
    if work.resolve() not in comparison_path.resolve().parents or comparison_path.is_symlink():
        raise ValueError("Script layout proof lies outside the generated work directory")
    comparison_bytes = comparison_path.read_bytes()
    comparison = json.loads(comparison_bytes)
    if comparison.get("command") != "compare-monoscript-layouts" or comparison.get("status") != "compared":
        raise ValueError("Unsupported script layout comparison")
    # Layout alone cannot establish release identity: another release can have
    # the same fields but different behavior. Match both recovered code inputs.
    schema_path = Path(summary["schema_path"])
    if work.resolve() not in schema_path.resolve().parents:
        raise ValueError("Original schema lies outside the generated work directory")
    schema_path = _owned_file(work, schema_path.relative_to(work))
    schema_bytes = schema_path.read_bytes()
    schema_digest = hashlib.sha256(schema_bytes).hexdigest()
    schema = json.loads(schema_bytes)
    schema_summary = json.loads(_owned_file(work, "native-recovery/latest-schemas.json").read_bytes())
    schema_source = schema_summary.get("source", {})
    if schema_summary.get("status") != "ready" or schema_summary.get("command") != "schemas" or schema_summary.get(
            "evidence_report") != str(schema_path) or schema.get("status") != "ready" or schema.get("command") != "schemas":
        raise ValueError("Original schema source proof is incomplete")
    if schema_digest != comparison.get("original_schema_sha256") or schema_digest != summary.get("original_schema_sha256"):
        raise ValueError("Original schema differs from the Unity layout proof")
    if schema.get("metadata_sha256") != critical.get("Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat") or schema_source.get(
            "metadata_sha256") != schema.get("metadata_sha256") or schema_source.get("binary_sha256") != critical.get(
                "Contents/Frameworks/GameAssembly.dylib") or schema_source.get("analysis_binary_sha256") != schema.get("native_binary_sha256"):
        raise ValueError("Original schema belongs to a different supplied release")
    current = artifact_fingerprint(root)
    if comparison.get("source_fingerprint") != current or summary.get("source_fingerprint") != current:
        raise ValueError("Script layout proof is stale for maintained source")
    player = None
    if any(rule.get("layout_plane") == "player" for rule, _, _, _ in selected):
        player = _player_plane(root, work, current, schema, schema_digest, summary)
    bindings = []
    for rule, original, source, raw in selected:
        candidates = [c for c in comparison.get("candidates", [])
                      if c.get("assembly") == rule["assembly"] and c.get("full_name") == rule["full_name"]]
        if rule.get("layout_plane", "editor") not in ("editor", "player"):
            raise ValueError("Unsupported script layout plane: " + rule["full_name"])
        if rule.get("layout_plane") == "player":
            # The Editor plane carries UNITY_EDITOR-only members by construction;
            # it still identifies the loaded MonoScript, while the verdict is the
            # player plane's (which includes the Editor guard).
            verdict = player["verdicts"].get((rule["assembly"], rule["full_name"]))
            if len(candidates) != 1 or verdict is None or verdict["status"] != "compatible_layout" or verdict["issues"]:
                raise ValueError("Original script player layout is unresolved: " + rule["full_name"])
        elif len(candidates) != 1 or candidates[0].get("status") != "compatible_layout" or candidates[0].get("issues"):
            raise ValueError("Original script layout is unresolved: " + rule["full_name"])
        candidate = candidates[0]
        scripts = candidate.get("loaded_scripts", [])
        if len(scripts) != 1 or scripts[0].get("path") != rule["maintained_path"] or scripts[0].get("class_resolved") is not True:
            raise ValueError("Loaded maintained MonoScript identity is ambiguous")
        target = scripts[0]
        if "package" in rule:
            version, _, _, builtin = _package_identity(rule["package"])
            expected_package = root / "Library/PackageCache" / (rule["package"] + "@" + version)
            if (target.get("package_name") != rule["package"] or target.get("package_version") != version or
                    target.get("package_builtin") is not builtin or target.get("package_resolved_path") != str(expected_package) or
                    target.get("source_path") != str(source)):
                raise ValueError("Loaded package differs from its approved physical source: " + rule["package"])
        digest = hashlib.sha256(source.read_bytes()).hexdigest()
        if target.get("source_sha256") != digest:
            raise ValueError("Maintained script changed after its Unity inventory")
        if not re.fullmatch(r"[0-9a-f]{32}", target.get("guid", "")) or target["guid"] == "0" * 32 or type(target.get("file_id")) is not int or not (
                -(1 << 63) <= target["file_id"] < (1 << 63)) or not target["file_id"]:
            raise ValueError("Loaded script GUID/local file ID is invalid")
        local_meta = _script_file(root, rule["maintained_path"] + ".meta", rule)
        if GUID.findall(local_meta.read_text(encoding="utf-8")) != [target["guid"]]:
            raise ValueError("Maintained script metadata differs from the loaded MonoScript")
        bindings.append({**rule, "exported_guid": original["guid"], "exported_file_id": 11500000,
                         "target_guid": target["guid"], "target_file_id": target["file_id"],
                         "original_token": candidate["original_token"], "source_sha256": digest,
                         "exported_source_sha256": hashlib.sha256(raw.read_bytes()).hexdigest()})
    result = {"status": "verified_layout", "bindings": bindings, "references_modified": False,
              "source_fingerprint": current, "comparison_path": str(comparison_path),
              "comparison_sha256": hashlib.sha256(comparison_bytes).hexdigest(),
              "original_schema_sha256": comparison.get("original_schema_sha256")}
    if player is not None:
        result["player_plane"] = {
            "target": player["target"], "report_path": player["report_path"], "report_sha256": player["report_sha256"],
            "editor_inventory_sha256": player["inventory_sha256"], "editor_only": player["editor_only"],
            "verdicts": {key[0] + ":" + key[1]: value["status"] for key, value in player["verdicts"].items()}}
    return result



UGUI_FIELDS = {
    "UnityEngine.UI.LayoutElement": {
        "m_IgnoreLayout": "bool", "m_MinWidth": "float", "m_MinHeight": "float",
        "m_PreferredWidth": "float", "m_PreferredHeight": "float",
        "m_FlexibleWidth": "float", "m_FlexibleHeight": "float", "m_LayoutPriority": "int"},
    "UnityEngine.UI.ContentSizeFitter": {"m_HorizontalFit": 2, "m_VerticalFit": 2},
    "UnityEngine.UI.AspectRatioFitter": {"m_AspectMode": 4, "m_AspectRatio": "float"},
}
UGUI_COMMON_FIELDS = ("m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
                      "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier")


def _validate_ugui_document(fields, rule):
    """Accept only the complete scalar documents observed in this release."""
    if rule.get("package") != "com.unity.ugui":
        return
    own = UGUI_FIELDS.get(rule.get("full_name"))
    if own is None:
        raise ValueError("Unsupported Unity UI component")
    values = {}
    for line in fields.splitlines():
        parsed = re.fullmatch(r"  ([A-Za-z_][A-Za-z_0-9]*): *(.*)", line)
        if not parsed or parsed[1] in values:
            raise ValueError("Unsupported or duplicate Unity UI field")
        values[parsed[1]] = parsed[2]
    if tuple(values) != UGUI_COMMON_FIELDS + tuple(own):
        raise ValueError("Unsupported Unity UI field set or order")
    if any(values[key] != "0" for key in ("m_ObjectHideFlags", "m_EditorHideFlags")) or values["m_Enabled"] != "1" or any(
            values[key] != "{fileID: 0}" for key in ("m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset")) or any(
            values[key] for key in ("m_Name", "m_EditorClassIdentifier")):
        raise ValueError("Unsupported Unity UI common field value")
    owner = re.fullmatch(r"\{fileID: (-?[0-9]+)\}", values["m_GameObject"])
    if not owner or not -(1 << 63) <= int(owner[1]) < (1 << 63) or int(owner[1]) == 0:
        raise ValueError("Unsupported Unity UI owner pointer")
    if not re.fullmatch(r"\{fileID: -?[0-9]+, guid: [0-9a-fA-F]{32}, type: 3\}", values["m_Script"]):
        raise ValueError("Unsupported Unity UI script pointer syntax")
    for key, kind in own.items():
        value = values[key]
        if kind == "float":
            try:
                if not re.fullmatch(r"-?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?", value):
                    raise ValueError("Unsupported Unity UI numeric syntax")
                number = float(value)
                if not math.isfinite(number) or not math.isfinite(struct.unpack("<f", struct.pack("<f", number))[0]):
                    raise ValueError("Nonfinite Unity UI scalar")
            except (ValueError, OverflowError, struct.error) as error:
                raise ValueError("Unsupported Unity UI float") from error
        elif kind == "bool":
            if value not in ("0", "1"):
                raise ValueError("Unsupported Unity UI Boolean")
        elif not re.fullmatch(r"-?[0-9]+", value) or not -(1 << 31) <= int(value) < (1 << 31) or (type(kind) is int and not 0 <= int(value) <= kind):
            raise ValueError("Unsupported Unity UI integer or enum")


def rewrite_script_pointers(content, bindings):
    """Change only exact m_Script pointers, preserving every other byte."""
    mapping = {row["exported_guid"]: row for row in bindings}
    if len(mapping) != len(bindings):
        raise ValueError("Duplicate exported script binding")
    changes = 0
    package_changes = {row["exported_guid"]: 0 for row in bindings if row.get("package") == "com.unity.ugui"}

    def replace(match):
        nonlocal changes
        body = match.group("body")
        referenced = re.search(r"\bguid:\s*([0-9a-fA-F]{32})\b", body)
        if not referenced or referenced[1].lower() not in mapping:
            return match[0]
        exact = EXACT_POINTER.fullmatch(body)
        rule = mapping[referenced[1].lower()]
        if not exact or int(exact["id"]) != rule["exported_file_id"] or int(exact["type"]) != 3:
            raise ValueError("Unsupported original MonoScript pointer identity")
        changes += 1
        if rule.get("package") == "com.unity.ugui":
            package_changes[referenced[1].lower()] += 1
        return match["prefix"] + "{fileID: " + str(rule["target_file_id"]) + ", guid: " + rule["target_guid"] + ", type: 3}" + match["trailer"]

    def document(match):
        fields = match["fields"]
        pointers = [p for p in POINTER.finditer(fields)
                    if any(guid in p["body"].lower() for guid in mapping)]
        if not pointers:
            # A selected UI pointer with unsupported indentation must not be
            # silently left unresolved alongside successfully rewritten peers.
            if any(row.get("package") == "com.unity.ugui" and row["exported_guid"] in line.lower()
                   for row in bindings for line in re.findall(r"^[ \t]*m_Script:[^\r\n]*", fields, re.MULTILINE)):
                raise ValueError("Unsupported Unity UI script pointer indentation")
            return match[0]
        if match["class"] != "114" or match["label"] != "MonoBehaviour" or len(pointers) != 1:
            raise ValueError("Unsupported Unity script document")
        # The approved definition has only scalar fields and inline engine
        # pointers. Refuse nested/block/quoted multiline data rather than risk
        # mistaking string contents for a field. Extend this only with proof for
        # a newly approved type's real authored serialization.
        for line in fields.splitlines():
            field = re.fullmatch(r"  [A-Za-z_][A-Za-z_0-9]*: *(.*)", line)
            if not field or field[1].startswith(("|", ">", "'")):
                raise ValueError("Unsupported authored script field shape")
            if field[1].startswith('"'):
                try:
                    if not isinstance(json.loads(field[1]), str):
                        raise ValueError("Unsupported quoted Unity field")
                except (ValueError, TypeError) as error:
                    raise ValueError("Unsupported quoted Unity field") from error
        referenced = EXACT_POINTER.fullmatch(pointers[0]["body"])
        if referenced:
            _validate_ugui_document(fields, mapping[referenced["guid"].lower()])
        rewritten = POINTER.sub(replace, fields)
        return match[0][:match.start("fields") - match.start()] + rewritten

    # Unity text assets are UTF-8. Decoding only selected assets avoids treating
    # arbitrary binary/audio data as YAML; newline bytes survive unchanged.
    text = content.decode("utf-8")
    # A malformed/stripped document header or label must not hide a selected
    # script beside a valid document. Count exact selected pointer field lines
    # independently of the supported document parser before rewriting.
    pointer_lines = re.findall(r"^[ \t]*m_Script:[^\r\n]*", text, re.MULTILINE)
    expected = {guid: sum(guid in line.lower() for line in pointer_lines) for guid in package_changes}
    rewritten = DOCUMENT.sub(document, text)
    if package_changes != expected:
        raise ValueError("Unsupported Unity UI script document envelope")
    return rewritten.encode("utf-8"), changes


def rewrite_prepared_script_pointers(content, bindings, shaders=None, shader_log=None):
    """Route only the qualified configuration, TextMeshPro, component and renderer script identities.

    ``shaders`` maps exported compiled shader GUIDs to URP package shader GUIDs
    for the render pipeline profiles' shader slots; ``shader_log`` collects
    each slot they visit.
    """
    from .componentbindings import PROFILES as COMPONENT_PROFILES, rewrite_component_pointers
    from .configurationbindings import PROFILES, rewrite_configuration_pointers
    from .rendererbindings import PROFILES as RENDERER_PROFILES, rewrite_renderer_pointers
    from .textmeshprobindings import PROFILES as TMP_PROFILES, rewrite_textmeshpro_pointers

    if len({row["exported_guid"] for row in bindings}) != len(bindings):
        raise ValueError("Duplicate exported script binding")
    owner = lambda row: (row.get("assembly"), row.get("full_name"))
    configuration = [row for row in bindings if owner(row) in PROFILES]
    textmeshpro = [row for row in bindings if owner(row) in TMP_PROFILES]
    component = [row for row in bindings if owner(row) in COMPONENT_PROFILES]
    renderer = [row for row in bindings if owner(row) in RENDERER_PROFILES]
    scalar = [row for row in bindings if owner(row) not in PROFILES and owner(row) not in TMP_PROFILES
              and owner(row) not in COMPONENT_PROFILES and owner(row) not in RENDERER_PROFILES]
    # Select by script field lines first. The profile helpers' stricter
    # standalone/control guards must not apply to unrelated scalar documents.
    text = content.decode("utf-8")
    script_lines = re.findall(r"^[ \t]*m_Script:[^\r\n]*", text, re.MULTILINE)
    rewritten, changes = content, 0
    renderer_rewrite = lambda data, rows: rewrite_renderer_pointers(data, rows, {} if shaders is None else shaders, shader_log)
    for rows, rewrite in ((configuration, rewrite_configuration_pointers), (textmeshpro, rewrite_textmeshpro_pointers),
                          (component, rewrite_component_pointers), (renderer, renderer_rewrite)):
        if any(row["exported_guid"] in line.lower() for row in rows for line in script_lines):
            rewritten, count = rewrite(rewritten, rows)
            changes += count
    rewritten, scalar_changes = rewrite_script_pointers(rewritten, scalar)
    return rewritten, changes + scalar_changes
