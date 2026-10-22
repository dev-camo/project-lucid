// Reconstructed original plugin source, preserved for reference; not built or
// loaded by the offline game. CoreUtilities.mm is the original filename (kept in
// the STAB source groups of both the x86_64 and arm64 slices).
#include <string>
#include <cstdint>
#include <cstdlib>
#include <cstring>

char *MarshallString(const std::string &value)
{
    // Both binaries take the low 32 bits of the string length, add 1 in 32 bits,
    // sign-extend the result to malloc's size argument, then call strcpy. The
    // casts below spell that out; the original cast and local spellings, and how
    // very large sizes convert, are not known. The arm64 and x86_64 libc++
    // short-string layouts differ, so std::string stands in for the common
    // operation rather than matching the original code byte for byte.
    const std::uint32_t allocationBits = static_cast<std::uint32_t>(value.length()) + 1u;
    const std::int32_t allocationCount = static_cast<std::int32_t>(allocationBits);
    char *result = static_cast<char *>(std::malloc(static_cast<std::size_t>(allocationCount)));
    // As in the original: no allocation check and no exported release function,
    // so the caller's marshaler decides whether this memory is ever freed.
    std::strcpy(result, value.c_str());
    return result;
}
