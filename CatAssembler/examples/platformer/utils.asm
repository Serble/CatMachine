
; ms in r1
sleep:
    int 0x85              ; uptime in r0
    mov r2, r0            ; current time
    add r2, r1            ; r2 is target time
.loop:
    int 0x85
    cmp r0, r2
    jul .loop
    
    ; done
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


; draw a 512x512 image onto the screen
; img pointer in r1
draw_screen:
    int 0x84
    cpy r1, 0x100000
    ret


; fills the entire screen with a solid colour
; uses cpy to copy continually doubling numbers of pixels
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


; draws a rectangle of data with transparency onto the screen
; x in r1
; y in r2
; w in r3
; h in stack
; data pointer in stack
draw_recta:
    ; prologue
    push r7             ; x iterator
    push r6             ; y iterator
    push r5             ; data pointer (will change)
    push r4             ; height
    
    ; let's get our parameters from stack
    mov r0, sp          ; use r0 as our modifiable stack pointer
    add r0, 20          ; 4*5 bytes (point to start of data pointer value)
    mov r5, @r0         ; place in r5
    add r0, 4           ; move one back to height value
    mov r4, @r0         ; place in r4
    
    int 0x84            ; get disp buffer in r0
    
    umul r1, 4          ; turn x into pixel offset from left
    add r0, r1          ; add it to start
    
    ; we need to add 512*y*4
    mov r7, r2
    umul r7, 2048
    add r0, r7
    
    mov r6, 0           ; y iterator (when r6 == r4[height] then done)
.yloop:                 ; loop through each line
    mov r7, 0           ; x iterator (when r7 == r3[width] then done row)
.xloop:
    mov r1, @r5         ; place the pixel in r1
    and r1, 0xFF000000  ; get alpha component
    cmp r1, 0
    je .finisheddraw   ; it has alpha, don't draw
    
    mov r1, @r5         ; reload data
    mov @r0, r1         ; and put that in the buffer
    
.finisheddraw:
    add r0, 4           ; move the next pixel
    add r5, 4           ; on the data pointer as well
    
    add r7, 1
    cmp r7, r3          ; did we get to the end?
    jul .xloop          ; if not then keep looping
    
    ; end of row
    add r0, 2048        ; go to the next line (same column)
    sub r0, r3          ; sub r3*4=width in bytes
    sub r0, r3
    sub r0, r3
    sub r0, r3
    
    add r6, 1
    cmp r6, r4          ; did we reach the end of the rows
    jul .yloop          ; if not then keep looping
    
    ; done, run epilogue
    pop r4
    pop r5
    pop r6
    pop r7
    ret

