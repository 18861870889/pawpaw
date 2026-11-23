#include "PrintArt.h"
#include <stddef.h>

static unsigned straight(unsigned v,unsigned a) {
    unsigned n=(v*255+a/2)/a;
    return n>255?255:n;
}
// The input is premultiplied RGBA. Ink samples come from InkLook.cs and never change coverage.
void kc_print_rgba(uint8_t *rgba,int width,int height,int stride,int step,const uint8_t *inks) {
    for(int y=0,py=0;y<height;y++,py+=step) {
        uint8_t *p=rgba+(size_t)y*stride;
        int row=(py>>8)&3;
        for(int x=0,px=0;x<width;x++,px+=step,p+=4) {
            unsigned a=p[3];
            if(!a) continue;
            unsigned r=p[0],g=p[1],b=p[2];
            if(a!=255) {r=straight(r,a);g=straight(g,a);b=straight(b,a);}
            unsigned i=(((r>>3)<<10)|((g>>3)<<5)|(b>>3))*6;
            int column=(px>>8)&3;
            if((row==0 && column==0)||(row==2 && column==2)) i+=3;
            for(int k=0;k<3;k++) p[k]=a==255?inks[i+k]:(uint8_t)((inks[i+k]*a+127)/255);
        }
    }
}
