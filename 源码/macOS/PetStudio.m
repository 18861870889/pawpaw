#import "PetStudio.h"
#import "PetArt.h"

static int indexOrZero(id value,NSUInteger count) {
    NSInteger n=[value isKindOfClass:NSNumber.class]?[value integerValue]:0;
    return n>=0 && (NSUInteger)n<count?(int)n:0;
}
static BOOL validIndex(id value,NSUInteger count) {
    return [value isKindOfClass:NSNumber.class] && [value integerValue]>=0 && [value unsignedIntegerValue]<count;
}
static BOOL validUserID(id value) {
    if(![value isKindOfClass:NSString.class] || [value length]!=37 || ![value hasPrefix:@"user-"]) return NO;
    NSCharacterSet *hex=[NSCharacterSet characterSetWithCharactersInString:@"0123456789abcdefABCDEF"];
    return [[value substringFromIndex:5] rangeOfCharacterFromSet:hex.invertedSet].location==NSNotFound;
}
static BOOL validName(id name) {
    if(![name isKindOfClass:NSString.class] || ![name length] || [name length]>24) return NO;
    return [name rangeOfCharacterFromSet:NSCharacterSet.controlCharacterSet].location==NSNotFound &&
        [[name stringByTrimmingCharactersInSet:NSCharacterSet.whitespaceAndNewlineCharacterSet] length]>0;
}

@implementation PetStudio {
    NSMutableArray<NSDictionary *> *_items;
    NSUserDefaults *_prefs;
}
- (NSArray *)roles {return _items;}
- (NSDictionary *)role {return _items[_selected];}
- (BOOL)canEdit {return (NSUInteger)_selected>=[_catalog[@"roles"] count];}
- (instancetype)initWithCatalog:(NSDictionary *)catalog preferences:(NSUserDefaults *)prefs {
    self=[super init];if(!self) return nil;
    _catalog=catalog;_prefs=prefs;_items=[catalog[@"roles"] mutableCopy];
    NSArray *saved=[prefs arrayForKey:@"customRoles"];
    BOOL migrated=NO;
    for(NSDictionary *r in saved) {
        if(![r isKindOfClass:NSDictionary.class] || !validUserID(r[@"id"]) || !validName(r[@"name"]) ||
            !validIndex(r[@"shape"],[catalog[@"shapes"] count]) || !validIndex(r[@"pattern"],[catalog[@"patterns"] count]) ||
            !validIndex(r[@"eyes"],[catalog[@"eyes"] count]) || [[_items valueForKey:@"id"] containsObject:r[@"id"]] ||
            ![self nameAvailable:r[@"name"] forID:r[@"id"]]) continue;
        if(r[@"photo"]) {
            NSMutableDictionary *role=[r mutableCopy];[role removeObjectForKey:@"photo"];
            [_items addObject:role];migrated=YES;
        } else [_items addObject:r];
    }
    if(migrated) [self saveUsers];
    id active=[prefs objectForKey:@"selectedRoleID"];
    NSUInteger found=active?[[_items valueForKey:@"id"] indexOfObject:active]:NSNotFound;
    _selected=found!=NSNotFound?(NSInteger)found:0;
    _board=indexOrZero([prefs objectForKey:@"deskBoard"],[catalog[@"boards"] count]);
    _mouse=indexOrZero([prefs objectForKey:@"deskMouse"],[catalog[@"mice"] count]);
    [self loadOutfit];
    return self;
}
- (NSString *)outfitKey:(NSString *)kind {return [NSString stringWithFormat:@"%@.%@",kind,self.role[@"id"]];}
- (int)setting:(NSString *)kind count:(NSUInteger)count {
    id value=[_prefs objectForKey:[self outfitKey:kind]];
    return indexOrZero(value,count);
}
- (void)loadOutfit {
    _clothes=[self setting:@"clothes" count:[_catalog[@"clothes"] count]];
    _hat=[self setting:@"hat" count:[_catalog[@"hats"] count]];
}
- (void)select:(NSInteger)index {
    if(index<0 || (NSUInteger)index>=_items.count) return;
    _selected=index;[_prefs setObject:self.role[@"id"] forKey:@"selectedRoleID"];[self loadOutfit];
}
- (void)wear:(NSInteger)clothes hat:(NSInteger)hat board:(NSInteger)board mouse:(NSInteger)mouse {
    _clothes=indexOrZero(@(clothes),[_catalog[@"clothes"] count]);_hat=indexOrZero(@(hat),[_catalog[@"hats"] count]);
    _board=indexOrZero(@(board),[_catalog[@"boards"] count]);_mouse=indexOrZero(@(mouse),[_catalog[@"mice"] count]);
    [_prefs setInteger:_clothes forKey:[self outfitKey:@"clothes"]];[_prefs setInteger:_hat forKey:[self outfitKey:@"hat"]];
    [_prefs setInteger:_board forKey:@"deskBoard"];[_prefs setInteger:_mouse forKey:@"deskMouse"];
}
- (BOOL)nameAvailable:(NSString *)name forID:(NSString *)identifier {
    if(!validName(name))return NO;
    for(NSDictionary *item in _items)
        if(![item[@"id"] isEqual:identifier] && [item[@"name"] caseInsensitiveCompare:name]==NSOrderedSame) return NO;
    return YES;
}
- (void)saveUsers {
    NSUInteger start=[_catalog[@"roles"] count];
    [_prefs setObject:[_items subarrayWithRange:NSMakeRange(start,_items.count-start)] forKey:@"customRoles"];
}
- (void)storeRole:(NSDictionary *)role {
    if(!validUserID(role[@"id"]) || ![self nameAvailable:role[@"name"] forID:role[@"id"]] ||
        !validIndex(role[@"shape"],[_catalog[@"shapes"] count]) || !validIndex(role[@"pattern"],[_catalog[@"patterns"] count]) ||
        !validIndex(role[@"eyes"],[_catalog[@"eyes"] count])) return;
    NSUInteger pos=[[_items valueForKey:@"id"] indexOfObject:role[@"id"]];
    if(pos==NSNotFound) {pos=_items.count;[_items addObject:role];} else _items[pos]=role;
    [self saveUsers];[self select:pos];
}
- (void)removeCurrentRole {
    if(!self.canEdit) return;
    [_prefs removeObjectForKey:[self outfitKey:@"clothes"]];[_prefs removeObjectForKey:[self outfitKey:@"hat"]];
    [_items removeObjectAtIndex:_selected];[self saveUsers];[self select:0];
}
@end

@implementation PetRoleEditor {
    PetStudio *_studio;
    PetArt *_art;
    NSAlert *_dialog;
    NSString *_identifier;
    NSTextField *_name,*_feedback;
    NSPopUpButton *_shape,*_pattern,*_eyes;
    NSImageView *_portrait;
}
- (NSDictionary *)candidate {
    NSString *name=[_name.stringValue stringByTrimmingCharactersInSet:NSCharacterSet.whitespaceAndNewlineCharacterSet];
    return @{@"id":_identifier,@"name":name,@"shape":@(_shape.indexOfSelectedItem),
             @"pattern":@(_pattern.indexOfSelectedItem),@"eyes":@(_eyes.indexOfSelectedItem)};
}
- (void)validate {
    NSString *name=self.candidate[@"name"];
    BOOL valid=[_studio nameAvailable:name forID:_identifier];
    _dialog.buttons.firstObject.enabled=valid;
    _feedback.textColor=name.length?NSColor.systemRedColor:NSColor.secondaryLabelColor;
    _feedback.stringValue=!name.length?@"给这只猫起个名字。":valid?@"":validName(name)?@"这个名字已经有猫在用啦。":@"名字需要 1 至 24 个字符。";
}
- (void)controlTextDidChange:(NSNotification *)note {[self validate];}
- (void)preview:(id)sender {_portrait.image=[_art portraitForRole:self.candidate size:128];}
- (void)label:(NSString *)title y:(CGFloat)y inView:(NSView *)view {
    NSTextField *label=[NSTextField labelWithString:title];label.frame=NSMakeRect(139,y+4,45,20);[view addSubview:label];
}
- (NSPopUpButton *)picker:(NSString *)key title:(NSString *)title y:(CGFloat)y inView:(NSView *)view {
    [self label:title y:y inView:view];
    NSPopUpButton *button=[[NSPopUpButton alloc] initWithFrame:NSMakeRect(183,y,170,27) pullsDown:NO];
    [button addItemsWithTitles:_studio.catalog[key]];
    button.target=self;button.action=@selector(preview:);[button setAccessibilityLabel:title];[view addSubview:button];return button;
}
- (BOOL)runWithStudio:(PetStudio *)studio art:(PetArt *)art edit:(BOOL)editing {
    if(editing && !studio.canEdit) return NO;
    _studio=studio;_art=art;
    _identifier=editing?studio.role[@"id"]:[@"user-" stringByAppendingString:
        [NSUUID.UUID.UUIDString.lowercaseString stringByReplacingOccurrencesOfString:@"-" withString:@""]];
    _dialog=[NSAlert new];_dialog.messageText=editing?@"编辑角色":@"新建角色";
    [_dialog addButtonWithTitle:@"保存"];[_dialog addButtonWithTitle:@"取消"];
    if(editing) [_dialog addButtonWithTitle:@"删除角色…"];
    NSView *view=[[NSView alloc] initWithFrame:NSMakeRect(0,0,360,165)];
    _portrait=[[NSImageView alloc] initWithFrame:NSMakeRect(0,21,133,133)];
    _portrait.imageScaling=NSImageScaleProportionallyUpOrDown;[view addSubview:_portrait];
    [self label:@"名字" y:133 inView:view];
    _name=[[NSTextField alloc] initWithFrame:NSMakeRect(185,134,166,23)];_name.delegate=self;
    [_name setAccessibilityLabel:@"名字"];_name.stringValue=editing?studio.role[@"name"]:@"";[view addSubview:_name];
    _shape=[self picker:@"shapes" title:@"体型" y:98 inView:view];[_shape selectItemAtIndex:[studio.role[@"shape"] integerValue]];
    _pattern=[self picker:@"patterns" title:@"花纹" y:65 inView:view];[_pattern selectItemAtIndex:[studio.role[@"pattern"] integerValue]];
    _eyes=[self picker:@"eyes" title:@"瞳色" y:32 inView:view];[_eyes selectItemAtIndex:[studio.role[@"eyes"] integerValue]];
    _feedback=[NSTextField labelWithString:@""];_feedback.frame=NSMakeRect(140,3,213,22);_feedback.textColor=NSColor.systemRedColor;[view addSubview:_feedback];
    _dialog.icon=[art portraitForRole:studio.role size:64];_dialog.accessoryView=view;_dialog.window.initialFirstResponder=_name;[self preview:nil];[self validate];
    NSModalResponse result=[_dialog runModal];
    if(result==NSAlertFirstButtonReturn && [studio nameAvailable:self.candidate[@"name"] forID:_identifier]) {
        [studio storeRole:self.candidate];return YES;
    }
    if(editing && result==NSAlertThirdButtonReturn) {
        NSAlert *remove=[NSAlert new];remove.messageText=[NSString stringWithFormat:@"删除「%@」和它保存的穿搭？",studio.role[@"name"]];
        [remove addButtonWithTitle:@"取消"];[remove addButtonWithTitle:@"删除角色"];
        if([remove runModal]==NSAlertSecondButtonReturn) {[studio removeCurrentRole];return YES;}
    }
    return NO;
}
@end
