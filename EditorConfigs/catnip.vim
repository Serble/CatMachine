" Vim/Neovim syntax highlighting for Catnip

" Place this file in ~/.config/nvim/syntax/catnip.vim

" Place the following in ~/.config/nvim/ftdetect/catnip.vim:
" au BufRead,BufNewFile *.nip set filetype=catnip

setlocal commentstring=//%s

" --- Highlight Directives (#define, #include) ---
syntax match catnipPreProc /^\s*#\w\+/
highlight def link catnipPreProc PreProc

" --- Highlight Comments (// and # for comments) ---
syntax match catnipComment "\s*//.*$"
highlight def link catnipComment Comment

" --- Highlight Strings ---
syntax region catnipString start=+"+ skip=+\\\\\|\\"+ end=+"+ contains=catnipEscape
highlight def link catnipString String

syntax region asmChar start=+'+ end=+'+ contains=catnipEscape,catnipNumber oneline
highlight link asmChar Number

syntax match catnipEscape /\\./ contained
highlight link catnipEscape SpecialChar

" --- Highlight Numbers (decimal, hex) ---
syntax match catnipNumber "\<0x[0-9A-Fa-f]\+\>"
syntax match catnipNumber "\<[0-9]\+\>"
syntax match asmCharContent /[ -~]/ contained
highlight def link catnipNumber Number

" --- Highlight Keywords (fun, struct, global, let, return, if, etc.) ---
syntax keyword catnipKeyword fun struct global let return if else while
highlight def link catnipKeyword Keyword

" --- Highlight Constants (true/false? not specified but common) ---
syntax keyword catnipConstant null
highlight def link catnipConstant Constant

" --- Macro substitution (${...}) ---
syntax match catnipMacro "\${[A-Za-z0-9_]\+}"
highlight def link catnipMacro Identifier

" --- Built-in symbol-operators ($Thing, Thing#y) ---
syntax match catnipSymbol "\$[A-Za-z_][A-Za-z0-9_]*"
syntax match catnipSymbol "[A-Za-z_][A-Za-z0-9_]*#[A-Za-z_][A-Za-z0-9_]*"
highlight def link catnipSymbol Type

" --- Section: Operators (arithmetic, etc) ---
syntax match catnipOp "+\|-|\*|/|%|==|!=|<=|>=|<|>|~\*|~/|~%|~<|~>|=|&|\^|\||!"
highlight def link catnipOp Operator

" --- Highlight Inline Assembly blocks (~~~ delimited) ---
syntax region catnipAsm start=/^~~~\s/ end=/^~~~\s*$/
highlight def link catnipAsm SpecialComment

" --- Brackets/Parenthesis (optional, can help) ---
syntax match catnipBracket "[\[\]{}()]"

" --- Highlight size specifiers (:1, :2, :4, :XX) ---
syntax match catnipSize ":[0-9]\+"
highlight def link catnipSize Number

" --- Support for file association ---

" --- End of Catnip Syntax File ---
