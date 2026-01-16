jmp main

; =================
; Data Section
; =================
#include levels.asm

title_screen:
    dfile title_img.data

platform:
    dfile platform.data

player:
    dfile player.data

current_level:          ; store level being played
    d8 0
player_x:
    d32 0
player_y:
    d32 0
last_draw_player_x:
    d32 0
last_draw_player_y:
    d32 0
held_left:
    d8 0
held_right:
    d8 0
held_up:
    d8 0
held_down:
    d8 0

; =================
; Code section
; =================

#include utils.asm

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
    
    mov r1, 0x365235            ; bg colour
    call fill_screen
    
    mov r1, 0
    call draw_level
    
    
.loop:                          ; MAIN GAME LOOP (60tps, )
    int 0x85
    mov r7, r0
    
    call read_inputs            ; get all user inputs
    
    mov8 r1, @held_left
    mov8 r2, @held_right
    sub r2, r1                  ; calculate x change
    mov r1, @player_x
    add r1, r2
    mov @player_x, r1
    
    call redraw_player          ; remove and redraw player in new pos
    
    ; now that we've done everything for this frame
    ; let's wait until the next frame needs to run
    int 0x85
    sub r0, r7                  ; r0 is now time taken for frame
    
    ; debug frame time
    mov r1, r0
    ;int 0x90                    ; print frame time in ms
    
    cmp r0, 32                  ; compare to target time (1/60 * 1000) for 60fps
    juge .goodtiming            ; if it took 16 or longer then skip waiting
    
    ; wait some time to make it 60fps
    mov r1, 32
    sub r1, r0                  ; 16 - time taken ms = time to wait for
    call sleep
    
.goodtiming:
    jmp .loop


redraw_player:
    call unrender_player
    call render_player
    
    mov r1, @player_x
    mov @last_draw_player_x, r1
    mov r1, @player_y
    mov @last_draw_player_y, r1
    ret


; drain and handle user input
read_inputs:
    in r0, 0                    ; read from inp device
    cmp r0, -1                  ; will be -1 if no data available
    je .nodata
    
    ; okay there is data
    ;  r0                         = device id (keyboard is 0), we'll ignore this
    in r1, 0                    ; = type (0 is down, 1 is up)
    in r2, 0                    ; = value (key code)
    
    mov r3, 0                   ; value to set to = unpressed
    cmp r1, 0                   ; pressed
    jne .keyup
    
    ; key down
    mov r3, 1                   ; set value
.keyup:
    ; alright now set the key, get pointer to memory for key
    cmp r2, 'W'
    je .up
    cmp r2, 'S'
    je .down
    cmp r2, 'A'
    je .left
    cmp r2, 'D'
    je .right
    
    jmp .doneprocessing         ; not relevant to us
.up:
    mov r0, held_up
    jmp .setkey
.down:
    mov r0, held_down
    jmp .setkey
.left:
    mov r0, held_left
    jmp .setkey
.right:
    mov r0, held_right
    
.setkey:
    mov8 @r0, r3
    
.doneprocessing:
    jmp read_inputs              ; keep handling until no data is available
.nodata:
    ret


; paint background colour over where the player is
unrender_player:
    int 0x84                    ; buffer in r0

    mov r1, @last_draw_player_y           ; player start y
    mov r2, @last_draw_player_x           ; player start x
    
    umul r1, 2048               ; make it offset to start of row
    add r0, r1                  ; add it
    umul r2, 4
    add r0, r2                  ; now we're at the correct start of img area
    
    mov r1, r0                  ; r1 can be start of first row
    mov r2, r1                  ; current pos in row
    mov r3, r2
    add r3, 128                 ; end pos
    
.firstrowloop:
    mov @r2, 0x365235           ; draw
    add r2, 4
    cmp r2, r3
    jul .firstrowloop
    
    ; okay we did it, copy this row a bunch of times
    
    ; setup row loop
    ; we're going to be copying
    mov r3, 1                   ; rows done
.rowloop:
    cpy r1, 128                 ; copy row to destination in r0
    add r0, 2048
    add r3, 1
    cmp r3, 32                  ; have we made it to 32 rows?
    jule .rowloop
    
    ; done
    ret


render_player:
    mov r1, @player_x
    mov r2, @player_y
    mov r3, 32
    push 32
    push player
    call draw_recta
    add sp, 8
    ret


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


