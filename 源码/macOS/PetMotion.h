#ifndef KEYBOARD_CAT_MOTION_H
#define KEYBOARD_CAT_MOTION_H
#include <stdbool.h>
#include <stdint.h>

enum { KC_WORDS=9, KC_ALIASES=KC_WORDS*2, KC_IDLE_COUNT=3, KC_ACTIONS=KC_WORDS+KC_IDLE_COUNT };
#define KC_WORD_BUBBLE_SECONDS 1.55

typedef enum { KCBreakWord, KCKeepWord, KCKeepPinyin } KCWordBoundary;

typedef struct { int cap,hardware; double start,released; } KCPaw;
typedef struct { int hardware,cap,lane; double start; } KCBubble;
typedef struct {
    uint32_t positions[KC_ALIASES][26],matched[KC_ALIASES],lastBit[KC_ALIASES];
    uint32_t pinyinAliases;
    double duration[KC_ACTIONS], lastLetter, began, lastMove, lastClick, nextIdle, wordBegan;
    double lookX,lookY,mouseReach,privacyStart,privacyChanged;
    int trick,pending,idleIndex,mouseButtons,mouseSide,lane,bubbleCount,wordBubble;
    bool down[128],wordsEnabled,labels,privateInput,gameActive,syllableStarted;
    KCPaw paw[2];
    KCBubble bubbles[4];
} KCMotion;

void kc_init(KCMotion *m,const char *const aliases[KC_ALIASES],const double durations[KC_ACTIONS],uint32_t pinyinAliases);
void kc_break(KCMotion *m);
void kc_interrupt_idle(KCMotion *m,double now);
void kc_idle(KCMotion *m,double now,double quiet,bool allowed,bool longhair);
void kc_clear_bubbles(KCMotion *m);
void kc_clear_input(KCMotion *m);
void kc_reset(KCMotion *m);
void kc_play(KCMotion *m,int trick,double now);
int kc_event_letter(int physicalLetter,uint16_t character,unsigned count);
void kc_key(KCMotion *m,int hardware,int cap,int hand,int letter,bool release,bool repeat,bool shortcut,KCWordBoundary boundary,double now);
void kc_mouse(KCMotion *m,int button,bool pressed,double now);
void kc_step(KCMotion *m,double t,double dt,double dx,double dy,bool follow);
double kc_cover(const KCMotion *m,double t);
void kc_privacy(KCMotion *m,bool secure,double t);
double kc_temper(const KCPaw *paw,double t);
double kc_reach(const KCPaw *paw,double now);
double kc_blink(double t);
double kc_smooth(double x);
#endif
