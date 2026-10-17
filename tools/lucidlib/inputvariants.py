"""Declared original-input variants and the evidence each one is qualified for.

Release 1.10.1 / build 1022527550 ships as more than one byte-distinct macOS
bundle, and every variant reports the same release strings. A variant is
therefore identified only by exact critical-file hashes, and evidence recovered
from one variant is never reused for another.
"""

GAME_ASSEMBLY = "Contents/Frameworks/GameAssembly.dylib"
METADATA = "Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat"
INFO_PLIST = "Contents/Info.plist"

# Evidence purposes a variant can be qualified for.
ASSET_PREPARATION = "asset_preparation"  # extract -> prepare, which ends in script bindings
SCRIPT_BINDINGS = "script_bindings"      # scriptbindings.RULES behaviour and layout proofs
THEME_DIAGNOSTICS = "theme_diagnostics"  # themebindings reviewed original schema content
CODE_PROGRESS = "code_progress"          # docs/code-progress.json measurement seal

# A None hash was never recorded for that variant; it is unknown, not wildcarded.
VARIANTS = {
    # The private local input and the 2026-10-05 reference export
    # (20261005T062956Z-cef5ba97 input_fingerprint). Its thin x86_64 slice is the
    # native recovery input that the Theme schema and dumper evidence were read from.
    "6c89-acb": {
        "binary_sha256": "6c89f9837ba64b8799c68ce1ac2bd7b48bf7e23a16bb26657a998c6fc3e34216",
        "metadata_sha256": "acb10c65e45486ff62a39fd677404857f99a3d2a98fdd36362d0bf19aac4b89b",
        "info_plist_sha256": "497b0fdb7b50091f00fd36cf7bbfe667d22e081b30d853d71fed38670eb9c9ae",
        "x86_64_slice_sha256": "797ef6ac359e8dbfc83d97a6148fe9aa7ac85113217e3013e00c3c5b30502c94",
        "qualified": frozenset((ASSET_PREPARATION, SCRIPT_BINDINGS, THEME_DIAGNOSTICS, CODE_PROGRESS)),
    },
    # The canonical public input named by the progress seal from 1c24c1eb. No
    # native schema, Theme schema content or Unity layout proof exists for it, so
    # it is qualified only for the progress measurement that was taken from it.
    "e94d-88af": {
        "binary_sha256": "e94d984c0bc726a3a66ed0b39785d5166f92aa4356b3dd742fce098d42521987",
        "metadata_sha256": "88af04a6e548dae5e00c13dd52add4e48f9917a31db784d3b21274b137e49837",
        "info_plist_sha256": None,
        "x86_64_slice_sha256": None,
        "qualified": frozenset((CODE_PROGRESS,)),
    },
}


def variant(name, purpose):
    """Return a declared variant that is qualified for one evidence purpose."""
    profile = VARIANTS[name]
    if purpose not in profile["qualified"]:
        raise ValueError("Original input variant " + name + " is not qualified for " + purpose)
    return profile


def _describe(profile):
    plist = profile["info_plist_sha256"]
    return ("GameAssembly.dylib " + str(profile["binary_sha256"]) + ", global-metadata.dat " +
            str(profile["metadata_sha256"]) + ", Info.plist " + (plist if plist else "(none)"))


def _supported(purpose):
    rows = [name + " (" + _describe(profile) + ")" for name, profile in VARIANTS.items()
            if purpose in profile["qualified"]]
    return "; ".join(rows) or "none"


def code_variant(binary_sha256, metadata_sha256):
    """Name the declared variant with this exact code/metadata pair, or None."""
    for name, profile in VARIANTS.items():
        if profile["binary_sha256"] == binary_sha256 and profile["metadata_sha256"] == metadata_sha256:
            return name
    return None


def require_code_variant(binary_sha256, metadata_sha256, purpose):
    """Reject a code/metadata pair unless its declared variant is qualified for purpose."""
    name = code_variant(binary_sha256, metadata_sha256)
    if name is None:
        raise ValueError("Original input variant is not declared: supplied GameAssembly.dylib " + str(binary_sha256) +
                         ", global-metadata.dat " + str(metadata_sha256) +
                         "; supported for " + purpose + ": " + _supported(purpose))
    if purpose not in VARIANTS[name]["qualified"]:
        raise ValueError("Original input variant " + name + " is declared but not qualified for " + purpose +
                         "; supported: " + _supported(purpose))
    return name


def admit(critical_files, purpose):
    """Admit a supplied bundle by its exact critical-file hashes before any export runs."""
    supplied = {"binary_sha256": critical_files.get(GAME_ASSEMBLY), "metadata_sha256": critical_files.get(METADATA),
                "info_plist_sha256": critical_files.get(INFO_PLIST)}
    name = code_variant(supplied["binary_sha256"], supplied["metadata_sha256"])
    if name is None:
        raise ValueError("Unsupported original input variant: supplied " + _describe(supplied) +
                         "; supported for " + purpose + ": " + _supported(purpose))
    if purpose not in VARIANTS[name]["qualified"]:
        raise ValueError("Original input variant " + name + " is declared but not qualified for " + purpose +
                         ": supplied " + _describe(supplied) + "; supported: " + _supported(purpose))
    if VARIANTS[name]["info_plist_sha256"] is None:
        raise ValueError("Info.plist for declared variant " + name + " was never recorded; admission requires it: supplied " +
                         _describe(supplied))
    if supplied["info_plist_sha256"] != VARIANTS[name]["info_plist_sha256"]:
        raise ValueError("Original input Info.plist differs from declared variant " + name + ": supplied " +
                         _describe(supplied) + "; supported for " + purpose + ": " + _supported(purpose))
    return name
