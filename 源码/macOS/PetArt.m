#import "PetArt.h"
#import "PrintArt.h"
#import <ImageIO/ImageIO.h>
#import <CoreText/CoreText.h>
#import <math.h>

static const int pixelWidth=640,pixelHeight=520;
static inline CGFloat rad(double angle) {return angle*M_PI/180.;}
static inline int clampi(int v,int lo,int hi) {return v<lo?lo:(v>hi?hi:v);}
static double earFlick(double time,double period,double offset) {
    double phase=fmod(time+offset,period);
    if(phase>=.62) return 0;
    double beat=sin(phase*M_PI/.62);
    return beat*beat*sin(phase*M_PI*3/.62);
}
static void figure(CGContextRef g,int cat) {
    CGContextTranslateCTM(g,153,199); CGContextScaleCTM(g,cat?1.02:.90,cat?1:.86); CGContextTranslateCTM(g,-153,-199);
}
static void coloredRect(CGContextRef g,CGRect rect,double radius,double r,double gr,double b,double a,bool stroke) {
    CGPathRef p=CGPathCreateWithRoundedRect(rect,radius,radius,NULL);
    CGContextAddPath(g,p);
    if(stroke) {CGContextSetRGBStrokeColor(g,r,gr,b,a);CGContextStrokePath(g);}
    else {CGContextSetRGBFillColor(g,r,gr,b,a);CGContextFillPath(g);}
    CGPathRelease(p);
}
static void star(CGContextRef g,double x,double y,double w,double h) {
    CGPoint p[]={{x,y-h},{x+w*.18,y-h*.18},{x+w,y},{x+w*.18,y+h*.18},{x,y+h},{x-w*.18,y+h*.18},{x-w,y},{x-w*.18,y-h*.18}};
    CGContextBeginPath(g);CGContextAddLines(g,p,8);CGContextClosePath(g);CGContextFillPath(g);
}
static void fishGlints(CGContextRef g,bool longhair,double seconds) {
    double t=(seconds-.18)/1.15;
    if(t<=0 || t>=1) return;
    double rise=1-(1-t)*(1-t),strength=fmin(1,fmin(t/.17,(1-t)/.34));
    double x=(longhair?239:229)+10*rise,y=(longhair?93:100)-12*rise;
    CGContextSetRGBFillColor(g,.455,.302,.404,.902*strength);star(g,x+1.7,y+1.4,4.2*strength,8.2*strength);
    CGContextSetRGBFillColor(g,1,.863,.514,strength);star(g,x,y,4.2*strength,8.2*strength);
    CGContextSetRGBFillColor(g,.455,.302,.404,.902*strength);star(g,x+13,y-9,2.4*strength,4.6*strength);
}
@interface KCTexture : NSObject { @public CGImageRef pixels; }
@end
@implementation KCTexture
- (void)dealloc {if(pixels) CGImageRelease(pixels);}
@end

@implementation PetArt {
    NSData *_atlas,*_printInks;
    int _clipFPS;
    NSDictionary *_sprites,*_clips;
    NSCache<NSNumber *,KCTexture *> *_textures;
    CGContextRef _ctx;
    CGImageRef _frame;
    CTFontRef _font;
    NSMutableDictionary<NSString *,id> *_keyLines;
}
- (instancetype)initWithBundle:(NSBundle *)bundle {
    self=[super init]; if(!self) return nil;
    NSURL *index=[bundle URLForResource:@"Artwork" withExtension:@"json"];
    NSURL *bin=[bundle URLForResource:@"Artwork" withExtension:@"bin"];
    if(!index || !bin) return nil;
    NSData *data=[NSData dataWithContentsOfURL:index];
    NSDictionary *m=data?[NSJSONSerialization JSONObjectWithData:data options:0 error:NULL]:nil;
    _atlas=[NSData dataWithContentsOfURL:bin options:NSDataReadingMappedIfSafe error:NULL];
    if(![m isKindOfClass:[NSDictionary class]] || !_atlas.length || [m[@"scale"] intValue]!=2 ||
        [m[@"tricks"] count]!=KC_WORDS || [m[@"keys"] count]!=63 || [m[@"version"] intValue]!=8 || [m[@"idles"] count]!=KC_IDLE_COUNT || ![m[@"catalog"][@"roles"] count] || !m[@"sprites"][@"board/0"]) return nil;
    _printInks=[NSData dataWithContentsOfURL:[bundle URLForResource:@"PrintInks" withExtension:@"rgb"]];
    if(_printInks.length!=32768*6) return nil;
    _clipFPS=[m[@"fps"] intValue];if(_clipFPS<1 || _clipFPS>60) return nil;
    _catalog=m[@"catalog"]; _sprites=m[@"sprites"]; _clips=m[@"clips"]; _tricks=m[@"tricks"]; _idles=m[@"idles"]; _keymap=m[@"keymap"]; _keys=m[@"keys"];
    _chinese=[[ChineseInput alloc] initWithBundle:bundle tricks:_tricks];
    if(!_chinese) return nil;
    _textures=[NSCache new]; _textures.totalCostLimit=48u*1024*1024;
    CGColorSpaceRef rgb=CGColorSpaceCreateWithName(kCGColorSpaceSRGB);
    _ctx=CGBitmapContextCreate(NULL,pixelWidth,pixelHeight,8,pixelWidth*4,rgb,kCGImageAlphaPremultipliedLast|kCGBitmapByteOrder32Big);
    CGColorSpaceRelease(rgb);
    if(!_ctx) return nil;
    _keyLines=[NSMutableDictionary dictionary];
    _font=CTFontCreateWithName(CFSTR("Helvetica-Bold"),12,NULL);
    return self;
}
- (void)dealloc {
    if(_frame) CGImageRelease(_frame);
    if(_ctx) CGContextRelease(_ctx);
    if(_font) CFRelease(_font);
}
- (CGImageRef)frame {return _frame;}
- (BOOL)opaqueAtX:(double)x topY:(double)y {
    int px=(int)floor(x*2),py=(int)floor(y*2);
    if(px<0 || py<0 || px>=pixelWidth || py>=pixelHeight) return NO;
    const uint8_t *bytes=CGBitmapContextGetData(_ctx);
    return bytes[((size_t)py*pixelWidth+px)*4+3]>18;
}
- (KCTexture *)textureForEntry:(NSArray *)a {
    if(!a) return nil;
    NSNumber *offset=a[0];
    KCTexture *texture=[_textures objectForKey:offset];
    if(!texture) {
        NSUInteger begin=offset.unsignedIntegerValue,len=[a[1] unsignedIntegerValue];
        if(begin>_atlas.length || len>_atlas.length-begin) return nil;
        NSData *bytes=[NSData dataWithBytesNoCopy:(void *)((const uint8_t *)_atlas.bytes+begin) length:len freeWhenDone:NO];
        CGImageSourceRef source=CGImageSourceCreateWithData((__bridge CFDataRef)bytes,NULL);
        if(!source) return nil;
        texture=[KCTexture new];
        NSDictionary *opts=@{(__bridge NSString *)kCGImageSourceShouldCacheImmediately:@YES};
        texture->pixels=CGImageSourceCreateImageAtIndex(source,0,(__bridge CFDictionaryRef)opts);
        CFRelease(source);
        if(!texture->pixels) return nil;
        [_textures setObject:texture forKey:offset cost:CGImageGetBytesPerRow(texture->pixels)*CGImageGetHeight(texture->pixels)];
    }
    return texture;
}
- (NSImage *)portraitForRole:(NSDictionary *)role size:(CGFloat)size {
    NSString *key=[NSString stringWithFormat:@"%@/portrait/%@/%@",[role[@"shape"] intValue]?@"long":@"short",role[@"pattern"],role[@"eyes"]];
    KCTexture *texture=[self textureForEntry:_sprites[key]];
    if(!texture) return nil;
    return [[NSImage alloc] initWithCGImage:texture->pixels size:NSMakeSize(size,size)];
}
- (void)sprite:(NSString *)name {
    NSArray *a=_sprites[name];
    KCTexture *texture=[self textureForEntry:a];
    if(!texture) return;
    CGFloat x=[a[2] doubleValue]*.5,y=[a[3] doubleValue]*.5,w=[a[4] doubleValue]*.5,h=[a[5] doubleValue]*.5;
    CGContextSaveGState(_ctx); CGContextTranslateCTM(_ctx,x,y+h); CGContextScaleCTM(_ctx,1,-1);
    CGContextDrawImage(_ctx,CGRectMake(0,0,w,h),texture->pixels); CGContextRestoreGState(_ctx);
}
- (void)face:(NSString *)cat pattern:(int)pattern eyes:(int)eyes hat:(int)hat x:(double)x y:(double)y open:(double)open smile:(double)smile leftEar:(int)left rightEar:(int)right temper:(double)temper {
    NSString *part;
    if(open<.095) part=smile>.5?@"smile":@"blink";
    else if(open<.865) part=open<.375?@"lid/24":(open<.63?@"lid/50":@"lid/76");
    else part=[NSString stringWithFormat:@"head/%d_%d",clampi((int)lround(x*2)+2,0,4),clampi((int)lround(y*2)+2,0,4)];
    [self sprite:[NSString stringWithFormat:@"%@pattern/%d/%d_%d",cat,pattern,left,right]];
    [self sprite:[NSString stringWithFormat:@"%@eyes/%d/%@",cat,eyes,part]];
    [self sprite:[cat stringByAppendingString:@"muzzle"]];
    int brow=(int)lround(temper*8);
    if(brow) [self sprite:[NSString stringWithFormat:@"%@temper/%d",cat,brow]];
    if(hat) [self sprite:[NSString stringWithFormat:@"%@hat/%d",cat,hat]];
}
- (void)drawBubble:(NSString *)label cap:(NSArray *)cap lane:(int)lane age:(double)age {
    if(!label || age<0 || age>=1) return;
    CTLineRef line=(__bridge CTLineRef)_keyLines[label];
    if(!line) {
        NSAttributedString *str=[[NSAttributedString alloc] initWithString:label attributes:@{
            (__bridge NSString *)kCTFontAttributeName:(__bridge id)_font,
            (__bridge NSString *)kCTForegroundColorFromContextAttributeName:@YES}];
        line=CTLineCreateWithAttributedString((__bridge CFAttributedStringRef)str);
        _keyLines[label]=CFBridgingRelease(line);
    }
    CGFloat ascent=0,descent=0;
    double tw=CTLineGetTypographicBounds(line,&ascent,&descent,NULL);
    BOOL word=cap==nil;
    double opacity=1-kc_smooth((age-.55)/.45),rise=1-(1-age)*(1-age);
    double width=word?fmax(40,tw+16):fmax(20,10+label.length*7.4),height=word?21:17.5;
    double x=word?fmin(254,310-width/2):[cap[1] doubleValue]+(lane-1)*(5+5*rise);
    double y=word?47-5*rise:[cap[2] doubleValue]-19-34*rise-lane*4;
    double radius=word?height/2:4;
    CGContextSaveGState(_ctx); CGContextTranslateCTM(_ctx,x,y);
    if(!word) CGContextRotateCTM(_ctx,rad((lane-1)*4));
    CGContextSetAlpha(_ctx,opacity);
    CGRect r=CGRectMake(-width/2,-height/2,width,height);
    coloredRect(_ctx,CGRectOffset(r,0,1.5),radius,.38,.34,.35,1,false);
    coloredRect(_ctx,r,radius,1,word?.88:.975,word?.61:.895,1,false);
    CGContextSetLineWidth(_ctx,1.15); coloredRect(_ctx,r,radius,.31,.28,.31,1,true);
    CGContextScaleCTM(_ctx,1,-1); CGContextSetRGBFillColor(_ctx,.22,.20,.24,1);
    CGContextSetTextMatrix(_ctx,CGAffineTransformIdentity); CGContextSetTextPosition(_ctx,-tw/2,-(ascent-descent)*.50);
    CTLineDraw(line,_ctx); CGContextRestoreGState(_ctx);
}
- (void)renderRole:(NSDictionary *)role clothes:(int)clothes hat:(int)hat board:(int)board mouse:(int)mouse motion:(const KCMotion *)m time:(double)t {
    int cat=[role[@"shape"] intValue],pattern=[role[@"pattern"] intValue],eyes=[role[@"eyes"] intValue];
    if(_frame) {CGImageRelease(_frame);_frame=NULL;}
    NSString *who=cat?@"long/":@"short/";
    NSString *outfit=[NSString stringWithFormat:@"%@%d/",who,clothes];
    CGContextClearRect(_ctx,CGRectMake(0,0,pixelWidth,pixelHeight));
    CGContextSaveGState(_ctx); CGContextTranslateCTM(_ctx,0,pixelHeight); CGContextScaleCTM(_ctx,2,-2);
    CGContextSetInterpolationQuality(_ctx,kCGInterpolationHigh);
    double reach[2]={kc_reach(&m->paw[0],t),kc_reach(&m->paw[1],t)};
    double typing=fmax(reach[0],reach[1]),cover=kc_cover(m,t),temper=fmax(kc_temper(&m->paw[0],t),kc_temper(&m->paw[1],t));
    double lx=m->lookX*(1-typing*.5),ly=m->lookY+(.83-m->lookY)*typing*.68;
    lx*=1-cover;ly*=1-cover;
    double breath=sin(t*2.05)*(cat?1:.62);
    NSString *frameName=nil,*foreground=nil; NSArray *headMatrix=nil;
    if(m->trick>=0 && m->trick<KC_ACTIONS) {
        NSArray *rule=m->trick<KC_WORDS?_tricks[m->trick]:_idles[m->trick-KC_WORDS];
        NSString *clip=[NSString stringWithFormat:@"%@clip/%@",outfit,rule[0]];
        NSArray *frames=_clips[clip];
        if(frames.count) {
            int i=clampi((int)floor((t-m->began)*_clipFPS),0,(int)frames.count-1);
            headMatrix=frames[i]; frameName=[NSString stringWithFormat:@"%@/%d/",clip,i];
            foreground=[frameName stringByAppendingString:@"front"];
        }
    }
    double phase=t+(cat?2.3:0),weight=[headMatrix[16] doubleValue],still=[headMatrix[19] doubleValue];
    double left=.77*earFlick(phase,9.2,2.1)-.45*reach[0]+.72*temper+.55*cover;
    double right=.77*earFlick(phase,11.4,6.3)-.45*reach[1]+.72*temper+.55*cover;
    int earL=clampi((int)lround(3*(left*(1-weight)+[headMatrix[17] doubleValue]*weight)),-3,3);
    int earR=clampi((int)lround(3*(right*(1-weight)+[headMatrix[18] doubleValue]*weight)),-3,3);
    double bend=3.3*sin(phase*1.8)+1.3*sin(phase*3.1)+temper*5*sin(t*12);
    int swish=clampi((int)lround(bend*(1-still)*(1-.90*cover)+[headMatrix[14] doubleValue])+12,0,24);
    double turn=3.2*sin(phase*1.35)-3.5*typing+temper*5*sin(t*9);
    turn=(turn*(1-cover)+8*cover)*(1-weight)+[headMatrix[15] doubleValue];
    double air=[headMatrix[23] doubleValue];
    if(air>.005) {
        double radius=36-10*air;
        CGContextSetRGBFillColor(_ctx,103./255,89./255,106./255,.19*air);
        CGContextFillEllipseInRect(_ctx,CGRectMake(153-radius,192,2*radius,5.2));
    }
    double focus=[headMatrix[20] doubleValue];
    lx=lx*(1-focus)+[headMatrix[21] doubleValue]*focus;ly=ly*(1-focus)+[headMatrix[22] doubleValue]*focus;
    CGContextSaveGState(_ctx);
    if(headMatrix) {
        NSArray *a=headMatrix;
        CGContextConcatCTM(_ctx,CGAffineTransformMake([a[8] doubleValue],[a[9] doubleValue],[a[10] doubleValue],
            [a[11] doubleValue],[a[12] doubleValue],[a[13] doubleValue]));
    } else figure(_ctx,cat);
    CGContextSaveGState(_ctx);
    CGContextTranslateCTM(_ctx,190,180);CGContextRotateCTM(_ctx,rad(turn));CGContextTranslateCTM(_ctx,-190,-180);
    [self sprite:[NSString stringWithFormat:@"%@tail/%d/%d",who,pattern,swish]];
    CGContextRestoreGState(_ctx);
    if(clothes==0) [self sprite:[NSString stringWithFormat:@"%@body/%d",who,pattern]];
    else if(frameName) [self sprite:[NSString stringWithFormat:@"%@posture/%d",outfit,[headMatrix[24] intValue]]];
    else [self sprite:[outfit stringByAppendingString:@"body"]];
    if(frameName) [self sprite:[frameName stringByAppendingString:@"back"]];
    CGContextRestoreGState(_ctx);
    [self sprite:[NSString stringWithFormat:@"board/%d",board]];
    [self sprite:[NSString stringWithFormat:@"mouse/%d",mouse]];
    for(int hand=0;hand<2;hand++) if(reach[hand]>.58) {
        CGContextSaveGState(_ctx); CGContextSetAlpha(_ctx,kc_smooth((reach[hand]-.58)/.42));
        [self sprite:[NSString stringWithFormat:@"press/%d/%d",board,m->paw[hand].cap]]; CGContextRestoreGState(_ctx);
    }
    double mouseAge=t-m->lastClick;
    if(mouseAge>=0 && mouseAge<.38) {
        int frame=clampi((int)floor(mouseAge/.38*8),0,7);
        [self sprite:[NSString stringWithFormat:@"mouse/flash/%d/%d",m->mouseSide,frame]];
    }
    CGContextSaveGState(_ctx);
    if(headMatrix) {
        NSArray *a=headMatrix;
        CGContextConcatCTM(_ctx,CGAffineTransformMake([a[0] doubleValue],[a[1] doubleValue],[a[2] doubleValue],
            [a[3] doubleValue],[a[4] doubleValue],[a[5] doubleValue]));
        [self face:who pattern:pattern eyes:eyes hat:hat x:lx y:ly open:(kc_blink(t)*(1-still)+still)*[a[6] doubleValue] smile:[a[7] doubleValue] leftEar:earL rightEar:earR temper:temper];
    } else {
        figure(_ctx,cat);
        if(cover<.01) for(int hand=0;hand<2;hand++) {
            int step=clampi((int)lround(reach[hand]*6),0,6);
            NSString *arm=@"rest";
            if(step) {
                arm=[NSString stringWithFormat:@"%d/%d",m->paw[hand].cap,step];
            } else if(hand==0) {
                int mouseStep=clampi((int)lround(m->mouseReach*6),0,6);
                if(mouseStep>0) {
                    double pressure=fmax(m->mouseButtons ? .8 : 0,1-mouseAge/.2);
                    int depth=clampi((int)lround(pressure*2),0,2);
                    arm=[NSString stringWithFormat:@"mouse/%d/%d/%d",m->mouseSide,depth,mouseStep];
                }
            }
            [self sprite:[NSString stringWithFormat:@"%@arm/%d/%@",outfit,hand,arm]];
        }
        double lean=cat?3.3:1.75;
        CGContextTranslateCTM(_ctx,151+lx*lean,102+breath+ly*1.7+typing);
        CGContextRotateCTM(_ctx,rad(lx*lean)); CGContextScaleCTM(_ctx,1.24,1.12); CGContextTranslateCTM(_ctx,-151,-90);
        [self face:who pattern:pattern eyes:eyes hat:hat x:lx y:ly open:kc_blink(t)*(1-cover)*(1-.27*temper) smile:0 leftEar:earL rightEar:earR temper:temper];
    }
    CGContextRestoreGState(_ctx);
    if(cover>=.01) {
        CGContextSaveGState(_ctx);figure(_ctx,cat);
        [self sprite:[NSString stringWithFormat:@"%@privacy/%d",outfit,clampi((int)lround(cover*10),1,10)]];
        CGContextRestoreGState(_ctx);
    }
    if(foreground) [self sprite:foreground];
    if(self.printLook && m->trick==2) fishGlints(_ctx,cat==1,t-m->began);
    for(int i=0;i<m->bubbleCount;i++) {
        KCBubble b=m->bubbles[i];
        if(b.cap<0 || (NSUInteger)b.cap>=_keys.count) continue;
        NSArray *entry=_keymap[[NSString stringWithFormat:@"%d",b.hardware]];
        [self drawBubble:entry[1] cap:_keys[b.cap] lane:b.lane age:(t-b.start)/.72];
    }
    if(m->wordBubble>=0) {
        int alias=m->wordBubble;
        NSString *word=alias>=KC_ALIASES?_chinese.rules[(alias-KC_ALIASES)/2][1+(alias-KC_ALIASES)%2]:
            (m->pinyinAliases&(1u<<alias))?_chinese.rules[alias/2][1]:_tricks[alias/2][alias%2];
        [self drawBubble:word cap:nil lane:1 age:(t-m->wordBegan)/KC_WORD_BUBBLE_SECONDS];
    }
    CGContextRestoreGState(_ctx);
    if(self.printLook) {
        CGContextFlush(_ctx);
        kc_print_rgba(CGBitmapContextGetData(_ctx),pixelWidth,pixelHeight,pixelWidth*4,128,_printInks.bytes);
    }
    _frame=CGBitmapContextCreateImage(_ctx);
}
@end
