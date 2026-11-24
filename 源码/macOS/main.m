#import <Cocoa/Cocoa.h>
#import <CoreGraphics/CoreGraphics.h>
#import "PetArt.h"
#import "PetStudio.h"
#import "PressStudio.h"
#import <math.h>
#import <Carbon/Carbon.h>
#import <ApplicationServices/ApplicationServices.h>
#import <QuartzCore/QuartzCore.h>

static double Now(void) {return NSProcessInfo.processInfo.systemUptime;}
static int choice(NSInteger value,int count) {return value>=0 && value<count?(int)value:0;}
static const double scales[]={.70,.93,1.24};
static const NSInteger MaxTransparency=80;

@interface SettingsPanel : NSPanel @end
@implementation SettingsPanel
- (void)cancelOperation:(id)sender {[self close];}
@end

@class KeyboardCatApp;
@interface PetPanel : NSPanel @end
@implementation PetPanel
- (BOOL)canBecomeKeyWindow {return NO;}
- (BOOL)canBecomeMainWindow {return NO;}
@end

@interface UnlockControl : NSButton @end
@implementation UnlockControl
- (BOOL)acceptsFirstMouse:(NSEvent *)event {return YES;}
@end

@interface PetView : NSView
@property(weak) KeyboardCatApp *owner;
@property BOOL dragging;
@end
@interface KeyboardCatApp : NSObject<NSApplicationDelegate,NSMenuDelegate>
@property(nonatomic,strong) PetArt *art;
@property(nonatomic,strong) PetPanel *pet;
@property(nonatomic,readonly) BOOL locked;
- (NSMenu *)makeMenu;
- (void)doubleTap;
- (void)moveTo:(NSPoint)p screen:(NSScreen *)screen;
- (void)savePosition;
- (void)breakInput;
- (void)input:(CGEventType)type event:(CGEventRef)event;
- (void)restoreTap;
@end

static NSScreen *screenAt(NSPoint p) {
    NSArray<NSScreen *> *screens=NSScreen.screens;
    for(NSScreen *s in screens) if(NSPointInRect(p,s.frame)) return s;
    NSScreen *best=NSScreen.mainScreen;
    double dist=INFINITY;
    for(NSScreen *s in screens) {
        NSRect r=s.visibleFrame;
        double dx=fmax(NSMinX(r)-p.x,fmax(0,p.x-NSMaxX(r)));
        double dy=fmax(NSMinY(r)-p.y,fmax(0,p.y-NSMaxY(r)));
        if(dx*dx+dy*dy<dist) {dist=dx*dx+dy*dy;best=s;}
    }
    return best;
}
@implementation PetView {
    NSPoint _grab,_origin;
}
- (BOOL)isOpaque {return NO;}
- (BOOL)acceptsFirstMouse:(NSEvent *)event {return YES;}
- (void)drawRect:(NSRect)rect {
    CGContextRef c=NSGraphicsContext.currentContext.CGContext;
    CGContextClearRect(c,self.bounds);
    CGImageRef frame=self.owner.art.frame;
    if(frame) {CGContextSetInterpolationQuality(c,kCGInterpolationHigh);CGContextDrawImage(c,NSRectToCGRect(self.bounds),frame);}
}
- (void)rightMouseDown:(NSEvent *)e {if(!self.owner.locked) [NSMenu popUpContextMenu:[self.owner makeMenu] withEvent:e forView:self];}
- (void)mouseDown:(NSEvent *)e {
    if(self.owner.locked) return;
    if(e.modifierFlags&NSEventModifierFlagControl) {[self rightMouseDown:e];return;}
    _grab=NSEvent.mouseLocation;_origin=self.window.frame.origin;self.dragging=NO;
    if(e.clickCount==2) [self.owner doubleTap];
}
- (void)mouseDragged:(NSEvent *)e {
    if(self.owner.locked) return;
    NSPoint here=NSEvent.mouseLocation; double dx=here.x-_grab.x,dy=here.y-_grab.y;
    if(!self.dragging && hypot(dx,dy)<3) return;
    if(!self.dragging) [self.owner breakInput];
    self.dragging=YES; [self.owner moveTo:NSMakePoint(_origin.x+dx,_origin.y+dy) screen:screenAt(here)];
}
- (void)mouseUp:(NSEvent *)e {if(self.dragging) [self.owner savePosition];self.dragging=NO;}
@end

static CGEventRef listenEvent(CGEventTapProxy proxy,CGEventType type,CGEventRef event,void *context) {
    KeyboardCatApp *app=(__bridge KeyboardCatApp *)context;
    if(type==kCGEventTapDisabledByTimeout || type==kCGEventTapDisabledByUserInput) [app restoreTap];
    else [app input:type event:event];
    return event;
}
@implementation KeyboardCatApp {
    NSUserDefaults *_prefs;
    NSStatusItem *_status;
    NSTimer *_timer;
    PetView *_view;
    PetPanel *_unlockPanel;
    NSPanel *_transparencyPanel;
    NSSlider *_transparencySlider;
    NSTextField *_transparencyValue;
    NSSegmentedControl *_transparencyPresets;
    PressStudio *_press;
    PetStudio *_studio;
    KCMotion _motion;
    CFMachPortRef _tap;
    CFRunLoopSourceRef _source;
    NSPoint _priorMouse;
    double _previousTick,_lastRender,_nextPermissionCheck,_lastInput;
    int _size,_menuDepth,_chineseTicket,_expectedChinese;
    double _checkChineseAt;
    BOOL _chineseSource,_checkingChinese;
    BOOL _paused,_allowed,_updateMenuAfterTracking;
}
- (void)positionUnlock {
    NSRect pet=self.pet.frame; CGFloat side=28;
    [_unlockPanel setFrame:NSMakeRect(NSMaxX(pet)-side-4,NSMaxY(pet)-side-4,side,side) display:YES];
}
- (void)setPetLocked:(BOOL)locked {
    if(locked) {
        [_press.panel orderOut:nil];[_transparencyPanel orderOut:nil];_view.dragging=NO;
        if(!_unlockPanel) {
            NSRect r=NSMakeRect(0,0,28,28);
            _unlockPanel=[[PetPanel alloc] initWithContentRect:r styleMask:NSWindowStyleMaskBorderless|NSWindowStyleMaskNonactivatingPanel backing:NSBackingStoreBuffered defer:NO];
            _unlockPanel.opaque=NO;_unlockPanel.backgroundColor=NSColor.clearColor;
            _unlockPanel.hasShadow=NO;_unlockPanel.releasedWhenClosed=NO;_unlockPanel.hidesOnDeactivate=NO;
            _unlockPanel.level=self.pet.level;_unlockPanel.collectionBehavior=self.pet.collectionBehavior;
            UnlockControl *badge=[[UnlockControl alloc] initWithFrame:r];
            badge.bordered=NO;badge.title=@"";badge.imagePosition=NSImageOnly;
            badge.image=[NSImage imageWithSystemSymbolName:@"lock.fill" accessibilityDescription:@"解锁桌宠"];
            badge.contentTintColor=[NSColor colorWithSRGBRed:1 green:.94 blue:.84 alpha:1];
            badge.wantsLayer=YES;badge.layer.cornerRadius=14;badge.layer.backgroundColor=[NSColor colorWithSRGBRed:.20 green:.22 blue:.25 alpha:1].CGColor;
            badge.layer.borderWidth=1;badge.layer.borderColor=[NSColor colorWithSRGBRed:.97 green:.85 blue:.71 alpha:1].CGColor;
            badge.toolTip=@"解锁桌宠";[badge setAccessibilityLabel:@"解锁桌宠"];
            badge.target=self;badge.action=@selector(unlockPet:);_unlockPanel.contentView=badge;
        }
        if(!_unlockPanel.parentWindow) [self.pet addChildWindow:_unlockPanel ordered:NSWindowAbove];
        [self positionUnlock];[_unlockPanel orderFrontRegardless];
    } else {
        if(_unlockPanel.parentWindow) [self.pet removeChildWindow:_unlockPanel];
        [_unlockPanel orderOut:nil];
    }
    _locked=locked;self.pet.ignoresMouseEvents=locked;
    [_prefs setBool:locked forKey:@"locked"];[self breakInput];[self refreshStatus];
}
- (void)lockPet:(id)sender {[self setPetLocked:YES];}
- (void)unlockPet:(id)sender {[self setPetLocked:NO];}
- (void)applicationDidFinishLaunching:(NSNotification *)note {
    self.art=[[PetArt alloc] initWithBundle:NSBundle.mainBundle];
    if(!self.art) {
        NSAlert *a=[NSAlert new];a.messageText=@"桌宠画稿加载失败";a.informativeText=@"请重新解压下载包，将完整的 PawPaw.app 拖入「应用程序」后再打开。";
        [a runModal];[NSApp terminate:nil];return;
    }
    _prefs=NSUserDefaults.standardUserDefaults;
    // Pick up supported settings from a previous installed bundle once, on this Mac only.
    NSString *domain=NSBundle.mainBundle.bundleIdentifier;
    if(![_prefs persistentDomainForName:domain].count) {
        NSArray *domains=CFBridgingRelease(CFPreferencesCopyApplicationList(kCFPreferencesCurrentUser,kCFPreferencesAnyHost));
        for(NSString *prior in domains) {
            if(![prior hasSuffix:@".KeyboardCat"]) continue;
            NSDictionary *saved=[_prefs persistentDomainForName:prior];
            if(!saved[@"petX"] && !saved[@"selectedRoleID"] && !saved[@"permissionAsked"]) continue;
            NSArray *keys=@[@"size",@"labels",@"words",@"printLook",@"petX",@"petY",@"paused",@"deskBoard",@"deskMouse",@"customRoles",@"selectedRoleID",@"transparency"];
            for(NSString *key in saved)
                if([keys containsObject:key] || [key hasPrefix:@"clothes."] || [key hasPrefix:@"hat."]) [_prefs setObject:saved[key] forKey:key];
            break;
        }
    }
    [_prefs registerDefaults:@{@"size":@1,@"labels":@YES,@"words":@YES,@"printLook":@YES}];
    self.art.printLook=[_prefs boolForKey:@"printLook"];
    _studio=[[PetStudio alloc] initWithCatalog:self.art.catalog preferences:_prefs];
    _size=choice([_prefs integerForKey:@"size"],3);
    double durations[KC_ACTIONS]; const char *aliases[KC_ALIASES];
    for(int i=0;i<KC_WORDS;i++) {
        NSArray *rule=self.art.tricks[i];
        aliases[i*2]=[rule[0] UTF8String];aliases[i*2+1]=[rule[1] UTF8String];durations[i]=[rule[3] doubleValue];
    }
    for(int i=0;i<KC_IDLE_COUNT;i++) durations[KC_WORDS+i]=[self.art.idles[i][1] doubleValue];
    kc_init(&_motion,aliases,durations);
    [[NSDistributedNotificationCenter defaultCenter] addObserver:self selector:@selector(inputSourceChanged:)
        name:(__bridge NSString *)kTISNotifySelectedKeyboardInputSourceChanged object:nil];
    [self inputSourceChanged:nil];
    _motion.wordsEnabled=[_prefs boolForKey:@"words"];_motion.labels=[_prefs boolForKey:@"labels"];_paused=[_prefs boolForKey:@"paused"];
    NSRect r=NSMakeRect(0,0,round(320*scales[_size]),round(260*scales[_size]));
    self.pet=[[PetPanel alloc] initWithContentRect:r styleMask:NSWindowStyleMaskBorderless|NSWindowStyleMaskNonactivatingPanel backing:NSBackingStoreBuffered defer:NO];
    self.pet.opaque=NO; self.pet.backgroundColor=NSColor.clearColor; self.pet.hasShadow=NO;
    NSNumber *savedTransparency=[_prefs objectForKey:@"transparency"];
    if([savedTransparency isKindOfClass:NSNumber.class] && savedTransparency.integerValue>=0 && savedTransparency.integerValue<=MaxTransparency)
        self.pet.alphaValue=1.0-savedTransparency.integerValue/100.0;
    self.pet.level=NSFloatingWindowLevel; self.pet.hidesOnDeactivate=NO; self.pet.releasedWhenClosed=NO;
    self.pet.collectionBehavior=NSWindowCollectionBehaviorCanJoinAllSpaces|NSWindowCollectionBehaviorFullScreenAuxiliary|NSWindowCollectionBehaviorIgnoresCycle;
    _view=[[PetView alloc] initWithFrame:r]; _view.owner=self; self.pet.contentView=_view;
    if([_prefs objectForKey:@"petX"] && [_prefs objectForKey:@"petY"]) {
        NSPoint p=NSMakePoint([_prefs doubleForKey:@"petX"],[_prefs doubleForKey:@"petY"]);
        if(isfinite(p.x) && isfinite(p.y)) [self moveTo:p screen:screenAt(NSMakePoint(p.x+r.size.width/2,p.y+r.size.height/2))];
        else [self corner:nil];
    } else [self corner:nil];
    _status=[[NSStatusBar systemStatusBar] statusItemWithLength:NSSquareStatusItemLength];
    _status.button.toolTip=@"爪爪 PawPaw · 桌宠菜单";
    _allowed=CGPreflightListenEventAccess();
    [self refreshStatus]; [self.pet orderFrontRegardless];
    if([_prefs boolForKey:@"locked"]) [self setPetLocked:YES];
    NSNotificationCenter *ws=NSWorkspace.sharedWorkspace.notificationCenter;
    [ws addObserver:self selector:@selector(focusChanged:) name:NSWorkspaceDidActivateApplicationNotification object:nil];
    [ws addObserver:self selector:@selector(woke:) name:NSWorkspaceDidWakeNotification object:nil];
    [NSNotificationCenter.defaultCenter addObserver:self selector:@selector(focusChanged:) name:NSWindowDidBecomeKeyNotification object:nil];
    [NSNotificationCenter.defaultCenter addObserver:self selector:@selector(screensChanged:) name:NSApplicationDidChangeScreenParametersNotification object:nil];
    _previousTick=Now();_priorMouse=NSEvent.mouseLocation;
    [self focusChanged:nil]; [self updatePermission]; [self tick:nil];
    _timer=[NSTimer timerWithTimeInterval:1./40 target:self selector:@selector(tick:) userInfo:nil repeats:YES];
    _timer.tolerance=.005;[[NSRunLoop mainRunLoop] addTimer:_timer forMode:NSRunLoopCommonModes];
    if(!_paused && !_allowed && ![_prefs boolForKey:@"permissionAsked"]) {
        [_prefs setBool:YES forKey:@"permissionAsked"];
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW,(int64_t)(.7*NSEC_PER_SEC)),dispatch_get_main_queue(),^{
            if(!self->_paused && !CGPreflightListenEventAccess()) CGRequestListenEventAccess();
            [self updatePermission];
        });
    }
}
- (void)moveTo:(NSPoint)p screen:(NSScreen *)s {
    if(!s) return;
    NSRect area=s.visibleFrame; NSSize size=self.pet.frame.size;
    p.x=fmax(NSMinX(area),fmin(p.x,NSMaxX(area)-size.width));p.y=fmax(NSMinY(area),fmin(p.y,NSMaxY(area)-size.height));
    [self.pet setFrameOrigin:NSMakePoint(round(p.x),round(p.y))];
    if(_locked) [self positionUnlock];
}
- (void)savePosition {
    NSPoint p=self.pet.frame.origin;[_prefs setDouble:p.x forKey:@"petX"];[_prefs setDouble:p.y forKey:@"petY"];
}
- (void)corner:(id)sender {
    NSScreen *s=screenAt(NSEvent.mouseLocation);if(!s) return;
    [self moveTo:NSMakePoint(NSMaxX(s.visibleFrame)-self.pet.frame.size.width-16,NSMinY(s.visibleFrame)+16) screen:s];
    [self savePosition];
}
- (void)screensChanged:(NSNotification *)note {
    NSRect r=self.pet.frame;
    [self moveTo:r.origin screen:screenAt(NSMakePoint(NSMidX(r),NSMidY(r)))];[self savePosition];
    NSPanel *closet=_press.panel;
    if(closet.visible) [closet setFrame:[closet constrainFrameRect:closet.frame toScreen:screenAt(closet.frame.origin)] display:YES];
    if(_transparencyPanel.visible) [_transparencyPanel setFrame:[_transparencyPanel constrainFrameRect:_transparencyPanel.frame toScreen:screenAt(_transparencyPanel.frame.origin)] display:YES];
}
- (void)privacy:(BOOL)secure time:(double)t {
    if(secure!=_motion.privateInput) [self clearChinese];
    kc_privacy(&_motion,secure,t);
}
- (void)clearChinese {
    [self.art.chinese clear]; _chineseTicket++; _checkChineseAt=INFINITY;
}
- (void)inputSourceChanged:(NSNotification *)note {
    _chineseSource=NO;
    TISInputSourceRef source=TISCopyCurrentKeyboardInputSource();
    if(source) {
        NSArray *languages=(__bridge NSArray *)TISGetInputSourceProperty(source,kTISPropertyInputSourceLanguages);
        for(NSString *language in languages) {
            if([language isEqualToString:@"zh"] || [language hasPrefix:@"zh-"] || [language hasPrefix:@"zh_"]) {_chineseSource=YES;break;}
        }
        CFRelease(source);
    }
    [self clearChinese];kc_break(&_motion);
}
- (void)scheduleChinese:(int)expected time:(double)t {
    _expectedChinese=expected; _checkChineseAt=t+.075; _chineseTicket++;
}
- (void)checkChinese:(double)t {
    _checkChineseAt=INFINITY;
    if(!_chineseSource || !_motion.wordsEnabled || _motion.privateInput || _motion.gameActive) return;
    int ticket=_chineseTicket, expected=_expectedChinese;
    pid_t pid=NSWorkspace.sharedWorkspace.frontmostApplication.processIdentifier;
    ChineseInput *words=self.art.chinese;
    _checkingChinese=YES;
    dispatch_async(dispatch_get_global_queue(QOS_CLASS_UTILITY,0),^{
        int match=[words atCaretOf:pid fallback:expected];
        dispatch_async(dispatch_get_main_queue(),^{
            self->_checkingChinese=NO;
            if(match<0 || ticket!=self->_chineseTicket || !self->_chineseSource || self->_paused || self->_menuDepth ||
               Now()-t>1.5 || self->_view.dragging || (NSApp.active && NSApp.keyWindow) || NSApp.modalWindow ||
               NSWorkspace.sharedWorkspace.frontmostApplication.processIdentifier!=pid || IsSecureEventInputEnabled()) return;
            kc_chinese_match(&self->_motion,match,Now());
        });
    });
}
- (void)breakInput {[self clearChinese];kc_clear_input(&_motion);_priorMouse=NSEvent.mouseLocation;}
- (void)focusChanged:(NSNotification *)n {
    [self breakInput];
    NSRunningApplication *front=NSWorkspace.sharedWorkspace.frontmostApplication;
    _motion.gameActive=[front.bundleIdentifier.lowercaseString isEqualToString:@"com.valvesoftware.dota2"] ||
        [front.executableURL.lastPathComponent.lowercaseString isEqualToString:@"dota2"];
}
- (void)woke:(NSNotification *)n {[self focusChanged:n];[self stopTap];[self updatePermission];}
- (void)restoreTap {[self breakInput];if(_tap && !_paused) CGEventTapEnable(_tap,true);}
- (void)stopTap {
    if(_source) {CFRunLoopRemoveSource(CFRunLoopGetMain(),_source,kCFRunLoopCommonModes);CFRelease(_source);_source=NULL;}
    if(_tap) {CFMachPortInvalidate(_tap);CFRelease(_tap);_tap=NULL;}
}
- (void)updatePermission {
    BOOL before=_allowed,hadTap=_tap!=NULL;
    _allowed=CGPreflightListenEventAccess();
    BOOL lostTap=_tap && (!CFMachPortIsValid(_tap) || !CGEventTapIsEnabled(_tap));
    if(_paused || !_allowed || lostTap) [self stopTap];
    if(!_paused && _allowed && !_tap) {
        CGEventMask mask=CGEventMaskBit(kCGEventKeyDown)|CGEventMaskBit(kCGEventKeyUp)|CGEventMaskBit(kCGEventFlagsChanged)|
          CGEventMaskBit(kCGEventLeftMouseDown)|CGEventMaskBit(kCGEventLeftMouseUp)|CGEventMaskBit(kCGEventRightMouseDown)|
          CGEventMaskBit(kCGEventRightMouseUp)|CGEventMaskBit(kCGEventOtherMouseDown)|CGEventMaskBit(kCGEventOtherMouseUp)|CGEventMaskBit(kCGEventScrollWheel);
        _tap=CGEventTapCreate(kCGSessionEventTap,kCGTailAppendEventTap,kCGEventTapOptionListenOnly,mask,listenEvent,(__bridge void *)self);
        if(_tap) {
            _source=CFMachPortCreateRunLoopSource(kCFAllocatorDefault,_tap,0);
            if(_source) {CFRunLoopAddSource(CFRunLoopGetMain(),_source,kCFRunLoopCommonModes);CGEventTapEnable(_tap,true);}
            else [self stopTap];
        }
    }
    if(lostTap || before!=_allowed || hadTap!=(_tap!=NULL)) { [self breakInput]; [self refreshStatus]; }
}
- (void)permission:(id)sender {
    if(!CGPreflightListenEventAccess()) {
        [_prefs setBool:YES forKey:@"permissionAsked"];CGRequestListenEventAccess();
        if(!CGPreflightListenEventAccess()) [NSWorkspace.sharedWorkspace openURL:[NSURL URLWithString:@"x-apple.systempreferences:com.apple.preference.security?Privacy_ListenEvent"]];
    }
    [self updatePermission];
}
- (void)input:(CGEventType)type event:(CGEventRef)e {
    if(_paused || _menuDepth || _view.dragging || (NSApp.active && NSApp.keyWindow) || NSApp.modalWindow) return;
    double t=Now();
    [self privacy:IsSecureEventInputEnabled() time:t];
    if(_motion.privateInput) {[self clearChinese];return;}
    if(type==kCGEventKeyDown || type==kCGEventKeyUp || type==kCGEventFlagsChanged) {
        int hw=(int)CGEventGetIntegerValueField(e,kCGKeyboardEventKeycode);
        CGEventFlags flags=CGEventGetFlags(e);
        BOOL up=type==kCGEventKeyUp;
        if(type==kCGEventFlagsChanged) up=hw==57?!(flags&kCGEventFlagMaskAlphaShift):!CGEventSourceKeyState(kCGEventSourceStateCombinedSessionState,(CGKeyCode)hw);
        NSArray *entry=self.art.keymap[[NSString stringWithFormat:@"%d",hw]];
        int cap=entry?[entry[0] intValue]:-1, hand=cap>=0?[self.art.keys[cap][3] intValue]:0;
        BOOL shortcut=(flags&(kCGEventFlagMaskCommand|kCGEventFlagMaskControl|kCGEventFlagMaskAlternate))!=0;
        int letter=entry?[entry[2] intValue]:0;
        bool shift=hw==56 || hw==60 || hw==57;
        bool repeat=type==kCGEventKeyDown && CGEventGetIntegerValueField(e,kCGKeyboardEventAutorepeat)!=0;
        if(type!=kCGEventKeyUp && _motion.wordsEnabled && (shortcut || type==kCGEventFlagsChanged)) [self clearChinese];
        if(type==kCGEventKeyDown && !shortcut && _motion.wordsEnabled && !repeat) {
            UniChar chars[64]={0}; UniCharCount count=0;
            CGEventKeyboardGetUnicodeString(e,64,&count,chars);
            letter=kc_event_letter(letter,chars[0],(unsigned)count);
            BOOL han=NO;
            for(UniCharCount i=0;i<MIN(count,64);i++) if(chars[i]>=0x3400 && chars[i]<=0x9fff) {han=YES;break;}
            if(han && !_motion.gameActive) {
                _chineseTicket++;_checkChineseAt=INFINITY;[self.art.chinese discardSpelling];
                for(UniCharCount i=0;i<MIN(count,64);i++)
                    kc_chinese_match(&_motion,[self.art.chinese committedCharacter:chars[i] time:t],t);
                letter=0;
            } else if(_chineseSource && !_motion.gameActive) {
                NSString *legend=entry[1];
                BOOL punctuation=hw==43 || hw==47 || hw==41 || hw==44 || hw==42;
                BOOL digit=legend.length==1 && [legend characterAtIndex:0]>='1' && [legend characterAtIndex:0]<='9';
                if(letter>='A' && letter<='Z') {
                    if([self.art.chinese letter:letter time:t]) letter=0;
                } else if(hw==49 || hw==36 || hw==76 || punctuation || digit || (count==1 && chars[0]>='1' && chars[0]<='9')) {
                    int expected=-1;
                    if([self.art.chinese selectAt:t expected:&expected]) [self scheduleChinese:(hw==36 || hw==76 || punctuation?-1:expected) time:t];
                } else if(hw==51) {
                    _chineseTicket++;_checkChineseAt=INFINITY;[self.art.chinese editAt:t];
                } else if(hw!=39) [self clearChinese];
            }
        }
        kc_key(&_motion,hw,cap,hand,letter,up,repeat,shortcut,shift,t);
    } else if(type==kCGEventScrollWheel) {
        [self clearChinese];kc_interrupt_idle(&_motion,t);kc_break(&_motion);_motion.lastClick=_motion.lastMove=t;_motion.mouseSide=2;
    } else {
        bool pressed=type==kCGEventLeftMouseDown || type==kCGEventRightMouseDown || type==kCGEventOtherMouseDown;
        if(pressed) {
            int ignored=-1;
            pid_t target=(pid_t)CGEventGetIntegerValueField(e,kCGEventTargetUnixProcessID);
            NSString *bundle=(target>0 && _chineseSource)?[NSRunningApplication runningApplicationWithProcessIdentifier:target].bundleIdentifier.lowercaseString:nil;
            BOOL candidate=bundle && ([bundle containsString:@".inputmethod."] || [bundle containsString:@"imklaunchagent"]);
            if(type==kCGEventLeftMouseDown && candidate && [self.art.chinese selectAt:t expected:&ignored])
                [self scheduleChinese:-1 time:t];
            else [self clearChinese];
        }
        kc_mouse(&_motion,(int)CGEventGetIntegerValueField(e,kCGMouseEventButtonNumber),pressed,t);
    }
    _lastInput=t;
}
- (void)tick:(id)sender {
    if(!self.art) return;
    double t=Now(),dt=t-_previousTick;_previousTick=t;
    if(t>_nextPermissionCheck && !_menuDepth) {_nextPermissionCheck=t+2.5;[self updatePermission];}
    NSPoint global=NSEvent.mouseLocation;
    NSRect r=self.pet.frame;
    double x=(global.x-NSMinX(r))*320/r.size.width,y=(NSMaxY(r)-global.y)*260/r.size.height;
    BOOL follow=!_paused && !_view.dragging && !_menuDepth && !(NSApp.active && NSApp.keyWindow) && !NSApp.modalWindow;
    [self privacy:(!_paused && IsSecureEventInputEnabled()) time:t];
    follow=follow && !_motion.privateInput;
    if(follow && !_checkingChinese && t>=_checkChineseAt) [self checkChinese:t];
    if(follow && !NSEqualPoints(global,_priorMouse)) {kc_interrupt_idle(&_motion,t);_motion.lastMove=t;}
    _priorMouse=global;
    kc_step(&_motion,t,dt,x-151,y-([_studio.role[@"shape"] intValue]?90:115),follow);
    BOOL idleAllowed=!_motion.privateInput && !_view.dragging && !_menuDepth && !(NSApp.active && NSApp.keyWindow) &&
        !NSApp.modalWindow && !_motion.mouseButtons && _motion.paw[0].cap<0 && _motion.paw[1].cap<0;
    double quiet=(_paused || _tap) ? INFINITY :
        CGEventSourceSecondsSinceLastEventType(kCGEventSourceStateCombinedSessionState,kCGAnyInputEventType);
    kc_idle(&_motion,t,quiet,idleAllowed,[_studio.role[@"shape"] boolValue]);
    double cover=kc_cover(&_motion,t);
    BOOL busy=_motion.paw[0].cap>=0 || _motion.paw[1].cap>=0 || (cover>0 && cover<1) || _motion.trick>=0 || _motion.bubbleCount || _motion.wordBubble>=0 || t-_motion.lastMove<1.4 || t-_lastInput<.75;
    if(busy || t-_lastRender>=.064 || !self.art.frame) {
        [self.art renderRole:_studio.role clothes:_studio.clothes hat:_studio.hat board:_studio.board mouse:_studio.mouse motion:&_motion time:t];_lastRender=t;
        [_view setNeedsDisplay:YES]; [_press refreshFrame];
    }
    if(!_locked && !_view.dragging && !_menuDepth && !(NSEvent.pressedMouseButtons&1)) self.pet.ignoresMouseEvents=![self.art opaqueAtX:x topY:y];
}
- (void)doubleTap {kc_play(&_motion,1,Now());}
- (void)refreshStatus {
    if(!_status) return;
    _status.button.image=[self.art portraitForRole:_studio.role size:20];_status.button.title=_status.button.image?@"":@"猫";
    _status.button.toolTip=[NSString stringWithFormat:@"爪爪 PawPaw · %@",_studio.role[@"name"]];
    if(_menuDepth) {_updateMenuAfterTracking=YES;return;}
    _updateMenuAfterTracking=NO;_status.menu=[self makeMenu];
}
- (NSMenuItem *)item:(NSString *)title menu:(NSMenu *)menu action:(SEL)action tag:(NSInteger)tag checked:(BOOL)checked {
    NSMenuItem *item=[[NSMenuItem alloc] initWithTitle:title action:action keyEquivalent:@""];
    item.target=self;item.tag=tag;item.state=checked?NSControlStateValueOn:NSControlStateValueOff;[menu addItem:item];return item;
}
- (NSMenu *)sub:(NSString *)title menu:(NSMenu *)menu {
    NSMenuItem *item=[self item:title menu:menu action:NULL tag:0 checked:NO];
    NSMenu *sub=[[NSMenu alloc] initWithTitle:title];item.submenu=sub;return sub;
}
- (NSMenu *)makeMenu {
    NSMenu *menu=[[NSMenu alloc] initWithTitle:@"爪爪 PawPaw"];menu.delegate=self;
    if(_locked) {
        [self item:@"请点猫咪右上角的小锁解锁" menu:menu action:NULL tag:0 checked:NO].enabled=NO;
        [self item:@"退出桌宠" menu:menu action:@selector(quit:) tag:0 checked:NO];
        return menu;
    }
    NSMenu *cats=[self sub:@"换只猫咪" menu:menu];
    for(NSInteger i=0;i<(NSInteger)_studio.roles.count;i++) {
        NSDictionary *role=_studio.roles[i];
        [self item:role[@"name"] menu:cats action:@selector(selectCat:) tag:i checked:_studio.selected==i].image=[self.art portraitForRole:role size:20];
    }
    [self item:@"装扮工作室…" menu:menu action:@selector(openCloset:) tag:0 checked:NO];
    NSMenu *looks=[self sub:@"桌宠画风" menu:menu];
    [self item:@"猫咪画报" menu:looks action:@selector(lookFromMenu:) tag:1 checked:self.art.printLook];
    [self item:@"经典原画" menu:looks action:@selector(lookFromMenu:) tag:0 checked:!self.art.printLook];
    NSMenu *tricks=[self sub:@"单词彩蛋" menu:menu];
    for(int i=0;i<KC_WORDS;i++) {
        NSArray *rule=self.art.tricks[i];
        [self item:[NSString stringWithFormat:@"%@　 %@ / %@ / %@",rule[2],self.art.chinese.rules[i][1],rule[0],rule[1]] menu:tricks action:@selector(play:) tag:i checked:NO];
    }
    if(!AXIsProcessTrusted()) [self item:@"中文选词辅助权限…" menu:tricks action:@selector(chinesePermission:) tag:0 checked:NO];
    [tricks addItem:NSMenuItem.separatorItem];[self item:@"打字触发彩蛋" menu:tricks action:@selector(toggleWords:) tag:0 checked:_motion.wordsEnabled];
    NSMenu *sizes=[self sub:@"桌宠大小" menu:menu];
    for(int i=0;i<3;i++) [self item:@[@"小",@"标准",@"大"][i] menu:sizes action:@selector(resizePet:) tag:i checked:_size==i];
    [self item:@"锁定桌宠（点击穿透）" menu:menu action:@selector(lockPet:) tag:0 checked:NO];
    [self item:@"桌宠透明度…" menu:menu action:@selector(openTransparency:) tag:0 checked:NO];
    [menu addItem:NSMenuItem.separatorItem];
    if(!_allowed || (!_paused && !_tap)) [self item:@"启用跨应用键盘互动…" menu:menu action:@selector(permission:) tag:0 checked:NO];
    [self item:@"显示按键气泡" menu:menu action:@selector(toggleLabels:) tag:0 checked:_motion.labels];
    [self item:_paused?@"恢复键鼠互动":@"暂停键鼠互动" menu:menu action:@selector(togglePause:) tag:0 checked:NO];
    [menu addItem:NSMenuItem.separatorItem];
    [self item:@"回到屏幕右下角" menu:menu action:@selector(corner:) tag:0 checked:NO];
    NSMenu *help=[self sub:@"帮助与反馈" menu:menu];
    [self item:@"使用说明…" menu:help action:@selector(showHelp:) tag:0 checked:NO];
    [self item:@"反馈问题…" menu:help action:@selector(feedback:) tag:0 checked:NO];
    [self item:@"退出桌宠" menu:menu action:@selector(quit:) tag:0 checked:NO];
    return menu;
}
- (void)menuWillOpen:(NSMenu *)menu {_menuDepth++;[self breakInput];}
- (void)menuDidClose:(NSMenu *)menu {
    if(_menuDepth>0) _menuDepth--;
    kc_break(&_motion);_priorMouse=NSEvent.mouseLocation;
    if(!_menuDepth && _updateMenuAfterTracking)
        dispatch_async(dispatch_get_main_queue(),^{[self refreshStatus];});
}
- (void)play:(NSMenuItem *)sender {kc_play(&_motion,(int)sender.tag,Now());}
- (void)chinesePermission:(id)sender {
    AXIsProcessTrustedWithOptions((__bridge CFDictionaryRef)@{(__bridge NSString *)kAXTrustedCheckOptionPrompt:@YES});
}
- (void)toggleWords:(id)sender {[self clearChinese];_motion.wordsEnabled=!_motion.wordsEnabled;kc_break(&_motion);[_prefs setBool:_motion.wordsEnabled forKey:@"words"];[self refreshStatus];}
- (void)toggleLabels:(id)sender {_motion.labels=!_motion.labels;kc_clear_bubbles(&_motion);[_prefs setBool:_motion.labels forKey:@"labels"];[self refreshStatus];}
- (void)togglePause:(id)sender {_paused=!_paused;[_prefs setBool:_paused forKey:@"paused"];kc_reset(&_motion);[self updatePermission];[self refreshStatus];}
- (void)showHelp:(id)sender {
    [self breakInput];
    NSString *state=_paused?@"已暂停":!_allowed?@"等待「输入监控」权限":_tap?@"已连接":@"连接中，会自动重试";
    NSString *version=[NSBundle.mainBundle objectForInfoDictionaryKey:@"CFBundleShortVersionString"];
    NSAlert *help=[NSAlert new];
    help.messageText=[NSString stringWithFormat:@"爪爪 PawPaw %@",version];
    help.informativeText=[NSString stringWithFormat:@"键鼠互动　%@\n\n拖动猫咪可移动位置，右键或点菜单栏的猫打开装扮工作室。输入「小鱼」并选词，看猫咪抱鱼，也可以连续敲 fish，双击猫咪看它捧心。锁定后点击可穿过猫咪，点右上角的小锁解锁。\n\n跨应用打字需要「输入监控」权限。你可以随时在桌宠菜单暂停互动。",state];
    [help addButtonWithTitle:@"好"];
    [NSApp activateIgnoringOtherApps:YES]; [help runModal];
    [self breakInput];
}
- (void)feedback:(id)sender {
    NSURL *url=[NSURL URLWithString:@"https://github.com/18861870889/pawpaw/issues/new/choose"];
    if(![NSWorkspace.sharedWorkspace openURL:url]) {
        NSAlert *alert=[NSAlert new];alert.messageText=@"请在浏览器中打开反馈页";
        alert.informativeText=url.absoluteString;[alert runModal];
    }
}
- (void)quit:(id)sender {[NSApp terminate:nil];}
- (void)resizePet:(NSMenuItem *)sender {
    NSRect r=self.pet.frame;NSPoint bottom=NSMakePoint(NSMidX(r),NSMinY(r));
    _size=choice(sender.tag,3);[_prefs setInteger:_size forKey:@"size"];
    [self.pet setContentSize:NSMakeSize(round(320*scales[_size]),round(260*scales[_size]))];
    [self moveTo:NSMakePoint(bottom.x-self.pet.frame.size.width/2,bottom.y) screen:screenAt(bottom)];[self savePosition];[self refreshStatus];
}
- (void)roleChanged {
    kc_reset(&_motion);[_press synchronize];[self refreshStatus];_lastRender=0;
}
- (void)selectCat:(NSMenuItem *)sender {[_studio select:sender.tag];[self roleChanged];}
- (void)applyPrint:(BOOL)value {
    if(self.art.printLook==value) return;
    self.art.printLook=value;[_prefs setBool:value forKey:@"printLook"];
    [_press synchronize];[self refreshStatus];_lastRender=0;
}
- (void)lookFromMenu:(NSMenuItem *)sender {[self applyPrint:sender.tag!=0];}
- (void)chooseRoleIndex:(NSInteger)index {[_studio select:index];[self roleChanged];}
- (void)editCurrentRole:(BOOL)editing {
    [self breakInput];
    PetRoleEditor *editor=[PetRoleEditor new];
    if([editor runWithStudio:_studio art:self.art edit:editing]) [self roleChanged];
    [self breakInput];
}
- (void)outfitChanged {[_press synchronize];_lastRender=0;}
- (void)studioTrick:(NSInteger)index {kc_play(&_motion,(int)index,Now());_lastRender=0;}
- (void)showSettingsPanel:(NSPanel *)panel {
    if(!panel.visible) {
        NSRect r=self.pet.frame,area=(self.pet.screen ?: NSScreen.mainScreen).visibleFrame,f=panel.frame;
        f.origin.x=NSMinX(r)-f.size.width-8; if(f.origin.x<NSMinX(area) && NSMaxX(area)-NSMaxX(r)>NSMinX(r)-NSMinX(area)) f.origin.x=NSMaxX(r)+8;
        f.origin.x=fmax(NSMinX(area),fmin(f.origin.x,NSMaxX(area)-f.size.width));
        f.origin.y=fmax(NSMinY(area),fmin(NSMidY(r)-f.size.height/2,NSMaxY(area)-f.size.height));
        [panel setFrame:f display:NO];
    }
    [NSApp activateIgnoringOtherApps:YES];
    [panel makeKeyAndOrderFront:nil];
}
- (void)transparencyPreset:(NSSegmentedControl *)control {
    _transparencySlider.integerValue=control.selectedSegment*40;[self changeTransparency:_transparencySlider];
}
- (void)changeTransparency:(NSSlider *)slider {
    NSInteger percent=lround(slider.doubleValue);
    self.pet.alphaValue=1.0-percent/100.0;
    _transparencyValue.stringValue=[NSString stringWithFormat:@"%ld%%",(long)percent];
    _transparencyPresets.selectedSegment=percent%40==0?percent/40:-1;
    [_prefs setInteger:percent forKey:@"transparency"];
}
- (void)openTransparency:(id)sender {
    if(!_transparencyPanel) {
        _transparencyPanel=[[SettingsPanel alloc] initWithContentRect:NSMakeRect(0,0,306,170)
            styleMask:NSWindowStyleMaskTitled|NSWindowStyleMaskClosable|NSWindowStyleMaskUtilityWindow
            backing:NSBackingStoreBuffered defer:NO];
        _transparencyPanel.title=@"桌宠透明度";
        _transparencyPanel.level=NSFloatingWindowLevel;
        _transparencyPanel.releasedWhenClosed=NO;
        _transparencyPanel.hidesOnDeactivate=NO;
        _transparencyPanel.appearance=[NSAppearance appearanceNamed:NSAppearanceNameAqua];
        _transparencyPanel.backgroundColor=[NSColor colorWithSRGBRed:.969 green:.953 blue:.914 alpha:1];
        NSView *content=_transparencyPanel.contentView;
        NSTextField *title=[NSTextField labelWithString:@"桌宠透明度"];title.frame=NSMakeRect(18,126,135,23);[content addSubview:title];
        NSBox *line=[[NSBox alloc] initWithFrame:NSMakeRect(18,115,270,1)];line.boxType=NSBoxSeparator;[content addSubview:line];
        _transparencyValue=[NSTextField labelWithString:@""];
        _transparencyValue.frame=NSMakeRect(151,118,138,44);
        _transparencyValue.font=[NSFont boldSystemFontOfSize:30];
        _transparencyValue.alignment=NSTextAlignmentRight;_transparencyValue.textColor=[NSColor colorWithSRGBRed:.67 green:.22 blue:.16 alpha:1];
        [content addSubview:_transparencyValue];
        _transparencySlider=[NSSlider sliderWithValue:0 minValue:0 maxValue:MaxTransparency
            target:self action:@selector(changeTransparency:)];
        _transparencySlider.frame=NSMakeRect(17,76,272,25);
        _transparencySlider.continuous=YES;
        [_transparencySlider setAccessibilityLabel:@"桌宠透明度"];
        [content addSubview:_transparencySlider];
        NSTextField *solid=[NSTextField labelWithString:@"0%  不透明"];
        solid.frame=NSMakeRect(18,52,120,20);
        [content addSubview:solid];
        NSTextField *faint=[NSTextField labelWithString:@"80%  更透明"];
        faint.frame=NSMakeRect(169,52,119,20);
        faint.alignment=NSTextAlignmentRight;
        [content addSubview:faint];
        _transparencyPresets=[NSSegmentedControl segmentedControlWithLabels:@[@"清晰",@"轻透",@"淡影"] trackingMode:NSSegmentSwitchTrackingSelectOne target:self action:@selector(transparencyPreset:)];
        _transparencyPresets.frame=NSMakeRect(16,12,274,30);[_transparencyPresets setAccessibilityLabel:@"透明度预设"];
        [content addSubview:_transparencyPresets];
    }
    NSInteger percent=lround((1.0-self.pet.alphaValue)*100);
    _transparencySlider.integerValue=percent;
    _transparencyValue.stringValue=[NSString stringWithFormat:@"%ld%%",(long)percent];
    _transparencyPresets.selectedSegment=percent%40==0?percent/40:-1;
    [self showSettingsPanel:_transparencyPanel];
    [_transparencyPanel makeFirstResponder:_transparencySlider];
}
- (void)openCloset:(id)sender {
    if(!_press) {
        _press=[[PressStudio alloc] initWithModel:_studio art:self.art];
        __weak KeyboardCatApp *owner=self;
        _press.selectRole=^(NSInteger index){[owner chooseRoleIndex:index];};
        _press.editRole=^(BOOL editing){[owner editCurrentRole:editing];};
        _press.setLook=^(BOOL print){[owner applyPrint:print];};
        _press.appearanceChanged=^{[owner outfitChanged];};
        _press.performTrick=^(NSInteger index){[owner studioTrick:index];};
    }
    [_press synchronize];[self showSettingsPanel:_press.panel];
}
- (BOOL)applicationShouldHandleReopen:(NSApplication *)app hasVisibleWindows:(BOOL)visible {
    [self.pet orderFrontRegardless];if(_locked) [_unlockPanel orderFrontRegardless];return YES;
}
- (void)applicationWillTerminate:(NSNotification *)note {
    [_unlockPanel orderOut:nil];[_timer invalidate];[self stopTap];[self savePosition];
    [[NSDistributedNotificationCenter defaultCenter] removeObserver:self];
    [NSNotificationCenter.defaultCenter removeObserver:self];[NSWorkspace.sharedWorkspace.notificationCenter removeObserver:self];
    if(_status) [NSStatusBar.systemStatusBar removeStatusItem:_status];
}
@end

int main(int argc,const char **argv) {
    @autoreleasepool {
        NSApplication *app=NSApplication.sharedApplication;[app setActivationPolicy:NSApplicationActivationPolicyAccessory];
        __attribute__((objc_precise_lifetime)) KeyboardCatApp *controller=[KeyboardCatApp new];
        app.delegate=controller;[app run];
    }
    return 0;
}
