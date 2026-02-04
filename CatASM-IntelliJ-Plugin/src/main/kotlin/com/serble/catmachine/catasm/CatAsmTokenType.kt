package com.serble.catmachine.catasm

import com.intellij.psi.tree.IElementType

class CatAsmTokenType(debugName: String) : IElementType(debugName, CatAsmLanguage.INSTANCE) {
    override fun toString(): String = "CatAsmTokenType." + super.toString()
}
