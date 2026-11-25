#import <AppKit/AppKit.h>
@class PetArt;

@interface PetStudio : NSObject
@property(nonatomic,readonly) NSDictionary *catalog,*role;
@property(nonatomic,readonly) NSArray *roles;
@property(nonatomic,readonly) NSInteger selected;
@property(nonatomic,readonly) int clothes,hat,board,mouse;
@property(nonatomic,readonly) BOOL canEdit;
- (instancetype)initWithCatalog:(NSDictionary *)catalog preferences:(NSUserDefaults *)prefs;
- (void)select:(NSInteger)index;
- (void)setBuild:(NSInteger)index;
- (void)wear:(NSInteger)clothes hat:(NSInteger)hat board:(NSInteger)board mouse:(NSInteger)mouse;
- (NSString *)menuNameForRole:(NSDictionary *)role;
- (BOOL)nameAvailable:(NSString *)name forID:(NSString *)identifier;
- (void)storeRole:(NSDictionary *)role;
- (void)removeCurrentRole;
@end

@interface PetRoleEditor : NSObject <NSTextFieldDelegate>
- (BOOL)runWithStudio:(PetStudio *)studio art:(PetArt *)art edit:(BOOL)editing;
@end
