#import <Foundation/Foundation.h>

typedef void (*StreakEngineCompletion)(bool success);

void StreakEngine_Run(StreakEngineCompletion callback);

FOUNDATION_EXPORT double StreakEngineVersionNumber;
FOUNDATION_EXPORT const unsigned char StreakEngineVersionString[];
