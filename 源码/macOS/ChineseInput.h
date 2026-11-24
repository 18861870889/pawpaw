#import <Foundation/Foundation.h>
#include <stdint.h>

@interface ChineseInput : NSObject
@property(readonly) NSArray<NSArray<NSString *> *> *rules;
@property(readonly) uint32_t pinyinAliases;
- (instancetype)initWithBundle:(NSBundle *)bundle tricks:(NSArray *)tricks;
@end
