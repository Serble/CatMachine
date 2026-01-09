using System.Text;
using CatAssembler.Parsers;

namespace CatAssembler;

class Program {
    public static Dictionary<string, byte> RegisterToId = new() {
        { "r0", 0x0 },
        { "r1", 0x1 },
        { "r2", 0x2 },
        { "r3", 0x3 },
        { "r4", 0x4 },
        { "r5", 0x5 },
        { "r6", 0x6 },
        { "r7", 0x7 },

        { "sp", 0x8 }, // Stack Pointer
        { "ip", 0x9 }, // Instruction Pointer
        { "fl", 0xA }, // Flags
        { "it", 0xB }, // Interrupt table pointer
    };
    
    public static readonly Dictionary<string, uint> Labels = new();
    public static readonly Dictionary<string, uint> LocalLabels = new();
    public static readonly Dictionary<string, List<NeededLabel>> NeededLabels = new();

    private static LineIterator iterator = null!;
    public static string LineNum => iterator.LineNum;
    
    public static readonly FileStream File = System.IO.File.Open("a.out", FileMode.Create, FileAccess.Write);

    private static int Main(string[] args) {
        iterator = new LineIterator(args[0]);
        int ret = Run();
        iterator.Dispose();
        return ret;
    }
    
    private static int Run() {
        foreach (string line in iterator) {
            string[] split = line.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length >= 2) {
                split = ((string[])[split[0]])
                    .Concat(split[1].Split(',', StringSplitOptions.RemoveEmptyEntries))
                    .ToArray();
            }
            
            // Line Comments
            for (int i = 0; i < split.Length; i++) {
                int index = split[i].IndexOf(';');
                if (index == -1) {
                    continue;
                }

                if (index == 0) {
                    split = split[..i];
                    break;
                }
                
                split[i] = split[i][..index];
                split = split[..(i + 1)];
                break;
            }

            for (int i = 0; i < split.Length; i++) {
                split[i] = split[i].Trim();
            }
            
            // Empty lines (including just comments)
            if (split.Length == 0) {
                continue;
            }
            
            Console.WriteLine($"{File.Position:X}: {line}");

            switch (split[0].ToLower()) {
                case "mov32":
                case "mov": {
                    if (!ParseMov.Parse(split)) return 1;
                    break;
                }
                
                case "mov16": {
                    if (!ParseMov.ParseSmall(split, 0x08, 2)) return 1;
                    break;
                }
                
                case "mov8": {
                    if (!ParseMov.ParseSmall(split, 0x0e, 1)) return 1;
                    break;
                }

                case "jmp": {
                    if (ParseJmp.Parse(split, 0x30)) break;
                    return 1;
                }
                
                case "jz":
                case "je": {
                    if (ParseJmp.Parse(split, 0x35)) break;
                    return 1;
                }
                
                case "jnz":
                case "jne": {
                    if (ParseJmp.Parse(split, 0x36)) break;
                    return 1;
                }
                
                case "jul": {
                    if (ParseJmp.Parse(split, 0x37)) break;
                    return 1;
                }
                
                case "jule": {
                    if (ParseJmp.Parse(split, 0x38)) break;
                    return 1;
                }
                
                case "jug": {
                    if (ParseJmp.Parse(split, 0x39)) break;
                    return 1;
                }
                
                case "juge": {
                    if (ParseJmp.Parse(split, 0x3a)) break;
                    return 1;
                }
                
                case "jil": {
                    if (ParseJmp.Parse(split, 0x3b)) break;
                    return 1;
                }
                
                case "jile": {
                    if (ParseJmp.Parse(split, 0x3c)) break;
                    return 1;
                }
                
                case "jig": {
                    if (ParseJmp.Parse(split, 0x3d)) break;
                    return 1;
                }
                
                case "jige": {
                    if (ParseJmp.Parse(split, 0x3e)) break;
                    return 1;
                }
                
                case "call": {
                    if (ParseJmp.Parse(split, 0x3f)) break;
                    return 1;
                }

                case "cmp": {
                    if (ParseTwoArgs.Parse(split, 0x31, true)) break;
                    return 1;
                }

                case "sub": {
                    if (ParseTwoArgs.Parse(split, 0x16, false)) break;
                    return 1;
                }

                case "add": {
                    if (ParseTwoArgs.Parse(split, 0x14, false)) break;
                    return 1;
                }

                case "umul": {
                    if (ParseTwoArgs.Parse(split, 0x18, false)) break;
                    return 1;
                }
                
                case "imul": {
                    if (ParseTwoArgs.Parse(split, 0x1a, false)) break;
                    return 1;
                }
                
                case "or": {
                    if (ParseTwoArgs.Parse(split, 0x29, false)) break;
                    return 1;
                }
                
                case "and": {
                    if (ParseTwoArgs.Parse(split, 0x2b, false)) break;
                    return 1;
                }
                
                case "xor": {
                    if (ParseTwoArgs.Parse(split, 0x2d, false)) break;
                    return 1;
                }
                
                case "not": {
                    if (ParseSingleArg.Parse(split, 0x2f, false)) break;
                    return 1;
                }

                case "int": {
                    ParseSingleArg.Parse(split, 0x1e, true, 1);
                    break;
                }

                case "udiv": {
                    if (ParseTwoRegisters.Parse(split, 0x1c)) break;
                    return 1;
                }
                
                case "idiv": {
                    if (ParseTwoRegisters.Parse(split, 0x1d)) break;
                    return 1;
                }

                case "push":
                case "push32": {
                    ParseSingleArg.Parse(split, 0x20, true);
                    break;
                }
                
                case "push16": {
                    ParseSingleArg.Parse(split, 0x22, true, 2);
                    break;
                }
                
                case "push8": {
                    ParseSingleArg.Parse(split, 0x24, true, 1);
                    break;
                }
                
                case "pop":
                case "pop32": {
                    if (ParseSingleArg.Parse(split, 0x26, false)) break;
                    return 1;
                }
                
                case "pop16": {
                    if (ParseSingleArg.Parse(split, 0x27, false)) break;
                    return 1;
                }
                
                case "pop8": {
                    if (ParseSingleArg.Parse(split, 0x28, false)) break;
                    return 1;
                }

                case "ret": {
                    if (ParseNoArgs.Parse(split, 0x40)) break;
                    return 1;
                }
                
                case "di": {
                    if (ParseNoArgs.Parse(split, 0x45)) break;
                    return 1;
                }
                
                case "ei": {
                    if (ParseNoArgs.Parse(split, 0x46)) break;
                    return 1;
                }

                case "cpy": {
                    if (split.Length != 3) {
                        Console.WriteLine(LineNum + ": cpy must have 3 arguments");
                        return 1;
                    }
                    
                    if (split[1][0] != '@' || split[2][0] != '@') {
                        Console.WriteLine(LineNum + ": All arguments in cpy must be a pointer");
                        return 1;
                    }

                    // remove pointers
                    split[1] = split[1][1..];
                    split[2] = split[2][1..];

                    ParseTwoArgs.Parse(split, 0x41, true);
                    break;
                }

                case "in": {
                    if (ParseTwoArgs.Parse(split, 0x47, false)) break;
                    return 1;
                }
                
                case "out": {
                    ParseTwoArgs.Parse(split, 0x47, true);
                    break;
                }

                case "d8": {
                    foreach (string text in split[1..]) {
                        Util.WriteParsed(text, 1, value => {
                            if ((int)value < -127 || (int)value > 255) {
                                Console.WriteLine(LineNum + ": number is not a 8 bit integer");
                                Environment.Exit(1);
                            }

                            return (byte[])[(byte)value];
                        });
                    }
                    break;
                }
                
                case "d16": {
                    foreach (string text in split[1..]) {
                        Util.WriteParsed(text, 2, value => {
                            if ((int)value < short.MinValue || (int)value > ushort.MaxValue) {
                                Console.WriteLine(LineNum + ": number is not a 16 bit integer");
                                Environment.Exit(1);
                            }

                            return BitConverter.GetBytes((ushort)value);
                        });
                    }
                    break;
                }
                
                case "d32": {
                    foreach (string text in split[1..]) {
                        Util.WriteParsed(text);
                    }
                    break;
                }

                case "dstr": {
                    string str = line[(line.IndexOf(' ') + 1)..];
                    StringBuilder output = new();
                    for (int i = 0; i < str.Length;) {
                        if (str[i] != '\\') {
                            output.Append(str[i]);
                            i++;
                            continue;
                        }

                        char? ch = str[i + 1] switch {
                            '\'' => '\'',
                            '\"' => '\"',
                            '\\' => '\\',
                            '0' => '\0',
                            'a' => '\a',
                            'b' => '\b',
                            'f' => '\f',
                            'n' => '\n',
                            'r' => '\r',
                            't' => '\t',
                            'v' => '\v',
                            's' => ' ',
                            _ => null
                        };

                        if (ch == null) {
                            Console.WriteLine(LineNum + ": Invalid escape code: \\" + str[i + 1]);
                            return 1;
                        }
                        
                        output.Append(ch);
                        i += 2;
                    }
                    
                    byte[] data = Encoding.UTF8.GetBytes(output.ToString());
                    File.Write(data);
                    break;
                }

                case "dfile": {
                    if (split.Length != 2) {
                        Console.WriteLine($"{LineNum}: includes must have one argument");
                        return 1;
                    }

                    if (!System.IO.File.Exists(split[1])) {
                        Console.WriteLine($"{LineNum}: the file {split[1]} does not exist");
                        return 1;
                    }
                    
                    File.Write(System.IO.File.ReadAllBytes(split[1]));
                    break;
                }

                case "res":
                case "res8": {
                    if (split.Length != 2 || !Util.Parse32Int(split[1], out uint amount)) {
                        Console.WriteLine($"{LineNum}: the argument of res8 must be an integer");
                        return 1;
                    }
                    
                    File.Write(new byte[amount]);
                    break;
                }
                
                case "res16": {
                    if (split.Length != 2 || !Util.Parse32Int(split[1], out uint amount)) {
                        Console.WriteLine($"{LineNum}: the argument of res16 must be an integer");
                        return 1;
                    }
                    
                    File.Write(new byte[amount * 2]);
                    break;
                }
                
                case "res32": {
                    if (split.Length != 2 || !Util.Parse32Int(split[1], out uint amount)) {
                        Console.WriteLine($"{LineNum}: the argument of res32 must be an integer");
                        return 1;
                    }
                    
                    File.Write(new byte[amount * 4]);
                    break;
                }

                case "#include": {
                    if (split.Length != 2) {
                        Console.WriteLine($"{LineNum}: includes must have one argument");
                        return 1;
                    }

                    if (!System.IO.File.Exists(split[1])) {
                        Console.WriteLine($"{LineNum}: the file {split[1]} does not exist");
                        return 1;
                    }
                    
                    iterator.AddFile(split[1]);
                    break;
                }

                default: {
                    // is it a label
                    if (split.Length == 1 && split[0][^1] == ':') {
                        string labelName = split[0][..^1];
                        if (labelName.Length == 0) {
                            Console.WriteLine(LineNum + ": Label name cannot be empty");
                            return 1;
                        }
                        
                        if ("0123456789".Contains(split[0][0]) || RegisterToId.ContainsKey(labelName)) {
                            Console.WriteLine(LineNum + ": Labels cannot start with numbers or be a register");
                            return 1;
                        }
                        
                        // fill all needed stuffs
                        if (NeededLabels.TryGetValue(labelName, out List<NeededLabel>? list)) {
                            long currentPos = File.Position;
                            foreach (NeededLabel needed in list) {
                                File.Position = needed.Position;
                                File.Write(needed.Transformer((uint)currentPos));
                            }
                            
                            NeededLabels.Remove(labelName);
                            File.Position = currentPos;
                        }

                        if (split[0][0] == '.') {
                            if (LocalLabels.ContainsKey(labelName)) {
                                Console.WriteLine(LineNum + $": local label {labelName} already exists!");
                                return 1;
                            }
                            
                            LocalLabels[labelName] = (uint)File.Position;
                            break;
                        }
                        
                        if (Labels.ContainsKey(labelName)) {
                            Console.WriteLine(LineNum + $": label {labelName} already exists!");
                            return 1;
                        }

                        if (NeededLabels.Keys.Any(label => label[0] == '.')) {
                            Console.WriteLine(GetNeededLabelsString(NeededLabels.Where(l => l.Key[0] == '.')));
                            return 1;
                        }
                        
                        Labels[labelName] = (uint)File.Position;
                        LocalLabels.Clear();
                        break;
                    }

                    Console.WriteLine(LineNum + ": Invalid token: " + line);
                    return 1;
                }
            }
        }

        if (NeededLabels.Count != 0) {
            Console.WriteLine(GetNeededLabelsString(NeededLabels));
            return 1;
        }
        
        File.Close();
        Console.WriteLine("Success!");
        return 0;
    }

    private static string GetNeededLabelsString(IEnumerable<KeyValuePair<string, List<NeededLabel>>> needed) {
        return "There are jumps with unknown labels:\n" + string.Join("\n",
            needed.Select(label => $"{label.Key}: [{string.Join(", ", label.Value.Select(l => l.LineNum))}]"));
    }
}
