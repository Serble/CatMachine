; ====================================
;         General Utilities
; ====================================

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


hang:
    jmp hang


; for when shit hits the fan
wat:
    mov r1, 69696969
    int 0x90
    jmp hang
