// Original filename Plugin.mm is retained in both CPUs' STAB source groups.
// Full original symbols/selectors and numeric/string maps support this reconstruction.
// ARC compiler helpers, original typed-pointer spellings and source form are
// inference limits. Never load/run this preserved service by default.
#import "PreservedControllerManagerDeclarations.h"
#include <string>

char *MarshallString(const std::string &value);

// Exact exported symbols and two32-byte zero-fill areas, each indexed in8-byte
// steps. id is an explicit inferred dynamic-object source spelling; original
// GCExtendedGamepad/Snapshot pointer declarations were not retained. Strong ARC
// array lifetimes require future compiler qualification, not runtime simulation.
id previousSnapshot[4] = {nil, nil, nil, nil};
id currentSnapshot[4] = {nil, nil, nil, nil};

// Exact local std::__1::string symbol controllerNames. The C++ compiler is
// responsible for the observed __GLOBAL__sub_I_Plugin.mm initialization and
// destructor registration; no invented authored initializer method is added.
static std::string controllerNames;

@implementation ControllerManager

+ (ControllerManager *)Instance
{
    // Exact nested static symbol Instance::instance; no dispatch_once/lock.
    static ControllerManager *instance = nil;
    if (instance == nil)
        instance = [[ControllerManager alloc] init];
    return instance;
}

- (void)update
{
    // Original repeatedly queries the live list; it neither caches one list nor
    // bounds it to the four physical slots nor clears removed/unsupported slots.
    // Each actual query/site is preserved. Unsigned loop source type is inferred
    // from64-bit increment/count and unsigned branch on both CPUs.
    for (NSUInteger index = 0; index < [[GCController controllers] count]; ++index)
    {
        if ([[GCController controllers] objectAtIndexedSubscript:index] != nil)
        {
            id profile = [[[GCController controllers] objectAtIndexedSubscript:index] extendedGamepad];
            if (profile != nil)
            {
                // Genuine provider call runs AFTER the previous slot store.
                previousSnapshot[index] = currentSnapshot[index];
                currentSnapshot[index] = [profile saveSnapshot];
            }
        }
    }
}

- (BOOL)getKeyDown:(int)key :(int)joystickIndex
{
    // Unchecked signed indexing is original; no repair or supplied provider.
    if (currentSnapshot[joystickIndex] == nil) return NO;
    // Both actual Instance/internal calls occur in this order before the result;
    // do not replace with self or short-circuit the second provider call.
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
    // Exact original reset precedes the initial controller-count call.
    controllerNames = "";
    if ([[GCController controllers] count] == 0) return nullptr;

    for (NSUInteger index = 0; index < [[GCController controllers] count]; ++index)
    {
        // Original obtains separate live controller objects for these two names.
        // No nil-name guard, escaping, profile filtering or trimming is present.
        controllerNames.append([[[[GCController controllers] objectAtIndexedSubscript:index] vendorName] UTF8String]);
        controllerNames.append([@" : " UTF8String]);
        controllerNames.append([[[[GCController controllers] objectAtIndexedSubscript:index] productCategory] UTF8String]);
        if (index < [[GCController controllers] count] - 1)
            controllerNames.append(",");
    }
    // Initial zero returnsnull; later list changes can reach this withempty text.
    // Provider mutations, nil messages, allocation ownership and faults unrun.
    return MarshallString(controllerNames);
}

- (BOOL)getKeyInternal:(int)key :(id)snapshot
{
    if (snapshot == nil) return NO;
    // All72 original signed table offsets decoded on both CPUs. These48 cases
    // are the complete supported numeric set; no guessed KeyCode/API is added.
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
    // Original48 case-sensitive names, axis-first/joystick-digit order. The
    // already supplied snapshot is used regardless of which digit matches.
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
