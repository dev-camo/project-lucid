// Reconstructed original plugin source, preserved for reference; not built or
// loaded by the offline game. Plugin.mm is the original filename (kept in the
// STAB source groups of both the x86_64 and arm64 slices). Symbols, selectors and
// the key and axis tables follow the shipped binaries; ARC lifetimes, the
// original typed-pointer spellings and the exact source form are inferred.
#import "PreservedControllerManagerDeclarations.h"
#include <string>

char *MarshallString(const std::string &value);

// Two 32-byte zero-initialised global arrays (four slots), indexed in 8-byte
// steps. The element type is spelled id because the original gamepad and
// snapshot pointer declarations are not recoverable; the ARC lifetime of the
// elements is inferred.
id previousSnapshot[4] = {nil, nil, nil, nil};
id currentSnapshot[4] = {nil, nil, nil, nil};

// The compiler generates the __GLOBAL__sub_I_Plugin.mm static initialiser and
// destructor registration for this std::string; there is no hand-written
// initialiser.
static std::string controllerNames;

@implementation ControllerManager

+ (ControllerManager *)Instance
{
    // Lazy singleton (symbol Instance::instance) with no dispatch_once or lock.
    static ControllerManager *instance = nil;
    if (instance == nil)
        instance = [[ControllerManager alloc] init];
    return instance;
}

- (void)update
{
    // The original queries the live controller list at every use: it does not
    // cache the list, cap it at the four slots or clear slots of removed or
    // unsupported controllers. The unsigned loop variable is inferred from the
    // 64-bit increment and unsigned compare in both slices.
    for (NSUInteger index = 0; index < [[GCController controllers] count]; ++index)
    {
        if ([[GCController controllers] objectAtIndexedSubscript:index] != nil)
        {
            id profile = [[[GCController controllers] objectAtIndexedSubscript:index] extendedGamepad];
            if (profile != nil)
            {
                // saveSnapshot runs after the previous-slot store, so the old
                // current snapshot is kept as previous.
                previousSnapshot[index] = currentSnapshot[index];
                currentSnapshot[index] = [profile saveSnapshot];
            }
        }
    }
}

- (BOOL)getKeyDown:(int)key :(int)joystickIndex
{
    // joystickIndex is used unchecked as a signed index, as in the original.
    if (currentSnapshot[joystickIndex] == nil) return NO;
    // Both getKeyInternal calls go through Instance, in this order, before the
    // result is combined; do not use self or short-circuit the second call.
    BOOL previous = [[ControllerManager Instance] getKeyInternal:key :previousSnapshot[joystickIndex]];
    BOOL current = [[ControllerManager Instance] getKeyInternal:key :currentSnapshot[joystickIndex]];
    return !previous && current;
}

- (BOOL)getKey:(int)key :(int)joystickIndex
{
    if (currentSnapshot[joystickIndex] == nil) return NO;
    return [[ControllerManager Instance] getKeyInternal:key :currentSnapshot[joystickIndex]];
}

- (BOOL)getKeyUp:(int)key :(int)joystickIndex
{
    if (currentSnapshot[joystickIndex] == nil) return NO;
    BOOL previous = [[ControllerManager Instance] getKeyInternal:key :previousSnapshot[joystickIndex]];
    BOOL current = [[ControllerManager Instance] getKeyInternal:key :currentSnapshot[joystickIndex]];
    return previous && !current;
}

- (float)getAxis:(NSString *)axisName :(int)joystickIndex
{
    if (currentSnapshot[joystickIndex] == nil) return 0.0f;
    return [[ControllerManager Instance] getAxisInternal:axisName :currentSnapshot[joystickIndex]];
}

- (const char *)getControllerNames
{
    // The reset comes before the first controller-count call.
    controllerNames = "";
    if ([[GCController controllers] count] == 0) return nullptr;

    for (NSUInteger index = 0; index < [[GCController controllers] count]; ++index)
    {
        // The controller is fetched again for each of the two names. There is no
        // nil check, escaping, filtering or trimming.
        controllerNames.append([[[[GCController controllers] objectAtIndexedSubscript:index] vendorName] UTF8String]);
        controllerNames.append([@" : " UTF8String]);
        controllerNames.append([[[[GCController controllers] objectAtIndexedSubscript:index] productCategory] UTF8String]);
        if (index < [[GCController controllers] count] - 1)
            controllerNames.append(",");
    }
    // Zero controllers returns null above; if the list shrinks to empty during
    // the loop, this returns an empty string. The result is never freed here.
    return MarshallString(controllerNames);
}

- (BOOL)getKeyInternal:(int)key :(id)snapshot
{
    if (snapshot == nil) return NO;
    // All 72 signed jump-table offsets were decoded from both slices. These 48
    // key codes are the complete supported set; every other key returns NO.
    switch (key)
    {
        case 354: case 374: case 394: case 414: return [[[snapshot dpad] up] isPressed];
        case 355: case 375: case 395: case 415: return [[[snapshot dpad] right] isPressed];
        case 356: case 376: case 396: case 416: return [[[snapshot dpad] down] isPressed];
        case 357: case 377: case 397: case 417: return [[[snapshot dpad] left] isPressed];
        case 358: case 378: case 398: case 418: return [[snapshot leftShoulder] isPressed];
        case 359: case 379: case 399: case 419: return [[snapshot rightShoulder] isPressed];
        case 360: case 380: case 400: case 420: return [[snapshot leftTrigger] isPressed];
        case 361: case 381: case 401: case 421: return [[snapshot rightTrigger] isPressed];
        case 362: case 382: case 402: case 422: return [[snapshot buttonY] isPressed];
        case 363: case 383: case 403: case 423: return [[snapshot buttonB] isPressed];
        case 364: case 384: case 404: case 424: return [[snapshot buttonA] isPressed];
        case 365: case 385: case 405: case 425: return [[snapshot buttonX] isPressed];
        default: return NO;
    }
}

- (float)getAxisInternal:(NSString *)axisName :(id)snapshot
{
    if (snapshot == nil) return 0.0f;
    // 48 case-sensitive names, matched axis by axis and joystick 1 to 4 in turn.
    // The snapshot passed in is used whichever joystick digit matches.
    if ([axisName isEqualToString:@"Joystick 1 Axis 1"] ||
        [axisName isEqualToString:@"Joystick 2 Axis 1"] ||
        [axisName isEqualToString:@"Joystick 3 Axis 1"] ||
        [axisName isEqualToString:@"Joystick 4 Axis 1"])
        return [[[snapshot leftThumbstick] xAxis] value];
    else if ([axisName isEqualToString:@"Joystick 1 Axis 2"] ||
        [axisName isEqualToString:@"Joystick 2 Axis 2"] ||
        [axisName isEqualToString:@"Joystick 3 Axis 2"] ||
        [axisName isEqualToString:@"Joystick 4 Axis 2"])
        return [[[snapshot leftThumbstick] yAxis] value];
    else if ([axisName isEqualToString:@"Joystick 1 Axis 3"] ||
        [axisName isEqualToString:@"Joystick 2 Axis 3"] ||
        [axisName isEqualToString:@"Joystick 3 Axis 3"] ||
        [axisName isEqualToString:@"Joystick 4 Axis 3"])
        return [[[snapshot rightThumbstick] xAxis] value];
    else if ([axisName isEqualToString:@"Joystick 1 Axis 4"] ||
        [axisName isEqualToString:@"Joystick 2 Axis 4"] ||
        [axisName isEqualToString:@"Joystick 3 Axis 4"] ||
        [axisName isEqualToString:@"Joystick 4 Axis 4"])
        return [[[snapshot rightThumbstick] yAxis] value];
    else if ([axisName isEqualToString:@"Joystick 1 Axis 8"] ||
        [axisName isEqualToString:@"Joystick 2 Axis 8"] ||
        [axisName isEqualToString:@"Joystick 3 Axis 8"] ||
        [axisName isEqualToString:@"Joystick 4 Axis 8"])
        return [[snapshot leftShoulder] value];
    else if ([axisName isEqualToString:@"Joystick 1 Axis 9"] ||
        [axisName isEqualToString:@"Joystick 2 Axis 9"] ||
        [axisName isEqualToString:@"Joystick 3 Axis 9"] ||
        [axisName isEqualToString:@"Joystick 4 Axis 9"])
        return [[snapshot rightShoulder] value];
    else if ([axisName isEqualToString:@"Joystick 1 Axis 10"] ||
        [axisName isEqualToString:@"Joystick 2 Axis 10"] ||
        [axisName isEqualToString:@"Joystick 3 Axis 10"] ||
        [axisName isEqualToString:@"Joystick 4 Axis 10"])
        return [[snapshot leftTrigger] value];
    else if ([axisName isEqualToString:@"Joystick 1 Axis 11"] ||
        [axisName isEqualToString:@"Joystick 2 Axis 11"] ||
        [axisName isEqualToString:@"Joystick 3 Axis 11"] ||
        [axisName isEqualToString:@"Joystick 4 Axis 11"])
        return [[snapshot rightTrigger] value];
    else if ([axisName isEqualToString:@"Joystick 1 Axis 12"] ||
        [axisName isEqualToString:@"Joystick 2 Axis 12"] ||
        [axisName isEqualToString:@"Joystick 3 Axis 12"] ||
        [axisName isEqualToString:@"Joystick 4 Axis 12"])
        return [[snapshot buttonY] value];
    else if ([axisName isEqualToString:@"Joystick 1 Axis 13"] ||
        [axisName isEqualToString:@"Joystick 2 Axis 13"] ||
        [axisName isEqualToString:@"Joystick 3 Axis 13"] ||
        [axisName isEqualToString:@"Joystick 4 Axis 13"])
        return [[snapshot buttonB] value];
    else if ([axisName isEqualToString:@"Joystick 1 Axis 14"] ||
        [axisName isEqualToString:@"Joystick 2 Axis 14"] ||
        [axisName isEqualToString:@"Joystick 3 Axis 14"] ||
        [axisName isEqualToString:@"Joystick 4 Axis 14"])
        return [[snapshot buttonA] value];
    else if ([axisName isEqualToString:@"Joystick 1 Axis 15"] ||
        [axisName isEqualToString:@"Joystick 2 Axis 15"] ||
        [axisName isEqualToString:@"Joystick 3 Axis 15"] ||
        [axisName isEqualToString:@"Joystick 4 Axis 15"])
        return [[snapshot buttonX] value];
    return 0.0f;
}

@end
