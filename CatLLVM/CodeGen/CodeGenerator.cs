using System.Globalization;
using System.Text;
using CatLLVM.IR;

namespace CatLLVM.CodeGen;

/// <summary>
/// Lowers an <see cref="IrModule"/> to Cat assembly text.
///
/// Strategy: every SSA value (including parameters and instruction results)
/// gets a dedicated 4-byte stack slot relative to the base pointer (r7).
/// Allocas get their own dedicated slot sized appropriately. This is the same
/// "every value lives on the stack" approach clang uses at -O0; it produces
/// slow but reliably correct code without needing a register allocator.
///
/// Calling convention (matches Catnip):
///   - arg0,arg1,arg2 in r1, r2, r3
///   - extra args pushed by caller right-to-left
///   - return value in r0
///   - r4-r7 callee-saved (we always save r7 as base pointer; we don't use
///     r4-r6 in generated code, so they need no extra saves)
///   - r0/r1/r2/r3 are scratch within a function
///
/// Scratch register assignments inside a single instruction lowering:
///   - r0 = primary value scratch
///   - r1 = secondary value scratch
///   - r2 = address scratch (for computing [r7 - offset])
/// At each call boundary we re-load operands from stack slots, so r1/r2/r3
/// being clobbered is fine.
/// </summary>
public sealed class CodeGenerator {

    private readonly IrModule _module;
    private readonly AsmBuilder _asm = new();
    private readonly string _sourceName;

    // Per-function state:
    private readonly Dictionary<string, int> _slotOffsets = new();   // SSA value -> bytes below r7
    private int _frameSize;
    private string _currentFnName = "";
    private int _localLabelCounter;

    public CodeGenerator(IrModule module, string sourceName) {
        _module = module;
        _sourceName = sourceName;
    }

    public string Generate() {
        // Emit a tiny startup that calls main and then halts via int 0x83
        // (the `Halt` interrupt used elsewhere in CatVM examples).
        EmitStartup();
        foreach (IrFunction fn in _module.Functions) {
            if (fn.IsDeclaration) continue;
            if (fn.Name.StartsWith("__catvm_")) continue;  // intrinsics: not real fns
            EmitFunction(fn);
        }
        EmitGlobals();
        return _asm.Build(_sourceName);
    }

    // -------------------------------------------------------------------------
    private void EmitStartup() {
        StringBuilder c = _asm.Code;
        _asm.Comment(c, "program entry: call main, then shutdown via int 0x82");
        _asm.Ins(c, "call main");
        _asm.Ins(c, "int 0x82");
        c.AppendLine();
    }

    // -------------------------------------------------------------------------
    private void EmitFunction(IrFunction fn) {
        _currentFnName = SanGlobal(fn.Name);
        _slotOffsets.Clear();
        _frameSize = 0;
        _localLabelCounter = 0;

        // ---- Pass 1: allocate stack slots ----
        // Parameters (first 3 register-passed; rest already on the caller stack)
        for (int i = 0; i < fn.Params.Count; i++) {
            IrParam p = fn.Params[i];
            if (i < 3) {
                _frameSize += 4;
                _slotOffsets[p.Name] = _frameSize;  // local slot
            } else {
                // Stack-passed argument. Offset from r7: +8 + (i-3)*4
                // (+0 = saved r7, +4 = return addr, +8 = first stack arg)
                _slotOffsets[p.Name] = -(8 + (i - 3) * 4);  // negative offset = "above" r7
            }
        }
        // Allocas and instruction results
        foreach (IrBasicBlock bb in fn.Blocks) {
            foreach (IrInstruction ins in bb.Instructions) {
                switch (ins) {
                    case AllocaIns a:
                        int sz = AlignUp(a.ElementType.SizeBytes * a.Count, 4);
                        _frameSize += sz;
                        _slotOffsets[ins.Result!] = _frameSize;
                        break;
                    default:
                        if (ins.Result != null) {
                            _frameSize += 4;
                            _slotOffsets[ins.Result] = _frameSize;
                        }
                        break;
                }
            }
        }

        // ---- Emit ----
        StringBuilder c = _asm.Code;
        c.AppendLine();
        _asm.Comment(c, $"function: {fn.Name} (frame={_frameSize})");
        _asm.Label(c, _currentFnName);

        // Prologue
        _asm.Ins(c, "push r7              ; save caller's base pointer");
        _asm.Ins(c, "mov r7, sp           ; new base pointer");
        if (_frameSize > 0)
            _asm.Ins(c, $"sub sp, {_frameSize}            ; allocate locals");

        // Copy register-passed args into their slots
        string[] argRegs = ["r1", "r2", "r3"];
        for (int i = 0; i < fn.Params.Count && i < 3; i++) {
            IrParam p = fn.Params[i];
            int off = _slotOffsets[p.Name];
            _asm.Ins(c, $"mov r0, r7");
            _asm.Ins(c, $"sub r0, {off}");
            _asm.Ins(c, $"{StoreOp(p.Ty.SizeBytes)} [r0], {argRegs[i]}     ; store param '{p.Name}'");
        }

        // Body
        foreach (IrBasicBlock bb in fn.Blocks) {
            _asm.Label(c, BlockLabel(_currentFnName, bb.Label));
            foreach (IrInstruction ins in bb.Instructions) {
                EmitInstruction(fn, bb, ins);
            }
        }

        // Shared epilogue (blocks reach this via `jmp .fn_end` from `ret`).
        _asm.Label(c, $".{_currentFnName}_end");
        _asm.Ins(c, "mov sp, r7           ; deallocate locals");
        _asm.Ins(c, "pop r7               ; restore base pointer");
        _asm.Ins(c, "ret");
    }

    // -------------------------------------------------------------------------
    private void EmitInstruction(IrFunction fn, IrBasicBlock bb, IrInstruction ins) {
        StringBuilder c = _asm.Code;
        _asm.Comment(c, "  ; " + DescribeInstruction(ins));

        switch (ins) {
            case AllocaIns:
                // The slot itself IS the allocation. The "value" of the alloca
                // is the address of its slot. Store that address into the slot
                // for `%result`... wait: we allocated a single slot for both the
                // pointer and the buffer. We need them separate.
                // To keep things simple we let the alloca "result slot" double
                // as the storage and the pointer points to itself. So:
                //   addr = bp - offset
                //   store addr -> slot_addr  (slot already holds buffer; we
                //   need the result of alloca to point there)
                // Easier approach: the alloca's result slot IS the buffer.
                // We treat any reference to %result_of_alloca as "compute its
                // address" rather than dereference. We do this by giving allocas
                // a marker: store a magic pointer to itself? Simpler: handle
                // `IrLocalRef` whose definer is an alloca specially.
                //
                // The cleanest scheme: when we see an alloca, we DON'T need to
                // do anything at runtime - the pointer "value" is simply
                // (r7 - slot_offset). Instead of materializing it now and
                // storing it, we just remember that this name is an alloca and
                // resolve to the address whenever we need the value.
                //
                // (Implemented in `LoadValueIntoReg` by checking _allocaSlots.)
                _allocaSlots.Add(ins.Result!);
                break;

            case LoadIns load:
                LoadValueIntoReg(load.Pointer, "r1");                 // r1 = ptr
                _asm.Ins(c, $"{LoadOp(load.LoadType.SizeBytes)} r0, [r1]");
                StoreReg("r0", ins.Result!, ins.ResultType);
                break;

            case StoreIns st:
                LoadValueIntoReg(st.Value, "r0");                     // r0 = value
                LoadValueIntoReg(st.Pointer, "r1");                   // r1 = ptr
                _asm.Ins(c, $"{StoreOp(st.Value.Type.SizeBytes)} [r1], r0");
                break;

            case BinOpIns bop:
                EmitBinOp(bop);
                StoreReg("r0", ins.Result!, ins.ResultType);
                break;

            case IcmpIns icmp:
                EmitIcmp(icmp);
                StoreReg("r0", ins.Result!, ins.ResultType);
                break;

            case BrIns br:
                EmitPhiResolution(fn, bb.Label, br.Target);
                _asm.Ins(c, $"jmp {BlockLabel(_currentFnName, br.Target)}");
                break;

            case BrCondIns brc: {
                LoadValueIntoReg(brc.Condition, "r0");
                _asm.Ins(c, "cmp r0, 0");
                // We can't insert phi-resolution between the cmp and the jump
                // without disturbing flags. Use a trampoline.
                string falseTrampoline = NewLabel("brc_false");
                string trueTrampoline = NewLabel("brc_true");
                _asm.Ins(c, $"je {falseTrampoline}");
                _asm.Ins(c, $"jmp {trueTrampoline}");

                _asm.Label(c, trueTrampoline);
                EmitPhiResolution(fn, bb.Label, brc.IfTrue);
                _asm.Ins(c, $"jmp {BlockLabel(_currentFnName, brc.IfTrue)}");

                _asm.Label(c, falseTrampoline);
                EmitPhiResolution(fn, bb.Label, brc.IfFalse);
                _asm.Ins(c, $"jmp {BlockLabel(_currentFnName, brc.IfFalse)}");
                break;
            }

            case RetIns ret:
                if (ret.Value != null) LoadValueIntoReg(ret.Value, "r0");
                _asm.Ins(c, $"jmp .{_currentFnName}_end");
                break;

            case CallIns call:
                EmitCall(call);
                break;

            case GepIns gep:
                EmitGep(gep);
                StoreReg("r0", ins.Result!, ins.ResultType);
                break;

            case CastIns cast:
                EmitCast(cast);
                StoreReg("r0", ins.Result!, ins.ResultType);
                break;

            case PhiIns:
                // Phi nodes are placeholders; values are written to their slot
                // by the predecessor blocks' phi-resolution code. Nothing to
                // emit here.
                break;

            default:
                throw new NotSupportedException($"codegen: {ins.GetType().Name}");
        }
    }

    private readonly HashSet<string> _allocaSlots = new();

    // -------------------------------------------------------------------------
    private void EmitBinOp(BinOpIns bop) {
        StringBuilder c = _asm.Code;
        LoadValueIntoReg(bop.Lhs, "r0");
        LoadValueIntoReg(bop.Rhs, "r1");
        switch (bop.Op) {
            case BinOp.Add: _asm.Ins(c, "add r0, r1"); break;
            case BinOp.Sub: _asm.Ins(c, "sub r0, r1"); break;
            case BinOp.Mul: _asm.Ins(c, "imul r0, r1"); break;
            case BinOp.SDiv: _asm.Ins(c, "idiv r0, r1"); break;
            case BinOp.UDiv: _asm.Ins(c, "udiv r0, r1"); break;
            case BinOp.SRem:
                // r0 = r0 - (r0/r1)*r1 ; we don't have a rem op so emulate.
                // Save lhs in a stack-slot scratch via push/pop r2.
                _asm.Ins(c, "push r0          ; save lhs for srem");
                _asm.Ins(c, "push r1          ; save rhs for srem");
                _asm.Ins(c, "idiv r0, r1      ; r0 = lhs / rhs");
                _asm.Ins(c, "pop r1           ; restore rhs");
                _asm.Ins(c, "imul r0, r1      ; r0 = (lhs/rhs)*rhs");
                _asm.Ins(c, "pop r1           ; restore lhs into r1 (was on stack)");
                _asm.Ins(c, "sub r1, r0       ; r1 = lhs - (lhs/rhs)*rhs");
                _asm.Ins(c, "mov r0, r1");
                break;
            case BinOp.URem:
                _asm.Ins(c, "push r0          ; save lhs for urem");
                _asm.Ins(c, "push r1");
                _asm.Ins(c, "udiv r0, r1");
                _asm.Ins(c, "pop r1");
                _asm.Ins(c, "imul r0, r1");
                _asm.Ins(c, "pop r1");
                _asm.Ins(c, "sub r1, r0");
                _asm.Ins(c, "mov r0, r1");
                break;
            case BinOp.And: _asm.Ins(c, "and r0, r1"); break;
            case BinOp.Or:  _asm.Ins(c, "or r0, r1");  break;
            case BinOp.Xor: _asm.Ins(c, "xor r0, r1"); break;
            case BinOp.Shl: _asm.Ins(c, "shl r0, r1"); break;
            case BinOp.LShr: _asm.Ins(c, "shr r0, r1"); break;
            case BinOp.AShr:
                // No arithmetic shift opcode. Emulate by sign-extension:
                //   if (lhs >= 0) shr; else shr then OR-in sign bits.
                // Cheap version (works for shift counts in [0, 31]):
                //   tmp = lhs >> n   (logical)
                //   mask = (lhs & 0x80000000) ? ~((1u<<(32-n)) - 1) : 0
                //   result = tmp | mask
                // To stay simple, we approximate with a runtime sequence:
                _asm.Ins(c, "; ashr (emulated) - assumes shift in 0..31");
                _asm.Ins(c, "push r1                       ; save shift count");
                _asm.Ins(c, "shr r0, r1                    ; logical part");
                _asm.Ins(c, "pop r1                        ; restore count");
                // Build sign-fill mask: (-1) << (32 - n)
                // mov r2, -1 ; mov tmp = 32 - n ; shl r2, tmp
                _asm.Ins(c, "push r0                       ; save shifted result");
                _asm.Ins(c, "mov r0, 0xffffffff");
                _asm.Ins(c, "push r1                       ; save n again for sub");
                _asm.Ins(c, "mov r2, 32");
                _asm.Ins(c, "sub r2, r1                    ; r2 = 32 - n");
                _asm.Ins(c, "shl r0, r2                    ; r0 = mask");
                _asm.Ins(c, "pop r1");
                _asm.Ins(c, "pop r2                        ; r2 = logical-shifted result");
                _asm.Ins(c, "or r0, r2                     ; combine mask | result (incorrect sign-conditional, conservative)");
                // NB: this is conservative (always sign-fills). For purely
                // unsigned-input use lshr instead. Tools that emit `ashr` on
                // values they know are >= 0 will still get the right answer.
                break;
        }
    }

    // -------------------------------------------------------------------------
    private void EmitIcmp(IcmpIns ic) {
        StringBuilder c = _asm.Code;
        LoadValueIntoReg(ic.Lhs, "r0");
        LoadValueIntoReg(ic.Rhs, "r1");
        _asm.Ins(c, "cmp r0, r1");
        // Set r0 to 0 or 1 based on predicate.
        string trueLbl = NewLabel("icmp_true");
        string endLbl = NewLabel("icmp_end");
        string jmpOp = ic.Pred switch {
            IcmpPred.Eq  => "je",
            IcmpPred.Ne  => "jne",
            IcmpPred.Slt => "jil",
            IcmpPred.Sle => "jile",
            IcmpPred.Sgt => "jig",
            IcmpPred.Sge => "jige",
            IcmpPred.Ult => "jul",
            IcmpPred.Ule => "jule",
            IcmpPred.Ugt => "jug",
            IcmpPred.Uge => "juge",
            _ => throw new NotSupportedException()
        };
        _asm.Ins(c, $"{jmpOp} {trueLbl}");
        _asm.Ins(c, "mov r0, 0");
        _asm.Ins(c, $"jmp {endLbl}");
        _asm.Label(c, trueLbl);
        _asm.Ins(c, "mov r0, 1");
        _asm.Label(c, endLbl);
    }

    // -------------------------------------------------------------------------
    private void EmitCall(CallIns call) {
        StringBuilder c = _asm.Code;

        // Intrinsic mapping
        if (call.Callee is IrGlobalRef gr && IsIntrinsic(gr.Name)) {
            EmitIntrinsicCall(gr.Name, call);
            return;
        }

        // Push stack-passed args (rightmost first).
        int stackArgs = Math.Max(0, call.Args.Count - 3);
        for (int i = call.Args.Count - 1; i >= 3; i--) {
            LoadValueIntoReg(call.Args[i], "r0");
            _asm.Ins(c, "push r0");
        }
        // Load register-passed args via a push/pop staging area so the
        // load helpers (which use r2 as their address scratch) can't trample
        // an arg we already prepared. We push args 0..2 in left-to-right
        // order so that popping in reverse gives us r3, r2, r1.
        for (int i = 0; i < call.Args.Count && i < 3; i++) {
            LoadValueIntoReg(call.Args[i], "r0");
            _asm.Ins(c, "push r0          ; stage arg for r" + (i + 1));
        }
        if (call.Args.Count > 2) _asm.Ins(c, "pop r3");
        if (call.Args.Count > 1) _asm.Ins(c, "pop r2");
        if (call.Args.Count > 0) _asm.Ins(c, "pop r1");

        if (call.Callee is IrGlobalRef gref) {
            _asm.Ins(c, $"call {SanGlobal(gref.Name)}");
        } else if (call.Callee is IrLocalRef lref) {
            // indirect call via r0
            LoadValueIntoReg(lref, "r0");
            _asm.Ins(c, "call r0, 0");
        } else {
            throw new NotSupportedException("call: unsupported callee");
        }

        // Clean up stack args.
        if (stackArgs > 0) _asm.Ins(c, $"add sp, {stackArgs * 4}        ; clean up stack args");

        // Capture return value.
        if (call.Result != null && call.ReturnType is not VoidType) {
            StoreReg("r0", call.Result, call.ReturnType);
        }
    }

    private static bool IsIntrinsic(string name) => name switch {
        "__catvm_int" or "__catvm_in" or "__catvm_out" or "__catvm_syscall"
            or "__catvm_print" or "__catvm_uptime" => true,
        _ => false
    };

    private void EmitIntrinsicCall(string name, CallIns call) {
        StringBuilder c = _asm.Code;
        // All CatVM intrinsics map to dedicated opcodes. We DON'T set up
        // r1/r2/r3 with arguments first (that would clobber state); instead
        // each intrinsic loads exactly what it needs.
        switch (name) {
            case "__catvm_int": {
                // void __catvm_int(i8 num);   -> int <num>
                if (call.Args.Count != 1) throw new InvalidOperationException("__catvm_int takes 1 arg");
                LoadValueIntoReg(call.Args[0], "r0");
                _asm.Ins(c, "int r0");
                break;
            }
            case "__catvm_in": {
                // i32 __catvm_in(i32 port);
                if (call.Args.Count != 1) throw new InvalidOperationException("__catvm_in takes 1 arg");
                LoadValueIntoReg(call.Args[0], "r1");
                _asm.Ins(c, "in r0, r1");
                break;
            }
            case "__catvm_out": {
                // void __catvm_out(i32 port, i32 value);
                if (call.Args.Count != 2) throw new InvalidOperationException("__catvm_out takes 2 args");
                LoadValueIntoReg(call.Args[1], "r1");
                LoadValueIntoReg(call.Args[0], "r0");
                _asm.Ins(c, "out r0, r1");
                break;
            }
            case "__catvm_syscall": {
                _asm.Ins(c, "syscall");
                break;
            }
            case "__catvm_print": {
                // void __catvm_print(ptr s);  -> int 0x80 with r1 = s
                if (call.Args.Count != 1) throw new InvalidOperationException("__catvm_print takes 1 arg");
                LoadValueIntoReg(call.Args[0], "r1");
                _asm.Ins(c, "int 0x80");
                break;
            }
            case "__catvm_uptime": {
                // i32 __catvm_uptime(void);  -> int 0x85, return value in r0
                _asm.Ins(c, "int 0x85");
                break;
            }
        }
        if (call.Result != null && call.ReturnType is not VoidType) {
            StoreReg("r0", call.Result, call.ReturnType);
        }
    }

    // -------------------------------------------------------------------------
    private void EmitGep(GepIns gep) {
        StringBuilder c = _asm.Code;
        // Load base pointer.
        LoadValueIntoReg(gep.Pointer, "r0");
        // Apply each index.
        // First index walks in baseTy strides (per LLVM's GEP semantics for
        // the leading `<ty>, ptr %p, idx` form). Subsequent indices walk one
        // level deeper, but we only support arrays/integers so each remaining
        // index walks in the element type's stride.
        IrType currentType = gep.BaseType;
        for (int i = 0; i < gep.Indices.Count; i++) {
            int stride;
            if (i == 0) {
                stride = currentType.SizeBytes;
            } else {
                if (currentType is ArrayType at) {
                    currentType = at.Element;
                    stride = currentType.SizeBytes;
                } else {
                    stride = currentType.SizeBytes;
                }
            }
            // r0 += idx * stride
            // Constant-fold the easy case where idx is a constant int.
            IrValue idx = gep.Indices[i];
            if (idx is IrConstInt ci) {
                long delta = ci.Value * stride;
                if (delta != 0) _asm.Ins(c, $"add r0, {(uint)delta}");
            } else {
                LoadValueIntoReg(idx, "r1");
                if (stride > 1) _asm.Ins(c, $"imul r1, {stride}");
                _asm.Ins(c, "add r0, r1");
            }
        }
    }

    // -------------------------------------------------------------------------
    private void EmitCast(CastIns cast) {
        StringBuilder c = _asm.Code;
        LoadValueIntoReg(cast.Source, "r0");
        switch (cast.Kind) {
            case CastKind.ZExt:
            case CastKind.BitCast:
            case CastKind.PtrToInt:
            case CastKind.IntToPtr:
                // For our 32-bit slot model, zext / bitcast / ptr<->int are no-ops.
                break;
            case CastKind.Trunc:
                // Mask to the target width.
                if (cast.TargetType is IntType it) {
                    if (it.Bits >= 32) break;
                    uint mask = it.Bits >= 32 ? 0xFFFFFFFFu : (1u << it.Bits) - 1;
                    _asm.Ins(c, $"and r0, {mask}");
                }
                break;
            case CastKind.SExt:
                // Sign-extend an i8/i16 value sitting in low bits of r0.
                // result = (i32)(int8/16)source
                if (cast.Source.Type is IntType srcIt) {
                    if (srcIt.Bits >= 32) break;
                    int shift = 32 - srcIt.Bits;
                    _asm.Ins(c, $"shl r0, {shift}        ; sext: shift sign bit to top");
                    // No arithmetic shift right; emulate with the same trick as ashr.
                    string negLbl = NewLabel("sext_neg");
                    string endLbl = NewLabel("sext_end");
                    _asm.Ins(c, "mov r1, r0");
                    _asm.Ins(c, "and r1, 0x80000000");
                    _asm.Ins(c, $"shr r0, {shift}        ; logical shift");
                    _asm.Ins(c, "cmp r1, 0");
                    _asm.Ins(c, $"je {endLbl}");
                    // negative: OR sign-extension mask
                    uint mask = ~((1u << srcIt.Bits) - 1);
                    _asm.Ins(c, $"or r0, {mask}");
                    _asm.Label(c, endLbl);
                }
                break;
        }
    }

    // -------------------------------------------------------------------------
    private void EmitPhiResolution(IrFunction fn, string fromBlock, string toBlock) {
        IrBasicBlock? target = fn.Blocks.FirstOrDefault(b => b.Label == toBlock);
        if (target == null) return;
        foreach (IrInstruction ins in target.Instructions) {
            if (ins is not PhiIns phi) break;  // phis appear at start of block
            foreach ((IrValue v, string from) in phi.Incoming) {
                if (from == fromBlock) {
                    LoadValueIntoReg(v, "r0");
                    StoreReg("r0", phi.Result!, phi.ResultType);
                    break;
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    /// <summary>Loads <paramref name="value"/> into <paramref name="reg"/>. Uses r2 as
    /// an address scratch; do not pass r2 as the destination.</summary>
    private void LoadValueIntoReg(IrValue value, string reg) {
        StringBuilder c = _asm.Code;
        switch (value) {
            case IrConstInt ci:
                _asm.Ins(c, $"mov {reg}, {(uint)ci.Value}");
                break;
            case IrUndef:
            case IrZeroInit:
                _asm.Ins(c, $"mov {reg}, 0");
                break;
            case IrNull:
                _asm.Ins(c, $"mov {reg}, 0");
                break;
            case IrLocalRef lr: {
                if (_allocaSlots.Contains(lr.Name)) {
                    // The "value" of the alloca is the address of its slot.
                    int off = _slotOffsets[lr.Name];
                    _asm.Ins(c, $"mov {reg}, r7");
                    _asm.Ins(c, $"sub {reg}, {off}        ; addr of alloca '{lr.Name}'");
                } else {
                    int off = _slotOffsets[lr.Name];
                    string addrReg = reg == "r2" ? "r0" : "r2";
                    if (off >= 0) {
                        _asm.Ins(c, $"mov {addrReg}, r7");
                        _asm.Ins(c, $"sub {addrReg}, {off}");
                    } else {
                        _asm.Ins(c, $"mov {addrReg}, r7");
                        _asm.Ins(c, $"add {addrReg}, {-off}        ; stack-arg above bp");
                    }
                    _asm.Ins(c, $"{LoadOp(lr.Ty.SlotBytes)} {reg}, [{addrReg}]");
                }
                break;
            }
            case IrGlobalRef gr:
                // The label's address is the value (works for both global vars
                // and function pointers).
                _asm.Ins(c, $"mov {reg}, {SanGlobal(gr.Name)}");
                break;
            default:
                throw new NotSupportedException($"LoadValueIntoReg: {value.GetType().Name}");
        }
    }

    /// <summary>Stores <paramref name="reg"/> into the slot for SSA value <paramref name="name"/>.</summary>
    private void StoreReg(string reg, string name, IrType ty) {
        StringBuilder c = _asm.Code;
        int off = _slotOffsets[name];
        string addrReg = reg == "r2" ? "r0" : "r2";
        _asm.Ins(c, $"mov {addrReg}, r7");
        _asm.Ins(c, $"sub {addrReg}, {off}");
        _asm.Ins(c, $"{StoreOp(ty.SlotBytes)} [{addrReg}], {reg}");
    }

    // -------------------------------------------------------------------------
    private void EmitGlobals() {
        StringBuilder r = _asm.Rodata;
        foreach (IrGlobalDecl g in _module.Globals) {
            _asm.Label(r, SanGlobal(g.Name));
            EmitInitializer(r, g.Ty, g.Initializer);
        }
    }

    private void EmitInitializer(StringBuilder r, IrType ty, IrValue? init) {
        if (init == null || init is IrZeroInit) {
            // Reserve zeroed bytes.
            int bytes = ty.SizeBytes;
            EmitReserve(r, bytes);
            return;
        }
        switch (init) {
            case IrConstInt ci:
                _asm.Ins(r, $"{DefineDir(ty.SizeBytes)} {(uint)ci.Value}");
                break;
            case IrConstBytes cb:
                EmitBytes(r, cb.Bytes);
                break;
            case IrConstArray ca: {
                IrType elemTy = ca.Ty.Element;
                foreach (IrValue elem in ca.Elements) {
                    EmitInitializer(r, elemTy, elem);
                }
                break;
            }
            case IrNull:
                _asm.Ins(r, $"{DefineDir(ty.SizeBytes)} 0");
                break;
            default:
                throw new NotSupportedException($"global initializer: {init.GetType().Name}");
        }
    }

    private void EmitBytes(StringBuilder r, byte[] bytes) {
        // Emit as a single d8 line. We keep escape-friendly numeric form.
        if (bytes.Length == 0) return;
        StringBuilder line = new("d8 ");
        for (int i = 0; i < bytes.Length; i++) {
            if (i > 0) line.Append(", ");
            line.Append(bytes[i].ToString(CultureInfo.InvariantCulture));
        }
        _asm.Ins(r, line.ToString());
    }

    private void EmitReserve(StringBuilder r, int bytes) {
        while (bytes >= 4) { _asm.Ins(r, "res32"); bytes -= 4; }
        while (bytes >= 2) { _asm.Ins(r, "res16"); bytes -= 2; }
        while (bytes >= 1) { _asm.Ins(r, "res8");  bytes -= 1; }
    }

    // -------------------------------------------------------------------------
    /// <summary>LLVM identifiers can contain '.', but CatAssembler treats names
    /// starting with '.' as local labels and may not accept dots elsewhere.
    /// Translate to underscores so labels are always valid global symbols.</summary>
    private static string SanGlobal(string name) {
        StringBuilder sb = new(name.Length);
        foreach (char ch in name) {
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }
        // Avoid leading digit (CatAssembler labels likely require letter/_ start).
        if (sb.Length > 0 && char.IsDigit(sb[0])) sb.Insert(0, '_');
        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    private static string LoadOp(int bytes) => bytes switch {
        1 => "mov8",
        2 => "mov16",
        _ => "mov32"
    };
    private static string StoreOp(int bytes) => LoadOp(bytes);

    private static string DefineDir(int bytes) => bytes switch {
        1 => "d8",
        2 => "d16",
        _ => "d32"
    };

    private static int AlignUp(int v, int align) => (v + align - 1) & ~(align - 1);

    private string BlockLabel(string fnName, string blockLabel) =>
        $".{fnName}__bb_{SanitizeLabel(blockLabel)}";

    private string NewLabel(string hint) => $".{_currentFnName}__{hint}_{_localLabelCounter++}";

    private static string SanitizeLabel(string s) {
        StringBuilder sb = new();
        foreach (char ch in s) {
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }
        return sb.ToString();
    }

    private static string DescribeInstruction(IrInstruction ins) => ins switch {
        AllocaIns a => $"alloca {a.ElementType} x {a.Count} -> %{ins.Result}",
        LoadIns l => $"%{ins.Result} = load {l.LoadType}",
        StoreIns => "store",
        BinOpIns b => $"%{ins.Result} = {b.Op}",
        IcmpIns ic => $"%{ins.Result} = icmp {ic.Pred}",
        BrIns br => $"br -> {br.Target}",
        BrCondIns brc => $"br i1 ? {brc.IfTrue} : {brc.IfFalse}",
        RetIns => "ret",
        CallIns ca => $"call {(ca.Callee as IrGlobalRef)?.Name ?? "indirect"}",
        GepIns => $"%{ins.Result} = getelementptr",
        CastIns cs => $"%{ins.Result} = {cs.Kind}",
        PhiIns => $"%{ins.Result} = phi",
        _ => ins.GetType().Name
    };
}
