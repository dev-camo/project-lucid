"""Whole-byte pointer tests for the boot data batch: TimeCategoryLookup, the device
performance group and per-platform assets, UIHighlightDefinition and the
UIHighlight prefab component.

The documents mirror the shapes of the shipped export (abridged lists) with
synthetic identities; the real export is not available to this suite.
"""
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib.componentbindings import rewrite_component_pointers
from lucidlib.configurationbindings import rewrite_configuration_pointers
from lucidlib.scriptbindings import BOOT_DATA_RULES, RULES, rewrite_prepared_script_pointers
from test_bootbindings import COMPONENT_COMMON, HEADER, binding, pointer, standalone, swapped


LOOKUP = binding("Game.Runtime", "HardlightProject.TimeCategoryLookup", 1)
GROUP = binding("Game.Runtime", "HardlightProject.DevicePerformanceGroup", 2)
MAC = binding("Game.Runtime", "HardlightProject.DevicePerformance_macOS", 3)
IOS = binding("Game.Runtime", "HardlightProject.DevicePerformance_iOS", 4)
TVOS = binding("Game.Runtime", "HardlightProject.DevicePerformance_tvOS", 5)
CONFIGURATIONS = [LOOKUP, GROUP, MAC, IOS, TVOS]

LOOKUP_BODY = ("  m_baseAsset: {fileID: 0}\n  m_overriddenFields:\n  - Dictionary\n  Dictionary:\n    m_values:\n"
               "    - m_key: 874239612\n      m_value: " + pointer(10) + "\n"
               "    - m_key: -2001650353\n      m_value: " + pointer(11) + "\n")
GROUP_BODY = ("  m_elements:\n  - Name: DevicePerformance_iOS\n    Data: " + pointer(12) + "\n"
              "  - Name: DevicePerformance_macOS\n    Data: " + pointer(13) + "\n")
MATCHES_BODY = ("  m_fallback: " + pointer(14) + "\n  m_devicePerformanceMatches:\n"
                "  - name: VeryLow\n    m_profile: " + pointer(15) + "\n    m_deviceGeneration: 1924880489\n"
                "  - name: iPadAir2\n    m_profile: " + pointer(14) + "\n    m_deviceGeneration: 24\n"
                "  - name: 79\n    m_profile: " + pointer(14) + "\n    m_deviceGeneration: 79\n")
BODIES = {1: ("TimeCategoryLookup", LOOKUP_BODY), 2: ("DevicePerformanceGroup", GROUP_BODY),
          3: ("DevicePerformance_macOS", MATCHES_BODY), 4: ("DevicePerformance_iOS", MATCHES_BODY),
          5: ("DevicePerformance_tvOS", MATCHES_BODY)}


def configuration(row, replace=None):
    name, body = BODIES[int(row["exported_guid"][0], 16)]
    if replace:
        assert replace[0] in body, replace
        body = body.replace(*replace)
    return standalone(row, name, body)


def rejects(test, content, rows):
    with test.assertRaises(ValueError):
        rewrite_configuration_pointers(content, rows)


class BootDataRecordListTests(unittest.TestCase):
    def test_observed_shapes_change_only_the_script_line(self):
        for row in CONFIGURATIONS:
            with self.subTest(owner=row["full_name"]):
                content = configuration(row)
                self.assertEqual((swapped(content, row), 1), rewrite_configuration_pointers(content, CONFIGURATIONS))
                self.assertEqual((swapped(content, row), 1), rewrite_prepared_script_pointers(content, CONFIGURATIONS))

    def test_empty_record_lists_are_accepted(self):
        for row, replace in ((GROUP, (GROUP_BODY, "  m_elements: []\n")),
                             (MAC, (MATCHES_BODY[MATCHES_BODY.index("  m_devicePerformanceMatches:"):], "  m_devicePerformanceMatches: []\n")),
                             (LOOKUP, (LOOKUP_BODY[LOOKUP_BODY.index("    m_values:"):], "    m_values: []\n"))):
            with self.subTest(owner=row["full_name"]):
                self.assertEqual(1, rewrite_configuration_pointers(configuration(row, replace), CONFIGURATIONS)[1])

    def test_record_members_are_exact_and_ordered(self):
        cases = (
            (GROUP, ("    Data: " + pointer(13) + "\n", "")),
            (GROUP, ("  - Name: DevicePerformance_iOS\n", "  - Label: DevicePerformance_iOS\n")),
            (LOOKUP, ("      m_value: " + pointer(11) + "\n", "      m_other: " + pointer(11) + "\n")),
            (GROUP, ("  - Name: DevicePerformance_iOS\n    Data: " + pointer(12) + "\n",
                     "  - Data: " + pointer(12) + "\n    Name: DevicePerformance_iOS\n")),
            (GROUP, ("    Data: " + pointer(12) + "\n", "    Data: " + pointer(12) + "\n    Extra: 1\n")),
            (GROUP, ("    Data: " + pointer(12) + "\n", "      Data: " + pointer(12) + "\n")),
            (GROUP, ("  m_elements:\n", "  m_elements: []\n")),
            (MAC, ("    m_deviceGeneration: 24\n", "    m_deviceGeneration: 4294967296\n")),
            (MAC, ("  - name: VeryLow\n", "  - name: 'VeryLow'\n")),
            (MAC, ("  - name: 79\n", "  - name: 7a-\n")),
            (MAC, ("  - name: 79\n", "  - name: -79\n")),
            (GROUP, ("  - Name: DevicePerformance_iOS\n", "  - Name: 79\n")),
            (MAC, ("    m_profile: " + pointer(15) + "\n", "    m_profile: {fileID: 2100000, guid: " + "a" * 32 + ", type: 2}\n")),
            (MAC, ("  m_fallback: " + pointer(14) + "\n", "  m_fallback:\n    fileID: 0\n")),
            (LOOKUP, ("      m_value: " + pointer(11) + "\n", "")),
            (LOOKUP, ("    m_values:\n", "    m_entries:\n")),
            (LOOKUP, ("    m_values:\n", "    m_values:\n    m_extra: 1\n")),
            (LOOKUP, ("    - m_key: 874239612\n", "  - m_key: 874239612\n")),
            (LOOKUP, ("  Dictionary:\n", "  Dictionary: []\n")),
            (LOOKUP, ("    - m_key: 874239612\n", "    - m_key: one\n")),
        )
        for row, replace in cases:
            with self.subTest(owner=row["full_name"], replace=replace):
                rejects(self, configuration(row, replace), CONFIGURATIONS)

    def test_a_duplicate_dictionary_key_is_rejected(self):
        rejects(self, configuration(LOOKUP, ("m_key: -2001650353", "m_key: 874239612")), CONFIGURATIONS)
        zero = configuration(LOOKUP, ("m_key: 874239612", "m_key: 0"))
        self.assertEqual(1, rewrite_configuration_pointers(zero, CONFIGURATIONS)[1])
        rejects(self, zero.replace(b"m_key: -2001650353", b"m_key: -0"), CONFIGURATIONS)
        # Device match names and generations are list data, not keys.
        same = configuration(MAC, ("  - name: iPadAir2\n", "  - name: VeryLow\n"))
        self.assertEqual(1, rewrite_configuration_pointers(same, CONFIGURATIONS)[1])

    def test_record_lists_belong_only_to_their_owner(self):
        # The same record block under an owner that does not profile it.
        other = binding("HLUnityCore.Runtime", "Hardlight.TimeCategoryConfiguration", 6)
        content = standalone(other, "TimeCategoryConfig", LOOKUP_BODY)
        rejects(self, content, [other])
        rejects(self, configuration(GROUP, ("  m_elements:\n", "  m_values:\n")), CONFIGURATIONS)


HIGHLIGHT = binding("HLModernUI.Runtime", "Hardlight.UIHighlight", 7)
DEFINITION = binding("HLModernUI.Runtime", "Hardlight.UIHighlightDefinition", 8)
HIGHLIGHT_BODY = ("  m_definition: " + pointer(9) + "\n  m_onShowHighlight:\n    m_PersistentCalls:\n      m_Calls:\n"
                  "      - m_Target: {fileID: 114243736805655561}\n"
                  "        m_TargetAssemblyTypeName: HardlightProject.HLAudioSfxComponent, Game.Runtime\n"
                  "        m_MethodName: PlayOneShotAtSource\n        m_Mode: 1\n        m_Arguments:\n"
                  "          m_ObjectArgument: {fileID: 0}\n          m_StringArgument:\n        m_CallState: 2\n")


def prefab(body=HIGHLIGHT_BODY):
    return (HEADER + "--- !u!1 &1709254077376921\nGameObject:\n  m_Name: uihighlight_default\n"
            "--- !u!114 &114884547391421755\nMonoBehaviour:\n" + COMPONENT_COMMON % (1709254077376921, HIGHLIGHT["exported_guid"]) +
            body + "--- !u!114 &114243736805655561\nMonoBehaviour:\n  m_audioClipIdentifier: -1045317302\n").encode()


class HighlightTests(unittest.TestCase):
    def test_prefab_component_changes_only_its_script_line(self):
        content = prefab()
        self.assertEqual((swapped(content, HIGHLIGHT), 1), rewrite_component_pointers(content, [HIGHLIGHT]))
        self.assertEqual((swapped(content, HIGHLIGHT), 1), rewrite_prepared_script_pointers(content, [HIGHLIGHT, DEFINITION]))

    def test_prefab_component_payload_outside_its_event_is_rejected(self):
        for body in (HIGHLIGHT_BODY.replace("  m_definition: " + pointer(9) + "\n", ""),
                     HIGHLIGHT_BODY + "  m_extra: 1\n",
                     HIGHLIGHT_BODY.replace("  m_definition: " + pointer(9) + "\n", "  m_definition:\n    fileID: 0\n"),
                     HIGHLIGHT_BODY.replace("  m_onShowHighlight:\n", "  m_onShowHighlight: 1\n")):
            with self.subTest(body=body):
                with self.assertRaises(ValueError):
                    rewrite_component_pointers(prefab(body), [HIGHLIGHT])

    def test_definition_takes_the_flat_scalar_path(self):
        content = standalone(DEFINITION, "uihighlight_default", "  m_guid: a80302bf7d1cf464aa4f6561e9bbc4e1\n"
                             "  m_prefab: {fileID: 114884547391421755, guid: " + "c" * 32 + ", type: 2}\n")
        self.assertEqual((swapped(content, DEFINITION), 1), rewrite_prepared_script_pointers(content, [HIGHLIGHT, DEFINITION]))
        nested = content.replace(b"  m_guid: a80302bf7d1cf464aa4f6561e9bbc4e1\n", b"  m_guid:\n    value: 1\n")
        with self.assertRaises(ValueError):
            rewrite_prepared_script_pointers(nested, [HIGHLIGHT, DEFINITION])


class BootDataRosterTests(unittest.TestCase):
    def test_roster_and_sources(self):
        # The separate authored Image rule and the authored TransformRotator rule (#554) each add one member to
        # the original 42-rule roster.
        image_identity = ("UnityEngine.UI", "UnityEngine.UI.Image")
        rotator_identity = ("Game.Runtime", "HardlightProject.TransformRotator")
        identities = [(rule["assembly"], rule["full_name"]) for rule in RULES]
        self.assertEqual(42, len([identity for identity in identities if identity not in (image_identity, rotator_identity)]))
        self.assertEqual([image_identity], [identity for identity in identities if identity == image_identity])
        self.assertEqual([rotator_identity], [identity for identity in identities if identity == rotator_identity])
        self.assertEqual(44, len(RULES))
        self.assertEqual(["HardlightProject.TimeCategoryLookup", "HardlightProject.DevicePerformanceGroup",
                          "HardlightProject.DevicePerformance_iOS", "HardlightProject.DevicePerformance_macOS",
                          "HardlightProject.DevicePerformance_tvOS", "Hardlight.UIHighlightDefinition", "Hardlight.UIHighlight"],
                         [rule["full_name"] for rule in BOOT_DATA_RULES])
        for rule in BOOT_DATA_RULES:
            # Each owner's own source is pinned, and so is the dictionary chain it stores.
            self.assertIn(rule["maintained_path"], rule["runtime_sources"])
            self.assertTrue(rule["export_path"].startswith("Assets/Scripts/" + rule["assembly"] + "/"))
        for relative in ("SerializableDictionary.cs", "SerializableDictionaryBase.cs", "SerializableKeyValuePair.cs"):
            self.assertIn("Packages/com.hardlight.hlunitycore/Runtime/FiniteStateMachine/Dependencies/" + relative,
                          BOOT_DATA_RULES[0]["runtime_sources"])


if __name__ == "__main__":
    unittest.main()
