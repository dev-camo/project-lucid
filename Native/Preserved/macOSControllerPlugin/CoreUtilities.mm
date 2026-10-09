// Original filename CoreUtilities.mm and exact mangled MarshallString signature
// are retained in both CPU source/symbol groups. No platform adapter is present.
#include <string>
#include <cstdint>
#include <cstdlib>
#include <cstring>

char *MarshallString(const std::string &value)
{
    // Both binaries extract the low32 bits of string length, add1 in32 bits,
    // sign-extend the result to malloc's size argument, then call strcpy.
    // This explicit reconstruction preserves that machine schedule. The authored
    // cast/local spelling and large-size C++ conversion semantics are unresolved.
    // ARM/x86 libc++ short-string layouts differ; std::string API represents the
    // common inferred operation, not a claim of byte-identical rebuilt code.
    const std::uint32_t allocationBits = static_cast<std::uint32_t>(value.length()) + 1u;
    const std::int32_t allocationCount = static_cast<std::int32_t>(allocationBits);
    char *result = static_cast<char *>(std::malloc(static_cast<std::size_t>(allocationCount)));
    // Original has no allocation check and no exported release function. It
    // does not prove whether the genuine managed string marshaler frees memory.
    std::strcpy(result, value.c_str());
    return result;
}
