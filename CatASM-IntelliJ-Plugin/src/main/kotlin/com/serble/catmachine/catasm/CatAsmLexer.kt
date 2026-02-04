package com.serble.catmachine.catasm

import com.intellij.lexer.LexerBase
import com.intellij.psi.tree.IElementType

class CatAsmLexer : LexerBase() {
    private var buffer: CharSequence = ""
    private var startOffset: Int = 0
    private var endOffset: Int = 0
    private var currentOffset: Int = 0
    private var tokenType: IElementType? = null
    
    companion object {
        // Instructions from CatASM specification
        val INSTRUCTIONS = setOf(
            "mov", "mov32", "mov16", "mov8",
            "add", "sub", "umul", "imul", "udiv", "idiv",
            "int", "push", "push32", "push16", "push8",
            "pop", "pop32", "pop16", "pop8",
            "or", "and", "xor", "not",
            "jmp", "cmp",
            "jz", "je", "jnz", "jne",
            "jul", "jule", "jug", "juge",
            "jil", "jile", "jig", "jige",
            "call", "ret",
            "cpy", "di", "ei",
            "in", "out", "nop",
            "shl", "shr"
        )
        
        // Directives
        val DIRECTIVES = setOf(
            "res8", "res16", "res32",
            "d8", "d16", "d32",
            "dfile", "dstr",
            "#const", "#include"
        )
        
        // Registers
        val REGISTERS = setOf(
            "r0", "r1", "r2", "r3", "r4", "r5", "r6", "r7",
            "sp", "bp", "ip", "flags"
        )
    }
    
    override fun start(buffer: CharSequence, startOffset: Int, endOffset: Int, initialState: Int) {
        this.buffer = buffer
        this.startOffset = startOffset
        this.endOffset = endOffset
        this.currentOffset = startOffset
        advance()
    }
    
    override fun getState(): Int = 0
    
    override fun getTokenType(): IElementType? = tokenType
    
    override fun getTokenStart(): Int = startOffset
    
    override fun getTokenEnd(): Int = currentOffset
    
    override fun advance() {
        if (currentOffset >= endOffset) {
            tokenType = null
            return
        }
        
        startOffset = currentOffset
        val c = buffer[currentOffset]
        
        when {
            // Comments
            c == ';' -> {
                while (currentOffset < endOffset && buffer[currentOffset] != '\n') {
                    currentOffset++
                }
                tokenType = CatAsmTypes.COMMENT
            }
            // Whitespace
            c.isWhitespace() -> {
                while (currentOffset < endOffset && buffer[currentOffset].isWhitespace()) {
                    currentOffset++
                }
                tokenType = CatAsmTypes.WHITE_SPACE
            }
            // Numbers (hex or decimal)
            c == '0' && currentOffset + 1 < endOffset && buffer[currentOffset + 1].lowercaseChar() == 'x' -> {
                currentOffset += 2
                while (currentOffset < endOffset && buffer[currentOffset].isHexDigit()) {
                    currentOffset++
                }
                tokenType = CatAsmTypes.NUMBER
            }
            c.isDigit() || (c == '-' && currentOffset + 1 < endOffset && buffer[currentOffset + 1].isDigit()) -> {
                if (c == '-') currentOffset++
                while (currentOffset < endOffset && buffer[currentOffset].isDigit()) {
                    currentOffset++
                }
                tokenType = CatAsmTypes.NUMBER
            }
            // Strings
            c == '\'' || c == '"' -> {
                val quote = c
                currentOffset++
                while (currentOffset < endOffset && buffer[currentOffset] != quote) {
                    if (buffer[currentOffset] == '\\' && currentOffset + 1 < endOffset) {
                        currentOffset += 2
                    } else {
                        currentOffset++
                    }
                }
                if (currentOffset < endOffset) currentOffset++ // closing quote
                tokenType = CatAsmTypes.STRING
            }
            // Labels (start with dot or end with colon)
            c == '.' -> {
                currentOffset++
                while (currentOffset < endOffset && (buffer[currentOffset].isLetterOrDigit() || buffer[currentOffset] == '_')) {
                    currentOffset++
                }
                tokenType = CatAsmTypes.LABEL
            }
            // Symbols
            c == '@' -> {
                currentOffset++
                tokenType = CatAsmTypes.AT
            }
            c == ',' -> {
                currentOffset++
                tokenType = CatAsmTypes.COMMA
            }
            c == ':' -> {
                currentOffset++
                tokenType = CatAsmTypes.COLON
            }
            c == '#' -> {
                currentOffset++
                while (currentOffset < endOffset && buffer[currentOffset].isLetter()) {
                    currentOffset++
                }
                val text = buffer.substring(startOffset, currentOffset)
                tokenType = if (DIRECTIVES.contains(text)) CatAsmTypes.DIRECTIVE else CatAsmTypes.IDENTIFIER
            }
            // Identifiers, instructions, registers
            c.isLetter() || c == '_' -> {
                while (currentOffset < endOffset && (buffer[currentOffset].isLetterOrDigit() || buffer[currentOffset] == '_')) {
                    currentOffset++
                }
                // Check for label (identifier followed by colon)
                if (currentOffset < endOffset && buffer[currentOffset] == ':') {
                    currentOffset++
                    tokenType = CatAsmTypes.LABEL
                } else {
                    val text = buffer.substring(startOffset, currentOffset).toLowerCase()
                    tokenType = when {
                        INSTRUCTIONS.contains(text) -> CatAsmTypes.INSTRUCTION
                        REGISTERS.contains(text) -> CatAsmTypes.REGISTER
                        DIRECTIVES.contains(text) -> CatAsmTypes.DIRECTIVE
                        else -> CatAsmTypes.IDENTIFIER
                    }
                }
            }
            // Operators
            c == '+' || c == '-' || c == '*' || c == '/' -> {
                currentOffset++
                tokenType = CatAsmTypes.OPERATOR
            }
            // Unknown
            else -> {
                currentOffset++
                tokenType = CatAsmTypes.BAD_CHARACTER
            }
        }
    }
    
    override fun getBufferSequence(): CharSequence = buffer
    
    override fun getBufferEnd(): Int = endOffset
    
    private fun Char.isHexDigit(): Boolean = this in '0'..'9' || this in 'a'..'f' || this in 'A'..'F'
}
