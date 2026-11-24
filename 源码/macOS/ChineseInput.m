#import "ChineseInput.h"
#import <Cocoa/Cocoa.h>
#import <ApplicationServices/ApplicationServices.h>
#import <Carbon/Carbon.h>

@implementation ChineseInput {
    NSInteger _longest;
}
- (instancetype)initWithBundle:(NSBundle *)bundle tricks:(NSArray *)tricks {
    if(!(self=[super init])) return nil;
    NSString *file=[NSString stringWithContentsOfURL:[bundle URLForResource:@"ChineseWords" withExtension:@"tsv"]
                                            encoding:NSUTF8StringEncoding error:NULL];
    NSMutableDictionary *byKey=[NSMutableDictionary dictionary];
    for(NSString *line in [file componentsSeparatedByCharactersInSet:NSCharacterSet.newlineCharacterSet]) {
        NSArray *fields=[line componentsSeparatedByString:@"\t"];
        if(fields.count>=4) byKey[fields[0]]=fields;
    }
    NSMutableArray *rules=[NSMutableArray array];
    for(NSArray *trick in tricks) {
        NSArray *rule=byKey[trick[0]];
        if(!rule) return nil;
        NSRange spellings=NSMakeRange(3,rule.count-3);
        for(NSUInteger j=0;j<2;j++) {
            if([rule indexOfObject:trick[j] inRange:spellings]!=NSNotFound)
                _pinyinAliases|=1u<<(rules.count*2+j);
        }
        _longest=MAX(_longest,MAX([rule[1] length],[rule[2] length]));
        [rules addObject:rule];
    }
    _rules=[rules copy];
    return self;
}
static id attribute(AXUIElementRef element,CFStringRef name) {
    CFTypeRef value=NULL;
    AXUIElementCopyAttributeValue(element,name,&value);
    return CFBridgingRelease(value);
}
- (int)matchSuffix:(NSString *)suffix endingAt:(NSInteger *)end {
    static NSString *const marks=@" 　，。！？、；：,.!?;:\u200b";
    if(suffix.length && [marks rangeOfString:[suffix substringFromIndex:suffix.length-1]].location!=NSNotFound) {
        suffix=[suffix substringToIndex:suffix.length-1];(*end)--;
    }
    for(NSUInteger i=0;i<_rules.count;i++) for(NSUInteger j=1;j<=2;j++) {
        NSString *word=_rules[i][j];
        if([suffix hasSuffix:word]) {*end-=word.length;return (int)(2*i+j-1);}
    }
    return -1;
}
- (int)matchField:(AXUIElementRef)field start:(NSInteger *)start {
    id selection=attribute(field,kAXSelectedTextRangeAttribute);
    CFRange caret;
    if(!selection || CFGetTypeID((__bridge CFTypeRef)selection)!=AXValueGetTypeID() ||
       !AXValueGetValue((__bridge AXValueRef)selection,kAXValueCFRangeType,&caret) || caret.length || caret.location<=0) return -1;
    CFIndex n=MIN(caret.location,_longest+1);
    CFRange range=CFRangeMake(caret.location-n,n);
    AXValueRef param=AXValueCreate(kAXValueCFRangeType,&range);
    CFTypeRef value=NULL;
    AXUIElementCopyParameterizedAttributeValue(field,kAXStringForRangeParameterizedAttribute,param,&value);
    CFRelease(param);
    id suffix=CFBridgingRelease(value);
    if(![suffix isKindOfClass:NSString.class] || [suffix length]>n || IsSecureEventInputEnabled()) return -1;
    *start=caret.location;
    return [self matchSuffix:suffix endingAt:start];
}
@end

@implementation KCWordTurn
@end

static BOOL ordinaryField(AXUIElementRef field) {
    NSString *role=attribute(field,kAXRoleAttribute);
    if(![role isEqual:@"AXTextField"] && ![role isEqual:@"AXTextArea"] && ![role isEqual:@"AXComboBox"]) return NO;
    if([attribute(field,kAXSubroleAttribute) isEqual:(__bridge NSString *)kAXSecureTextFieldSubrole] ||
       [attribute(field,CFSTR("AXProtectedContent")) isEqual:@YES]) return NO;
    id editable=attribute(field,CFSTR("AXEditable"));
    if(editable) return [editable isEqual:@YES];
    Boolean settable=false;
    return AXUIElementIsAttributeSettable(field,kAXValueAttribute,&settable)==kAXErrorSuccess && settable;
}
static id focusedField(pid_t pid) {
    if(pid<=0 || IsSecureEventInputEnabled() || !AXIsProcessTrusted() ||
       NSWorkspace.sharedWorkspace.frontmostApplication.processIdentifier!=pid) return nil;
    AXUIElementRef app=AXUIElementCreateApplication(pid);
    AXUIElementSetMessagingTimeout(app,.15);
    id field=attribute(app,kAXFocusedUIElementAttribute);CFRelease(app);
    if(!field || CFGetTypeID((__bridge CFTypeRef)field)!=AXUIElementGetTypeID()) return nil;
    AXUIElementRef ax=(__bridge AXUIElementRef)field;
    AXUIElementSetMessagingTimeout(ax,.15);
    return ordinaryField(ax)?field:nil;
}

@interface ChineseWordObserver ()
- (void)changedObserver:(id)observer field:(id)field;
@end
static void valueChanged(AXObserverRef observer,AXUIElementRef field,CFStringRef name,void *context) {
    (void)name;
    [(__bridge ChineseWordObserver *)context changedObserver:(__bridge id)observer field:(__bridge id)field];
}
@implementation ChineseWordObserver {
    ChineseInput *_catalog;
    void (^_hit)(pid_t,KCWordTurn *,int);
    dispatch_queue_t _worker;
    // The main run loop owns the current input turn. Accessibility IPC runs on the serial worker.
    pid_t _pid;
    KCWordTurn *_turn,*_confirmation;
    unsigned _epoch,_ticket;
    id _field,_observer;
    int _previous;
    NSInteger _position;
}
- (instancetype)initWithCatalog:(ChineseInput *)catalog hit:(void (^)(pid_t,KCWordTurn *,int))hit {
    if((self=[super init])) {
        _catalog=catalog;_hit=[hit copy];_previous=-1;
        _worker=dispatch_queue_create("app.pawpaw.word-field",DISPATCH_QUEUE_SERIAL);
    }
    return self;
}
- (void)bind:(id)field pid:(pid_t)pid baseline:(BOOL)baseline {
    if(_field==field || [_field isEqual:field]) return;
    if(_observer) {
        AXObserverRef observer=(__bridge AXObserverRef)_observer;
        CFRunLoopRemoveSource(CFRunLoopGetMain(),AXObserverGetRunLoopSource(observer),kCFRunLoopCommonModes);
        AXObserverRemoveNotification(observer,(__bridge AXUIElementRef)_field,kAXValueChangedNotification);
    }
    _observer=nil;_field=field;_previous=-1;
    if(!field) return;
    AXObserverRef obs=NULL;
    if(AXObserverCreate(pid,valueChanged,&obs)==kAXErrorSuccess) {
        if(AXObserverAddNotification(obs,(__bridge AXUIElementRef)field,kAXValueChangedNotification,(__bridge void *)self)==kAXErrorSuccess) {
            _observer=(__bridge id)obs;
            CFRunLoopAddSource(CFRunLoopGetMain(),AXObserverGetRunLoopSource(obs),kCFRunLoopCommonModes);
        }
        CFRelease(obs);
    }
    if(baseline) _previous=[_catalog matchField:(__bridge AXUIElementRef)field start:&_position];
}
- (void)sample:(pid_t)pid epoch:(unsigned)epoch turn:(KCWordTurn *)turn observer:(id)source field:(id)notified {
    dispatch_async(_worker,^{
        if(source && (![_observer isEqual:source] || ![_field isEqual:notified])) return;
        id field=focusedField(pid);
        [self bind:field pid:pid baseline:!turn || source!=nil || _field!=nil];
        if(!field || !turn) return;
        NSInteger start=0;
        int match=[_catalog matchField:(__bridge AXUIElementRef)field start:&start];
        BOOL fresh=match>=0 && (match/2!=_previous/2 || _previous<0 || _position!=start);
        _previous=match;_position=start;
        if(fresh && [field isEqual:focusedField(pid)]) dispatch_async(dispatch_get_main_queue(),^{
            if(_epoch==epoch && _pid==pid && !IsSecureEventInputEnabled() &&
               NSWorkspace.sharedWorkspace.frontmostApplication.processIdentifier==pid) {
                if(_confirmation==turn) _confirmation=nil;
                _hit(pid,turn,match);
            }
        });
    });
}
- (void)checkAfterEdit:(id)observer field:(id)field {
    pid_t pid=_pid;unsigned epoch=_epoch,ticket=++_ticket;
    if(!observer) _confirmation=_turn;
    KCWordTurn *turn=_confirmation?:_turn;
    if(!pid || !turn) return;
    for(NSNumber *ms in @[@35,@160,@420]) {
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW,ms.intValue*NSEC_PER_MSEC),dispatch_get_main_queue(),^{
            if(epoch==_epoch && ticket==_ticket) {
                [self sample:pid epoch:epoch turn:turn observer:observer field:field];
                if(ms.intValue==420) _confirmation=nil;
            }
        });
    }
}
- (void)changedObserver:(id)observer field:(id)field {
    [self checkAfterEdit:observer field:field];
}
- (void)follow:(pid_t)pid turn:(KCWordTurn *)turn refresh:(BOOL)refresh confirm:(BOOL)confirm {
    BOOL changed=pid!=_pid;
    if(changed) {_pid=pid;_epoch++;_ticket++;_confirmation=nil;}
    _turn=turn;
    if(changed || (refresh && pid)) [self sample:pid epoch:_epoch turn:nil observer:nil field:nil];
    if(confirm && pid) [self checkAfterEdit:nil field:nil];
}
@end
