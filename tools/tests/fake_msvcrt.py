"""A shared msvcrt.locking double for the Windows lock paths in the tool tests."""

import errno
import os
import threading
import time


class RegionMsvcrt:
    """Models one property of msvcrt.locking: a file has at most one owning descriptor."""
    LK_UNLCK, LK_LOCK, LK_NBLCK = 0, 1, 2
    # Real LK_LOCK retries for about ten seconds before failing; a short wait keeps contenders from spinning.
    CONTENTION_WAIT = 0.01

    def __init__(self):
        self.held = {}
        self.calls = []
        self.guard = threading.Lock()

    def locking(self, fd, mode, length):
        info = os.fstat(fd)
        key = (info.st_dev, info.st_ino)
        with self.guard:
            self.calls.append(mode)
            if mode == self.LK_UNLCK:
                if self.held.pop(key, None) != fd:
                    raise OSError(errno.EACCES, "region not held by this descriptor")
                return
            if self.held.get(key, fd) == fd:
                self.held[key] = fd
                return
        if mode == self.LK_NBLCK:
            raise OSError(errno.EACCES, "region held")
        time.sleep(self.CONTENTION_WAIT)
        raise OSError(errno.EDEADLK, "region held")
