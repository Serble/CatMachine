package com.serble.catmachine.catasm

import com.intellij.extapi.psi.PsiFileBase
import com.intellij.openapi.fileTypes.FileType
import com.intellij.psi.FileViewProvider

class CatAsmFile(viewProvider: FileViewProvider) : PsiFileBase(viewProvider, CatAsmLanguage.INSTANCE) {
    override fun getFileType(): FileType = CatAsmFileType
    
    override fun toString(): String = "CatASM File"
}
