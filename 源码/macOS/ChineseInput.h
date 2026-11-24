#import <Foundation/Foundation.h>

@interface ChineseInput : NSObject
@property(readonly) NSArray<NSArray<NSString *> *> *rules;
- (instancetype)initWithBundle:(NSBundle *)bundle tricks:(NSArray *)tricks;
- (void)clear;
- (void)discardSpelling;
- (BOOL)letter:(int)key time:(double)time;
- (void)editAt:(double)time;
- (BOOL)selectAt:(double)time expected:(int *)expected;
// Values encode a rule index and script. Only fixed catalog words leave the matcher.
- (int)matchSuffix:(NSString *)text;
- (int)committedCharacter:(unichar)character time:(double)time;
- (int)atCaretOf:(pid_t)pid fallback:(int)fallback;
- (NSString *)wordForMatch:(int)match;
@end
