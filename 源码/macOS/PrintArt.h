#ifndef KC_PRINT_ART_H
#define KC_PRINT_ART_H
#include <stdint.h>
void kc_print_rgba(uint8_t *rgba,int width,int height,int stride,int step,const uint8_t *inks);
#endif
