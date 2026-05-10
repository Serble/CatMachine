using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CatLLVM.IR;

// =============================================================================
// LLVM IR Text Parser (subset)
// =============================================================================
// Parses a useful subset of `.ll` files produced by clang/rustc. The goal is
// to be tolerant of attribute / metadata noise we don't need, while precisely
// understanding the instructions and types we care about.
//
// What we parse:
//   - module-level "target", "source_filename", "attributes #N = {...}",
//     metadata !N = ..., declare/define, @global = ...
//   - functions: signatures with simple integer/ptr parameters; bodies broken
//     into basic blocks separated by "label:" lines
//   - instructions: see IRTypes.cs
// What we tolerate (silently ignore):
//   - parameter & instruction attributes (nsw, nuw, nounwind, !dbg ..., align N,
//     inbounds, dso_local, signext, zeroext, ...)
//   - metadata operands and !N references
//   - linkage / visibility / dllstorage modifiers on globals & functions

public static class IRParser {

    public static IrModule Parse(string source) {
        IrModule module = new();
        // Split into logical lines, but we need to consume function bodies as
        // multi-line blocks delimited by braces. We work line-by-line with a
        // small index.
        string[] rawLines = source.Replace("\r\n", "\n").Split('\n');
        int i = 0;
        while (i < rawLines.Length) {
            string line = rawLines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith(';')) { i++; continue; }
            if (line.StartsWith("target ") || line.StartsWith("source_filename") ||
                line.StartsWith("module ") || line.StartsWith("attributes ") ||
                line.StartsWith("!") || line.StartsWith("$") || line.StartsWith("metadata") ||
                line.StartsWith("@llvm.")) {
                i++; continue;
            }

            if (line.StartsWith("@")) {
                module.Globals.Add(ParseGlobal(line));
                i++;
                continue;
            }

            if (line.StartsWith("declare")) {
                module.Functions.Add(ParseDeclare(line));
                i++;
                continue;
            }

            if (line.StartsWith("define")) {
                IrFunction fn = ParseDefine(rawLines, ref i);
                module.Functions.Add(fn);
                continue;
            }

            // Anything else: ignore (could be unknown directive).
            i++;
        }
        return module;
    }

    // -------------------------------------------------------------------------
    // Global parsing
    // -------------------------------------------------------------------------
    // @name = [linkage...] [unnamed_addr] [constant|global] <type> [initializer], [align N]
    //
    // Initializers we support:
    //   - integer literal       :  42
    //   - c"..."                :  string constant
    //   - [N x T] [ a, b, c ]   :  array literal of integers
    //   - zeroinitializer
    //   - null
    private static IrGlobalDecl ParseGlobal(string line) {
        // Name
        int eq = line.IndexOf('=');
        if (eq < 0) throw new ParseException($"global without '=': {line}");
        string name = line[1..eq].Trim();   // strip leading @
        string rhs = line[(eq + 1)..].Trim();

        // Strip linkage / visibility / unnamed_addr / etc.
        string[] modifiers = [
            "private", "internal", "available_externally", "linkonce", "weak",
            "common", "appending", "extern_weak", "linkonce_odr", "weak_odr",
            "external", "default", "hidden", "protected", "dso_local", "dso_preemptable",
            "unnamed_addr", "local_unnamed_addr", "thread_local", "addrspace(0)",
            "externally_initialized"
        ];
        rhs = StripLeadingTokens(rhs, modifiers);

        bool constant;
        if (rhs.StartsWith("constant ")) { constant = true; rhs = rhs[9..].TrimStart(); }
        else if (rhs.StartsWith("global ")) { constant = false; rhs = rhs[7..].TrimStart(); }
        else { constant = false; /* tolerate older syntax */ }

        // Now we have <type> <initializer-or-end>
        IrType ty = ParseType(ref rhs);
        rhs = rhs.TrimStart();

        IrValue? init = null;
        if (rhs.Length > 0 && !rhs.StartsWith(',')) {
            init = ParseConstant(ty, ref rhs);
        }
        return new IrGlobalDecl(name, ty, init, constant);
    }

    // -------------------------------------------------------------------------
    // Function declarations
    // -------------------------------------------------------------------------
    private static IrFunction ParseDeclare(string line) {
        // declare [attrs] <ret> @name(<arg>, ...) [#N] [attrs]
        string body = line["declare".Length..].TrimStart();
        body = StripLinkage(body);
        IrType ret = ParseType(ref body);
        body = body.TrimStart();
        if (!body.StartsWith('@')) throw new ParseException($"declare: expected @name in: {line}");
        int paren = body.IndexOf('(');
        string name = body[1..paren].Trim();
        IrFunction fn = new(name, ret);
        // We don't actually need parameter names for declarations; types only.
        // But for completeness, parse them.
        int closeParen = FindMatching(body, paren, '(', ')');
        string paramList = body.Substring(paren + 1, closeParen - paren - 1).Trim();
        ParseParamList(fn, paramList);
        return fn;
    }

    private static IrFunction ParseDefine(string[] lines, ref int idx) {
        // collect the signature - it might span the line with a trailing { or be on this line
        StringBuilder sig = new();
        while (idx < lines.Length) {
            string l = lines[idx].TrimEnd();
            sig.Append(l).Append(' ');
            idx++;
            if (l.EndsWith('{')) break;
        }
        string head = sig.ToString().Trim();
        // strip trailing {
        int brace = head.LastIndexOf('{');
        if (brace >= 0) head = head[..brace].Trim();
        // strip trailing function attributes/section/personality - everything after the closing )
        // we keep just up to and including the closing paren
        int openParen = head.IndexOf('(');
        int closeParen = FindMatching(head, openParen, '(', ')');
        string preParen = head[..openParen].TrimEnd();
        string paramList = head.Substring(openParen + 1, closeParen - openParen - 1).Trim();

        // preParen: define [attrs] <ret> @name
        string body = preParen["define".Length..].TrimStart();
        body = StripLinkage(body);
        IrType ret = ParseType(ref body);
        body = body.TrimStart();
        if (!body.StartsWith('@')) throw new ParseException($"define: expected @name in: {head}");
        string name = body[1..].Trim();

        IrFunction fn = new(name, ret);
        ParseParamList(fn, paramList);

        // Now parse instructions until the matching '}' line.
        IrBasicBlock current = new("entry");
        fn.Blocks.Add(current);
        bool entrySeen = false;

        while (idx < lines.Length) {
            string raw = lines[idx];
            string t = raw.Trim();
            idx++;
            if (string.IsNullOrEmpty(t) || t.StartsWith(';')) continue;
            if (t == "}") return fn;

            // strip trailing "; comment" and trailing "!dbg ..." metadata
            t = StripTrailingMetadata(t);
            if (string.IsNullOrEmpty(t)) continue;

            // is it a label? form: "name:" possibly followed by ; comment "preds = ..."
            if (Regex.IsMatch(t, @"^[A-Za-z0-9_.""\-]+:\s*$") ||
                Regex.IsMatch(t, @"^[A-Za-z0-9_.""\-]+:\s")) {
                int colon = t.IndexOf(':');
                string label = t[..colon].Trim().Trim('"');
                if (!entrySeen && current.Instructions.Count == 0 && current.Label == "entry") {
                    // Replace synthetic entry block label with the real first label.
                    fn.Blocks.Clear();
                    current = new IrBasicBlock(label);
                    fn.Blocks.Add(current);
                } else {
                    current = new IrBasicBlock(label);
                    fn.Blocks.Add(current);
                }
                entrySeen = true;
                continue;
            }
            entrySeen = true;

            IrInstruction ins = ParseInstruction(t);
            current.Instructions.Add(ins);
        }
        throw new ParseException($"unterminated function {name}");
    }

    /// <summary>Param attributes that may appear before OR after the type
    /// in a function-param/call-arg position. Includes simple keywords and
    /// parameterized forms like `align N`, `dereferenceable(N)`, `byval(T)`.</summary>
    private static readonly string[] ParamAttrKeywords = [
        "zeroext", "signext", "noundef", "nonnull", "readonly", "readnone",
        "writeonly", "nocapture", "sret", "inreg", "inalloca",
        "returned", "swiftself", "swifterror", "immarg", "noalias",
        "nofree", "nosync", "willreturn", "writable", "captures",
    ];

    /// <summary>Strip param attributes (both bare keywords and keyword(args)
    /// forms like `align(8)`, `dereferenceable(16)`, `byval(%struct.Foo)`)
    /// from the front of <paramref name="s"/>.</summary>
    private static string StripParamAttrs(string s) {
        bool changed = true;
        while (changed) {
            changed = false;
            string before = s;
            s = StripLeadingTokens(s, ParamAttrKeywords);
            if (!ReferenceEquals(s, before) && s != before) changed = true;
            // Parameterized forms: `align 8`, `align(8)`, `dereferenceable(16)`,
            // `dereferenceable_or_null(16)`, `byval(<ty>)`, `byref(<ty>)`,
            // `preallocated(<ty>)`, `sret(<ty>)`, `elementtype(<ty>)`,
            // `inalloca(<ty>)`, `captures(...)`, `range(...)`.
            Match m = Regex.Match(s, @"^(align|dereferenceable|dereferenceable_or_null|byval|byref|preallocated|sret|elementtype|inalloca|captures|range|alignstack|allocsize|allocalign)\b");
            if (m.Success) {
                int after = m.Index + m.Length;
                string tail = s[after..].TrimStart();
                if (tail.StartsWith('(')) {
                    int close = FindMatching(tail, 0, '(', ')');
                    if (close > 0) {
                        s = tail[(close + 1)..].TrimStart();
                        changed = true;
                        continue;
                    }
                } else {
                    // `align 8` form (number follows)
                    Match num = Regex.Match(tail, @"^\d+");
                    if (num.Success) {
                        s = tail[num.Length..].TrimStart();
                        changed = true;
                        continue;
                    }
                }
            }
        }
        return s;
    }

    private static void ParseParamList(IrFunction fn, string paramList) {
        if (string.IsNullOrWhiteSpace(paramList)) return;
        foreach (string param in SplitTopLevel(paramList, ',')) {
            string p = param.Trim();
            if (p == "..." || string.IsNullOrEmpty(p)) continue;
            p = StripParamAttrs(p);
            IrType ty = ParseType(ref p);
            p = p.TrimStart();
            p = StripParamAttrs(p);   // attrs may also appear after the type
            // optional "%name"
            string name;
            if (p.StartsWith('%')) {
                int spc = p.IndexOf(' ');
                name = spc < 0 ? p[1..] : p[1..spc];
            } else {
                name = $"_arg{fn.Params.Count}";
            }
            fn.Params.Add(new IrParam(name, ty));
        }
    }

    // -------------------------------------------------------------------------
    // Instruction parsing
    // -------------------------------------------------------------------------
    private static IrInstruction ParseInstruction(string line) {
        // Split off the "%name = " prefix if present.
        string? result = null;
        string body = line;
        int eq = line.IndexOf('=');
        if (line.StartsWith('%') && eq > 0 && eq < line.IndexOf(' ') + 8) {
            // form: %x = <op> ...
            // (but "= " could appear inside icmp predicate names? no, all preds
            //  are alphabetic). safe.
            result = line[1..eq].Trim();
            body = line[(eq + 1)..].TrimStart();
        }

        // Pop the opcode (first whitespace-separated token).
        int sp = body.IndexOf(' ');
        string op = sp < 0 ? body : body[..sp];
        string rest = sp < 0 ? "" : body[(sp + 1)..].TrimStart();
        // strip non-semantic flags after op (e.g. "add nsw i32 ...", "call fastcc ...")
        rest = StripLeadingTokens(rest, [
            "nsw", "nuw", "exact", "fast", "ninf", "nnan", "nsz", "arcp", "afn", "reassoc",
            "fastcc", "ccc", "tail", "musttail", "notail", "inbounds", "volatile",
            "atomic", "syncscope", "weak", "release", "acquire", "monotonic", "unordered",
            "seq_cst", "acq_rel", "preallocated"
        ]);

        switch (op) {
            case "alloca":      return ParseAlloca(result, rest);
            case "load":        return ParseLoad(result, rest);
            case "store":       return ParseStore(rest);
            case "ret":         return ParseRet(rest);
            case "br":          return ParseBr(rest);
            case "icmp":        return ParseIcmp(result, rest);
            case "call":        return ParseCall(result, rest);
            case "getelementptr": return ParseGep(result, rest);
            case "phi":         return ParsePhi(result, rest);
            case "zext":        return ParseCast(result, rest, CastKind.ZExt);
            case "sext":        return ParseCast(result, rest, CastKind.SExt);
            case "trunc":       return ParseCast(result, rest, CastKind.Trunc);
            case "bitcast":     return ParseCast(result, rest, CastKind.BitCast);
            case "ptrtoint":    return ParseCast(result, rest, CastKind.PtrToInt);
            case "inttoptr":    return ParseCast(result, rest, CastKind.IntToPtr);
            case "unreachable": return new RetIns(null);  // safe lowering
        }

        // BinOps
        if (Enum.TryParse<BinOp>(NormalizeBinOpName(op), ignoreCase: true, out BinOp bop)) {
            return ParseBinOp(result, rest, bop);
        }

        throw new ParseException($"unsupported instruction: {op} (line: {line})");
    }

    private static string NormalizeBinOpName(string op) => op switch {
        "add" => "Add", "sub" => "Sub", "mul" => "Mul",
        "sdiv" => "SDiv", "udiv" => "UDiv",
        "srem" => "SRem", "urem" => "URem",
        "and" => "And", "or" => "Or", "xor" => "Xor",
        "shl" => "Shl", "lshr" => "LShr", "ashr" => "AShr",
        _ => op
    };

    private static IrInstruction ParseAlloca(string? result, string rest) {
        // alloca <ty> [, <ty> <count>] [, align N]
        IrType ty = ParseType(ref rest);
        int count = 1;
        rest = rest.TrimStart();
        // optional ", <ty> <count>"
        foreach (string part in SplitTopLevel(rest.TrimStart(','), ',')) {
            string p = part.Trim();
            if (p.StartsWith("align")) continue;
            // form: <ty> <const>
            if (p.Length == 0) continue;
            string tmp = p;
            ParseType(ref tmp);
            tmp = tmp.TrimStart();
            if (int.TryParse(tmp, NumberStyles.Integer, CultureInfo.InvariantCulture, out int c)) {
                count = c;
            }
        }
        return new AllocaIns(ty, count) { Result = result, ResultType = PtrType.Instance };
    }

    private static IrInstruction ParseLoad(string? result, string rest) {
        // load <ty>, ptr <ptr>[, align N]
        // (older form: load <ty>* <ptr>)
        IrType ty = ParseType(ref rest);
        rest = rest.TrimStart().TrimStart(',').TrimStart();
        // parse pointer operand: "<ptrTy> <value>"
        IrType _ = ParseType(ref rest);
        rest = rest.TrimStart();
        IrValue ptr = ParseValueAfterType(_, ref rest);
        return new LoadIns(ty, ptr) { Result = result, ResultType = ty };
    }

    private static IrInstruction ParseStore(string rest) {
        // store <ty> <val>, ptr <ptr>[, align N]
        IrType vty = ParseType(ref rest);
        rest = rest.TrimStart();
        IrValue val = ParseValueAfterType(vty, ref rest);
        rest = rest.TrimStart().TrimStart(',').TrimStart();
        IrType pty = ParseType(ref rest);
        rest = rest.TrimStart();
        IrValue ptr = ParseValueAfterType(pty, ref rest);
        return new StoreIns(val, ptr);
    }

    private static IrInstruction ParseRet(string rest) {
        rest = rest.Trim();
        if (rest == "void" || rest.StartsWith("void")) return new RetIns(null);
        IrType ty = ParseType(ref rest);
        rest = rest.TrimStart();
        if (rest.Length == 0) return new RetIns(null);
        IrValue v = ParseValueAfterType(ty, ref rest);
        return new RetIns(v);
    }

    private static IrInstruction ParseBr(string rest) {
        // br label %x
        // br i1 %c, label %t, label %f
        rest = rest.TrimStart();
        if (rest.StartsWith("label")) {
            rest = rest["label".Length..].TrimStart();
            return new BrIns(StripLabelToken(rest));
        }
        IrType ty = ParseType(ref rest);
        rest = rest.TrimStart();
        IrValue cond = ParseValueAfterType(ty, ref rest);
        rest = rest.TrimStart().TrimStart(',').TrimStart();
        // expect "label %t, label %f"
        rest = SkipKeyword(rest, "label").TrimStart();
        string t = ReadLabelRef(ref rest);
        rest = rest.TrimStart().TrimStart(',').TrimStart();
        rest = SkipKeyword(rest, "label").TrimStart();
        string f = ReadLabelRef(ref rest);
        return new BrCondIns(cond, t, f);
    }

    private static IrInstruction ParseIcmp(string? result, string rest) {
        // icmp <pred> <ty> <a>, <b>
        rest = rest.TrimStart();
        int sp = rest.IndexOf(' ');
        string predName = rest[..sp];
        rest = rest[(sp + 1)..].TrimStart();
        IcmpPred pred = predName switch {
            "eq" => IcmpPred.Eq, "ne" => IcmpPred.Ne,
            "slt" => IcmpPred.Slt, "sgt" => IcmpPred.Sgt,
            "sle" => IcmpPred.Sle, "sge" => IcmpPred.Sge,
            "ult" => IcmpPred.Ult, "ugt" => IcmpPred.Ugt,
            "ule" => IcmpPred.Ule, "uge" => IcmpPred.Uge,
            _ => throw new ParseException($"unknown icmp predicate {predName}")
        };
        IrType ty = ParseType(ref rest);
        rest = rest.TrimStart();
        IrValue a = ParseValueAfterType(ty, ref rest);
        rest = rest.TrimStart().TrimStart(',').TrimStart();
        IrValue b = ParseValueAfterType(ty, ref rest);
        return new IcmpIns(pred, a, b) { Result = result, ResultType = new IntType(1) };
    }

    private static IrInstruction ParseCall(string? result, string rest) {
        // call [cc] [retattrs] <retty> @name(<args>) [fn-attrs]
        rest = rest.TrimStart();
        // strip optional address space "addrspace(N)"
        rest = Regex.Replace(rest, @"^addrspace\(\d+\)\s+", "");
        IrType retTy = ParseType(ref rest);
        rest = rest.TrimStart();
        // optional fn-pointer-style "( <signature> )* " before callee - tolerate by skipping a balanced "(...)" if present and not the args list.
        // For our subset we expect: <retty> @name(args) or <retty> %fnptr(args)
        IrValue callee;
        int paren = rest.IndexOf('(');
        if (paren < 0) throw new ParseException($"call: missing args: {rest}");
        // The token before '(' is the callee.
        string before = rest[..paren].TrimEnd();
        if (before.StartsWith('@')) {
            callee = new IrGlobalRef(before[1..], PtrType.Instance);
        } else if (before.StartsWith('%')) {
            callee = new IrLocalRef(before[1..], PtrType.Instance);
        } else {
            throw new ParseException($"call: unrecognized callee {before}");
        }
        int closeParen = FindMatching(rest, paren, '(', ')');
        string argsStr = rest.Substring(paren + 1, closeParen - paren - 1).Trim();

        List<IrValue> args = [];
        foreach (string a in SplitTopLevel(argsStr, ',')) {
            string ap = a.Trim();
            if (ap.Length == 0) continue;
            ap = StripParamAttrs(ap);
            IrType aty = ParseType(ref ap);
            ap = ap.TrimStart();
            ap = StripParamAttrs(ap);
            args.Add(ParseValueAfterType(aty, ref ap));
        }

        return new CallIns(callee, args, retTy) {
            Result = result,
            ResultType = retTy
        };
    }

    private static IrInstruction ParseGep(string? result, string rest) {
        // getelementptr [inbounds] <baseTy>, ptr <ptr>, <idxTy> <idx> [, <idxTy> <idx>]*
        IrType baseTy = ParseType(ref rest);
        rest = rest.TrimStart().TrimStart(',').TrimStart();
        IrType pty = ParseType(ref rest);
        rest = rest.TrimStart();
        IrValue ptr = ParseValueAfterType(pty, ref rest);
        List<IrValue> idx = [];
        while (true) {
            rest = rest.TrimStart();
            if (!rest.StartsWith(',')) break;
            rest = rest[1..].TrimStart();
            IrType ity = ParseType(ref rest);
            rest = rest.TrimStart();
            idx.Add(ParseValueAfterType(ity, ref rest));
        }
        return new GepIns(baseTy, ptr, idx) { Result = result, ResultType = PtrType.Instance };
    }

    private static IrInstruction ParsePhi(string? result, string rest) {
        // phi <ty> [ <v1>, %b1 ], [ <v2>, %b2 ], ...
        IrType ty = ParseType(ref rest);
        rest = rest.TrimStart();
        List<(IrValue, string)> incoming = [];
        // entries are bracketed
        int i = 0;
        while (i < rest.Length) {
            while (i < rest.Length && (char.IsWhiteSpace(rest[i]) || rest[i] == ',')) i++;
            if (i >= rest.Length) break;
            if (rest[i] != '[') break;
            int end = FindMatching(rest, i, '[', ']');
            string inside = rest.Substring(i + 1, end - i - 1).Trim();
            string[] parts = SplitTopLevel(inside, ',').ToArray();
            string vstr = parts[0].Trim();
            string bstr = parts[1].Trim();
            IrValue v = ParseValueAfterType(ty, ref vstr);
            string label = StripLabelToken(bstr);
            incoming.Add((v, label));
            i = end + 1;
        }
        return new PhiIns(incoming) { Result = result, ResultType = ty };
    }

    private static IrInstruction ParseCast(string? result, string rest, CastKind kind) {
        // <op> <fromty> <val> to <toty>
        IrType fromTy = ParseType(ref rest);
        rest = rest.TrimStart();
        IrValue v = ParseValueAfterType(fromTy, ref rest);
        rest = rest.TrimStart();
        rest = SkipKeyword(rest, "to").TrimStart();
        IrType toTy = ParseType(ref rest);
        return new CastIns(kind, v, toTy) { Result = result, ResultType = toTy };
    }

    private static IrInstruction ParseBinOp(string? result, string rest, BinOp op) {
        IrType ty = ParseType(ref rest);
        rest = rest.TrimStart();
        IrValue a = ParseValueAfterType(ty, ref rest);
        rest = rest.TrimStart().TrimStart(',').TrimStart();
        IrValue b = ParseValueAfterType(ty, ref rest);
        return new BinOpIns(op, a, b) { Result = result, ResultType = ty };
    }

    // -------------------------------------------------------------------------
    // Type & value parsing
    // -------------------------------------------------------------------------

    /// <summary>Reads a type from the start of <paramref name="text"/>, advancing it past the type.</summary>
    private static IrType ParseType(ref string text) {
        text = text.TrimStart();
        if (text.Length == 0) throw new ParseException("expected a type, got end of input");

        if (text.StartsWith("void")) { text = text["void".Length..]; return SkipPtrStars(VoidType.Instance, ref text); }
        if (text.StartsWith("ptr")) {
            // Could be "ptr" or "ptr addrspace(N)"
            text = text["ptr".Length..].TrimStart();
            if (text.StartsWith("addrspace(")) {
                int e = text.IndexOf(')');
                text = text[(e + 1)..].TrimStart();
            }
            return PtrType.Instance;
        }
        if (text.StartsWith('i')) {
            // i<bits>
            int n = 1;
            while (n < text.Length && char.IsDigit(text[n])) n++;
            if (n > 1) {
                int bits = int.Parse(text[1..n], CultureInfo.InvariantCulture);
                text = text[n..];
                return SkipPtrStars(new IntType(bits), ref text);
            }
        }
        if (text.StartsWith('[')) {
            int end = FindMatching(text, 0, '[', ']');
            string inner = text.Substring(1, end - 1).Trim();
            text = text[(end + 1)..];
            // form: N x T
            int xPos = inner.IndexOf('x');
            int count = int.Parse(inner[..xPos].Trim(), CultureInfo.InvariantCulture);
            string elemStr = inner[(xPos + 1)..].Trim();
            IrType elem = ParseType(ref elemStr);
            return SkipPtrStars(new ArrayType(elem, count), ref text);
        }
        // Tolerate named struct types as opaque ptr-sized? Just bail.
        throw new ParseException($"unsupported type: '{text}'");
    }

    /// <summary>Older syntax ".." appended * for pointer types. We treat any Type* as ptr.</summary>
    private static IrType SkipPtrStars(IrType t, ref string text) {
        text = text.TrimStart();
        bool wasPtr = false;
        while (text.StartsWith('*')) { text = text[1..].TrimStart(); wasPtr = true; }
        return wasPtr ? PtrType.Instance : t;
    }

    /// <summary>Parses a value (after its type was already consumed). Stops at ',' or end.</summary>
    private static IrValue ParseValueAfterType(IrType ty, ref string text) {
        text = text.TrimStart();
        if (text.StartsWith('%')) {
            int e = FindValueEnd(text, 1);
            string name = text[1..e];
            text = text[e..];
            return new IrLocalRef(name, ty);
        }
        if (text.StartsWith('@')) {
            int e = FindValueEnd(text, 1);
            string name = text[1..e];
            text = text[e..];
            return new IrGlobalRef(name, ty);
        }
        if (text.StartsWith("null")) { text = text[4..]; return new IrNull(ty); }
        if (text.StartsWith("undef") || text.StartsWith("poison")) {
            text = text.StartsWith("undef") ? text[5..] : text[6..];
            return new IrUndef(ty);
        }
        if (text.StartsWith("true"))  { text = text[4..]; return new IrConstInt(ty, 1); }
        if (text.StartsWith("false")) { text = text[5..]; return new IrConstInt(ty, 0); }
        if (text.StartsWith("zeroinitializer")) {
            text = text["zeroinitializer".Length..]; return new IrZeroInit(ty);
        }
        if (text.StartsWith('-') || char.IsDigit(text[0])) {
            int e = 0;
            if (text[0] == '-') e++;
            while (e < text.Length && (char.IsDigit(text[e]) || text[e] == 'x' ||
                  (text[e] >= 'a' && text[e] <= 'f') || (text[e] >= 'A' && text[e] <= 'F')))
                e++;
            string num = text[..e];
            text = text[e..];
            long val = num.StartsWith("-0x") ? -long.Parse(num[3..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                     : num.StartsWith("0x") ? long.Parse(num[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                     : long.Parse(num, NumberStyles.Integer, CultureInfo.InvariantCulture);
            return new IrConstInt(ty, val);
        }
        // c"..." string literal (only valid in initializer context)
        if (text.StartsWith("c\"")) {
            int q = 2;
            while (q < text.Length && text[q] != '"') {
                if (text[q] == '\\' && q + 2 < text.Length) q += 3;
                else q++;
            }
            string lit = text.Substring(2, q - 2);
            text = text[(q + 1)..];
            byte[] bytes = ParseCString(lit);
            ArrayType arr = ty as ArrayType ?? new ArrayType(new IntType(8), bytes.Length);
            return new IrConstBytes(arr, bytes);
        }
        // [N x T] [ ... ] array literal, only in initializer context
        if (text.StartsWith('[')) {
            int end = FindMatching(text, 0, '[', ']');
            string inner = text.Substring(1, end - 1).Trim();
            text = text[(end + 1)..];
            ArrayType arr = ty as ArrayType
                ?? throw new ParseException("array literal but type is not array");
            List<IrValue> elems = [];
            foreach (string e in SplitTopLevel(inner, ',')) {
                string ep = e.Trim();
                if (ep.Length == 0) continue;
                IrType ety = ParseType(ref ep);
                ep = ep.TrimStart();
                elems.Add(ParseValueAfterType(ety, ref ep));
            }
            return new IrConstArray(arr, elems);
        }
        throw new ParseException($"unrecognized value: '{text}'");
    }

    /// <summary>Parses an initializer constant for a global. Accepts the same forms as
    /// ParseValueAfterType plus bare types are not allowed here.</summary>
    private static IrValue ParseConstant(IrType ty, ref string text) => ParseValueAfterType(ty, ref text);

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static int FindValueEnd(string s, int start) {
        int i = start;
        while (i < s.Length) {
            char c = s[i];
            if (c == ',' || c == ')' || c == ']' || c == ' ' || c == '\t') break;
            i++;
        }
        return i;
    }

    private static int FindMatching(string s, int openIdx, char open, char close) {
        int depth = 0;
        for (int i = openIdx; i < s.Length; i++) {
            if (s[i] == open) depth++;
            else if (s[i] == close) { depth--; if (depth == 0) return i; }
        }
        throw new ParseException($"no matching '{close}' for '{open}' in: {s}");
    }

    private static IEnumerable<string> SplitTopLevel(string s, char sep) {
        int depthP = 0, depthB = 0, depthA = 0;
        int start = 0;
        for (int i = 0; i < s.Length; i++) {
            char c = s[i];
            if (c == '(') depthP++;
            else if (c == ')') depthP--;
            else if (c == '[') depthB++;
            else if (c == ']') depthB--;
            else if (c == '<') depthA++;
            else if (c == '>') depthA--;
            else if (c == sep && depthP == 0 && depthB == 0 && depthA == 0) {
                yield return s[start..i];
                start = i + 1;
            }
        }
        if (start <= s.Length) yield return s[start..];
    }

    private static string StripLeadingTokens(string s, string[] tokens) {
        bool changed = true;
        while (changed) {
            changed = false;
            s = s.TrimStart();
            foreach (string t in tokens) {
                if (s.StartsWith(t) && (s.Length == t.Length || !IsIdent(s[t.Length]))) {
                    s = s[t.Length..].TrimStart();
                    changed = true;
                    break;
                }
                // common pattern: token(N)
                if (s.StartsWith(t + "(")) {
                    int close = FindMatching(s, t.Length, '(', ')');
                    s = s[(close + 1)..].TrimStart();
                    changed = true;
                    break;
                }
            }
        }
        return s;
    }

    private static string StripLinkage(string s) => StripLeadingTokens(s, [
        "private", "internal", "available_externally", "linkonce", "weak", "common",
        "appending", "extern_weak", "linkonce_odr", "weak_odr", "external",
        "default", "hidden", "protected", "dso_local", "dso_preemptable",
        "unnamed_addr", "local_unnamed_addr", "noundef", "noreturn", "nounwind",
        "readonly", "readnone", "writeonly", "speculatable", "willreturn", "mustprogress",
        "fastcc", "ccc", "tailcc", "swiftcc", "preserve_mostcc", "preserve_allcc",
        "noinline", "alwaysinline", "optnone", "optsize", "minsize", "uwtable",
        "signext", "zeroext", "byval", "sret", "inreg", "nonnull", "nocapture"
    ]);

    private static string SkipKeyword(string s, string kw) {
        s = s.TrimStart();
        if (!s.StartsWith(kw)) throw new ParseException($"expected keyword '{kw}' in '{s}'");
        return s[kw.Length..];
    }

    private static bool IsIdent(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '$';

    private static string StripLabelToken(string s) {
        // s starts with "%label" possibly followed by junk
        s = s.TrimStart();
        if (s.StartsWith('%')) s = s[1..];
        int e = FindValueEnd(s, 0);
        return s[..e].Trim('"');
    }

    private static string ReadLabelRef(ref string s) {
        s = s.TrimStart();
        if (s.StartsWith('%')) s = s[1..];
        int e = FindValueEnd(s, 0);
        string label = s[..e].Trim('"');
        s = s[e..];
        return label;
    }

    private static string StripTrailingMetadata(string s) {
        // remove "; comment" and trailing ", !dbg !N" / ", !tbaa !N"
        int sc = s.IndexOf(';');
        if (sc >= 0) s = s[..sc].TrimEnd();
        // strip trailing metadata operands
        s = Regex.Replace(s, @",\s*!\w+\s+!\d+", "");
        s = Regex.Replace(s, @",\s*!\w+\s+\{[^}]*\}", "");
        return s.TrimEnd();
    }

    private static byte[] ParseCString(string s) {
        List<byte> bytes = [];
        for (int i = 0; i < s.Length; i++) {
            if (s[i] == '\\' && i + 2 < s.Length) {
                bytes.Add(byte.Parse(s.Substring(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                i += 2;
            } else {
                bytes.Add((byte)s[i]);
            }
        }
        return bytes.ToArray();
    }
}

public sealed class ParseException(string msg) : Exception(msg);
