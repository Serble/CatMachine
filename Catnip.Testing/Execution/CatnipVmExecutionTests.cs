namespace Catnip.Testing.Execution;

public class CatnipVmExecutionTests {
    private static void AssertProgramEmits(string source, uint marker, int maxInstructions = 500) {
        CatnipProgramExecutionResult result = CatnipProgramRunner.Execute(source, maxInstructions);
        Assert.That(result.SerialOutput, Does.Contain(marker));
    }

    [Test]
    public void Execute_FunctionCall_InvokesFunctionBody() {
        string source = """
                        invoke_me();

                        fun emit(v:4) {
                            ~~~r1[v:4] | | r1;
                            out 0x0CA7, r1
                            ~~~
                        }

                        fun invoke_me() {
                            emit(0xC0DE);
                            return;
                        }
                        """;

        AssertProgramEmits(source, 0xC0DEu, maxInstructions: 200);
    }

    [Test]
    public void Execute_ArithmeticBitwiseAndUnary_EmitsMarker() {
        string source = """
                        main();

                        fun emit(v:4) {
                            ~~~r1[v:4] | | r1;
                            out 0x0CA7, r1
                            ~~~
                        }

                        fun main() {
                            let a:4 = (5 + 3) * 2;
                            let b:4 = (a:4 - 4) / 2;
                            let c:4 = (5 ^ 1) | (8 & 3);
                            let d:4 = ~0;
                            let e:4 = !0;
                            if (b:4 == 6 && c:4 == 4 && d:4 == 0xFFFFFFFF && e:4 == 1) {
                                emit(0xA001);
                            }
                            return;
                        }
                        """;

        AssertProgramEmits(source, 0xA001u);
    }

    [Test]
    public void Execute_IfElseAndWhile_EmitsMarker() {
        string source = """
                        main();

                        fun emit(v:4) {
                            ~~~r1[v:4] | | r1;
                            out 0x0CA7, r1
                            ~~~
                        }

                        fun main() {
                            let i:4 = 4;
                            let sum:4 = 0;
                            while (i:4 > 0) {
                                sum:4 = sum:4 + i:4;
                                i:4 = i:4 - 1;
                            }

                            if (sum:4 == 10) {
                                emit(0xA002);
                            } else {
                                emit(0xBAD2);
                            }
                            return;
                        }
                        """;

        AssertProgramEmits(source, 0xA002u);
    }

    [Test]
    public void Execute_SwitchStatement_EmitsMarker() {
        string source = """
                        main();

                        fun emit(v:4) {
                            ~~~r1[v:4] | | r1;
                            out 0x0CA7, r1
                            ~~~
                        }

                        fun main() {
                            let x:4 = 3;
                            switch (x:4) {
                                case (1) { emit(0xBAD1); }
                                case (2), (3) { emit(0xA003); }
                                default { emit(0xBAD3); }
                            }
                            return;
                        }
                        """;

        AssertProgramEmits(source, 0xA003u);
    }

    [Test]
    public void Execute_SwitchStatementInWhile_Assembles() {
        string source = """
                        main();

                        fun emit(v:4) {
                            ~~~r1[v:4] | | r1;
                            out 0x0CA7, r1
                            ~~~
                        }

                        fun main() {
                            while (1) {
                                let x:4 = 3;
                                switch (x:4) {
                                    case (1) { emit(0xBAD1); }
                                    case (2), (3) { emit(0xA003); }
                                    default { emit(0xBAD3); }
                                }
                                return;
                            }
                        }
                        """;

        AssertProgramEmits(source, 0xA003u);
    }

    [Test]
    public void Execute_GlobalsAndLocals_EmitsMarker() {
        string source = """
                        global score:4 = 10;

                        main();

                        fun emit(v:4) {
                            ~~~r1[v:4] | | r1;
                            out 0x0CA7, r1
                            ~~~
                        }

                        fun main() {
                            let inc:4 = 5;
                            score:4 = score:4 + inc:4;
                            if (score:4 == 15) {
                                emit(0xA004);
                            }
                            return;
                        }
                        """;

        AssertProgramEmits(source, 0xA004u);
    }

    [Test]
    public void Execute_StructSizeAndOffset_EmitsMarker() {
        string source = """
                        main();

                        struct Device {
                            kind:1;
                            flags:2;
                            payload:4;
                        }

                        fun emit(v:4) {
                            ~~~r1[v:4] | | r1;
                            out 0x0CA7, r1
                            ~~~
                        }

                        fun main() {
                            let sz:4 = $Device;
                            let off:4 = Device#payload;
                            if (sz:4 == 7 && off:4 == 3) {
                                emit(0xA005);
                            }
                            return;
                        }
                        """;

        AssertProgramEmits(source, 0xA005u);
    }

    [Test]
    public void Execute_StringDereference_EmitsMarker() {
        string source = """
                        main();

                        fun emit(v:4) {
                            ~~~r1[v:4] | | r1;
                            out 0x0CA7, r1
                            ~~~
                        }

                        fun main() {
                            let s:4 = "Z";
                            if (s:4 > 0) {
                                emit(0xA006);
                            }
                            return;
                        }
                        """;

        AssertProgramEmits(source, 0xA006u);
    }

    [Test]
    public void Execute_InlineAsmOutputs_EmitsMarker() {
        string source = """
                        main();

                        fun emit(v:4) {
                            ~~~r1[v:4] | | r1;
                            out 0x0CA7, r1
                            ~~~
                        }

                        fun main() {
                            let x:4 = 0;
                            ~~~ | r1[x:4] | ;
                            mov r1, 0x1234
                            ~~~
                            if (x:4 == 0x1234) {
                                emit(0xA007);
                            }
                            return;
                        }
                        """;

        AssertProgramEmits(source, 0xA007u);
    }

    [Test]
    public void Execute_FunctionPointerCall_WithSimpleArgs_EmitsMarker() {
        string source = """
                        dispatch(target);

                        fun emit(v:4) {
                            ~~~r1[v:4] | | r1;
                            out 0x0CA7, r1
                            ~~~
                        }

                        fun dispatch(callback:4) {
                            (callback:4)(0x2A, 0x07);
                            return;
                        }

                        fun target(port:4, type:4) {
                            if (port:4 == 0x2A && type:4 == 0x07) {
                                emit(0xA008);
                            }
                            return;
                        }
                        """;

        AssertProgramEmits(source, 0xA008u);
    }

    [Test]
    public void Execute_CallbackWithCallArguments_InvokesCallback_Regression() {
        string source = """
                        dispatch(target);

                        fun val_a() {
                            return 0x2A;
                        }

                        fun val_b() {
                            return 0x07;
                        }

                        fun dispatch(callback:4) {
                            (callback:4)(val_a(), val_b());
                            return;
                        }

                        fun target(port:4, type:4) {
                            if (port:4 == 0x2A && type:4 == 0x07) {
                                ~~~r1[0xD00D] | | r1;
                                out 0x0CA7, r1
                                ~~~
                            }
                            return;
                        }
                        """;

        CatnipProgramExecutionResult result = CatnipProgramRunner.Execute(source, maxInstructions: 500);

        Assert.That(result.SerialOutput, Does.Contain(0xD00Du));
    }
}
