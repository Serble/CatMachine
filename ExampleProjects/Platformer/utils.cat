; ====================================
;         General Utilities
; ====================================

; ms in r1
sleep:
    int INT_GET_TIME            ; uptime in r0
    mov r2, r0                  ; current time
    add r2, r1                  ; r2 is target time
.loop:
    int INT_GET_TIME
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


; routine to call when a fatal error occurs
; make sure to CALL not JMP so the return address is saved
panic:
    push r1
    
    mov r1, sp
    add r1, 4*2                 ; point to return address
    mov r1, @r1                 ; get return address
    int INT_DEBUG_PRINT         ; debug print return address
    
    pop r1
    
    int INT_PANIC               ; this is an error interrupt, it will pause the VM and dump info
    jmp hang


; for when shit hits the fan
wat:
    mov r1, 69696969
    int INT_DEBUG_PRINT
    jmp hang
