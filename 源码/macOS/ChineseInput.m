#import "ChineseInput.h"
#import <ApplicationServices/ApplicationServices.h>
#import <Carbon/Carbon.h>

@implementation ChineseInput {
    NSString *_prefix,*_hanPrefix;
    double _hanLast;
    NSInteger _longest;
    int _complete;
    BOOL _hasLetters;
    double _last;
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
        _longest=MAX(_longest,MAX([rule[1] length],[rule[2] length]));
        [rules addObject:rule];
    }
    _rules=[rules copy];[self clear];return self;
}
- (void)discardSpelling {_prefix=@"";_complete=-1;_hasLetters=NO;}
- (void)clear {_hanPrefix=@"";[self discardSpelling];}
- (BOOL)letter:(int)key time:(double)t {
    if(t-_last>8) [self clear];
    _last=t;_hasLetters=YES;_complete=-1;
    NSString *next=[_prefix stringByAppendingFormat:@"%c",key-'A'+'a'];
    while(next.length) {
        BOOL found=NO;
        for(NSUInteger i=0;i<_rules.count;i++) {
            NSArray *rule=_rules[i];
            for(NSUInteger j=3;j<rule.count;j++) {
                NSString *spelling=rule[j];
                if([spelling isEqualToString:next]) _complete=(int)i*2;
                if([spelling hasPrefix:next]) found=YES;
            }
        }
        if(found) break;
        next=[next substringFromIndex:1];
    }
    _prefix=next;return _complete>=0;
}
- (void)editAt:(double)t {
    BOOL recent=_hasLetters && t-_last<=8;
    [self clear];_hasLetters=recent;_last=t;
}
- (BOOL)selectAt:(double)t expected:(int *)expected {
    *expected=-1;
    if(t-_last>8 || !_hasLetters) {[self clear];return NO;}
    *expected=_complete;
    if(_complete>=0) [self discardSpelling];
    _hasLetters=NO;_last=t;return YES;
}
- (int)matchSuffix:(NSString *)text {
    static NSString *const punctuation=@"，。！？、；：,.!?;:";
    if(text.length && [punctuation rangeOfString:[text substringFromIndex:text.length-1]].location!=NSNotFound)
        text=[text substringToIndex:text.length-1];
    for(NSUInteger i=0;i<_rules.count;i++) {
        if([text hasSuffix:_rules[i][1]]) return (int)i*2;
        if([text hasSuffix:_rules[i][2]]) return (int)i*2+1;
    }
    return -1;
}
- (int)committedCharacter:(unichar)character time:(double)t {
    if(t-_hanLast>8) _hanPrefix=@"";
    _hanLast=t;
    NSString *next=[_hanPrefix stringByAppendingString:[NSString stringWithCharacters:&character length:1]];
    while(next.length) {
        BOOL prefix=NO;
        for(NSUInteger i=0;i<_rules.count;i++) for(int j=1;j<=2;j++) {
            NSString *word=_rules[i][j];
            if([word isEqualToString:next]) {_hanPrefix=@"";return (int)i*2+j-1;}
            if([word hasPrefix:next]) prefix=YES;
        }
        if(prefix) break;
        next=[next substringFromIndex:1];
    }
    _hanPrefix=next;return -1;
}
- (NSString *)wordForMatch:(int)match {
    return match>=0 && (NSUInteger)(match/2)<_rules.count?_rules[match/2][1+match%2]:nil;
}
static id attribute(AXUIElementRef element,CFStringRef name) {
    CFTypeRef value=NULL;
    AXUIElementCopyAttributeValue(element,name,&value);
    return CFBridgingRelease(value);
}
- (int)atCaretOf:(pid_t)pid fallback:(int)fallback {
    if(pid<=0 || IsSecureEventInputEnabled()) return -1;
    if(!AXIsProcessTrusted()) return fallback;
    AXUIElementRef app=AXUIElementCreateApplication(pid);
    AXUIElementSetMessagingTimeout(app,.18);
    id field=attribute(app,kAXFocusedUIElementAttribute);
    CFRelease(app);
    if(!field || CFGetTypeID((__bridge CFTypeRef)field)!=AXUIElementGetTypeID()) return -1;
    AXUIElementRef el=(__bridge AXUIElementRef)field;
    AXUIElementSetMessagingTimeout(el,.18);
    NSString *role=attribute(el,kAXRoleAttribute),*subrole=attribute(el,kAXSubroleAttribute);
    if([subrole isEqualToString:(__bridge NSString *)kAXSecureTextFieldSubrole] ||
       [attribute(el,CFSTR("AXProtectedContent")) isEqual:@YES] || IsSecureEventInputEnabled()) return -1;
    if(![role isEqual:@"AXTextField"] && ![role isEqual:@"AXTextArea"] && ![role isEqual:@"AXComboBox"]) return fallback;
    id selection=attribute(el,kAXSelectedTextRangeAttribute);
    if(!selection) return fallback;
    CFRange caret;
    if(CFGetTypeID((__bridge CFTypeRef)selection)!=AXValueGetTypeID() ||
       !AXValueGetValue((__bridge AXValueRef)selection,kAXValueCFRangeType,&caret) || caret.length || caret.location<=0) return -1;
    CFIndex length=MIN(caret.location,_longest+1);
    CFRange range=CFRangeMake(caret.location-length,length);
    AXValueRef parameter=AXValueCreate(kAXValueCFRangeType,&range);
    CFTypeRef value=NULL;
    AXError result=AXUIElementCopyParameterizedAttributeValue(el,kAXStringForRangeParameterizedAttribute,parameter,&value);
    CFRelease(parameter);
    id suffix=CFBridgingRelease(value);
    if(IsSecureEventInputEnabled()) return -1;
    if(result==kAXErrorParameterizedAttributeUnsupported || result==kAXErrorNotImplemented) return fallback;
    if(![suffix isKindOfClass:NSString.class] || [suffix length]>length) return -1;
    return [self matchSuffix:suffix];
}
@end
