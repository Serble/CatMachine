package com.serble.catmachine.catasm

import com.intellij.psi.TokenType
import com.intellij.psi.tree.IElementType

object CatAsmTypes {
    @JvmField
    val INSTRUCTION = CatAsmTokenType("INSTRUCTION")
    
    @JvmField
    val REGISTER = CatAsmTokenType("REGISTER")
    
    @JvmField
    val DIRECTIVE = CatAsmTokenType("DIRECTIVE")
    
    @JvmField
    val LABEL = CatAsmTokenType("LABEL")
    
    @JvmField
    val IDENTIFIER = CatAsmTokenType("IDENTIFIER")
    
    @JvmField
    val NUMBER = CatAsmTokenType("NUMBER")
    
    @JvmField
    val STRING = CatAsmTokenType("STRING")
    
    @JvmField
    val COMMENT = CatAsmTokenType("COMMENT")
    
    @JvmField
    val COMMA = CatAsmTokenType("COMMA")
    
    @JvmField
    val COLON = CatAsmTokenType("COLON")
    
    @JvmField
    val AT = CatAsmTokenType("AT")
    
    @JvmField
    val OPERATOR = CatAsmTokenType("OPERATOR")
    
    @JvmField
    val WHITE_SPACE: IElementType = TokenType.WHITE_SPACE
    
    @JvmField
    val BAD_CHARACTER: IElementType = TokenType.BAD_CHARACTER
}
