// Reconstructed original plugin source, preserved for reference; not built or
// loaded by the offline game. ControllerManager declarations are rebuilt from the
// x86_64 and arm64 slices. The header filename and the parameter names are
// invented: the binary does not keep its header filename.
#pragma once
#import <Foundation/Foundation.h>
#import <GameController/GameController.h>

// The original class has no ivars, properties or protocols of its own. BOOL
// matches the original type encodings: B on arm64, c on x86_64. The shipped
// build used SDK 12.1.
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
// The six exported C symbols, named exactly as shipped. The int/BOOL spellings
// are inferred from argument forwarding, the method encodings and the return
// registers of both slices.
void UpdatePlugin(void);
BOOL GetKeyDown(int key, int joystickIndex);
BOOL GetKey(int key, int joystickIndex);
BOOL GetKeyUp(int key, int joystickIndex);
float GetAxisValue(const char *axisName, int joystickIndex);
const char *GetControllerNamesNative(void);
#ifdef __cplusplus
}
#endif
