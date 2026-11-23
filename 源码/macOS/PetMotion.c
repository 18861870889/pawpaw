#include "PetMotion.h"
#include <math.h>
#include <string.h>

double kc_smooth(double x) { x=fmax(0,fmin(1,x)); return x*x*(3-2*x); }
void kc_break(KCMotion *m) { memset(m->matched,0,sizeof(m->matched)); m->lastLetter=-1e9; }
void kc_clear_bubbles(KCMotion *m) { m->bubbleCount=0; m->wordBubble=-1; }
void kc_clear_input(KCMotion *m) {
    kc_break(m); memset(m->down,0,sizeof(m->down));
    m->mouseButtons=0; m->mouseSide=2; m->mouseReach=0; m->lookX=m->lookY=0;
    m->lastClick=m->lastMove=-1e9; kc_clear_bubbles(m);
    for(int i=0;i<2;i++) {m->paw[i].cap=-1; m->paw[i].start=m->paw[i].released=-1e9;}
}
void kc_reset(KCMotion *m) {
    kc_clear_input(m); m->trick=m->pending=-1; m->nextIdle=NAN; m->idleIndex=0;
}
static void advance(KCMotion *m,double now) {
    if(m->trick<0 || now<m->began+m->duration[m->trick]) return;
    m->began+=m->duration[m->trick]; m->trick=m->pending; m->pending=-1;
    if(m->trick>=0 && now>=m->began+m->duration[m->trick]) m->trick=-1;
}
void kc_init(KCMotion *m,const char *const aliases[KC_ALIASES],const double durations[KC_ACTIONS]) {
    memset(m,0,sizeof(*m)); m->labels=m->wordsEnabled=true;
    memcpy(m->duration,durations,sizeof(m->duration));
    for(int i=0;i<KC_ALIASES;i++) {
        const char *w=aliases[i]; size_t len=strlen(w);
        if (!len || len>31) continue;
        for(size_t j=0;j<len;j++) {
            int k=w[j]-'a'; if(k>=0 && k<26) m->positions[i][k]|=1u<<j;
        }
        m->lastBit[i]=1u<<(len-1);
    }
    kc_reset(m);
}
void kc_interrupt_idle(KCMotion *m,double now) {
    if(m->trick>=KC_WORDS) m->trick=-1;
    m->nextIdle=now+12;
}
void kc_idle(KCMotion *m,double now,double quiet,bool allowed,bool longhair) {
    advance(m,now);
    if(!allowed || m->privateInput) {kc_interrupt_idle(m,now);return;}
    if(isnan(m->nextIdle)) m->nextIdle=now+12;
    if(quiet<12) {
        if(m->trick>=KC_WORDS) m->trick=-1;
        m->nextIdle=fmax(m->nextIdle,(now-fmax(0,quiet))+12);
        return;
    }
    if(m->trick>=0 || now<m->nextIdle) return;
    int choice=(m->idleIndex+(longhair?1:0))%KC_IDLE_COUNT;
    m->trick=KC_WORDS+choice; m->began=now;
    m->nextIdle=now+m->duration[m->trick]+16+4*m->idleIndex;
    m->idleIndex=(m->idleIndex+1)%KC_IDLE_COUNT;
}
void kc_play(KCMotion *m,int trick,double now) {
    if(m->privateInput || trick<0 || trick>=KC_WORDS) return;
    kc_break(m); kc_interrupt_idle(m,now); advance(m,now);
    if(m->trick>=0) m->pending=trick;
    else {m->trick=trick; m->began=now;}
}
int kc_event_letter(int physicalLetter,uint16_t character,unsigned count) {
    if(count==1) {
        if(character>='a' && character<='z') return character-'a'+'A';
        if(character>='A' && character<='Z') return character;
        if(character>=32 && character<127) return 0;
    }
    // Input methods may leave a physical keystroke untranslated or return composed text.
    return physicalLetter;
}
static int observe(KCMotion *m,int letter,bool shortcut,bool shiftOnly,double t) {
    if(!m->wordsEnabled) return -1;
    if(shortcut) {kc_break(m); return -1;}
    if(shiftOnly) return -1;
    if(letter<'A' || letter>'Z') {kc_break(m); return -1;}
    if(t-m->lastLetter>2.5) kc_break(m);
    m->lastLetter=t;
    // Each set bit is a length of a matching prefix of a fixed trigger word.
    for(int i=0;i<KC_ALIASES;i++) {
        m->matched[i]=((m->matched[i]<<1)|1u)&m->positions[i][letter-'A'];
        if(m->lastBit[i] && (m->matched[i]&m->lastBit[i])) {kc_play(m,i/2,t);return i;}
    }
    return -1;
}
void kc_key(KCMotion *m,int hw,int cap,int hand,int letter,bool release,bool repeat,bool shortcut,bool shiftOnly,double t) {
    if(m->privateInput) return;
    kc_interrupt_idle(m,t);
    if(hw<0 || hw>=128) {if(!release) kc_break(m); return;}
    if(release) {
        m->down[hw]=false;
        for(int i=0;i<2;i++) if(m->paw[i].hardware==hw && isinf(m->paw[i].released)) m->paw[i].released=t;
        return;
    }
    if(m->down[hw] || repeat) return;
    m->down[hw]=true;
    int alias=observe(m,letter,shortcut,shiftOnly,t);
    if(alias>=0 && m->labels) {
        m->wordBubble=alias; m->wordBegan=t; m->bubbleCount=0;
    }
    if(cap<0) return;
    if(hw==49) hand=m->paw[0].start<m->paw[1].start?0:1;
    if(hand<0 || hand>1) return;
    KCPaw *paw=&m->paw[hand];
    paw->cap=cap; paw->hardware=hw; paw->start=t; paw->released=INFINITY;
    if(!m->labels || alias>=0) return;
    if(m->bubbleCount==4) {memmove(m->bubbles,m->bubbles+1,3*sizeof(KCBubble));m->bubbleCount=3;}
    m->bubbles[m->bubbleCount++]=(KCBubble){hw,cap,m->lane,t};
    m->lane=(m->lane+1)%3;
}
void kc_mouse(KCMotion *m,int button,bool pressed,double t) {
    if(m->privateInput || button<0 || button>7) return;
    kc_interrupt_idle(m,t);
    if(pressed) {
        kc_break(m); m->mouseButtons|=1<<button; m->lastClick=m->lastMove=t;
        m->mouseSide=button==0?0:(button==1?1:2);
    } else m->mouseButtons&=~(1<<button);
}
void kc_step(KCMotion *m,double t,double dt,double dx,double dy,bool follow) {
    double blend=1-exp(-15*fmax(0,fmin(.2,dt)));
    if(follow) {
        m->lookX+=(dx/(fabs(dx)+118)-m->lookX)*blend;
        m->lookY+=(dy/(fabs(dy)+100)-m->lookY)*blend;
        double target;
        if(m->gameActive && (m->paw[0].cap>=0 || m->paw[1].cap>=0)) target=0;
        else if(m->mouseButtons) target=1;
        else if(m->gameActive) target=1-kc_smooth((t-m->lastClick-.10)/.18);
        else target=1-kc_smooth((t-m->lastMove-.65)/.55);
        m->mouseReach+=(target-m->mouseReach)*blend;
    }
    for(int i=0;i<2;i++) if(t>=fmax(m->paw[i].start+.102,m->paw[i].released)+.30) m->paw[i].cap=-1;
    int keep=0;
    for(int i=0;i<m->bubbleCount;i++) if(t-m->bubbles[i].start<.72) m->bubbles[keep++]=m->bubbles[i];
    m->bubbleCount=keep;
    if(m->wordBubble>=0 && t-m->wordBegan>=KC_WORD_BUBBLE_SECONDS) m->wordBubble=-1;
    advance(m,t);
}
double kc_cover(const KCMotion *m,double t) {
    return m->privacyStart+((m->privateInput?1.:0.)-m->privacyStart)*kc_smooth((t-m->privacyChanged)/.18);
}
void kc_privacy(KCMotion *m,bool secure,double t) {
    if(m->privateInput==secure) return;
    m->privacyStart=kc_cover(m,t);m->privacyChanged=t;m->privateInput=secure;kc_reset(m);
}
double kc_reach(const KCPaw *paw,double t) {
    if(paw->cap<0 || t<paw->start) return 0;
    double age=fmin(t,paw->released)-paw->start,beat=fmax(0,age-.42);
    double contact=kc_smooth((t-paw->start)/.055);
    contact*=1-.46*kc_smooth(beat/.22)*(.5-.5*cos(beat*37.69911184307752));
    return contact*(1-kc_smooth((t-fmax(paw->start+.102,paw->released))/.188));
}
double kc_temper(const KCPaw *paw,double t) {
    if(paw->cap<0) return 0;
    return kc_smooth((fmin(t,paw->released)-paw->start-.44)/.45)*(1-kc_smooth((t-paw->released)/.30));
}
double kc_blink(double t) {
    double phase=fmod(t,8.15),d=fmin(fabs(phase-2.83),fabs(phase-6.94));
    if(phase>7) d=fmin(d,fabs(phase-7.23));
    return kc_smooth(d/.105);
}
