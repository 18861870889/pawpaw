#import "PetArt.h"
#import <CoreGraphics/CoreGraphics.h>
#include <assert.h>
#include <stdio.h>
#include <math.h>

static NSData *pixels(PetArt *art,NSDictionary *role,KCMotion *motion,double time) {
    [art renderRole:role clothes:0 hat:0 board:0 mouse:0 motion:motion time:time];
    CGImageRef image=art.frame;
    assert(image && CGImageGetWidth(image)==640 && CGImageGetHeight(image)==520);
    return CFBridgingRelease(CGDataProviderCopyData(CGImageGetDataProvider(image)));
}

int main(int argc,const char **argv) {
    @autoreleasepool {
        assert(argc==2);
        NSBundle *bundle=[NSBundle bundleWithPath:[NSString stringWithUTF8String:argv[1]]];
        PetArt *art=[[PetArt alloc] initWithBundle:bundle];
        assert(art && art.tricks.count==KC_WORDS && art.chinese.rules.count==KC_WORDS);
        NSInteger start=9;
        assert([art.chinese matchSuffix:@"蜜雪冰城甜蜜蜜！" endingAt:&start]==18 && start==1);
        start=4;assert([art.chinese matchSuffix:@"乾飯時間" endingAt:&start]==21 && start==0);
        start=2;assert([art.chinese matchSuffix:@"小雨" endingAt:&start]<0);
        const char *aliases[KC_ALIASES]; double durations[KC_ACTIONS];
        for(int i=0;i<KC_WORDS;i++){
            aliases[i*2]=[art.tricks[i][0] UTF8String];aliases[i*2+1]=[art.tricks[i][1] UTF8String];
            durations[i]=[art.tricks[i][3] doubleValue];
            NSArray *rule=art.chinese.rules[i];
            for(int alias=0;alias<2;alias++) {
                BOOL isPinyin=[rule indexOfObject:art.tricks[i][alias] inRange:NSMakeRange(3,rule.count-3)]!=NSNotFound;
                assert(isPinyin==((art.chinese.pinyinAliases&(1u<<(2*i+alias)))!=0));
            }
        }
        for(int i=0;i<KC_IDLE_COUNT;i++) durations[KC_WORDS+i]=[art.idles[i][1] doubleValue];
        NSURL *index=[bundle URLForResource:@"Artwork" withExtension:@"json"];
        NSDictionary *atlas=[NSJSONSerialization JSONObjectWithData:[NSData dataWithContentsOfURL:index] options:0 error:NULL];
        int fps=[atlas[@"fps"] intValue];
        assert([atlas[@"version"] intValue]==8 && fps==24);
        for(NSString *shape in @[@"short",@"long"]) for(int garment=0;garment<7;garment++) {
            NSString *outfit=[NSString stringWithFormat:@"%@/%d/",shape,garment];
            for(int egg=0;egg<KC_WORDS;egg++) {
                NSArray *rule=art.tricks[egg];
                NSArray *clip=atlas[@"clips"][[outfit stringByAppendingFormat:@"clip/%@",rule[0]]];
                assert(clip.count==(NSUInteger)ceil([rule[3] doubleValue]*fps));
                for(NSArray *frame in clip) {
                    assert(frame.count==25);
                    if(garment) {
                        NSString *posture=[outfit stringByAppendingFormat:@"posture/%d",[frame[24] intValue]];
                        assert(atlas[@"sprites"][posture]);
                    }
                }
                if(egg==11) assert([clip[(int)lround(1.08*fps)][23] doubleValue]>.94 && [clip[(int)lround(1.85*fps)][23] doubleValue]==0);
            }
        }
        int keys[]={3,34,1,4}; // F I S H on the Mac virtual keyboard
        assert([art.catalog[@"roles"] count]==7);
        for(int cat=0;cat<(int)[art.catalog[@"roles"] count];cat++) for(int print=0;print<2;print++){
            KCMotion m;kc_init(&m,aliases,durations,art.chinese.pinyinAliases);art.printLook=print;
            NSDictionary *role=art.catalog[@"roles"][cat];
            for(int i=0;i<4;i++){
                NSArray *key=art.keymap[[NSString stringWithFormat:@"%d",keys[i]]];
                assert(key && [key[2] intValue]=="FISH"[i]);
                int cap=[key[0] intValue], hand=[art.keys[cap][3] intValue];
                int letter=kc_event_letter([key[2] intValue],0,0);
                kc_key(&m,keys[i],cap,hand,letter,false,false,false,false,1+i*.1);
                kc_key(&m,keys[i],cap,hand,letter,true,false,false,false,1.03+i*.1);
            }
            assert(m.trick==2 && m.wordBubble==4);
            NSData *withWord=pixels(art,role,&m,1.65);
            kc_clear_bubbles(&m);
            NSData *withoutWord=pixels(art,role,&m,1.65);
            assert(withWord.length && ![withWord isEqual:withoutWord]);
            double typedAt=1.66;
            for(const char *p="XIAOYU";*p;p++,typedAt+=.04) {
                int hw=*p-'A';
                kc_key(&m,hw,-1,0,*p,false,false,false,KCBreakWord,typedAt);
                kc_key(&m,hw,-1,0,*p,true,false,false,KCBreakWord,typedAt+.01);
            }
            assert(m.wordBubble==5 && (m.pinyinAliases&(1u<<5)) && [art.chinese.rules[2][1] isEqual:@"小鱼"]);
            NSData *withChinese=pixels(art,role,&m,typedAt+.1);
            m.pinyinAliases=0;
            assert(![withChinese isEqual:pixels(art,role,&m,typedAt+.1)]);
            m.pinyinAliases=art.chinese.pinyinAliases;
            KCMotion dance;kc_init(&dance,aliases,durations,art.chinese.pinyinAliases);
            kc_play(&dance,8,1.0);
            NSData *dribble=pixels(art,role,&dance,1.35);
            assert(![dribble isEqual:pixels(art,role,&dance,2.62)]);
            for(int egg=9;egg<KC_WORDS;egg++) {
                KCWordGate turn={0};kc_reset(&dance);dance.labels=false;
                assert(kc_exact_word(&dance,&turn,egg*2,1));
                NSData *frame=pixels(art,role,&dance,1.9);
                kc_reset(&dance);int base[]={4,2,0};kc_play(&dance,base[egg-9],1);
                assert(![frame isEqual:pixels(art,role,&dance,1.9)]);
            }
            kc_privacy(&m,true,1.66);
            assert(m.wordBubble<0 && m.trick<0 && kc_cover(&m,2)>.99);
            assert(![withoutWord isEqual:pixels(art,role,&m,2)]);
        }
        for(int garment=0;garment<7;garment++) {
            KCMotion leap;kc_init(&leap,aliases,durations,art.chinese.pinyinAliases);
            kc_play(&leap,11,1);
            [art renderRole:art.catalog[@"roles"][garment] clothes:garment hat:garment%6 board:0 mouse:0 motion:&leap time:1.86];
            assert(art.frame && CGImageGetWidth(art.frame)==640);
        }
        puts("All seven breeds, garments, independent choreography, both art styles and secure input passed.");
    }
    return 0;
}
