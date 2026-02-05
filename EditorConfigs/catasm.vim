" Custom syntax highlighting for catasm language

" Place this file in ~/.config/nvim/syntax/catasm.vim

" Place the following in ~/.config/nvim/ftdetect/catasm.vim:
" au BufRead,BufNewFile *.cat set filetype=catasm

setlocal commentstring=;%s

" 1. Registers: r0-r7, sp, ip, it, fl (whole word matches)
syntax keyword asmRegister r0 r1 r2 r3 r4 r5 r6 r7 sp ip it fl
highlight def link asmRegister Statement " Use another group if preferred

" 2. Instructions: first word on a line (followed by whitespace or end)
syntax match asmInstruction "^\s*\zs[a-zA-Z][a-zA-Z0-9_]*\ze\>"
highlight def link asmInstruction Keyword

" 3. Labels: name: at start of a line (or after whitespace)
syntax match asmLabel "^\s*\zs[a-zA-Z_][a-zA-Z0-9_]*:\ze"
highlight def link asmLabel Identifier

" 4. Directives: #NAME (at beginning of line or after WS), plus arguments
syntax match asmDirective "^\s*#\w\+"
highlight def link asmDirective PreProc

" 5. Number literals: decimal, hex, char ('A'), binary
syntax match asmNumber "\<0x[0-9A-Fa-f]\+\>"
syntax match asmNumber "\<0b[01]\+\>"
syntax match asmCharContent /[ -~]/ contained
syntax match asmNumber "\<[0-9]\+\>"
highlight def link asmNumber Number

" 6. Strings: "..."
syntax region asmString start=+\"+ skip=+\\\\\|\\"+ end=+\"+ contains=catnipEscape
highlight def link asmString String

syntax region asmChar start=+'+ end=+'+ contains=catnipEscape,asmNumber oneline
highlight link asmChar Number

syntax match catnipEscape /\\./ contained
highlight link catnipEscape SpecialChar

" 7. Comments: ; to end of line
syntax match asmComment ";.*$"
highlight def link asmComment Comment

" 8. Non-register arguments for instructions:
"    A hack: arguments separated by commas, not registers or numbers or string
"    This is not perfect, but will highlight tokens after first, if not register, number, string, or comment

" Highlight comma separators for clarity
syntax match asmComma ","
highlight def link asmComma Delimiter

" Arguments: match identifiers (not registers/labels) after instruction & before ';' or newline
" -- Exclude registers (use \%(\m\c\V\) if Vim 8.2+, otherwise do manual filter)
syntax match asmArgument "\<\%(?!r[0-7]\|sp\|ip\|it\|fl\)[a-zA-Z_][a-zA-Z0-9_]*\>"
    \ containedin=ALLBUT,asmRegister,asmLabel,asmInstruction,asmDirective,asmNumber,asmString,asmComment
highlight def link asmArgument Type

" --- End of syntax file ---

" (In ftdetect/asm.vim, associate your extensions, e.g.:
"  au BufRead,BufNewFile *.asm set filetype=asm
" )
