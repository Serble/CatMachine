package com.serble.catmachine.catasm

import com.intellij.lang.ASTNode
import com.intellij.lang.ParserDefinition
import com.intellij.lang.PsiParser
import com.intellij.lexer.Lexer
import com.intellij.openapi.project.Project
import com.intellij.psi.FileViewProvider
import com.intellij.psi.PsiElement
import com.intellij.psi.PsiFile
import com.intellij.psi.tree.IFileElementType
import com.intellij.psi.tree.TokenSet

class CatAsmParserDefinition : ParserDefinition {
    companion object {
        val FILE = IFileElementType(CatAsmLanguage.INSTANCE)
        val COMMENTS = TokenSet.create(CatAsmTypes.COMMENT)
        val STRINGS = TokenSet.create(CatAsmTypes.STRING)
    }
    
    override fun createLexer(project: Project?): Lexer = CatAsmLexer()
    
    override fun createParser(project: Project?): PsiParser = CatAsmParser()
    
    override fun getFileNodeType(): IFileElementType = FILE
    
    override fun getCommentTokens(): TokenSet = COMMENTS
    
    override fun getStringLiteralElements(): TokenSet = STRINGS
    
    override fun createElement(node: ASTNode?): PsiElement = CatAsmElement(node!!)
    
    override fun createFile(viewProvider: FileViewProvider): PsiFile = CatAsmFile(viewProvider)
}
