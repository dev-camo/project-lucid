// Original filename pluginExtern.m is retained in both CPUs' STAB source groups.
// These six complete wrapper schedules are inferred from the full native ranges;
// compiler/ARC/source-form/Objective-C runtime fidelity is still unapproved.
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
    // Original converts first, then obtains Instance; no C-pointer guard.
    // Foundation invalid/null UTF8 input and ARC lifetime remain provider holds.
    NSString *axis = [NSString stringWithUTF8String:axisName];
    return [[ControllerManager Instance] getAxis:axis :joystickIndex];
}

const char *GetControllerNamesNative(void)
{
    // The original names wrapper does not update snapshots before this call.
    return [[ControllerManager Instance] getControllerNames];
}
