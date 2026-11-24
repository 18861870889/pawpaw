#import <Foundation/Foundation.h>
#include "PetMotion.h"

@interface ChineseInput : NSObject
@property(readonly) NSArray<NSArray<NSString *> *> *rules;
@property(readonly) uint32_t pinyinAliases;
- (int)matchSuffix:(NSString *)suffix endingAt:(NSInteger *)start;
- (instancetype)initWithBundle:(NSBundle *)bundle tricks:(NSArray *)tricks;
@end

@interface KCWordTurn : NSObject {
    @public KCWordGate gate;
}
@property BOOL closed,typed;
@end

@interface ChineseWordObserver : NSObject
- (instancetype)initWithCatalog:(ChineseInput *)catalog hit:(void (^)(pid_t,KCWordTurn *,int))hit;
- (void)follow:(pid_t)pid turn:(KCWordTurn *)turn refresh:(BOOL)refresh confirm:(BOOL)confirm;
@end
