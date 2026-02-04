package com.serble.catmachine.catasm

import com.intellij.lang.Language

class CatAsmLanguage private constructor() : Language("CatASM") {
    companion object {
        @JvmStatic
        val INSTANCE = CatAsmLanguage()
    }
}
