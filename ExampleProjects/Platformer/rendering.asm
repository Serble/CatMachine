; ====================================
;      Rendering Utils/Routines
;
; This file contains all the code that
; actually writes to the display buffer.
; ====================================


; draws a level to the screen (excluding player)
; level in r1 (index)
draw_level:
    ; prologue
    push r4                     ; current tile pointer
    push r5                     ; y iterator
    push r6                     ; x iterator
    push r7                     ;
    
    mov r4, r1
    umul r4, GRID_WIDTH*GRID_HEIGHT ; r4 is now offset from levels
    add r4, levels              ; and now a pointer to the level data
    mov r5, 0                   ; current y
.yloop:
    mov r6, 0                   ; current x
.xloop:
    ; draw this tile (the type is at r4)
    mov r7, 0
    mov8 r7, @r4
    cmp r7, 0
    je .dontdraw                ; nothing there
    
    ; okay there's something there
    mov r1, r6                  ; current x position
    umul r1, TILE_SIZE          ; current x pixel
    
    mov r2, r5                  ; current y position
    umul r2, TILE_SIZE          ; current y pixel
    
    mov r3, TILE_SIZE           ; width
    push TILE_SIZE              ; height
    push platform               ; data
    call draw_rect
    add sp, 8                   ; remove args from stack
.dontdraw:
    add r4, 1                   ; go to next tile in level data
    
    add r6, 1
    cmp r6, GRID_WIDTH
    jul .xloop
    
    ; done this row
    add r5, 1
    cmp r5, GRID_HEIGHT
    jul .yloop
    
    ; epilogue
    pop r7
    pop r6
    pop r5
    pop r4
    ret


; paint background colour over where the player is
unrender_player:
    mov r0, @disp_buff

    mov r1, @last_draw_player_y ; player start y
    mov r2, @last_draw_player_x ; player start x
    
    ; sanity check
    cmp r1, SCREEN_HEIGHT
    juge panic                  ; negatives will also trigger this (unsigned compare)
    cmp r2, SCREEN_WIDTH
    juge panic
    
    umul r1, SCREEN_WIDTH*4     ; make it offset to start of row (512x4)
    add r0, r1                  ; add it
    umul r2, 4
    add r0, r2                  ; now we're at the correct start of img area
    
    mov r1, r0                  ; r1 can be start of first row
    mov r2, r1                  ; current pos in row
    mov r3, r2
    add r3, PLAYER_WIDTH*4      ; end pos (32x4)
.firstrowloop:
    mov @r2, BACKGROUND_COLOUR  ; draw
    add r2, 4
    cmp r2, r3
    jul .firstrowloop
    
    ; okay we did it, copy this row a bunch of times
    
    ; setup row loop
    ; we're going to be copying
    mov r3, 1                   ; rows done
.rowloop:
    cpy r1, PLAYER_WIDTH*4      ; copy row to destination in r0
    add r0, SCREEN_WIDTH*4      ; move to next row
    add r3, 1
    cmp r3, PLAYER_HEIGHT       ; have we made it to 32 rows?
    jule .rowloop
    
    ; done
    ret


; draw the player at their current position
render_player:
    mov r1, @player_x
    mov r2, @player_y
    mov r3, PLAYER_WIDTH
    push PLAYER_HEIGHT
    push player
    call draw_recta
    add sp, 8
    ret


; draw a 512x512 image onto the screen
; img pointer in r1
draw_screen:
    mov r0, @disp_buff
    cpy r1, SCREEN_BUFF_SIZE
    ret


; fills the entire screen with a solid colour
; uses cpy to copy continually doubling numbers of pixels
; colour in r1
fill_screen:
    push r4
    
    mov r0, @disp_buff          ; start of buffer (to change)
    mov r4, r0                  ; place buffer start in r4 (to never change)
    mov r2, SCREEN_BUFF_SIZE    ; buffer size
    
    ; fill one pixel so we can copy
    mov @r0, r1
    
    mov r3, 4                   ; size to copy
    add r0, 4
    sub r2, 4
.loop:
    cpy r4, r3                  ; copies the amount we have written to the next position
    add r0, r3                  ; advance current buffer pos by amount we copied
    sub r2, r3                  ; remove the amount we copied from the remaining bytes
    add r3, r3                  ; we can now copy double
    
    cmp r2, 0                   ; did we copy everything
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
    push r7                     ; 
    push r6                     ; line iterator
    push r5                     ; data pointer (will change)
    push r4                     ; height
    
    ; let's get our parameters from stack
    mov r0, sp                  ; use r0 as our modifiable stack pointer
    add r0, 4*5                 ; 4*5 bytes (point to start of data pointer value)
    mov r5, @r0                 ; place in r5
    add r0, 4                   ; move one back to height value
    mov r4, @r0                 ; place in r4
    add r4, r2
    
    ; sanity check parameters
    cmp r1, SCREEN_WIDTH
    juge panic
    cmp r2, SCREEN_HEIGHT
    juge panic
    cmp r3, SCREEN_WIDTH
    juge panic
    cmp r4, SCREEN_HEIGHT
    juge panic
    
    mov r0, @disp_buff
    
    umul r3, 4                  ; multiply width by 4 to get line byte count (4 bytes per pixel)
    
    mov r6, r2                  ; current line
    umul r1, 4                  ; turn x into pixel offset from left
    add r0, r1                  ; add it to start
    
    ; we need to add y*512*4
    mov r7, r2
    umul r7, SCREEN_WIDTH*4
    add r0, r7
    
.loop:                          ; loop through each line
    ;int 0
    cpy r5, r3                  ; copy r3 bytes from r5 to addr in r0
    add r0, SCREEN_WIDTH*4      ; set r0 to start of next line
    add r6, 1                   ; next line
    add r5, r3                  ; move pointer to next line
    
    cmp r6, r4                  ; did we get to the end?
    jul .loop                   ; if not then keep looping
    
    ; done, run epilogue
    pop r4
    pop r5
    pop r6
    pop r7
    ret


; draws a rectangle of data with transparency onto the screen
; PARTIAL TRANSPARENCY IS NOT SUPPORTED AND WILL BE TREATED
; AS OPAQUE.
; x in r1
; y in r2
; w in r3
; h in stack
; data pointer in stack
draw_recta:
    ; prologue
    push r7                     ; x iterator
    push r6                     ; y iterator
    push r5                     ; data pointer (will change)
    push r4                     ; height
    
    ; let's get our parameters from stack
    mov r0, sp                  ; use r0 as our modifiable stack pointer
    add r0, 4*5                 ; 4*5 bytes (point to start of data pointer value)
    mov r5, @r0                 ; place in r5
    add r0, 4                   ; move one back to height value
    mov r4, @r0                 ; place in r4
    
    ; sanity check parameters
    cmp r1, SCREEN_WIDTH
    juge panic
    cmp r2, SCREEN_HEIGHT
    juge panic
    cmp r3, SCREEN_WIDTH
    juge panic
    cmp r4, SCREEN_HEIGHT
    juge panic
    
    mov r0, @disp_buff
    
    umul r1, 4                  ; turn x into pixel offset from left
    add r0, r1                  ; add it to start
    
    ; we need to add 512*y*4
    mov r7, r2
    umul r7, 2048
    add r0, r7
    
    mov r6, 0                   ; y iterator (when r6 == r4[height] then done)
.yloop:                         ; loop through each line
    mov r7, 0                   ; x iterator (when r7 == r3[width] then done row)
.xloop:
    mov r1, @r5                 ; place the pixel in r1
    and r1, 0xFF000000          ; get alpha component
    cmp r1, 0
    je .finisheddraw            ; it has alpha, don't draw
    
    mov r1, @r5                 ; reload data
    mov @r0, r1                 ; and put that in the buffer
.finisheddraw:
    add r0, 4                   ; move the next pixel
    add r5, 4                   ; on the data pointer as well
    
    add r7, 1
    cmp r7, r3                  ; did we get to the end?
    jul .xloop                  ; if not then keep looping
    
    ; end of row
    add r0, SCREEN_WIDTH*4      ; go to the next line (same column)
    sub r0, r3                  ; sub r3*4=width in bytes
    sub r0, r3
    sub r0, r3
    sub r0, r3
    
    add r6, 1
    cmp r6, r4                  ; did we reach the end of the rows
    jul .yloop                  ; if not then keep looping
    
    ; done, run epilogue
    pop r4
    pop r5
    pop r6
    pop r7
    ret

