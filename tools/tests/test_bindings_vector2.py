"""Pure literal Vector2 metadata cases; no runtime/provider substitution."""
import copy
import unittest
from unittest.mock import patch

import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib.bindings import LayoutError, _Comparison, compare_layouts
import test_bindings as base_tests
from test_bindings import attribute, field, named, record


def vector2_record():
    def completed(name, reference, flags, default=None):
        value = field(name, reference, flags=flags)
        value.update(has_default_value=bool(flags & 64), default_value_complete=True,
                     default_value=default, default_value_type=named('mscorlib', 'System.Single') if flags & 64 else None)
        return value
    fields = [completed(name, named('mscorlib', 'System.Single'), 6) for name in ('x', 'y')]
    fields += [completed(name, named('UnityEngine.CoreModule', 'UnityEngine.Vector2'), 49) for name in
               ('zeroVector', 'oneVector', 'upVector', 'downVector', 'leftVector', 'rightVector', 'positiveInfinityVector', 'negativeInfinityVector')]
    fields += [completed('kEpsilon', named('mscorlib', 'System.Single'), 32854, 1e-5),
               completed('kEpsilonNormalSqrt', named('mscorlib', 'System.Single'), 32854, 1e-15)]
    result = record('UnityEngine.CoreModule', 'UnityEngine.Vector2', fields,
                    named('mscorlib', 'System.ValueType'), flags=0x100109)
    result.update(namespace='UnityEngine', name='Vector2', is_value_type=True, enum_underlying_type=None)
    def primitive(kind, value): return {'kind': 'primitive', 'type': 'IL2CPP_TYPE_' + kind, 'value': value}
    def cad(assembly, name, arguments=(), properties=()):
        return {'assembly': assembly, 'full_name': name, 'arguments': list(arguments), 'fields': [], 'properties': list(properties)}
    result['custom_attributes'] = [
        cad('UnityEngine.SharedInternalsModule', 'UnityEngine.NativeClassAttribute', [primitive('STRING', 'Vector2f')]),
        cad('UnityEngine.CoreModule', 'Unity.IL2CPP.CompilerServices.Il2CppEagerStaticClassConstructionAttribute'),
        cad('UnityEngine.SharedInternalsModule', 'UnityEngine.Scripting.RequiredByNativeCodeAttribute', properties=[
            {'name': 'Optional', 'value': primitive('BOOLEAN', True)}, {'name': 'GenerateProxy', 'value': primitive('BOOLEAN', True)}]),
        cad('mscorlib', 'System.Reflection.DefaultMemberAttribute', [primitive('STRING', 'Item')])]
    return result


class Vector2BoundaryComparisonTests(unittest.TestCase):
    key = ('UnityEngine.CoreModule', 'UnityEngine.Vector2')

    def fixture(self):
        original, loaded = base_tests.BindingTests().fixture()
        for report in (original, loaded):
            report['assemblies'][0]['types'][0]['fields'] = [field('position', named(*self.key))]
            report['assemblies'][1]['types'].append(vector2_record())
        return original, loaded

    def vector(self, report): return report['assemblies'][1]['types'][-1]

    def result(self, original, loaded): return compare_layouts(original, loaded)['candidates'][0]

    def test_complete_Vector2_is_non_pointer_and_separately_builtin_eligible(self):
        original, loaded = self.fixture(); before = copy.deepcopy((original, loaded))
        comparison = _Comparison(original, loaded)
        self.assertFalse(comparison._unity_object(self.key))
        self.assertTrue(comparison._unity_2022_builtin_value(self.key))
        self.assertEqual('compatible_layout', self.result(original, loaded)['status'])
        self.assertEqual(before, (original, loaded))
        self.assertFalse(self.vector(original)['attributes'] & 0x2000)
        self.assertFalse(self.result(original, loaded)['remap_eligible'])

    def test_negative_pointer_classification_alone_does_not_establish_serialization(self):
        original, loaded = self.fixture()
        with patch.object(_Comparison, '_unity_2022_builtin_value', return_value=False):
            result = self.result(original, loaded)
        self.assertEqual('blocked', result['status'])
        self.assertEqual(['field_type_eligibility_unverified'], [issue['reason'] for issue in result['issues']])

    def test_missing_incomplete_shape_flags_and_scoped_base_fail_closed_both_graphs(self):
        for side in (0, 1):
            for change in ('missing', 'incomplete', 'CAD-incomplete', 'flags', 'Serializable', 'reference-class', 'enum',
                           'abstract', 'open', 'component', 'scriptable', 'declaring', 'missing-declaring',
                           'missing-enum-underlying', 'namespace', 'name', 'base-scope', 'base-kind'):
                with self.subTest(side=side, change=change):
                    original, loaded = self.fixture(); selected = (original, loaded)[side]; vector = self.vector(selected)
                    if change == 'missing': selected['assemblies'][1]['types'].pop()
                    elif change == 'incomplete': vector['schema_complete'] = False
                    elif change == 'CAD-incomplete': vector['custom_attributes_complete'] = False
                    elif change == 'flags': vector['attributes'] ^= 8
                    elif change == 'Serializable':
                        for graph in (original, loaded): self.vector(graph)['attributes'] |= 0x2000
                    elif change == 'reference-class': vector['is_value_type'] = False
                    elif change == 'enum': vector['is_enum'] = True
                    elif change == 'abstract': vector['is_abstract'] = True
                    elif change == 'open': vector['generic_parameters'] = [{'name': 'T'}]
                    elif change == 'component': vector['unity_component'] = True
                    elif change == 'scriptable': vector['unity_scriptable_object'] = True
                    elif change == 'declaring': vector['declaring_type'] = 'UnityEngine.Outer'
                    elif change == 'missing-declaring': del vector['declaring_type']
                    elif change == 'missing-enum-underlying': del vector['enum_underlying_type']
                    elif change == 'namespace': vector['namespace'] = 'Other'
                    elif change == 'name': vector['name'] = 'Other'
                    elif change == 'base-scope': vector['base_type']['assembly'] = 'Other'
                    elif change == 'base-kind': vector['base_type'] = {'kind': 'generic_instance', 'assembly': 'mscorlib',
                        'definition': 'System.ValueType', 'arguments': [named('mscorlib', 'System.Single')]}
                    self.assertEqual('blocked', self.result(original, loaded)['status'])

    def test_complete_private_static_constants_and_payload_remain_checked(self):
        for side in (0, 1):
            for change in ('xy-order', 'x-scope', 'x-type', 'x-flags', 'x-default', 'private-missing', 'private-order',
                           'private-readonly', 'private-type', 'constant', 'constant-incomplete', 'constant-type', 'field-CAD',
                           'missing-default', 'missing-default-type'):
                with self.subTest(side=side, change=change):
                    original, loaded = self.fixture(); vector = self.vector((original, loaded)[side]); fields = vector['fields']
                    if change == 'xy-order': fields[0], fields[1] = fields[1], fields[0]
                    elif change == 'x-scope': fields[0]['field_type']['assembly'] = 'Other'
                    elif change == 'x-type': fields[0]['field_type'] = named('mscorlib', 'System.Double')
                    elif change == 'x-flags': fields[0] = field('x', named('mscorlib', 'System.Single'), flags=1)
                    elif change == 'x-default': fields[0]['default_value_complete'] = False
                    elif change == 'private-missing': fields.pop(2)
                    elif change == 'private-order': fields[2], fields[3] = fields[3], fields[2]
                    elif change == 'private-readonly': fields[2]['is_readonly'] = False
                    elif change == 'private-type': fields[2]['field_type'] = named('UnityEngine.CoreModule', 'UnityEngine.Vector3')
                    elif change == 'constant': fields[-1]['default_value'] = 2e-15
                    elif change == 'constant-incomplete': fields[-1]['default_value_complete'] = False
                    elif change == 'constant-type': fields[-1]['default_value_type']['assembly'] = 'Other'
                    elif change == 'field-CAD': fields[2]['custom_attributes'] = [attribute('Example.ChangedAttribute')]
                    elif change == 'missing-default': del fields[0]['default_value']
                    elif change == 'missing-default-type': del fields[0]['default_value_type']
                    self.assertEqual('blocked', self.result(original, loaded)['status'])

    def test_type_CAD_and_named_argument_order_are_not_projected(self):
        for change in ('order', 'named-order', 'value', 'assembly', 'integer-Boolean'):
            with self.subTest(change=change):
                original, loaded = self.fixture()
                for report in (original, loaded):
                    attrs = self.vector(report)['custom_attributes']
                    if change == 'order': attrs.reverse()
                    elif change == 'named-order': attrs[2]['properties'].reverse()
                    elif change == 'value': attrs[0]['arguments'][0]['value'] = 'Other'
                    elif change == 'assembly': attrs[0]['assembly'] = 'Other'
                    elif change == 'integer-Boolean': attrs[2]['properties'][0]['value']['value'] = 1
                self.assertEqual('blocked', self.result(original, loaded)['status'])

    def test_lookalike_scopes_and_other_value_structs_are_not_builtin_admissions(self):
        for change in ('foreign-scope', 'other-struct'):
            with self.subTest(change=change):
                original, loaded = self.fixture()
                for report in (original, loaded):
                    if change == 'foreign-scope':
                        vector = self.vector(report); report['assemblies'][1]['types'].pop(); vector['assembly'] = 'Other'
                        report['assemblies'].append({'name': 'Other', 'types': [vector]})
                        report['assemblies'][0]['types'][0]['fields'][0]['field_type'] = named('Other', 'UnityEngine.Vector2')
                    else:
                        vector = self.vector(report); vector['full_name'] = 'UnityEngine.Vector3'; vector['name'] = 'Vector3'
                        report['assemblies'][0]['types'][0]['fields'][0]['field_type'] = named('UnityEngine.CoreModule', 'UnityEngine.Vector3')
                self.assertEqual('blocked', self.result(original, loaded)['status'])

    def test_other_matching_Editor_versions_do_not_inherit_builtin_rule(self):
        original, loaded = self.fixture()
        for report in (original, loaded): report['unity_version'] = '2023.1.0f1'
        comparison = _Comparison(original, loaded)
        self.assertFalse(comparison._unity_object(self.key))
        self.assertFalse(comparison._unity_2022_builtin_value(self.key))
        self.assertEqual('blocked', self.result(original, loaded)['status'])

    def test_malformed_actual_Object_ancestry_remains_rejected(self):
        for side in (0, 1):
            for change in ('value', 'enum', 'interface', 'missing-base', 'open', 'cycle', 'Vector2-base'):
                with self.subTest(side=side, change=change):
                    original, loaded = self.fixture()
                    for report in (original, loaded):
                        report['assemblies'][0]['types'][0]['fields'] = [field('pointer', named('UnityEngine.CoreModule', 'UnityEngine.MonoBehaviour'))]
                    root = (original, loaded)[side]['assemblies'][1]['types'][1]
                    if change == 'value': root['is_value_type'] = True
                    elif change == 'enum': root['is_enum'] = True
                    elif change == 'interface': root['attributes'] |= 0x20
                    elif change == 'missing-base': root['base_type'] = None
                    elif change == 'open': root['generic_parameters'] = [{'name': 'T'}]
                    elif change == 'cycle': root['base_type'] = named('UnityEngine.CoreModule', 'UnityEngine.MonoBehaviour')
                    elif change == 'Vector2-base': root['base_type'] = named(*self.key)
                    self.assertEqual('blocked', self.result(original, loaded)['status'])

    def test_Vector2_field_managed_reference_and_identity_guards_remain(self):
        for change in ('managed-reference', 'one-sided-scope'):
            with self.subTest(change=change):
                original, loaded = self.fixture()
                if change == 'managed-reference':
                    for report in (original, loaded):
                        report['assemblies'][0]['types'][0]['fields'][0] = field('position', named(*self.key),
                            attributes=[attribute('UnityEngine.SerializeReference')])
                else: loaded['assemblies'][0]['types'][0]['fields'][0]['field_type']['assembly'] = 'Other'
                self.assertEqual('blocked', self.result(original, loaded)['status'])

    def test_matching_reference_class_with_Vector2_ancestry_is_not_negative_classified(self):
        original, loaded = self.fixture()
        for report in (original, loaded):
            report['assemblies'][1]['types'][0]['base_type'] = named(*self.key)
        with self.assertRaisesRegex(LayoutError, 'not a reference class'):
            _Comparison(original, loaded)._unity_object(('UnityEngine.CoreModule', 'UnityEngine.MonoBehaviour'))
