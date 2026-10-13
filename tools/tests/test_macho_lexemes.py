import unittest

from lucidlib.macho_lexemes import Limits, inventory


COMMON = "dyld-1378-common-"
MODERN = "dyld-1378-mach-o-"


class LegacyOperandInventoryTests(unittest.TestCase):
    def read(self, data, grammar=COMMON + "bind", *, max_lexemes=32):
        return inventory(data, grammar=grammar,
                         limits=Limits(max_bytes=len(data), max_lexemes=max_lexemes))

    def assert_lossless(self, data, result):
        retained = b"".join(bytes([row.raw_opcode]) + b"".join(op.raw for op in row.operands)
                            for row in result.lexemes) + result.raw_tail
        self.assertEqual(retained, data)
        cursor = 0
        for row in result.lexemes:
            self.assertEqual(row.start, cursor)
            cursor += 1
            for op in row.operands:
                self.assertEqual(op.start, cursor)
                cursor += len(op.raw)
                self.assertEqual(op.end, cursor)
            self.assertEqual(row.end, cursor)
        self.assertEqual(result.tail_start, cursor)

    def test_done_uses_distinct_source_traversal_and_keeps_opaque_tail(self):
        data = b"\x00\xff"
        for suffix in ("bind", "weak-bind", "rebase"):
            result = self.read(data, COMMON + suffix)
            self.assertEqual(result.termination, "done")
            self.assertEqual(result.raw_tail, b"\xff")
            self.assert_lossless(data, result)
        result = self.read(data, MODERN + "rebase")
        self.assertEqual(result.termination, "held")
        self.assertEqual(result.first_diagnostic.code, "unknown-opcode")
        self.assertEqual([row.raw_opcode for row in result.lexemes], [0, 255])
        self.assert_lossless(data, result)

    def test_rebase_done_fifteen_byte_boundary_is_not_a_zero_padding_rule(self):
        for family in (COMMON, MODERN):
            accepted = b"\x00" + b"\x10" * 15
            result = self.read(accepted, family + "rebase")
            self.assertIsNone(result.first_diagnostic)
            self.assert_lossless(accepted, result)
            held = accepted + b"\x10"
            result = self.read(held, family + "rebase")
            self.assertEqual(result.first_diagnostic.code, "source-done-tail-limit")
            self.assertEqual(len(result.raw_tail), 16)
            self.assert_lossless(held, result)

    def test_lazy_done_continues_and_eof_does_not_require_done(self):
        data = b"\x00\x70\x01\x90\x00\x90"
        for family in (COMMON, MODERN):
            result = self.read(data, family + "lazy-bind")
            self.assertEqual(result.termination, "eof")
            self.assertEqual([row.raw_opcode for row in result.lexemes], [0, 112, 144, 0, 144])
            self.assert_lossless(data, result)
        for family in (COMMON, MODERN):
            self.assertEqual(self.read(b"\x90", family + "bind").termination, "eof")

    def test_family_rejections_stop_before_operands(self):
        for opcode in (0x10, 0x20, 0x30):
            data = bytes([opcode, 1])
            common = self.read(data, COMMON + "weak-bind")
            self.assertEqual(common.first_diagnostic.code, "source-grammar-rejection")
            self.assertEqual(common.lexemes[0].operands, ())
            self.assertEqual(common.raw_tail, b"\x01")
            self.assert_lossless(data, common)
            self.assertNotEqual(self.read(data, MODERN + "weak-bind").first_diagnostic.code
                                if self.read(data, MODERN + "weak-bind").first_diagnostic else None,
                                "source-grammar-rejection")
        for opcode in (0x50, 0x80, 0xA0, 0xB0, 0xC0):
            data = bytes([opcode, 0x80])
            result = self.read(data, COMMON + "lazy-bind")
            self.assertEqual(result.first_diagnostic.code, "source-grammar-rejection")
            self.assertEqual(result.lexemes[0].operands, ())
            self.assertFalse(result.lexemes[0].source_handler_precedes_operand)
            self.assert_lossless(data, result)

    def test_math_leb_extremes_and_representable_nonminimal_encodings(self):
        cases = (
            (b"\x20\x80\x00", 0),
            (b"\x20" + b"\xff" * 9 + b"\x01", (1 << 64) - 1),
            (b"\x60\xff\x7f", -1),
            (b"\x60\xbf\x7f", -65),
            (b"\x60" + b"\xff" * 9 + b"\x00", (1 << 63) - 1),
            (b"\x60" + b"\x80" * 9 + b"\x7f", -(1 << 63)),
        )
        for data, value in cases:
            with self.subTest(data=data.hex()):
                result = self.read(data)
                self.assertIsNone(result.first_diagnostic)
                self.assertEqual(result.lexemes[0].operands[0].value, value)
                self.assert_lossless(data, result)

    def test_leb_range_width_and_truncation_retain_first_cause(self):
        for opcode, final in ((0x20, 2), (0x60, 1)):
            data = bytes([opcode]) + b"\x80" * 9 + bytes([final]) + b"\xff"
            result = self.read(data)
            self.assertEqual(result.first_diagnostic.code, "leb-outside-analysis-range")
            self.assertIsNone(result.lexemes[0].operands[0].value)
            self.assertEqual(result.raw_tail, b"\xff")
            self.assert_lossless(data, result)
        data = b"\x20" + b"\x80" * 10 + b"\x00"
        result = self.read(data)
        self.assertEqual(result.first_diagnostic.code, "leb-exceeds-analysis-width")
        self.assertEqual(result.raw_tail, b"\x00")
        self.assert_lossless(data, result)
        data = b"\xc0\x01\x80"
        result = self.read(data)
        self.assertEqual(result.first_diagnostic.code, "truncated-leb")
        self.assertEqual(result.lexemes[0].operands[0].value, 1)
        self.assertFalse(result.lexemes[0].operands[1].complete)
        self.assert_lossless(data, result)

    def test_symbol_is_raw_nul_terminated_bytes(self):
        data = b"\x49\xff\x80\x00\x90"
        result = self.read(data)
        self.assertEqual(result.lexemes[0].immediate, 9)
        self.assertEqual(result.lexemes[0].operands[0].raw, b"\xff\x80\x00")
        self.assertIsNone(result.lexemes[0].operands[0].value)
        self.assert_lossless(data, result)
        data = b"\x40\xff\x80"
        result = self.read(data)
        self.assertEqual(result.first_diagnostic.code, "unterminated-symbol")
        self.assertEqual(result.lexemes[0].operands[0].raw, b"\xff\x80")
        self.assert_lossless(data, result)

    def test_source_handler_order_is_retained_without_applying_an_event(self):
        for data, grammar in ((b"\xa0\x80", COMMON + "bind"),
                              (b"\x70\x80", MODERN + "rebase")):
            result = self.read(data, grammar)
            self.assertEqual(result.first_diagnostic.code, "truncated-leb")
            self.assertTrue(result.lexemes[0].source_handler_precedes_operand)
            self.assertFalse(result.lexemes[0].complete)
            self.assert_lossless(data, result)

    def test_threaded_and_unknown_opcodes_keep_the_unsupported_suffix(self):
        for data, code in ((b"\xd1\x80\x00", "threaded-held"),
                           (b"\xf7\x90", "unknown-opcode")):
            result = self.read(data)
            self.assertEqual(result.first_diagnostic.code, code)
            self.assertEqual(result.lexemes[0].raw_opcode, data[0])
            self.assertEqual(result.lexemes[0].operands, ())
            self.assert_lossless(data, result)

    def test_admission_budgets_stop_without_examining_unadmitted_bytes(self):
        data = b"\x10\x11"
        result = inventory(data, grammar=COMMON + "bind", limits=Limits(1, 10))
        self.assertEqual(result.first_diagnostic.code, "byte-budget")
        self.assertIsNone(result.input_sha256)
        self.assertIsNone(result.raw_tail)
        result = self.read(data, max_lexemes=1)
        self.assertEqual(result.first_diagnostic.code, "lexeme-budget")
        self.assertEqual(result.raw_tail, b"\x11")
        self.assert_lossless(data, result)

    def test_all_single_bytes_are_lossless_under_each_explicit_grammar(self):
        for family in (COMMON, MODERN):
            for suffix in ("bind", "weak-bind", "lazy-bind", "rebase"):
                for value in range(256):
                    data = bytes([value])
                    self.assert_lossless(data, self.read(data, family + suffix))

    def test_no_implicit_grammar_or_mutable_input_is_admitted(self):
        with self.assertRaises(ValueError):
            self.read(b"", "original")
        with self.assertRaises(TypeError):
            self.read(bytearray(b"\x90"))
        for limits in (Limits(-1, 1), Limits(1, 0), Limits(True, 1)):
            with self.assertRaises(ValueError):
                inventory(b"", grammar=COMMON + "bind", limits=limits)


if __name__ == "__main__":
    unittest.main()
