; ====================================
;       Physics/Collision Code
; ====================================


; checks if a square at specified position would collide with
; a block on our tilemap
; r1 is x pos
; r2 is y pos
; r3 is width
; stack 1 is height
; r0 returns 1 for yes, 0 for no
square_collides:
    push r4
    push r5
    push r6
    push r7
    
    ; we need r1-3 free for point_collides calls
    mov r4, r1                  ; x pos
    mov r5, r2                  ; y pos
    mov r6, r3                  ; width
    mov r7, sp
    add r7, 16                  ; 4x4=16 bytes to height
    mov r7, @r7                 ; height
    
    ; the strat here will be to check each corner of the square with
    ; point_collides, if any are true then we're colliding.
    ; I'm going to manually write the calls, there's only 4.
    
    call point_collides         ; first call is easy, top left
    cmp r0, 1                   ; when calls are colliding, immediately return (r0 is already 1, don't need to set)
    je .done
    
    mov r1, r4
    add r1, r6                  ; x + width = right
    mov r2, r5
    call point_collides         ; top right
    cmp r0, 1
    je .done
    
    mov r1, r4
    mov r2, r5
    add r2, r7                  ; y + height = bottom
    call point_collides         ; bottom left
    cmp r0, 1
    je .done
    
    mov r1, r4
    add r1, r6
    mov r2, r5
    add r2, r7
    call point_collides         ; bottom right
    cmp r0, 1
    je .done
    
    ; at this point we know r0 is 0
    ; and none of the points collided.
    ; so this is already the correct return value.
.done:
    pop r7
    pop r6
    pop r5
    pop r4
    ret


; checks if a point is within a tile on the tilemap
; r1 is x
; r2 is y
; r0 returns 1 for yes, 0 for no
point_collides:
    push r4
    
    mov r3, 32
    udiv r1, r3                 ; r1 is tilemap x
    mov r3, 32
    udiv r2, r3                 ; r2 is tilemap y
    
    ; now we just query the tile
    mov r3, current_level
    umul r3, 256                ; 16x16 tiles, r3 is now offset from levels label
    mov r4, levels
    add r4, r3                  ; r4 is start of map
    
    umul r2, 16                 ; r2 is now offset to correct row
    add r4, r2
    add r4, r1                  ; and now r4 is tile value pointer
    mov r0, 0
    mov8 r0, @r4                ; actual value in r1
    
    cmp r0, 0
    je .done                    ; it's false
    
    mov r0, 1                   ; make sure it's 1 and not some other non zero value
.done:
    pop r4
    ret


process_physics:
    push r7
    push r6
    push r5
    push r4
    
    mov r1, @player_y
    mov r2, @player_x
    
    ; are we at 0, let's say that the bottom kills you
    mov r1, @player_y
    cmp r1, 512
    ;juge game_over
    
.end:
    pop r4
    pop r5
    pop r6
    pop r7
    ret

