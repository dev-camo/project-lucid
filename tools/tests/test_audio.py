import hashlib
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from unittest.mock import patch
import wave

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from lucidlib.audio import normalize_audio, repair_pcm_header


def exported_pcm(samples):
    return (b"RIFF" + struct.pack("<I", 0) + b"WAVEfmt "
            + struct.pack("<IHHIIHH", 16, 1, 2, 48000, 192000, 4, 16)
            + b"data" + struct.pack("<I", 0) + samples)


class AudioTests(unittest.TestCase):
    def test_header_repair_makes_pcm_readable_without_changing_samples(self):
        with tempfile.TemporaryDirectory(prefix="audio space ") as directory:
            path = Path(directory) / "original.wav"
            samples = struct.pack("<hhhh", -32768, 32767, -5, 9)
            path.write_bytes(exported_pcm(samples))
            result = normalize_audio(Path(directory))
            self.assertEqual(1, result["repaired_pcm_headers"])
            self.assertEqual(hashlib.sha256(samples).hexdigest(), result["files"][0]["sample_sha256"])
            with wave.open(str(path)) as audio:
                self.assertEqual((2, 2, 48000, 2), (audio.getnchannels(), audio.getsampwidth(),
                                                   audio.getframerate(), audio.getnframes()))
                self.assertEqual(samples, audio.readframes(2))
            before = path.read_bytes()
            self.assertEqual(0, normalize_audio(Path(directory))["repaired_pcm_headers"])
            self.assertEqual(before, path.read_bytes())

    def test_invalid_alignment_does_not_change_original(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "short.wav"
            original = exported_pcm(b"abc")
            path.write_bytes(original)
            with self.assertRaises(ValueError):
                repair_pcm_header(path)
            self.assertEqual(original, path.read_bytes())

    def test_interrupted_promotion_retains_prior_file(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "clip.wav"
            original = exported_pcm(b"\x00" * 4)
            path.write_bytes(original)
            with patch("lucidlib.audio.os.replace", side_effect=OSError("interrupted")):
                with self.assertRaises(OSError):
                    repair_pcm_header(path)
            self.assertEqual(original, path.read_bytes())
            self.assertEqual([path], list(Path(directory).iterdir()))


if __name__ == "__main__":
    unittest.main()
