#import "PressStudio.h"
#import "PetStudio.h"
#import "PetArt.h"
#import <ImageIO/ImageIO.h>
#import <math.h>

static NSColor *color(unsigned rgb) {
    return [NSColor colorWithSRGBRed:((rgb>>16)&255)/255. green:((rgb>>8)&255)/255. blue:(rgb&255)/255. alpha:1];
}
static const unsigned paper=0xf7f3e9,ink=0x202b36,accent=0xab382a;
static NSFont *displayFont(CGFloat size) {
    return [NSFont fontWithName:@"Songti SC Bold" size:size] ?: [NSFont fontWithName:@"Songti SC" size:size] ?: [NSFont boldSystemFontOfSize:size];
}
static NSTextField *label(NSView *view,NSString *title,NSFont *font,NSRect box) {
    NSTextField *t=[NSTextField labelWithString:title];t.frame=box;t.font=font;t.textColor=color(ink);
    t.lineBreakMode=NSLineBreakByTruncatingTail;t.usesSingleLineMode=YES;[view addSubview:t];return t;
}
static void rule(NSView *parent,CGFloat x,CGFloat y,CGFloat width,unsigned rgb) {
    NSBox *line=[[NSBox alloc] initWithFrame:NSMakeRect(x,y,width,1)];line.boxType=NSBoxCustom;
    line.borderWidth=0;line.fillColor=color(rgb);[parent addSubview:line];
}
static CGImageRef loadPNG(NSString *name) {
    NSURL *url=[NSBundle.mainBundle URLForResource:name withExtension:@"png"];
    if(!url) return NULL;
    CGImageSourceRef source=CGImageSourceCreateWithURL((__bridge CFURLRef)url,NULL);
    if(!source) return NULL;
    CGImageRef img=CGImageSourceCreateImageAtIndex(source,0,NULL);CFRelease(source);return img;
}
static NSImage *cut(CGImageRef source,CGRect crop,NSSize logical) {
    if(!source) return nil;
    CGImageRef frame=CGImageCreateWithImageInRect(source,crop);
    if(!frame) return nil;
    NSImage *image=[[NSImage alloc] initWithCGImage:frame size:logical];CGImageRelease(frame);return image;
}
static void drawImage(NSImage *image,NSRect target) {
    [image drawInRect:target fromRect:NSZeroRect operation:NSCompositingOperationSourceOver fraction:1 respectFlipped:YES hints:nil];
}
@interface PressPanel : NSPanel @end
@implementation PressPanel
- (void)cancelOperation:(id)sender {[self close];}
@end
@interface PressPage : NSView @end
@implementation PressPage
- (BOOL)isFlipped {return YES;}
- (void)drawRect:(NSRect)dirty {[color(paper) setFill];NSRectFill(dirty);}
@end

@interface PressCover : NSView
@property(nonatomic,strong) NSImage *cover;
@property(nonatomic,weak) PetArt *art;
@end
@implementation PressCover
- (BOOL)isFlipped {return YES;}
- (void)drawRect:(NSRect)dirty {
    drawImage(self.cover,self.bounds);
    CGImageRef image=self.art.frame;
    if(!image) return;
    CGFloat scale=fmin(NSWidth(self.bounds)/320,(NSHeight(self.bounds)-76)/260);
    CGFloat w=320*scale,h=260*scale;
    CGContextRef g=NSGraphicsContext.currentContext.CGContext;
    CGContextSaveGState(g);CGContextSetInterpolationQuality(g,kCGInterpolationHigh);
    CGContextTranslateCTM(g,(NSWidth(self.bounds)-w)/2,NSHeight(self.bounds)-4);
    CGContextScaleCTM(g,1,-1);CGContextDrawImage(g,CGRectMake(0,0,w,h),image);CGContextRestoreGState(g);
}
@end

typedef NS_ENUM(NSInteger,PressKind) {PressTab,PressSwitch,PressQuiet,PressPlay,PressPrimary,PressCard};
@interface PressButton : NSButton
@property(nonatomic) PressKind kind;
@property(nonatomic) BOOL chosen;
@property(nonatomic) unsigned tint;
@property(nonatomic,strong) NSImage *artwork,*printed,*original;
@property(nonatomic) BOOL keyboard;
@property(nonatomic,copy) void (^arrow)(NSInteger step);
@end
@implementation PressButton {
    NSTrackingArea *_tracking;
    BOOL _hover;
}
- (BOOL)isFlipped {return YES;}
- (void)updateTrackingAreas {
    [super updateTrackingAreas];
    if(_tracking) [self removeTrackingArea:_tracking];
    _tracking=[[NSTrackingArea alloc] initWithRect:NSZeroRect options:NSTrackingMouseEnteredAndExited|NSTrackingActiveInKeyWindow|NSTrackingInVisibleRect owner:self userInfo:nil];
    [self addTrackingArea:_tracking];
}
- (void)mouseEntered:(NSEvent *)event {_hover=YES;[self setNeedsDisplay:YES];}
- (void)mouseExited:(NSEvent *)event {_hover=NO;[self setNeedsDisplay:YES];}
- (void)keyDown:(NSEvent *)e {
    if(self.arrow && e.keyCode>=123 && e.keyCode<=126) {
        NSInteger step=e.keyCode==123?-1:e.keyCode==124?1:e.keyCode==125?3:-3;
        self.arrow(step);return;
    }
    [super keyDown:e];
}
- (void)setChosen:(BOOL)value {
    if(_chosen==value) return;
    _chosen=value;[self setAccessibilityValue:value?@1:@0];[self setNeedsDisplay:YES];
}
- (void)drawRect:(NSRect)dirty {
    NSRect b=self.bounds;
    BOOL dark=self.kind==PressPrimary || (self.kind==PressSwitch && self.chosen);
    BOOL hover=(_hover || self.cell.highlighted) && self.enabled;
    unsigned foreground=self.enabled?(dark?paper:ink):0x847f74;
    unsigned bg=self.kind==PressCard?self.tint:(dark?ink:hover?0xeae6da:paper);
    if(self.cell.highlighted && self.enabled && self.kind!=PressCard) bg=dark?0x404952:0xdcd6c8;
    [color(bg) setFill];NSRectFill(b);
    CGFloat textTop=0,textLeft=0,textHeight=NSHeight(b);
    if(self.kind==PressCard) {
        textHeight=23;textTop=NSHeight(b)-textHeight;
        CGFloat side=(textTop-1)*.98;
        if(self.keyboard) {
            CGFloat w=fmin(NSWidth(b)-14,textTop*2);
            drawImage(self.artwork,NSMakeRect((NSWidth(b)-w)/2,(textTop-w/2)/2,w,w/2));
        } else drawImage(self.artwork,NSMakeRect((NSWidth(b)-side)/2,(textTop-side)/2+1,side,side));
        if(self.chosen || hover) {
            CGFloat width=self.chosen?2:1;
            [color(self.chosen?accent:ink) setStroke];
            NSBezierPath *edge=[NSBezierPath bezierPathWithRect:NSInsetRect(b,width/2,width/2)];edge.lineWidth=width;[edge stroke];
        }
        if(self.chosen) {
            CGFloat x=NSWidth(b)-12;
            [color(accent) setFill];[[NSBezierPath bezierPathWithOvalInRect:NSMakeRect(x-6,5,12,12)] fill];
            NSBezierPath *tick=[NSBezierPath bezierPath];[tick moveToPoint:NSMakePoint(x-3,11)];
            [tick lineToPoint:NSMakePoint(x-1,13)];[tick lineToPoint:NSMakePoint(x+3,9)];tick.lineWidth=1.4;
            [NSColor.whiteColor setStroke];[tick stroke];
        }
    } else if(self.kind==PressTab) {
        CGFloat h=self.chosen?3:1;[color(self.chosen?accent:0xccc9bf) setFill];NSRectFill(NSMakeRect(0,NSHeight(b)-h,NSWidth(b),h));
        if(self.chosen) foreground=accent;
    } else if(self.kind==PressPlay || self.kind==PressSwitch) {
        [color(ink) setStroke];[NSBezierPath strokeRect:NSInsetRect(b,.5,.5)];
        if(self.kind==PressPlay) {
            CGFloat cy=NSMidY(b);NSBezierPath *play=[NSBezierPath bezierPath];[play moveToPoint:NSMakePoint(12,cy-4)];
            [play lineToPoint:NSMakePoint(12,cy+4)];[play lineToPoint:NSMakePoint(19,cy)];[play closePath];[color(accent) setFill];[play fill];textLeft=12;
        }
    }
    NSMutableParagraphStyle *p=[NSMutableParagraphStyle new];p.alignment=NSTextAlignmentCenter;p.lineBreakMode=NSLineBreakByTruncatingTail;
    NSDictionary *attrs=@{NSFontAttributeName:self.font,NSForegroundColorAttributeName:color(foreground),NSParagraphStyleAttributeName:p};
    CGFloat h=ceil([self.title sizeWithAttributes:attrs].height);
    [self.title drawInRect:NSMakeRect(textLeft+2,textTop+(textHeight-h)/2,NSWidth(b)-textLeft-4,h+1) withAttributes:attrs];
    if(self.window.firstResponder==self && self.window.isKeyWindow) {
        [color(foreground) setStroke];NSBezierPath *focus=[NSBezierPath bezierPathWithRect:NSInsetRect(b,4,4)];
        CGFloat pattern[]={1,2};[focus setLineDash:pattern count:2 phase:0];[focus stroke];
    }
}
@end

@implementation PressStudio {
    PetStudio *_studio;
    PetArt *_art;
    PressCover *_preview;
    NSTextField *_name,*_detail,*_outfitLine,*_deskLine,*_scope;
    PressButton *_edit,*_reset,*_print,*_classic,*_playButton;
    NSMenu *_trickMenu;
    NSPopUpButton *_roles,*_builds;
    NSMutableArray<PressButton *> *_tabs;
    NSMutableArray<NSArray<PressButton *> *> *_cards;
    NSMutableArray<NSImage *> *_covers;
    NSInteger _group,_selectedTrick;
    NSArray *_roleSnapshot;
}
- (PressButton *)button:(NSString *)title kind:(PressKind)kind rect:(NSRect)rect action:(SEL)action on:(NSView *)parent {
    PressButton *b=[[PressButton alloc] initWithFrame:rect];b.title=title;b.font=[NSFont systemFontOfSize:13];
    b.bordered=NO;b.kind=kind;b.target=self;b.action=action;b.focusRingType=NSFocusRingTypeNone;
    [b setAccessibilityLabel:title];[parent addSubview:b];return b;
}
- (instancetype)initWithModel:(PetStudio *)studio art:(PetArt *)art {
    self=[super init];if(!self) return nil;
    _studio=studio;_art=art;_tabs=[NSMutableArray array];_cards=[NSMutableArray array];_covers=[NSMutableArray array];
    NSSize size=NSMakeSize(782,555);NSRect screen=(NSScreen.mainScreen ?: NSScreen.screens.firstObject).visibleFrame;
    _panel=[[PressPanel alloc] initWithContentRect:NSMakeRect(0,0,fmin(size.width,screen.size.width),fmin(size.height,screen.size.height-32))
        styleMask:NSWindowStyleMaskTitled|NSWindowStyleMaskClosable|NSWindowStyleMaskUtilityWindow backing:NSBackingStoreBuffered defer:NO];
    _panel.title=@"爪爪 PawPaw · 装扮工作室";_panel.level=NSFloatingWindowLevel;_panel.releasedWhenClosed=NO;_panel.hidesOnDeactivate=NO;
    _panel.appearance=[NSAppearance appearanceNamed:NSAppearanceNameAqua];_panel.backgroundColor=color(paper);
    NSScrollView *scroll=[[NSScrollView alloc] initWithFrame:_panel.contentView.bounds];
    scroll.hasVerticalScroller=YES;scroll.hasHorizontalScroller=YES;scroll.autohidesScrollers=YES;
    scroll.borderType=NSNoBorder;scroll.drawsBackground=NO;scroll.autoresizingMask=NSViewWidthSizable|NSViewHeightSizable;
    PressPage *page=[[PressPage alloc] initWithFrame:NSMakeRect(0,0,size.width,size.height)];
    scroll.documentView=page;_panel.contentView=scroll;

    CGImageRef sheet=loadPNG(@"StudioCovers"),items=loadPNG(@"StudioItems"),mark=loadPNG(@"StudioMark");
    for(int i=0;i<6 && sheet;i++) {
        NSImage *image=cut(sheet,CGRectMake(i%3*664,i/3*676,664,676),NSMakeSize(332,338));
        if(image) [_covers addObject:image];
    }
    if(sheet) CGImageRelease(sheet);
    NSImageView *logo=[[NSImageView alloc] initWithFrame:NSMakeRect(24,16,36,32)];
    if(mark){logo.image=[[NSImage alloc] initWithCGImage:mark size:NSMakeSize(36,32)];CGImageRelease(mark);}
    [page addSubview:logo];
    label(page,@"爪爪 PawPaw",displayFont(31),NSMakeRect(66,9,280,45));
    label(page,@"桌宠画风",[NSFont systemFontOfSize:13],NSMakeRect(545,20,73,22));
    _print=[self button:@"画报" kind:PressSwitch rect:NSMakeRect(622,14,65,31) action:@selector(look:) on:page];
    _classic=[self button:@"原画" kind:PressSwitch rect:NSMakeRect(687,14,69,31) action:@selector(look:) on:page];
    rule(page,24,59,732,ink);
    _preview=[[PressCover alloc] initWithFrame:NSMakeRect(24,77,332,338)];_preview.art=art;[page addSubview:_preview];
    _name=label(_preview,@"",displayFont(53),NSMakeRect(16,8,247,67));
    _detail=label(_preview,@"",[NSFont systemFontOfSize:13],NSMakeRect(19,75,270,23));
    NSBox *band=[[NSBox alloc] initWithFrame:NSMakeRect(24,415,332,27)];band.boxType=NSBoxCustom;band.borderWidth=0;band.fillColor=color(ink);[page addSubview:band];
    _outfitLine=label(page,@"",[NSFont systemFontOfSize:13],NSMakeRect(36,420,310,21));_outfitLine.textColor=color(paper);
    _deskLine=label(page,@"",[NSFont systemFontOfSize:13],NSMakeRect(380,420,376,21));
    label(page,@"角色",[NSFont systemFontOfSize:13],NSMakeRect(24,448,46,20));
    _roles=[[NSPopUpButton alloc] initWithFrame:NSMakeRect(23,469,164,31) pullsDown:NO];
    _roles.target=self;_roles.action=@selector(role:);[_roles setAccessibilityLabel:@"选择角色"];[page addSubview:_roles];
    label(page,@"体型",[NSFont systemFontOfSize:13],NSMakeRect(197,448,60,20));
    _builds=[[NSPopUpButton alloc] initWithFrame:NSMakeRect(196,469,161,31) pullsDown:NO];
    [_builds addItemsWithTitles:[_studio.catalog[@"builds"] valueForKey:@"name"]];
    _builds.target=self;_builds.action=@selector(build:);[_builds setAccessibilityLabel:@"猫咪体型"];[page addSubview:_builds];
    [self button:@"新建角色…" kind:PressQuiet rect:NSMakeRect(24,509,110,30) action:@selector(edit:) on:page];
    _edit=[self button:@"编辑角色…" kind:PressQuiet rect:NSMakeRect(142,509,110,30) action:@selector(edit:) on:page];

    label(page,@"装扮工作室",[NSFont boldSystemFontOfSize:22],NSMakeRect(380,75,265,32));
    _scope=label(page,@"",[NSFont systemFontOfSize:13],NSMakeRect(537,82,219,22));
    _scope.alignment=NSTextAlignmentRight;_scope.textColor=color(0x615e56);
    CGFloat side=items?CGImageGetWidth(items)/7.:224;
    NSArray *groups=@[@"clothes",@"hats",@"boards",@"mice"],*titles=@[@"衣服",@"头饰",@"键盘",@"鼠标"];
    const unsigned tints[]={0xeee9dc,0xf2deda,0xe0e8e7,0xf4e7c4,0xe3e7d6,0xe7e4ed,0xeee3d8};
    __weak PressStudio *weak=self;
    for(NSInteger i=0;i<4;i++) {
        PressButton *tab=[self button:titles[i] kind:PressTab rect:NSMakeRect(380+i*94,113,94,33) action:@selector(group:) on:page];
        tab.tag=i;[tab setAccessibilityLabel:[titles[i] stringByAppendingString:@"分类"]];[_tabs addObject:tab];
        NSArray *names=studio.catalog[groups[i]];NSMutableArray *row=[NSMutableArray array];
        for(NSInteger j=0;j<(NSInteger)names.count;j++) {
            PressButton *card=[self button:names[j] kind:PressCard rect:NSMakeRect(380+j%3*127,158+j/3*86,120,79) action:@selector(choose:) on:page];
            card.tag=j;card.tint=tints[(j+(i>=2?i:0))%7];card.keyboard=i==2;
            CGFloat inset=card.keyboard?side/4:0,h=side-2*inset;
            card.printed=cut(items,CGRectMake(j*side,i*side+inset,side,h),NSMakeSize(side,h));
            card.original=cut(items,CGRectMake(j*side,(i+4)*side+inset,side,h),NSMakeSize(side,h));
            [card setAccessibilityRole:NSAccessibilityRadioButtonRole];
            card.arrow=^(NSInteger step){[weak moveFrom:j step:step];};[row addObject:card];
        }
        [_cards addObject:row];
    }
    if(items) CGImageRelease(items);
    rule(page,380,448,376,0xc1c0b7);
    _selectedTrick=2;_trickMenu=[NSMenu new];
    for(NSInteger i=0;i<(NSInteger)art.tricks.count;i++) {
        NSArray *trick=art.tricks[i];
        NSString *title=[NSString stringWithFormat:@"%@    %@ / %@ / %@",trick[2],art.chinese.rules[i][1],trick[0],trick[1]];
        NSMenuItem *item=[[NSMenuItem alloc] initWithTitle:title action:@selector(selectTrick:) keyEquivalent:@""];
        item.tag=i;item.target=self;[_trickMenu addItem:item];
    }
    _playButton=[self button:@"试演抱鱼" kind:PressPlay rect:NSMakeRect(380,471,103,33) action:@selector(play:) on:page];
    PressButton *choose=[self button:@"▾" kind:PressSwitch rect:NSMakeRect(482,471,30,33) action:@selector(showTricks:) on:page];
    [choose setAccessibilityLabel:@"选择试演动作"];
    _reset=[self button:@"恢复原装" kind:PressQuiet rect:NSMakeRect(519,471,91,33) action:@selector(reset:) on:page];
    PressButton *done=[self button:@"完成" kind:PressPrimary rect:NSMakeRect(678,470,78,35) action:@selector(done:) on:page];
    done.keyEquivalent=@"\r";done.keyEquivalentModifierMask=0;[self synchronize];return self;
}
- (void)refreshFrame {if(_panel.visible) [_preview setNeedsDisplay:YES];}
- (void)synchronize {
    NSDictionary *role=_studio.role;
    if(![_roleSnapshot isEqual:_studio.roles]) {
        _roleSnapshot=[_studio.roles copy];[_roles removeAllItems];
        for(NSDictionary *item in _roleSnapshot) {
            NSMenuItem *choice=[[NSMenuItem alloc] initWithTitle:[_studio menuNameForRole:item] action:NULL keyEquivalent:@""];
            choice.image=[_art portraitForRole:item size:18];[_roles.menu addItem:choice];
        }
    }
    [_builds selectItemAtIndex:[role[@"build"] integerValue]];
    [_roles selectItemAtIndex:_studio.selected];_edit.enabled=_studio.canEdit;_reset.enabled=_studio.clothes!=0 || _studio.hat!=0;
    _scope.stringValue=_group<2?@"穿搭随角色保存":@"所有角色共用这套桌搭";
    _print.chosen=_art.printLook;_classic.chosen=!_art.printLook;
    NSInteger shape=[role[@"shape"] integerValue],pattern=[role[@"pattern"] integerValue];
    NSInteger edition=pattern==0?shape:(pattern+1)%6;
    if(edition<(NSInteger)_covers.count) _preview.cover=_covers[edition];
    _name.stringValue=role[@"name"];
    __block NSUInteger chars=0;[_name.stringValue enumerateSubstringsInRange:NSMakeRange(0,_name.stringValue.length) options:NSStringEnumerationByComposedCharacterSequences usingBlock:^(NSString *s,NSRange r,NSRange enclosing,BOOL *stop){chars++;if(chars>4)*stop=YES;}];
    _name.font=displayFont(chars<=4?53:32);
    _detail.stringValue=[NSString stringWithFormat:@"%@  /  %@",_studio.catalog[@"shapes"][shape],_studio.catalog[@"builds"][[role[@"build"] integerValue]][@"name"]];
    NSDictionary *catalog=_studio.catalog;
    NSMutableArray *worn=[NSMutableArray array];
    if(_studio.clothes) [worn addObject:catalog[@"clothes"][_studio.clothes]];
    if(_studio.hat) [worn addObject:catalog[@"hats"][_studio.hat]];
    _outfitLine.stringValue=[@"穿搭   " stringByAppendingString:worn.count?[worn componentsJoinedByString:@"  ·  "]:@"原装毛色"];
    _deskLine.stringValue=[NSString stringWithFormat:@"桌面   %@  ·  %@",catalog[@"boards"][_studio.board],catalog[@"mice"][_studio.mouse]];
    int current[]={_studio.clothes,_studio.hat,_studio.board,_studio.mouse};
    for(NSInteger i=0;i<4;i++) {
        _tabs[i].chosen=i==_group;
        for(PressButton *card in _cards[i]) {
            NSImage *next=_art.printLook?card.printed:card.original;
            if(card.artwork!=next) {card.artwork=next;[card setNeedsDisplay:YES];}
            card.chosen=card.tag==current[i];card.hidden=i!=_group;
        }
    }
    [_preview setNeedsDisplay:YES];
}
- (void)moveFrom:(NSInteger)index step:(NSInteger)step {
    NSArray *options=_cards[_group];NSInteger count=options.count;
    NSInteger target=index+step;
    if(target<0 || target>=count || (labs(step)==1 && target/3!=index/3)) return;
    PressButton *b=options[target];[_panel makeFirstResponder:b];[b performClick:nil];
}
- (void)choose:(PressButton *)sender {
    int choice[]={_studio.clothes,_studio.hat,_studio.board,_studio.mouse};choice[_group]=(int)sender.tag;
    [_studio wear:choice[0] hat:choice[1] board:choice[2] mouse:choice[3]];self.appearanceChanged();
}
- (void)build:(NSPopUpButton *)sender {[_studio setBuild:sender.indexOfSelectedItem];self.appearanceChanged();}
- (void)group:(PressButton *)sender {_group=sender.tag;[self synchronize];}
- (void)edit:(PressButton *)sender {self.editRole(sender==_edit);}
- (void)role:(NSPopUpButton *)sender {self.selectRole(sender.indexOfSelectedItem);}
- (void)look:(PressButton *)sender {self.setLook(sender==_print);}
- (void)showTricks:(NSButton *)sender {
    for(NSMenuItem *item in _trickMenu.itemArray) item.state=item.tag==_selectedTrick?NSControlStateValueOn:NSControlStateValueOff;
    [_trickMenu popUpMenuPositioningItem:nil atLocation:NSMakePoint(0,NSHeight(sender.bounds)) inView:sender];
}
- (void)selectTrick:(NSMenuItem *)sender {
    _selectedTrick=sender.tag;_playButton.title=[@"试演" stringByAppendingString:_art.tricks[_selectedTrick][4]];[_playButton setAccessibilityLabel:_playButton.title];
    [self play:sender];
}
- (void)play:(id)sender {self.performTrick(_selectedTrick);}
- (void)reset:(id)sender {[_studio wear:0 hat:0 board:_studio.board mouse:_studio.mouse];self.appearanceChanged();}
- (void)done:(id)sender {[_panel close];}
@end
