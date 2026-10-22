// Reconstructed original plugin source, preserved for reference; not built or
// loaded by the offline game. pluginExtern.m is the original filename (kept in
// the STAB source groups of both the x86_64 and arm64 slices).
// The six exported wrappers are inferred from the full native code ranges; ARC
// lifetimes and the exact source form are not proven.
#import "PreservedControllerManagerDeclarations.h"

void UpdatePlugin(void)
{
    [[ControllerManager Instance] update];
}

BOOL GetKeyDown(int key, int joystickIndex)
{
    return [[ControllerManager Instance] getKeyDown:key :joystickIndex] != NO;
}

BOOL GetKey(int key, int joystickIndex)
{
    return [[ControllerManager Instance] getKey:key :joystickIndex] != NO;
}

BOOL GetKeyUp(int key, int joystickIndex)
{
    return [[ControllerManager Instance] getKeyUp:key :joystickIndex] != NO;
}

float GetAxisValue(const char *axisName, int joystickIndex)
{
    // The original converts the name first, then fetches Instance, with no
    // guard on axisName (null or invalid UTF-8 is passed straight to Foundation).
    NSString *axis = [NSString stringWithUTF8String:axisName];
    return [[ControllerManager Instance] getAxis:axis :joystickIndex];
}

const char *GetControllerNamesNative(void)
{
    // Does not refresh the snapshots first, as in the original.
    return [[ControllerManager Instance] getControllerNames];
}
