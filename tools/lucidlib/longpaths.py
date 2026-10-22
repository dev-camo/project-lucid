"""Windows long-path support checks (read-only; the registry is never written)."""

import os
import sys

# Windows rejects paths of 260 characters or more (MAX_PATH including the NUL).
MAX_PATH_CHARS = 259
REGISTRY_KEY = r"SYSTEM\CurrentControlSet\Control\FileSystem"
REGISTRY_VALUE = "LongPathsEnabled"
ENABLE_COMMAND = ("Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\FileSystem' "
                  "-Name LongPathsEnabled -Type DWord -Value 1")
ENABLE_HELP = ("Enable long paths from an elevated (administrator) PowerShell, then sign out and in again: "
               + ENABLE_COMMAND + " -- or enable the Group Policy \"Enable Win32 long paths\" "
               "(Computer Configuration > Administrative Templates > System > Filesystem).")
EXTRACT_HELP = ("Extract the bundle to a short folder such as C:\\sdt with a tool that supports long paths "
                "(7-Zip, or `tar -xf SonicDreamTeam.zip` in Windows), not Explorer's \"Extract All\", "
                "and enable long paths.")


def is_windows():
    return sys.platform.startswith("win")


def long_paths_status():
    """Return enabled, disabled, unreadable, or not-applicable (not Windows)."""
    if not is_windows():
        return "not-applicable"
    try:
        import winreg
        with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, REGISTRY_KEY) as key:
            value, kind = winreg.QueryValueEx(key, REGISTRY_VALUE)
    except FileNotFoundError:
        return "disabled"  # The value is absent on systems that never set it.
    except (OSError, ImportError):
        return "unreadable"
    # Windows honours the setting only as a DWORD; a QWORD 1 still leaves long paths off.
    return "enabled" if kind == winreg.REG_DWORD and value == 1 else "disabled"


def extended_path(path):
    """The extended-length form of an absolute Windows path, which is not limited to MAX_PATH."""
    if path.startswith("\\\\?\\"):
        return path
    if path.startswith("\\\\"):  # \\server\share\... becomes \\?\UNC\server\share\...
        return "\\\\?\\UNC\\" + path[2:]
    return "\\\\?\\" + path


def longest_path_length(root):
    """Length of the longest path under root, counting the root itself."""
    root = os.path.abspath(str(root))
    # The extended prefix lets the walk see paths beyond MAX_PATH on Windows.
    walk_root = extended_path(root) if is_windows() else root
    offset = len(walk_root) - len(root)
    longest = len(root)
    for directory, _dirs, files in os.walk(walk_root):
        for name in files:
            longest = max(longest, len(directory) - offset + 1 + len(name))
    return longest


def check_long_paths_for_input(root):
    """Raise ValueError before inspecting when Windows cannot open the input's paths."""
    if long_paths_status() != "disabled":
        return
    longest = longest_path_length(root)
    if longest > MAX_PATH_CHARS:
        raise ValueError("Long paths are disabled in Windows and the input contains a path of %d characters "
                         "(the limit is %d), so it cannot be read reliably. %s %s"
                         % (longest, MAX_PATH_CHARS, ENABLE_HELP, EXTRACT_HELP))


def partial_extraction_hint():
    hint = ("The input looks like an incomplete extraction: the app's Contents folder exists but required "
            "files are absent. Extract the original archive completely again.")
    if is_windows():
        hint += (" On Windows, long file names in the bundle (up to about 270 characters) make Explorer's "
                 "\"Extract All\" stop at the first path over the limit. " + EXTRACT_HELP)
    return hint
