#include "PetMotion.h"
#include <assert.h>
#include <math.h>
#include <stdio.h>
static const char *const words[]={"hello","nihao","love","aixin","fish","xiaoyu","sleep","wanan","dance","tiaowu","laicai","fortune","jingzijiyibei","cheers","yingkong","freeze","jinitaimei","jntm","mixuebingchengtianmimi","mxbctmm","dinnertime","ganfanshijian","triplelike","yijiansanlian"};
static const double durations[]={2.2,2.4,2.55,3.25,3.55,2.75,3.1,2.7,3.8,3.55,3.12,2.72,3.,2.65,3.05};
static const uint32_t pinyinAliases=(1u<<1)|(1u<<3)|(1u<<5)|(1u<<7)|(1u<<9)|(1u<<10)|(1u<<12)|(1u<<14)|(1u<<16)|(1u<<17)|(1u<<18)|(1u<<19)|(1u<<21)|(1u<<23);
static void type(KCMotion *m,const char *word,double t) {
    for(;*word;word++,t+=.09) {
        int h=*word-'a',k=h+'A';
        kc_key(m,h,1,0,k,false,false,false,false,t);
        kc_key(m,h,1,0,k,true,false,false,false,t+.02);
    }
}
static void chat_and_game(void) {
    KCMotion m; kc_init(&m,words,durations,pinyinAliases);
    assert(kc_event_letter('Q','a',1)=='A');
    assert(kc_event_letter('F',0,0)=='F' && kc_event_letter('I',0x6765,1)=='I');
    assert(kc_event_letter('S',0x6765,2)=='S' && kc_event_letter('H',0,1)=='H');
    assert(kc_event_letter('Q','!',1)==0 && kc_event_letter(0,0x6765,1)==0);
    for(int i=0;i<KC_ALIASES;i++) {
        kc_reset(&m);type(&m,words[i],1);
        assert(m.wordBubble==i && m.trick==i/2 && m.bubbleCount==0);
        double hit=m.wordBegan;
        type(&m,"z",hit+.15);kc_step(&m,hit+1,.02,0,0,true);
        assert(m.wordBubble==i && m.bubbleCount==0);
        kc_step(&m,hit+1.56,.02,0,0,true);assert(m.wordBubble==-1);
    }
    kc_reset(&m);type(&m,"fi",1);kc_clear_input(&m);type(&m,"sh",1.4);
    assert(m.wordBubble<0 && m.trick<0);
    kc_reset(&m);m.labels=false;type(&m,"fish",1);assert(m.trick==2 && m.wordBubble<0 && !m.bubbleCount);
    kc_reset(&m);m.labels=true;m.wordsEnabled=false;type(&m,"fish",1);
    assert(m.trick<0 && m.wordBubble<0 && m.bubbleCount==4);
    kc_reset(&m);m.wordsEnabled=true;type(&m,"laicai",1);kc_privacy(&m,true,2);
    type(&m,"fish",2.1);assert(m.wordBubble<0 && !m.bubbleCount && m.trick<0 && kc_cover(&m,2.4)>.99);
    kc_privacy(&m,false,3);m.gameActive=true;
    for(int i=1;i<=40;i++){double t=3+i*.025;m.lastMove=t;kc_step(&m,t,.025,130,-20,true);}
    assert(m.lookX>.4 && m.mouseReach<.01);
    kc_mouse(&m,1,true,4.01);kc_step(&m,4.20,.19,0,0,true);assert(m.mouseReach>.9);
    kc_key(&m,12,2,0,'Q',false,false,false,false,4.21);
    kc_step(&m,4.43,.20,0,0,true);assert(m.paw[0].cap==2 && m.mouseReach<.1);
    kc_key(&m,12,2,0,'Q',true,false,false,false,4.5);
    kc_step(&m,4.85,.20,0,0,true);kc_step(&m,5.1,.2,0,0,true);assert(m.mouseReach>.9);
    kc_mouse(&m,1,false,5.11);kc_step(&m,6,.20,0,0,true);assert(m.mouseReach<.1);
    m.gameActive=false;m.lastMove=6.1;kc_step(&m,6.11,.20,0,0,true);assert(m.mouseReach>.9);
}
int main(void) {
    chat_and_game();
    KCMotion m;kc_init(&m,words,durations,pinyinAliases);
    assert(m.mouseSide==2);
    for(int button=0;button<3;button++) {
        kc_mouse(&m,button,true,.2);assert(m.mouseSide==button && m.mouseButtons);
        kc_mouse(&m,button,false,.25);assert(!m.mouseButtons);
    }
    kc_clear_input(&m);assert(m.mouseSide==2 && m.mouseReach==0);
    for(int n=0;n<KC_ALIASES;n++) {kc_reset(&m);type(&m,words[n],1);assert(m.trick==n/2);}
    kc_reset(&m);type(&m,"fi",0);kc_break(&m);type(&m,"sh",.2);assert(m.trick<0);
    kc_reset(&m);type(&m,"fi",0);type(&m,"sh",3);assert(m.trick<0);
    kc_reset(&m);type(&m,"fis",0);kc_key(&m,4,1,0,'H',false,false,true,false,.4);assert(m.trick<0);
    kc_reset(&m);kc_key(&m,3,1,0,'F',false,false,false,false,0);
    for(int n=1;n<=6;n++) kc_key(&m,3,1,0,'F',false,true,false,false,n*.18);
    assert(m.bubbleCount==1 && kc_temper(&m.paw[0],1.5)>.99 && kc_reach(&m.paw[0],.92)>.98 && kc_reach(&m.paw[0],1.0033)<.60);
    kc_key(&m,3,1,0,'F',true,false,false,false,1.5);assert(kc_reach(&m.paw[0],1.59)>.15 && kc_reach(&m.paw[0],1.59)<.8);
    kc_step(&m,1.81,.1,0,0,true);assert(m.paw[0].cap<0);
    kc_reset(&m);kc_key(&m,3,1,0,'F',false,false,false,false,0);kc_key(&m,3,1,0,'F',true,false,false,false,.01);assert(kc_reach(&m.paw[0],.078)>.99);
    kc_mouse(&m,2,true,1);assert(m.mouseButtons==4 && m.mouseSide==2);kc_mouse(&m,2,false,1.1);assert(m.mouseButtons==0);
    kc_play(&m,2,2);kc_play(&m,3,2.25);kc_play(&m,4,2.5);kc_clear_input(&m);
    kc_step(&m,4.54,.1,0,0,false);assert(m.trick==2);kc_step(&m,4.56,.1,0,0,false);assert(m.trick==4 && fabs(m.began-4.55)<.001);
    kc_step(&m,7.26,.1,0,0,false);assert(m.trick<0);
    m.wordsEnabled=false;type(&m,"dance",10);assert(m.trick<0);
    m.wordsEnabled=true;kc_play(&m,2,14);kc_play(&m,4,14.2);kc_privacy(&m,true,14.25);
    type(&m,"dance",14.3);assert(kc_cover(&m,15)==1 && m.trick<0 && !m.bubbleCount && m.paw[0].cap<0);
    kc_privacy(&m,false,15.1);assert(kc_cover(&m,15.4)==0);type(&m,"love",15.5);assert(m.trick==1);
    kc_init(&m,words,durations,pinyinAliases);kc_idle(&m,0,0,true,false);kc_idle(&m,11.99,11.99,true,false);assert(m.trick<0);
    kc_idle(&m,12,12,true,false);assert(m.trick==KC_WORDS);
    type(&m,"laicai",12.2);assert(m.trick==5);kc_play(&m,6,13);assert(m.pending==6);
    kc_step(&m,15.6,.1,0,0,true);assert(m.trick==6);
    kc_idle(&m,31,20,true,false);assert(m.trick==KC_WORDS+1);
    kc_idle(&m,31.8,.6,true,false);assert(m.trick<0);
    kc_idle(&m,56,24,true,false);assert(m.trick==KC_WORDS+2);
    kc_privacy(&m,true,56.1);kc_idle(&m,77,21,true,false);assert(m.trick<0);
    kc_privacy(&m,false,78);kc_idle(&m,78,0,false,false);kc_idle(&m,91,13,false,false);assert(m.trick<0);
    kc_init(&m,words,durations,pinyinAliases);kc_idle(&m,0,0,true,true);kc_idle(&m,12,12,true,true);assert(m.trick==KC_WORDS+1);
    kc_mouse(&m,0,true,12.2);assert(m.trick<0);
    kc_reset(&m);type(&m,"xiaoy",1);
    assert(m.trick<0);
    kc_key(&m,32,1,0,'U',false,false,false,KCBreakWord,1.6);
    assert(m.trick==2 && m.wordBubble==5 && m.wordBegan==1.6);
    kc_key(&m,32,1,0,'U',false,true,false,KCBreakWord,1.61);
    kc_key(&m,32,1,0,'U',true,false,false,KCBreakWord,1.62);
    kc_key(&m,49,1,0,0,false,false,false,KCKeepPinyin,1.7);
    kc_key(&m,18,1,0,0,false,false,false,KCKeepPinyin,1.8);
    kc_mouse(&m,0,true,1.9);
    assert(m.wordBegan==1.6 && m.pending==-1);
    kc_reset(&m);type(&m,"jing",1);
    kc_key(&m,49,1,0,0,false,false,false,KCKeepPinyin,1.6);
    type(&m,"ziji",2);kc_key(&m,18,1,0,0,false,false,false,KCKeepPinyin,2.5);
    type(&m,"yibei",3);assert(m.trick==6 && m.wordBubble==12);
    kc_reset(&m);type(&m,"l",1);kc_key(&m,49,1,0,0,false,false,false,KCKeepPinyin,1.2);type(&m,"ove",1.4);
    assert(m.trick<0);
    kc_reset(&m);type(&m,"xiao",1);type(&m,"yu",10);assert(m.trick<0);
    kc_reset(&m);type(&m,"xiao",1);
    kc_key(&m,49,1,0,0,false,false,false,KCKeepPinyin,1.5);
    kc_key(&m,49,1,0,0,true,false,false,KCKeepPinyin,1.51);
    kc_key(&m,49,1,0,0,false,false,false,KCKeepPinyin,1.6);
    type(&m,"yu",2);assert(m.trick<0);
    kc_reset(&m);type(&m,"xiaoyu",1);type(&m,"xiaoyu",2);assert(m.trick==2 && m.pending==2);
    kc_reset(&m);kc_privacy(&m,true,1);type(&m,"xiaoyu",1.1);assert(m.trick<0 && m.wordBubble<0);
    kc_privacy(&m,false,2);m.wordsEnabled=false;type(&m,"jntm",2.1);assert(m.trick<0);
    m.wordsEnabled=true;m.labels=false;type(&m,"jntm",3);assert(m.trick==8 && m.wordBubble<0);
    KCWordGate turn={0},next={0};
    kc_reset(&m);m.wordsEnabled=m.labels=true;
    assert(kc_word_accept(&turn,9,true) && !kc_exact_word(&m,&turn,18,4));
    assert(kc_exact_word(&m,&next,18,4.01) && m.wordBubble==KC_ALIASES+18);
    assert(!kc_word_accept(&next,9,true) && m.pending<0);
    assert(kc_exact_word(&m,&next,18,4.02) && m.pending==9);
    m.gameActive=true;assert(!kc_exact_word(&m,&next,20,5));m.gameActive=false;
    m.wordsEnabled=false;assert(!kc_exact_word(&m,&next,20,5));m.wordsEnabled=true;
    kc_privacy(&m,true,5);assert(!kc_exact_word(&m,&next,18,5.1));
    puts("Mac: immediate pinyin, candidate and repeat deduplication, privacy, input timing and idle actions passed.");
    return 0;
}
