package com.serble.catmachine.catasm

import com.intellij.openapi.fileTypes.LanguageFileType
import javax.swing.Icon

object CatAsmFileType : LanguageFileType(CatAsmLanguage.INSTANCE) {
    override fun getName(): String = "CatASM File"
    
    override fun getDescription(): String = "CatASM assembly language file"
    
    override fun getDefaultExtension(): String = "asm"
    
    override fun getIcon(): Icon? = CatAsmIcons.FILE
}
