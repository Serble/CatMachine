jmp main

; =================
; Data Section
; =================
#include levels.asm

title_screen:
    dfile title_img.data

platform:
    dfile platform.data

current_level:          ; store level being played
    d8 0

; =================
; Code section
; =================

main:
    ; show title screen
    mov r1, title_screen
    call draw_screen
    
    mov r1, 69
    int 0x90
    
    ; wait for input to continue
    call wait_for_input
    
    mov r1, 7
    int 0x90
    
    mov r1, 0x365235
    call fill_screen
    
    mov r1, 0
    call draw_level
    int 0x81
    


; draws a level to the screen (excluding player)
; level in r1 (index)
draw_level:
    ; prologue
    push r4                    ; current tile pointer
    push r5                    ; y iterator
    push r6                    ; x iterator
    push r7                    ;
    
    mov r4, r1
    umul r4, 256               ; r4 is now offset from levels
    add r4, levels             ; and now a pointer to the level data
    mov r5, 0                  ; current y
.yloop:
    mov r6, 0                  ; current x
.xloop:
    ; draw this tile (the type is at r4)
    mov r7, 0
    mov8 r7, @r4
    cmp r7, 0
    je .dontdraw               ; nothing there
    
    ; okay there's something there
    mov r1, r6                 ; current x position
    umul r1, 32                ; current x pixel
    
    mov r2, r5                 ; current y position
    umul r2, 32                ; current y pixel
    
    mov r3, 32                 ; width
    push 32                    ; height
    push platform              ; data
    call draw_rect
    add sp, 8                  ; remove args from stack
    
.dontdraw:
    add r4, 1                  ; go to next tile in level data
    
    add r6, 1
    cmp r6, 16
    jul .xloop
    
    ; done this row
    add r5, 1
    cmp r5, 16
    jul .yloop
    
    ; epilogue
    pop r7
    pop r6
    pop r5
    pop r4
    ret


; busy wait until input is available
wait_for_input:
.loop:
    in r1, 0
    cmp r1, -1
    je .loop
    ; got it, drain rest of input data
    in r1, 0
    in r1, 0
    ret


; img pointer in r1
draw_screen:
    int 0x84
    cpy r1, 0x100000
    ret


; colour in r1
fill_screen:
    push r4
    
    int 0x84
    mov r4, r0             ; place buffer start in r4 (to never change)
    mov r2, 0x100000       ; buffer size
    
    ; fill one pixel so we can copy
    mov @r0, r1
    
    mov r3, 4              ; size to copy
    add r0, 4
    sub r2, 4
.loop:
    cpy r4, r3             ; copies the amount we have written to the next position
    add r0, r3             ; advance current buffer pos by amount we copied
    sub r2, r3             ; remove the amount we copied from the remaining bytes
    add r3, r3             ; we can now copy double
    
    cmp r2, 0              ; did we copy everything
    jne .loop
    
    pop r4
    ret


; draws a rectangle of data onto the screen
; x in r1
; y in r2
; w in r3
; h in stack
; data pointer in stack
draw_rect:
    ; prologue
    push r7             ; 
    push r6             ; line iterator
    push r5             ; data pointer (will change)
    push r4             ; height
    
    ; let's get our parameters from stack
    mov r0, sp          ; use r0 as our modifiable stack pointer
    add r0, 20          ; 4*5 bytes (point to start of data pointer value)
    mov r5, @r0         ; place in r5
    add r0, 4           ; move one back to height value
    mov r4, @r0         ; place in r4
    add r4, r2
    
    int 0x84            ; get disp buffer in r0
    
    umul r3, 4          ; multiply width by 4 to get line byte count (4 bytes per pixel)
    
    mov r6, r2          ; current line
    umul r1, 4          ; turn x into pixel offset from left
    add r0, r1          ; add it to start
    
    ; we need to add 512*y*4
    mov r7, r2
    umul r7, 2048
    add r0, r7
    
.loop:                  ; loop through each line
    ;int 0
    cpy r5, r3          ; copy r3 bytes from r5 to addr in r0
    add r0, 2048        ; set r0 to start of next line
    add r6, 1           ; next line
    add r5, r3          ; move pointer to next line
    
    cmp r6, r4          ; did we get to the end?
    jul .loop           ; if not then keep looping
    
    ; done, run epilogue
    pop r4
    pop r5
    pop r6
    pop r7
    ret

