#define main pawpaw_application_main
#import "../main.m"
#undef main
#include <assert.h>

int main(int argc,const char **argv) {
    @autoreleasepool {
        [NSApplication sharedApplication];
        KeyboardCatApp *controller=[KeyboardCatApp new];
        NSString *suite=[@"app.pawpaw.test." stringByAppendingString:NSUUID.UUID.UUIDString];
        NSUserDefaults *defaults=[[NSUserDefaults alloc] initWithSuiteName:suite];
        assert(argc==2);
        NSBundle *bundle=[NSBundle bundleWithPath:[NSString stringWithUTF8String:argv[1]]];
        PetArt *art=[[PetArt alloc] initWithBundle:bundle];assert(art);
        NSString *custom=@"user-1234567890abcdef1234567890abcdef";
        [defaults setObject:@[@{@"id":custom,@"name":@"黑猫",@"shape":@1,@"pattern":@0,@"eyes":@2}] forKey:@"customRoles"];
        [defaults setObject:custom forKey:@"selectedRoleID"];
        PetStudio *studio=[[PetStudio alloc] initWithCatalog:art.catalog preferences:defaults];
        assert([studio.role[@"id"] isEqual:custom] && [studio.role[@"build"] intValue]==0);
        assert([[studio menuNameForRole:studio.role] isEqual:@"黑猫（自建）"]);
        [studio setBuild:4];[studio wear:2 hat:3 board:1 mouse:1];
        [studio select:7];[studio setBuild:2];
        studio=[[PetStudio alloc] initWithCatalog:art.catalog preferences:defaults];
        assert([studio.role[@"id"] isEqual:@"black-cat"] && [studio.role[@"build"] intValue]==2);
        [studio select:11];assert([studio.role[@"id"] isEqual:custom] && [studio.role[@"build"] intValue]==4 && studio.clothes==2 && studio.hat==3);
        [studio setBuild:99];assert([studio.role[@"build"] intValue]==4);
        [studio removeCurrentRole];assert(studio.roles.count==11);
        [controller setValue:defaults forKey:@"prefs"];
        NSRect r=NSMakeRect(100,100,300,240);
        controller.pet=[[PetPanel alloc] initWithContentRect:r styleMask:NSWindowStyleMaskBorderless|NSWindowStyleMaskNonactivatingPanel backing:NSBackingStoreBuffered defer:NO];
        [controller.pet orderFrontRegardless];
        [controller lockPet:nil];
        assert(controller.locked && controller.pet.ignoresMouseEvents && [defaults boolForKey:@"locked"]);
        NSWindow *badge=controller.pet.childWindows.firstObject;
        assert(badge && badge.visible && !badge.ignoresMouseEvents && !badge.canBecomeKeyWindow);
        assert(NSContainsRect(controller.pet.frame,badge.frame) && NSWidth(badge.frame)<=32);
        assert([controller makeMenu].numberOfItems==2);
        [controller moveTo:NSMakePoint(150,150) screen:NSScreen.mainScreen];
        assert(NSContainsRect(controller.pet.frame,badge.frame));
        [(NSButton *)badge.contentView performClick:nil];
        assert(!controller.locked && !controller.pet.ignoresMouseEvents && !badge.visible && ![defaults boolForKey:@"locked"]);
        [controller.pet orderOut:nil];[defaults removePersistentDomainForName:suite];
        puts("Native lock panel, click-through, position and unlock control passed.");
    }
    return 0;
}
