#!/usr/bin/env python3
"""
catcc.py — tiny toy C-to-Cat-ASM compiler (restricted C subset) with inline asm.

Inline asm:
  asm("MOV r1, r1\nINT 0x80\n");
or:
  __asm__("INT 0x80");

Requires:
  pip install pycparser
"""

import argparse
from dataclasses import dataclass
from typing import Dict, List, Optional, Tuple

from pycparser import c_ast, parse_file


# ---------------- CONFIG ----------------
USE_COMMA_SEPARATED_ARGS = False  # False => space-separated
ARG_SEP = ", " if USE_COMMA_SEPARATED_ARGS else " "
# ----------------------------------------


def mem(x: str) -> str:
    """Memory operand using Cat ASM '@' syntax."""
    return f"@{x}"


@dataclass
class VarInfo:
    offset: int  # bytes from FP (r7), 32-bit slots


class CodeGen:
    def __init__(self):
        self.out: List[str] = []
        self.label_id = 0
        self.globals_seen = set()

        # function-scoped
        self.func_name: Optional[str] = None
        self.locals: Dict[str, VarInfo] = {}
        self.params: Dict[str, VarInfo] = {}
        self.frame_size = 0

        # string pool
        self.strings: Dict[str, str] = {}
        self.string_list: List[Tuple[str, str]] = []  # (label, escaped_text_for_DSTR)

    def emit(self, s: str = ""):
        self.out.append(s)

    def ins(self, op: str, *args: str, indent: bool = True):
        if args:
            line = f"{op} {ARG_SEP.join(args)}"
        else:
            line = op
        self.emit(("    " + line) if indent else line)

    def unique(self, prefix: str) -> str:
        self.label_id += 1
        return f"{prefix}_{self.label_id}"

    # ---------- Locals discovery ----------
    def collect_locals(self, node: c_ast.Node) -> List[str]:
        """
        Collect local variable Decl names, including pointer types (PtrDecl).
        (Still very limited: no structs/arrays handling beyond naming.)
        """
        names: List[str] = []

        def is_scalar_decl(t: c_ast.Node) -> bool:
            # int x;
            if isinstance(t, c_ast.TypeDecl):
                return True
            # char *p;  (PtrDecl(TypeDecl(...)))
            if isinstance(t, c_ast.PtrDecl) and isinstance(t.type, c_ast.TypeDecl):
                return True
            return False

        class V(c_ast.NodeVisitor):
            def visit_Decl(self, n: c_ast.Decl):
                if n.name and is_scalar_decl(n.type):
                    names.append(n.name)

        V().visit(node)
        return names

    def alloc_frame(self, func: c_ast.FuncDef):
        self.locals.clear()
        self.params.clear()

        param_names: List[str] = []
        if isinstance(func.decl.type, c_ast.FuncDecl) and func.decl.type.args:
            for p in func.decl.type.args.params:
                if isinstance(p, c_ast.Decl) and p.name:
                    param_names.append(p.name)

        local_names = self.collect_locals(func.body)
        local_names = [n for n in local_names if n not in param_names]

        offset = 0
        for n in param_names:
            self.params[n] = VarInfo(offset=offset)
            offset += 4
        for n in local_names:
            self.locals[n] = VarInfo(offset=offset)
            offset += 4

        self.frame_size = offset

    def var_offset(self, name: str) -> int:
        if name in self.locals:
            return self.locals[name].offset
        if name in self.params:
            return self.params[name].offset
        raise KeyError(f"unknown variable: {name}")

    # ---------- Inline asm ----------
    def emit_inline_asm(self, asm_text: str):
        """
        Emit asm_text verbatim as Cat ASM. Supports embedded newlines.
        """
        # pycparser usually gives unescaped text; but handle literal "\n" too:
        asm_text = asm_text.replace("\\n", "\n")
        for line in asm_text.splitlines():
            line = line.rstrip()
            if not line:
                continue
            self.emit("    " + line)

    def try_inline_asm_stmt(self, call: c_ast.FuncCall) -> bool:
        """
        Detect asm("...") / __asm__("...") used as a statement and emit it.
        """
        if not isinstance(call.name, c_ast.ID):
            return False
        if call.name.name not in ("asm", "__asm__", "__asm"):
            return False
        if not call.args or not isinstance(call.args, c_ast.ExprList) or len(call.args.exprs) < 1:
            raise NotImplementedError("asm() requires a string literal argument")
        arg0 = call.args.exprs[0]
        if not (isinstance(arg0, c_ast.Constant) and arg0.type == "string"):
            raise NotImplementedError("asm() currently only supports a string literal argument")
        self.emit("    ; inline asm begin")
        # strip quotes from string literal
        finalinline = arg0.value[1:-1]
        self.emit_inline_asm(finalinline)
        self.emit("    ; inline asm end")
        return True

    # ---------- Prologue/epilogue ----------
    def prologue(self):
        self.emit(f"{self.func_name}:")
        self.emit("    ; prologue")
        self.ins("PUSH", "r4")
        self.ins("PUSH", "r5")
        self.ins("PUSH", "r6")
        self.ins("PUSH", "r7")
        if self.frame_size:
            self.ins("SUB", "sp", str(self.frame_size))
        self.ins("MOV", "r7", "sp")  # fp = sp

        # spill up to 3 params from r1,r2,r3 into stack frame
        param_names = list(self.params.keys())
        for i, reg in enumerate(["r1", "r2", "r3"]):
            if i < len(param_names):
                name = param_names[i]
                off = self.params[name].offset
                self.emit(f"    ; param {name} -> [fp+{off}]")
                self.ins("MOV", "r4", "r7")
                if off:
                    self.ins("ADD", "r4", str(off))
                self.ins("MOV", mem("r4"), reg)

        self.emit("")

    def epilogue(self):
        self.emit("    ; epilogue")
        self.ins("MOV", "sp", "r7")
        if self.frame_size:
            self.ins("ADD", "sp", str(self.frame_size))
        self.ins("POP", "r7")
        self.ins("POP", "r6")
        self.ins("POP", "r5")
        self.ins("POP", "r4")
        self.ins("RET")
        self.emit("")

    def load_var_to_r0(self, name: str):
        off = self.var_offset(name)
        self.emit(f"    ; load {name}")
        self.ins("MOV", "r4", "r7")
        if off:
            self.ins("ADD", "r4", str(off))
        self.ins("MOV", "r0", mem("r4"))

    def store_r0_to_var(self, name: str):
        off = self.var_offset(name)
        self.emit(f"    ; store {name}")
        self.ins("MOV", "r4", "r7")
        if off:
            self.ins("ADD", "r4", str(off))
        self.ins("MOV", mem("r4"), "r0")

    def c_string_label(self, s: str) -> str:
        if s in self.strings:
            return self.strings[s]
        lab = f".str_{len(self.strings)}"
        self.strings[s] = lab

        esc = (
            s.replace("\\", "\\\\")
             .replace("\n", "\\n")
             .replace("\t", "\\t")
             .replace("\r", "\\r")
             .replace("\0", "\\0")
        )
        self.string_list.append((lab, esc + "\\0"))
        return lab

    # ---------- Expressions (result in r0) ----------
    def gen_expr(self, e: c_ast.Node):
        if isinstance(e, c_ast.Constant):
            if e.type == "int":
                self.ins("MOV", "r0", e.value); return
            if e.type == "char":
                self.ins("MOV", "r0", e.value); return
            if e.type == "string":
                lab = self.c_string_label(e.value)
                self.ins("MOV", "r0", lab); return
            raise NotImplementedError(f"constant type: {e.type}")

        if isinstance(e, c_ast.ID):
            self.load_var_to_r0(e.name); return

        if isinstance(e, c_ast.UnaryOp):
            if e.op == "-":
                self.gen_expr(e.expr)
                self.ins("MOV", "r1", "0")
                self.ins("SUB", "r1", "r0")
                self.ins("MOV", "r0", "r1")
                return
            if e.op == "!":
                self.gen_expr(e.expr)
                t = self.unique(".true")
                done = self.unique(".done")
                self.ins("CMP", "r0", "0")
                self.ins("JZ", "0xFF", t)
                self.ins("MOV", "r0", "0")
                self.ins("JMP", "0xFF", done)
                self.emit(f"{t}:")
                self.ins("MOV", "r0", "1")
                self.emit(f"{done}:")
                return
            raise NotImplementedError(f"unary op: {e.op}")

        if isinstance(e, c_ast.Assignment):
            if e.op != "=":
                raise NotImplementedError(f"assignment op: {e.op}")
            if not isinstance(e.lvalue, c_ast.ID):
                raise NotImplementedError("only simple lvalue IDs supported")
            self.gen_expr(e.rvalue)
            self.store_r0_to_var(e.lvalue.name)
            return

        if isinstance(e, c_ast.BinaryOp):
            op = e.op
            self.gen_expr(e.left)
            self.ins("PUSH", "r0")
            self.gen_expr(e.right)
            self.ins("POP", "r1")

            if op == "+":
                self.ins("ADD", "r1", "r0"); self.ins("MOV", "r0", "r1"); return
            if op == "-":
                self.ins("SUB", "r1", "r0"); self.ins("MOV", "r0", "r1"); return
            if op == "*":
                self.ins("UMUL", "r1", "r0"); self.ins("MOV", "r0", "r1"); return
            if op in ["/", "%"]:
                self.ins("UDIV", "r1", "r0")
                if op == "/":
                    self.ins("MOV", "r0", "r1")
                else:
                    self.emit("    ; remainder assumed in second register per Cat spec (left as r0)")
                return

            if op in ["==", "!=", "<", "<=", ">", ">="]:
                true_lab = self.unique(".true")
                done_lab = self.unique(".done")
                self.ins("CMP", "r1", "r0")
                j = {
                    "==": "JZ",
                    "!=": "JNZ",
                    "<":  "JUL",
                    "<=": "JULE",
                    ">":  "JUG",
                    ">=": "JUGE",
                }[op]
                self.ins(j, "0xFF", true_lab)
                self.ins("MOV", "r0", "0")
                self.ins("JMP", "0xFF", done_lab)
                self.emit(f"{true_lab}:")
                self.ins("MOV", "r0", "1")
                self.emit(f"{done_lab}:")
                return

            raise NotImplementedError(f"binary op: {op}")

        if isinstance(e, c_ast.FuncCall):
            # NOTE: asm("...") is only supported as a *statement* (handled in gen_stmt)
            if not isinstance(e.name, c_ast.ID):
                raise NotImplementedError("only direct function calls supported")

            args = []
            if e.args and isinstance(e.args, c_ast.ExprList):
                args = e.args.exprs
            if len(args) > 3:
                raise NotImplementedError("only up to 3 call args supported")

            for i, arg in enumerate(args):
                self.gen_expr(arg)
                self.ins("MOV", f"r{i+1}", "r0")

            self.ins("CALL", "0xFF", e.name.name)
            return

        raise NotImplementedError(f"expr node: {type(e).__name__}")

    # ---------- Statements ----------
    def gen_stmt(self, s: c_ast.Node):
        if s is None:
            return

        if isinstance(s, c_ast.Compound):
            for item in (s.block_items or []):
                self.gen_stmt(item)
            return

        if isinstance(s, c_ast.Decl):
            if s.init is not None:
                self.gen_expr(s.init)
                self.store_r0_to_var(s.name)
            return

        if isinstance(s, c_ast.Return):
            if s.expr is not None:
                self.gen_expr(s.expr)
            self.ins("JMP", "0xFF", f".{self.func_name}_ret")
            return

        if isinstance(s, c_ast.If):
            else_lab = self.unique(".else")
            done_lab = self.unique(".ifend")
            self.gen_expr(s.cond)
            self.ins("CMP", "r0", "0")
            self.ins("JZ", "0xFF", else_lab)
            self.gen_stmt(s.iftrue)
            self.ins("JMP", "0xFF", done_lab)
            self.emit(f"{else_lab}:")
            if s.iffalse is not None:
                self.gen_stmt(s.iffalse)
            self.emit(f"{done_lab}:")
            return

        if isinstance(s, c_ast.While):
            start = self.unique(".while")
            done = self.unique(".wend")
            self.emit(f"{start}:")
            self.gen_expr(s.cond)
            self.ins("CMP", "r0", "0")
            self.ins("JZ", "0xFF", done)
            self.gen_stmt(s.stmt)
            self.ins("JMP", "0xFF", start)
            self.emit(f"{done}:")
            return

        if isinstance(s, c_ast.FuncCall):
            # inline asm statement?
            if self.try_inline_asm_stmt(s):
                return
            self.gen_expr(s)
            return

        if isinstance(s, (c_ast.Assignment, c_ast.BinaryOp, c_ast.UnaryOp, c_ast.ID, c_ast.Constant)):
            self.gen_expr(s)
            return

        raise NotImplementedError(f"stmt node: {type(s).__name__}")

    # ---------- Functions / TU ----------
    def gen_func(self, f: c_ast.FuncDef):
        self.func_name = f.decl.name
        if self.func_name in self.globals_seen:
            raise RuntimeError(f"duplicate global label/function: {self.func_name}")
        self.globals_seen.add(self.func_name)

        self.alloc_frame(f)
        self.prologue()
        self.gen_stmt(f.body)
        self.emit(f".{self.func_name}_ret:")
        self.epilogue()

    def gen(self, ast: c_ast.FileAST) -> str:
        self.emit("; generated by catcc.py")
        self.emit("; ----------------------------------------")
        self.emit("; entrypoint (Cat starts executing at top of file)")
        self.ins("JMP", "0xFF", "main", indent=False)
        self.emit("")

        for ext in ast.ext:
            if isinstance(ext, c_ast.FuncDef):
                self.gen_func(ext)
            elif isinstance(ext, c_ast.Decl):
                if isinstance(ext.type, c_ast.FuncDecl):
                    continue  # prototype
                raise NotImplementedError("global variables not supported")
            else:
                raise NotImplementedError(f"top-level node: {type(ext).__name__}")

        if self.string_list:
            self.emit("; ----------------------------------------")
            self.emit("; string pool")
            for lab, esc in self.string_list:
                self.emit(f"{lab}:")
                self.emit(f"    DSTR {esc}")
                self.emit("")

        return "\n".join(self.out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("input", help="input C file")
    ap.add_argument("-o", "--out", default="-", help="output asm file ('-' for stdout)")
    ap.add_argument("--cpp", default="cpp", help="C preprocessor command (default: cpp)")
    args = ap.parse_args()

    ast = parse_file(
        args.input,
        use_cpp=True,
        cpp_path=args.cpp,
        cpp_args=["-E", "-P"],
    )

    asm = CodeGen().gen(ast)

    if args.out == "-" or args.out == "":
        print(asm)
    else:
        with open(args.out, "w", encoding="utf-8") as f:
            f.write(asm)


if __name__ == "__main__":
    main()