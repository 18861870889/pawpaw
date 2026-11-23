#define main pawpaw_application_main
#import "../main.m"
#undef main
#include <assert.h>

int main(void) {
    @autoreleasepool {
        [NSApplication sharedApplication];
        KeyboardCatApp *controller=[KeyboardCatApp new];
        NSString *suite=[@"app.pawpaw.test." stringByAppendingString:NSUUID.UUID.UUIDString];
        NSUserDefaults *defaults=[[NSUserDefaults alloc] initWithSuiteName:suite];
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
