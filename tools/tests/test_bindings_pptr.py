"""Pure declaration reports test PPtr boundaries; no Unity/provider construction."""
import copy
import json
import unittest

import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib.bindings import compare_layouts
import test_bindings as base_tests
from test_bindings import attribute, field, named, record


class PPtrBoundaryComparisonTests(unittest.TestCase):
    def fixture(self):
        original, loaded = base_tests.BindingTests().fixture()
        for report in (original, loaded):
            # Test-owned report records, never replacement runtime providers.
            report['assemblies'][0]['types'][0]['fields'] = [field('button', named('Example', 'Example.Button'))]
            report['assemblies'][0]['types'].extend([
                record('Example', 'Example.Button', [field('marker')], named('Example', 'Example.PayloadBase'), True),
                record('Example', 'Example.PayloadBase', [field('callback', {
                    'kind': 'generic_instance', 'assembly': 'UnityEngine.CoreModule',
                    'definition': 'UnityEngine.Events.UnityEvent`1',
                    'arguments': [named('mscorlib', 'System.Boolean')]})],
                    named('UnityEngine.CoreModule', 'UnityEngine.MonoBehaviour'), True)])
        return original, loaded

    @staticmethod
    def by_name(report, assembly, name):
        return next(t for a in report['assemblies'] if a['name'] == assembly
                    for t in a['types'] if t['full_name'] == name)

    @staticmethod
    def candidate(original, loaded, name='Example.Widget'):
        return next(c for c in compare_layouts(original, loaded)['candidates'] if c['full_name'] == name)

    def add_button_script(self, loaded):
        loaded['monoscripts'].append(dict(loaded['monoscripts'][0], full_name='Example.Button',
            guid='d' * 32, path='Assets/Scripts/Button.cs'))

    def test_same_scoped_pointer_does_not_inline_inherited_generic_payload(self):
        original, loaded = self.fixture()
        before = json.dumps([original, loaded], sort_keys=True)
        self.assertEqual('compatible_layout', self.candidate(original, loaded)['status'])
        self.assertEqual(before, json.dumps([original, loaded], sort_keys=True))
        self.assertFalse(self.candidate(original, loaded)['remap_eligible'])

    def test_changed_payload_is_a_separate_pointed_owner_layout_obligation(self):
        original, loaded = self.fixture()
        payload = self.by_name(loaded, 'Example', 'Example.PayloadBase')
        payload['fields'][0]['field_type']['arguments'][0] = named('mscorlib', 'System.Int32')
        self.add_button_script(loaded)
        result = compare_layouts(original, loaded)
        self.assertEqual('compatible_layout', next(c for c in result['candidates'] if c['full_name'] == 'Example.Widget')['status'])
        pointed = next(c for c in result['candidates'] if c['full_name'] == 'Example.Button')
        self.assertEqual('blocked', pointed['status'])
        self.assertIn('type_identity_mismatch', [i['reason'] for i in pointed['issues']])
        self.assertFalse(result['references_modified'])
        self.assertFalse(result['managed_semantics_verified'])

    def test_pointer_identity_and_ancestry_assembly_scopes_never_fuzzy_match(self):
        for change in ('field-scope', 'base-scope', 'lookalike-root'):
            with self.subTest(change=change):
                original, loaded = self.fixture()
                if change == 'field-scope':
                    self.by_name(loaded, 'Example', 'Example.Widget')['fields'][0]['field_type']['assembly'] = 'Other'
                elif change == 'base-scope':
                    self.by_name(loaded, 'Example', 'Example.PayloadBase')['base_type']['assembly'] = 'Other'
                else:
                    for report in (original, loaded):
                        self.by_name(report, 'Example', 'Example.PayloadBase')['base_type'] = named('Other', 'UnityEngine.Object')
                        report['assemblies'].append({'name': 'Other', 'types': [record('Other', 'UnityEngine.Object', base=named('mscorlib', 'System.Object'))]})
                self.assertEqual('blocked', self.candidate(original, loaded)['status'])

    def test_missing_incomplete_cyclic_and_open_ancestry_fail_closed_on_both_graphs(self):
        for side in ('original', 'loaded'):
            for change in ('missing', 'incomplete', 'cycle', 'generic-base', 'open-owner', 'root-base'):
                with self.subTest(side=side, change=change):
                    original, loaded = self.fixture();report = original if side == 'original' else loaded
                    base = self.by_name(report, 'Example', 'Example.PayloadBase')
                    root = self.by_name(report, 'UnityEngine.CoreModule', 'UnityEngine.Object')
                    if change == 'missing': report['assemblies'][1]['types'].remove(root)
                    elif change == 'incomplete': root['schema_complete'] = False
                    elif change == 'cycle': base['base_type'] = named('Example', 'Example.Button')
                    elif change == 'generic-base': base['base_type'] = {'kind': 'generic_instance', 'assembly': 'Example', 'definition': 'Example.Unknown`1', 'arguments': [named('mscorlib', 'System.Int32')]}
                    elif change == 'open-owner': base['generic_parameters'] = [{'name': 'T', 'attributes': 0, 'constraints': []}]
                    elif change == 'root-base': root['base_type'] = None
                    self.assertEqual('blocked', self.candidate(original, loaded)['status'])

    def test_full_ancestry_metadata_attributes_shape_and_declared_fields_stay_required(self):
        for change in ('type-attributes', 'field-attributes', 'field-type', 'type-flags', 'missing-shape', 'root-interface'):
            with self.subTest(change=change):
                original, loaded = self.fixture();base = self.by_name(loaded, 'Example', 'Example.PayloadBase')
                if change == 'type-attributes': base['custom_attributes'] = [attribute('Example.ChangedAttribute')]
                elif change == 'field-attributes': base['fields'][0]['custom_attributes_complete'] = False
                elif change == 'field-type': base['fields'][0]['field_type'] = {'kind': 'named', 'assembly': 'Example', 'reflection_full_name': 'Wrong', 'canonical_name': 'Different'}
                elif change == 'type-flags': base['attributes'] |= 0x2000
                elif change == 'missing-shape': del base['unity_component']
                elif change == 'root-interface': self.by_name(loaded, 'UnityEngine.CoreModule', 'UnityEngine.Object')['attributes'] |= 0x20
                self.assertEqual('blocked', self.candidate(original, loaded)['status'])

    def test_pointer_field_flags_and_managed_reference_guards_remain_strict(self):
        for change in ('flags', 'managed-reference'):
            with self.subTest(change=change):
                original, loaded = self.fixture()
                if change == 'flags':
                    self.by_name(loaded, 'Example', 'Example.Widget')['fields'][0] = field('button', named('Example', 'Example.Button'), flags=1, attributes=[attribute('UnityEngine.SerializeField')])
                else:
                    for report in (original, loaded):
                        self.by_name(report, 'Example', 'Example.Widget')['fields'][0] = field('button', named('Example', 'Example.Button'), attributes=[attribute('UnityEngine.SerializeReference')])
                self.assertEqual('blocked', self.candidate(original, loaded)['status'])

    def test_complete_private_root_field_differences_are_retained_without_inline_eligibility(self):
        original, loaded = self.fixture()
        root = self.by_name(loaded, 'UnityEngine.CoreModule', 'UnityEngine.Object')
        root['fields'].append(field('privateEditorOnlyData', named('mscorlib', 'System.Int32'), flags=1))
        root['token'] = '0x02000999'
        before = copy.deepcopy([original, loaded])
        self.assertEqual('compatible_layout', self.candidate(original, loaded)['status'])
        self.assertEqual(before, [original, loaded])
        self.assertEqual('0x02000999', root['token'])

    def test_non_Unity_serializable_data_still_compares_its_inline_payload(self):
        original, loaded = self.fixture()
        for report in (original, loaded):
            button = self.by_name(report, 'Example', 'Example.Button')
            button.update(base_type=named('mscorlib', 'System.Object'), unity_component=False, attributes=0x2001)
        self.by_name(loaded, 'Example', 'Example.Button')['fields'][0]['field_type'] = named('mscorlib', 'System.String')
        self.assertEqual('blocked', self.candidate(original, loaded)['status'])
        self.assertIn('type_identity_mismatch', [i['reason'] for i in self.candidate(original, loaded)['issues']])
