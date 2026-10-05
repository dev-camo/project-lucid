"""Normalize a narrowly identified AssetRipper PCM WAV header defect."""

import hashlib
import os
from pathlib import Path
import shutil
import struct
import tempfile


def repair_pcm_header(path):
    """Fix zero RIFF/data lengths only in canonical, complete PCM exports.

    AssetRipper 2.0.0 exports this release's PCM samples with both lengths zero.
    The format and sample bytes are retained exactly. Other WAV layouts are
    left alone; this is not a transcoder or a general corrupted-audio repair.
    """
    path = Path(path)
    if path.is_symlink():
        raise ValueError("Refusing symlinked audio file: {}".format(path))
    size = path.stat().st_size
    with path.open("rb") as source:
        header = source.read(44)
    if len(header) != 44 or header[:4] != b"RIFF" or header[8:12] != b"WAVE":
        return None
    if struct.unpack_from("<I", header, 4)[0] != 0:
        return None
    if (header[12:16] != b"fmt " or struct.unpack_from("<I", header, 16)[0] != 16
            or struct.unpack_from("<H", header, 20)[0] != 1
            or header[36:40] != b"data" or struct.unpack_from("<I", header, 40)[0] != 0):
        raise ValueError("Zero-length WAV has an unsupported layout: {}".format(path))
    channels, rate, byte_rate, alignment, bits = struct.unpack_from("<HIIHH", header, 22)
    if (not channels or not rate or bits not in (8, 16, 24, 32)
            or alignment != channels * (bits // 8) or byte_rate != rate * alignment
            or (size - 44) % alignment or size - 8 > 0xffffffff):
        raise ValueError("Zero-length WAV has inconsistent PCM dimensions: {}".format(path))
    fixed = bytearray(header)
    struct.pack_into("<I", fixed, 4, size - 8)
    struct.pack_into("<I", fixed, 40, size - 44)
    descriptor, temporary_name = tempfile.mkstemp(prefix=".lucid-audio-", dir=path.parent)
    temporary = Path(temporary_name)
    samples = hashlib.sha256()
    try:
        with os.fdopen(descriptor, "wb") as output, path.open("rb") as source:
            source.seek(44)
            output.write(fixed)
            for block in iter(lambda: source.read(1024 * 1024), b""):
                samples.update(block)
                output.write(block)
            output.flush()
            os.fsync(output.fileno())
        shutil.copymode(path, temporary)
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)
    return {"size": size, "sample_sha256": samples.hexdigest(),
            "channels": channels, "sample_rate": rate, "bits_per_sample": bits}


def normalize_audio(root):
    """Normalize generated assets before promotion; report each changed file."""
    root = Path(root)
    repaired = []
    for path in sorted(root.rglob("*.wav")):
        result = repair_pcm_header(path)
        if result:
            result["path"] = path.relative_to(root).as_posix()
            repaired.append(result)
    return {"repaired_pcm_headers": len(repaired), "files": repaired}
