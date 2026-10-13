"""Lossless operand inventory for explicit dyld-1378 legacy opcode grammars.

Reference: apple-oss-distributions/dyld at
fd8d0c4d52320ebf64db34f3cb280310d905c5ae, common/MachOAnalyzer.cpp and
mach_o/{BindOpcodes,RebaseOpcodes,Misc}.cpp. Numeric opcodes are the Apple
mach-o/loader.h constants. This reader accepts bytes; it never reads a file,
resolves a target, applies a fixup, or selects an original runtime family.

LEB operands use mathematical 64-bit bounds. These conservative analysis
bounds do not emulate the source's unchecked shifts, casts or arithmetic.
"""

from __future__ import annotations

from dataclasses import dataclass
import hashlib


@dataclass(frozen=True)
class Limits:
    max_bytes: int
    max_lexemes: int


@dataclass(frozen=True)
class Operand:
    name: str
    encoding: str
    start: int
    end: int
    raw: bytes
    value: int | None
    complete: bool


@dataclass(frozen=True)
class Lexeme:
    start: int
    end: int
    raw_opcode: int
    opcode: int
    immediate: int
    name: str | None
    operands: tuple[Operand, ...]
    complete: bool
    source_handler_precedes_operand: bool


@dataclass(frozen=True)
class Diagnostic:
    code: str
    offset: int
    detail: str


@dataclass(frozen=True)
class Inventory:
    grammar: str
    input_bytes: int
    input_sha256: str | None
    lexemes: tuple[Lexeme, ...]
    termination: str
    first_diagnostic: Diagnostic | None
    tail_start: int
    raw_tail: bytes | None


# Grammars name source entry points, not a detected game/runtime identity.
_GRAMMARS = {
    "dyld-1378-common-bind": ("bind", "stop"),
    "dyld-1378-common-weak-bind": ("bind", "stop"),
    "dyld-1378-common-lazy-bind": ("bind", "continue"),
    "dyld-1378-common-rebase": ("rebase", "stop-with-15-byte-tail"),
    "dyld-1378-mach-o-bind": ("bind", "stop"),
    "dyld-1378-mach-o-weak-bind": ("bind", "stop"),
    "dyld-1378-mach-o-lazy-bind": ("bind", "continue"),
    "dyld-1378-mach-o-rebase": ("rebase", "continue-with-15-byte-tail"),
}

_BIND = {
    0x00: ("BIND_OPCODE_DONE", ()),
    0x10: ("BIND_OPCODE_SET_DYLIB_ORDINAL_IMM", ()),
    0x20: ("BIND_OPCODE_SET_DYLIB_ORDINAL_ULEB", (("ordinal", "uleb128"),)),
    0x30: ("BIND_OPCODE_SET_DYLIB_SPECIAL_IMM", ()),
    0x40: ("BIND_OPCODE_SET_SYMBOL_TRAILING_FLAGS_IMM", (("symbol", "cstring"),)),
    0x50: ("BIND_OPCODE_SET_TYPE_IMM", ()),
    0x60: ("BIND_OPCODE_SET_ADDEND_SLEB", (("addend", "sleb128"),)),
    0x70: ("BIND_OPCODE_SET_SEGMENT_AND_OFFSET_ULEB", (("offset", "uleb128"),)),
    0x80: ("BIND_OPCODE_ADD_ADDR_ULEB", (("delta", "uleb128"),)),
    0x90: ("BIND_OPCODE_DO_BIND", ()),
    0xA0: ("BIND_OPCODE_DO_BIND_ADD_ADDR_ULEB", (("delta", "uleb128"),)),
    0xB0: ("BIND_OPCODE_DO_BIND_ADD_ADDR_IMM_SCALED", ()),
    0xC0: ("BIND_OPCODE_DO_BIND_ULEB_TIMES_SKIPPING_ULEB",
           (("count", "uleb128"), ("skip", "uleb128"))),
    0xD0: ("BIND_OPCODE_THREADED", ()),
}

_REBASE = {
    0x00: ("REBASE_OPCODE_DONE", ()),
    0x10: ("REBASE_OPCODE_SET_TYPE_IMM", ()),
    0x20: ("REBASE_OPCODE_SET_SEGMENT_AND_OFFSET_ULEB", (("offset", "uleb128"),)),
    0x30: ("REBASE_OPCODE_ADD_ADDR_ULEB", (("delta", "uleb128"),)),
    0x40: ("REBASE_OPCODE_ADD_ADDR_IMM_SCALED", ()),
    0x50: ("REBASE_OPCODE_DO_REBASE_IMM_TIMES", ()),
    0x60: ("REBASE_OPCODE_DO_REBASE_ULEB_TIMES", (("count", "uleb128"),)),
    0x70: ("REBASE_OPCODE_DO_REBASE_ADD_ADDR_ULEB", (("delta", "uleb128"),)),
    0x80: ("REBASE_OPCODE_DO_REBASE_ULEB_TIMES_SKIPPING_ULEB",
           (("count", "uleb128"), ("skip", "uleb128"))),
}


def _operand(data: bytes, start: int, name: str, encoding: str):
    if encoding == "cstring":
        nul = data.find(b"\0", start)
        end = len(data) if nul < 0 else nul + 1
        item = Operand(name, encoding, start, end, data[start:end], None, nul >= 0)
        problem = None if nul >= 0 else Diagnostic(
            "unterminated-symbol", start, "No NUL within the admitted byte span.")
        return item, problem

    pos = start
    value = 0
    for index in range(10):
        if pos == len(data):
            return Operand(name, encoding, start, pos, data[start:pos], None, False), Diagnostic(
                "truncated-leb", pos, "Operand continuation reaches the admitted span end.")
        byte = data[pos]
        pos += 1
        value |= (byte & 0x7F) << (index * 7)
        if byte & 0x80:
            continue
        if encoding == "sleb128" and byte & 0x40:
            value -= 1 << ((index + 1) * 7)
        lower, upper = (-(1 << 63), (1 << 63) - 1) if encoding == "sleb128" else (0, (1 << 64) - 1)
        if not lower <= value <= upper:
            return Operand(name, encoding, start, pos, data[start:pos], None, False), Diagnostic(
                "leb-outside-analysis-range", start,
                "Mathematical value exceeds the selected 64-bit analysis domain.")
        return Operand(name, encoding, start, pos, data[start:pos], value, True), None
    return Operand(name, encoding, start, pos, data[start:pos], None, False), Diagnostic(
        "leb-exceeds-analysis-width", start,
        "Continuation after ten bytes is held; source undefined/truncating behavior is not emulated.")


def inventory(data: bytes, *, grammar: str, limits: Limits) -> Inventory:
    """Inventory operand bytes under one explicit, versioned source grammar.

    EOF and DONE describe lexical traversal only. No result establishes
    semantic validity, segment geometry, write effects, or original ABI.
    Unsupported or malformed input retains its prefix and first diagnostic.
    Bytes rejected by the admission budget are not examined or duplicated.
    """
    if not isinstance(data, bytes):
        raise TypeError("data must be immutable bytes")
    if grammar not in _GRAMMARS:
        raise ValueError("an explicit supported source grammar is required")
    if (type(limits.max_bytes) is not int or limits.max_bytes < 0
            or type(limits.max_lexemes) is not int or limits.max_lexemes < 1):
        raise ValueError("limits require nonnegative bytes and positive lexemes")
    if len(data) > limits.max_bytes:
        return Inventory(grammar, len(data), None, (), "held",
                         Diagnostic("byte-budget", 0, "Input exceeds the caller's byte admission."),
                         0, None)

    kind, done_policy = _GRAMMARS[grammar]
    definitions = _BIND if kind == "bind" else _REBASE
    digest = hashlib.sha256(data).hexdigest()
    records = []
    pos = 0

    def finish(termination, problem=None):
        return Inventory(grammar, len(data), digest, tuple(records), termination,
                         problem, pos, data[pos:])

    while pos < len(data):
        if len(records) == limits.max_lexemes:
            return finish("held", Diagnostic("lexeme-budget", pos,
                                             "Next opcode exceeds the caller's lexeme admission."))
        start = pos
        raw_opcode = data[pos]
        pos += 1
        opcode, immediate = raw_opcode & 0xF0, raw_opcode & 0x0F
        definition = definitions.get(opcode)
        name, operands = definition if definition is not None else (None, ())
        problem = None
        if definition is None:
            problem = Diagnostic("unknown-opcode", start, "No opcode entry in this source grammar.")
        elif kind == "bind" and opcode == 0xD0:
            problem = Diagnostic("threaded-held", start,
                                 "Threaded binding is not admitted by this lexical reader.")
        elif grammar == "dyld-1378-common-weak-bind" and opcode in (0x10, 0x20, 0x30):
            problem = Diagnostic("source-grammar-rejection", start,
                                 "The common weak-bind switch rejects dylib ordinal opcodes.")
        elif grammar == "dyld-1378-common-lazy-bind" and opcode in (0x50, 0x80, 0xA0, 0xB0, 0xC0):
            problem = Diagnostic("source-grammar-rejection", start,
                                 "The common lazy-bind switch rejects this opcode before its operand.")
        parsed = []
        handler_first = problem is None and ((kind == "bind" and opcode == 0xA0)
                                              or (kind == "rebase" and opcode == 0x70))
        if problem is None:
            for operand_name, encoding in operands:
                item, problem = _operand(data, pos, operand_name, encoding)
                parsed.append(item)
                pos = item.end
                if problem is not None:
                    break
        records.append(Lexeme(start, pos, raw_opcode, opcode, immediate, name,
                              tuple(parsed), problem is None, handler_first))
        if problem is not None:
            return finish("held", problem)
        if opcode == 0:
            if "15-byte-tail" in done_policy and len(data) - pos > 15:
                return finish("held", Diagnostic("source-done-tail-limit", pos,
                                                 "This source DONE branch rejects more than fifteen remaining bytes."))
            if done_policy.startswith("stop"):
                return finish("done")
    return finish("eof")
