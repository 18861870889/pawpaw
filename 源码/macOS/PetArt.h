#import <AppKit/AppKit.h>
#import "PetMotion.h"

@interface PetArt : NSObject
@property(nonatomic) BOOL printLook;
@property(readonly) NSArray *tricks;
@property(readonly) NSArray *idles;
@property(readonly) NSDictionary *catalog;
- (NSImage *)portraitForRole:(NSDictionary *)role size:(CGFloat)size;
@property(readonly) NSArray *keys;
@property(readonly) NSDictionary *keymap;
@property(readonly) CGImageRef frame;
- (instancetype)initWithBundle:(NSBundle *)bundle;
- (void)renderRole:(NSDictionary *)role clothes:(int)clothes hat:(int)hat board:(int)board mouse:(int)mouse motion:(const KCMotion *)motion time:(double)now;
- (BOOL)opaqueAtX:(double)x topY:(double)y;
@end
