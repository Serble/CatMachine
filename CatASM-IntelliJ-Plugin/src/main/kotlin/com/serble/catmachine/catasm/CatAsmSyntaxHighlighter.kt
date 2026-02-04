package com.serble.catmachine.catasm

import com.intellij.openapi.editor.DefaultLanguageHighlighterColors
import com.intellij.openapi.editor.colors.TextAttributesKey
import com.intellij.openapi.fileTypes.SyntaxHighlighterBase
import com.intellij.psi.tree.IElementType

class CatAsmSyntaxHighlighter : SyntaxHighlighterBase() {
    companion object {
        val INSTRUCTION = TextAttributesKey.createTextAttributesKey(
            "CATASM_INSTRUCTION",
            DefaultLanguageHighlighterColors.KEYWORD
        )
        
        val REGISTER = TextAttributesKey.createTextAttributesKey(
            "CATASM_REGISTER",
            DefaultLanguageHighlighterColors.INSTANCE_FIELD
        )
        
        val DIRECTIVE = TextAttributesKey.createTextAttributesKey(
            "CATASM_DIRECTIVE",
            DefaultLanguageHighlighterColors.METADATA
        )
        
        val LABEL = TextAttributesKey.createTextAttributesKey(
            "CATASM_LABEL",
            DefaultLanguageHighlighterColors.FUNCTION_DECLARATION
        )
        
        val NUMBER = TextAttributesKey.createTextAttributesKey(
            "CATASM_NUMBER",
            DefaultLanguageHighlighterColors.NUMBER
        )
        
        val STRING = TextAttributesKey.createTextAttributesKey(
            "CATASM_STRING",
            DefaultLanguageHighlighterColors.STRING
        )
        
        val COMMENT = TextAttributesKey.createTextAttributesKey(
            "CATASM_COMMENT",
            DefaultLanguageHighlighterColors.LINE_COMMENT
        )
        
        val OPERATOR = TextAttributesKey.createTextAttributesKey(
            "CATASM_OPERATOR",
            DefaultLanguageHighlighterColors.OPERATION_SIGN
        )
        
        private val INSTRUCTION_KEYS = arrayOf(INSTRUCTION)
        private val REGISTER_KEYS = arrayOf(REGISTER)
        private val DIRECTIVE_KEYS = arrayOf(DIRECTIVE)
        private val LABEL_KEYS = arrayOf(LABEL)
        private val NUMBER_KEYS = arrayOf(NUMBER)
        private val STRING_KEYS = arrayOf(STRING)
        private val COMMENT_KEYS = arrayOf(COMMENT)
        private val OPERATOR_KEYS = arrayOf(OPERATOR)
        private val EMPTY_KEYS = emptyArray<TextAttributesKey>()
    }
    
    override fun getHighlightingLexer() = CatAsmLexer()
    
    override fun getTokenHighlights(tokenType: IElementType?): Array<TextAttributesKey> {
        return when (tokenType) {
            CatAsmTypes.INSTRUCTION -> INSTRUCTION_KEYS
            CatAsmTypes.REGISTER -> REGISTER_KEYS
            CatAsmTypes.DIRECTIVE -> DIRECTIVE_KEYS
            CatAsmTypes.LABEL -> LABEL_KEYS
            CatAsmTypes.NUMBER -> NUMBER_KEYS
            CatAsmTypes.STRING -> STRING_KEYS
            CatAsmTypes.COMMENT -> COMMENT_KEYS
            CatAsmTypes.OPERATOR -> OPERATOR_KEYS
            else -> EMPTY_KEYS
        }
    }
}
