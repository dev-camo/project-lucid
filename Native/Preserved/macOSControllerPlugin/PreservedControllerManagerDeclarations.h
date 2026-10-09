// Original ControllerManager declarations reconstructed from both supplied CPUs.
// This header filename and parameter/local spellings are new documentary source
// organization; the original binary does not retain its header filename.
// These preserved declarations do not install a plugin or activate a platform route.
#pragma once
#import <Foundation/Foundation.h>
#import <GameController/GameController.h>

// Original class has no ivars, properties or protocols of its own. BOOL matches
// original Objective-C B encoding on ARM64 and c encoding on x86_64. Future
// compiler/current SDK confirmation is separate; the shipped SDK was12.1.
@interface ControllerManager : NSObject
+ (ControllerManager *)Instance;
- (void)update;
- (BOOL)getKeyDown:(int)key :(int)joystickIndex;
- (BOOL)getKey:(int)key :(int)joystickIndex;
- (BOOL)getKeyUp:(int)key :(int)joystickIndex;
- (float)getAxis:(NSString *)axisName :(int)joystickIndex;
- (const char *)getControllerNames;
- (BOOL)getKeyInternal:(int)key :(id)snapshot;
- (float)getAxisInternal:(NSString *)axisName :(id)snapshot;
@end

#ifdef __cplusplus
extern "C" {
#endif
// Six exact shipped C export names. Wrapper int/BOOL spelling is inferred from
// argument forwarding, original method encodings and both CPU return registers.
void UpdatePlugin(void);
BOOL GetKeyDown(int key, int joystickIndex);
BOOL GetKey(int key, int joystickIndex);
BOOL GetKeyUp(int key, int joystickIndex);
float GetAxisValue(const char *axisName, int joystickIndex);
const char *GetControllerNamesNative(void);
#ifdef __cplusplus
}
#endif
