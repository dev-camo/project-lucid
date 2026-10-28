"""Read-only reader of UnityFS (version 6 to 8) bundle headers and SerializedFile object tables, pure stdlib.

It exists to check the identity record against the input without AssetRipper (issue #552): the PathIDs
that the patched exporter reports for a serialized file must be in that file's own object table. Only the
parts needed for that are decoded: the bundle's block/node tables, the leading blocks of a node, and the
SerializedFile header, type table and object table. Nothing is executed and nothing is written.
Bounded: sizes are checked against the file and the limits below before any allocation.
"""

from __future__ import annotations

from pathlib import Path
import struct

from .shaders import decode_lz4_block

MAX_BLOCK_INFO = 64 * 1024 * 1024
MAX_BLOCKS = 1 << 20
MAX_OBJECTS = 1 << 24
MAX_TYPES = 1 << 16


class UnityFsError(ValueError):
    """The file is not a bundle or serialized file this reader understands."""


def _cstring(data: bytes, position: int) -> tuple[str, int]:
    end = data.find(b"\0", position)
    if end < 0:
        raise UnityFsError("unterminated string")
    return data[position:end].decode("utf-8", "replace"), end + 1


def _decode(block_flags: int, data: bytes, size: int) -> bytes:
    kind = block_flags & 0x3F
    if kind == 0:
        return data
    if kind in (2, 3):  # LZ4 and LZ4HC share the block format
        return decode_lz4_block(data, size, max_size=size)
    raise UnityFsError(f"unsupported compression {kind}")


class Bundle:
    """A UnityFS bundle opened for reading; `nodes` maps a node name to (offset, size) in the decoded stream."""

    def __init__(self, path: Path):
        self.path = Path(path)
        self.stream = self.path.open("rb")
        try:
            self._read_header()
        except BaseException:
            self.stream.close()
            raise

    def close(self) -> None:
        self.stream.close()

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        self.close()

    def _read_header(self) -> None:
        total = self.path.stat().st_size
        head = self.stream.read(256)
        if not head.startswith(b"UnityFS\0"):
            raise UnityFsError(f"not a UnityFS bundle: {self.path}")
        version = struct.unpack_from(">I", head, 8)[0]
        if version not in (6, 7, 8):
            raise UnityFsError(f"unsupported UnityFS version {version}")
        _, position = _cstring(head, 12)
        _, position = _cstring(head, position)
        size, compressed, uncompressed, flags = struct.unpack_from(">QIII", head, position)
        position += 20
        if size != total or compressed > total or uncompressed > MAX_BLOCK_INFO or flags & 0x100:
            raise UnityFsError(f"bad UnityFS header sizes in {self.path}")
        if flags & 0x80:  # block info at the end of the file
            self.stream.seek(total - compressed)
            info_raw = self.stream.read(compressed)
            data_start = position
        else:
            if version >= 7:
                position = (position + 15) & ~15
            self.stream.seek(position)
            info_raw = self.stream.read(compressed)
            data_start = position + compressed
        if version >= 7 and flags & 0x200 and not flags & 0x80:
            data_start = (data_start + 15) & ~15
        info = _decode(flags, info_raw, uncompressed)
        count = struct.unpack_from(">i", info, 16)[0]
        if not 0 <= count <= MAX_BLOCKS:
            raise UnityFsError("bad block count")
        self.blocks, cursor, offset = [], 20, data_start
        for _ in range(count):
            decoded, packed, block_flags = struct.unpack_from(">IIH", info, cursor)
            cursor += 10
            self.blocks.append((offset, packed, decoded, block_flags))
            offset += packed
        if offset > total:
            raise UnityFsError("blocks run past the end of the file")
        nodes = struct.unpack_from(">i", info, cursor)[0]
        cursor += 4
        self.nodes = {}
        for _ in range(nodes):
            node_offset, node_size, _flags = struct.unpack_from(">qqI", info, cursor)
            name, cursor = _cstring(info, cursor + 20)
            if name in self.nodes:
                raise UnityFsError(f"duplicate node {name}")
            self.nodes[name] = (node_offset, node_size)

    def read(self, offset: int, length: int) -> bytes:
        """Bytes [offset, offset+length) of the decoded stream, decoding only the blocks that cover them."""
        out, decoded_start = bytearray(), 0
        for file_offset, packed, decoded, block_flags in self.blocks:
            if decoded_start + decoded > offset and decoded_start < offset + length:
                self.stream.seek(file_offset)
                block = _decode(block_flags, self.stream.read(packed), decoded)
                low, high = max(offset - decoded_start, 0), min(offset + length - decoded_start, decoded)
                out += block[low:high]
            decoded_start += decoded
            if decoded_start >= offset + length:
                break
        if len(out) != length:
            raise UnityFsError("read past the end of the decoded stream")
        return bytes(out)


def object_table(read) -> dict[int, int]:
    """PathID -> ClassID (resolved through the type table) of a SerializedFile (version 17 to 22+); `read(offset, length)` returns file bytes."""
    head = read(0, 48)
    metadata_size, file_size, version, data_offset = struct.unpack_from(">IIII", head, 0)
    if not 17 <= version <= 30:
        raise UnityFsError(f"unsupported SerializedFile version {version}")
    endian = ">" if head[16] else "<"
    position = 20
    if version >= 22:
        metadata_size, file_size, data_offset, _ = struct.unpack_from(">IQQQ", head, 20)
        position = 48
    # Metadata sizes are bounded by what a bundle can hold; read the whole table block once.
    if not 0 < metadata_size < (1 << 28):
        raise UnityFsError("bad metadata size")
    data = read(position, min(metadata_size, 1 << 28))
    cursor = 0

    def take(fmt: str):
        nonlocal cursor
        values = struct.unpack_from(endian + fmt, data, cursor)
        cursor += struct.calcsize(endian + fmt)
        return values if len(values) > 1 else values[0]

    _, cursor = _cstring(data, cursor)  # unity version
    take("i")  # target platform
    tree = bool(take("B"))
    types = take("i")
    if not 0 <= types <= MAX_TYPES:
        raise UnityFsError("bad type count")
    class_ids = []
    for _ in range(types):
        class_id = take("i")
        class_ids.append(class_id)
        take("B")  # stripped
        take("h")  # script type index
        if class_id == 114:  # MonoBehaviour: script hash
            cursor += 16
        cursor += 16  # old type hash
        if tree:
            nodes, strings = take("ii")
            if nodes < 0 or strings < 0:
                raise UnityFsError("bad type tree")
            cursor += nodes * (32 if version >= 19 else 24) + strings
            if version >= 21:  # the type dependencies belong to the type tree block
                deps = take("i")
                if deps < 0:
                    raise UnityFsError("bad type dependencies")
                cursor += 4 * deps
    count = take("i")
    if not 0 <= count <= MAX_OBJECTS:
        raise UnityFsError("bad object count")
    objects = {}
    for _ in range(count):
        cursor = (cursor + 3) & ~3
        path_id = take("q")
        take("Q" if version >= 22 else "I")  # byte start
        take("I")  # byte size
        type_id = take("i")
        if path_id in objects:
            raise UnityFsError(f"duplicate PathID {path_id}")
        if not 0 <= type_id < len(class_ids):
            raise UnityFsError(f"bad type index {type_id}")
        objects[path_id] = class_ids[type_id]
    return objects
