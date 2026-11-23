#import "PetArt.h"
#import <CoreGraphics/CoreGraphics.h>
#include <assert.h>
#include <stdio.h>

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
        assert(art && art.tricks.count==KC_WORDS);
        const char *aliases[KC_ALIASES]; double durations[KC_ACTIONS];
        for(int i=0;i<KC_WORDS;i++){
            aliases[i*2]=[art.tricks[i][0] UTF8String];aliases[i*2+1]=[art.tricks[i][1] UTF8String];
            durations[i]=[art.tricks[i][3] doubleValue];
        }
        for(int i=0;i<KC_IDLE_COUNT;i++) durations[KC_WORDS+i]=[art.idles[i][1] doubleValue];
        int keys[]={3,34,1,4}; // F I S H on the Mac virtual keyboard
        assert([art.catalog[@"roles"] count]==7);
        for(int cat=0;cat<(int)[art.catalog[@"roles"] count];cat++) for(int print=0;print<2;print++){
            KCMotion m;kc_init(&m,aliases,durations);art.printLook=print;
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
            KCMotion dance;kc_init(&dance,aliases,durations);
            kc_play(&dance,KC_WORDS-1,1.0);
            NSData *dribble=pixels(art,role,&dance,1.35);
            assert(![dribble isEqual:pixels(art,role,&dance,2.62)]);
            kc_privacy(&m,true,1.66);
            assert(m.wordBubble<0 && m.trick<0 && kc_cover(&m,2)>.99);
            assert(![withoutWord isEqual:pixels(art,role,&m,2)]);
        }
        puts("All seven breeds rendered word tricks, basketball and secure input in both art styles.");
    }
    return 0;
}
