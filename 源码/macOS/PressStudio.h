#import <AppKit/AppKit.h>
@class PetArt,PetStudio;

@interface PressStudio : NSObject
@property(nonatomic,readonly) NSPanel *panel;
@property(nonatomic,copy) void (^selectRole)(NSInteger index);
@property(nonatomic,copy) void (^editRole)(BOOL editing);
@property(nonatomic,copy) void (^setLook)(BOOL print);
@property(nonatomic,copy) void (^appearanceChanged)(void);
@property(nonatomic,copy) void (^performTrick)(NSInteger index);
- (instancetype)initWithModel:(PetStudio *)studio art:(PetArt *)art;
- (void)synchronize;
- (void)refreshFrame;
@end
