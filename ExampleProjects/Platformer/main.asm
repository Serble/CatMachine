; ====================================
;        Main Game Loop/Logic
;
; For some reason I decided that data
; will come before all code data.
; But CatVM executes from mem addr 0
; so there's a jmp to our main loop.
; ====================================

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
vel_y:
    d32 0
vel_x:
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
disp_buff:
    d32 0

; =================
; Code section
; =================

#include utils.asm
#include physics.asm
#include rendering.asm

main:
    ; load the display buffer into memory
    ; we do this because memory loads are 
    ; much faster than interrupts.
    int 0x84
    mov @disp_buff, r0
    
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
    call process_movement
    call process_physics
    
    ;  DEBUGGING STATEMENTS
    ;mov r1, 999
    ;int 0x90
    ;mov r1, @player_x
    ;int 0x90
    ;mov r1, @last_draw_player_x
    ;int 0x90
    ;mov r1, @player_y
    ;int 0x90
    ;mov r1, @last_draw_player_y
    ;int 0x90
    
    call redraw_player          ; remove and redraw player in new pos
    
    ; THIS IS END OF FRAME STUFF
    int 0x86                    ; tell screen to update
    
    ; now that we've done everything for this frame
    ; let's wait until the next frame needs to run
    int 0x85
    sub r0, r7                  ; r0 is now time taken for frame
    
    ; debug frame time
    mov r1, r0
    ;int 0x90                    ; print frame time in ms
    
    cmp r0, 16                  ; compare to target time (1/60 * 1000) for 60fps
    juge .goodtiming            ; if it took 16 or longer then skip waiting
    
    ; wait some time to make it 60fps
    mov r1, 16
    sub r1, r0                  ; 16 - time taken ms = time to wait for
    call sleep
    
.goodtiming:                    ; not really 'good' timing, more like not ahead
    jmp .loop


process_movement:
    mov8 r1, @held_left
    mov8 r2, @held_right
    sub r2, r1                  ; calculate x change
    ;mov r1, r2 ;dbg
    ;int 0x90   ;dbg
    mov r1, @player_x
    add r1, r2
    
    cmp r1, 0                   ; if it's higher than this it's negative
    jige .goodx
    
    ; bad
    mov r1, 0
.goodx:
    mov @player_x, r1
    ;int 0x90
    ret


; temporary game over method
; just draw red for now (no restarting yet)
game_over:
    mov r1, 0xFF0000
    call fill_screen
    int 0x86               ; refresh screen
    jmp hang


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


