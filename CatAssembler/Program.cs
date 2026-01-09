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
    
    public static int LineNum;
    
    public static readonly FileStream File = System.IO.File.Open("a.out", FileMode.Create, FileAccess.Write);
    
    static void Main(string[] args) {
        LineNum = 0;
        
        foreach (string line in System.IO.File.ReadLines(args[0])) {
            LineNum++;
            
            string[] split = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            
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
            
            // Empty lines (including just comments)
            if (split.Length == 0) {
                continue;
            }
            
            Console.WriteLine(string.Join(", ", split));

            switch (split[0].ToLower()) {
                case "mov32":
                case "mov": {
                    if (!ParseMov.Parse(split)) return;
                    break;
                }
                
                case "mov16": {
                    if (!ParseMov.ParseSmall(split, 0x08, 2)) return;
                    break;
                }
                
                case "mov8": {
                    if (!ParseMov.ParseSmall(split, 0x0e, 1)) return;
                    break;
                }

                case "jmp": {
                    if (ParseJmp.Parse(split, 0x30)) break;
                    return;
                }
                
                case "jz":
                case "je": {
                    if (ParseJmp.Parse(split, 0x35)) break;
                    return;
                }
                
                case "jnz":
                case "jne": {
                    if (ParseJmp.Parse(split, 0x36)) break;
                    return;
                }
                
                case "jul": {
                    if (ParseJmp.Parse(split, 0x37)) break;
                    return;
                }
                
                case "jule": {
                    if (ParseJmp.Parse(split, 0x38)) break;
                    return;
                }
                
                case "jug": {
                    if (ParseJmp.Parse(split, 0x39)) break;
                    return;
                }
                
                case "juge": {
                    if (ParseJmp.Parse(split, 0x3a)) break;
                    return;
                }
                
                case "jil": {
                    if (ParseJmp.Parse(split, 0x3b)) break;
                    return;
                }
                
                case "jile": {
                    if (ParseJmp.Parse(split, 0x3c)) break;
                    return;
                }
                
                case "jig": {
                    if (ParseJmp.Parse(split, 0x3d)) break;
                    return;
                }
                
                case "jige": {
                    if (ParseJmp.Parse(split, 0x3e)) break;
                    return;
                }
                
                case "call": {
                    if (ParseJmp.Parse(split, 0x3f)) break;
                    return;
                }

                case "cmp": {
                    if (ParseTwoArgs.Parse(split, 0x31, true)) break;
                    return;
                }

                case "sub": {
                    if (ParseTwoArgs.Parse(split, 0x16, false)) break;
                    return;
                }

                case "add": {
                    if (ParseTwoArgs.Parse(split, 0x14, false)) break;
                    return;
                }

                case "umul": {
                    if (ParseTwoArgs.Parse(split, 0x18, false)) break;
                    return;
                }
                
                case "imul": {
                    if (ParseTwoArgs.Parse(split, 0x1a, false)) break;
                    return;
                }
                
                case "or": {
                    if (ParseTwoArgs.Parse(split, 0x29, false)) break;
                    return;
                }
                
                case "and": {
                    if (ParseTwoArgs.Parse(split, 0x2b, false)) break;
                    return;
                }
                
                case "xor": {
                    if (ParseTwoArgs.Parse(split, 0x2d, false)) break;
                    return;
                }
                
                case "not": {
                    if (ParseSingleArg.Parse(split, 0x2f, false)) break;
                    return;
                }

                case "int": {
                    ParseSingleArg.Parse(split, 0x1e, true, 1);
                    break;
                }

                case "udiv": {
                    if (ParseTwoRegisters.Parse(split, 0x1c)) break;
                    return;
                }
                
                case "idiv": {
                    if (ParseTwoRegisters.Parse(split, 0x1d)) break;
                    return;
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
                    return;
                }
                
                case "pop16": {
                    if (ParseSingleArg.Parse(split, 0x27, false)) break;
                    return;
                }
                
                case "pop8": {
                    if (ParseSingleArg.Parse(split, 0x28, false)) break;
                    return;
                }

                case "ret": {
                    if (split.Length != 1) {
                        Console.WriteLine(LineNum + ": ret takes 0 arguments");
                        return;
                    }
                    File.WriteByte(0x2f);
                    break;
                }

                case "cpy": {
                    if (split.Length != 3) {
                        Console.WriteLine(LineNum + ": cpy must have 3 arguments");
                        return;
                    }
                    
                    if (split[1][0] != '@' || split[2][0] != '@') {
                        Console.WriteLine(LineNum + ": All arguments in cpy must be a pointer");
                        return;
                    }

                    // remove pointers
                    split[1] = split[1][1..];
                    split[2] = split[2][1..];
                    
                    if (ParseTwoArgs.Parse(split, 0x41, true)) break;
                    return;
                }

                case "d8": {
                    foreach (string text in split[1..]) {
                        Util.WriteParsed(text, 1, value => {
                            if ((int)value < -127 || (int)value > 255) {
                                Console.WriteLine(LineNum + ": number is not a 8 bit integer");
                                Environment.Exit(0);
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
                                Environment.Exit(0);
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
                            return;
                        }
                        
                        output.Append(ch);
                        i += 2;
                    }
                    
                    byte[] data = Encoding.UTF8.GetBytes(output.ToString());
                    File.Write(data);
                    break;
                }

                default: {
                    // is it a label
                    if (split.Length == 1 && split[0][^1] == ':') {
                        string labelName = split[0][..^1];
                        if (labelName.Length == 0) {
                            Console.WriteLine(LineNum + ": Label name cannot be empty");
                            return;
                        }
                        
                        if ("0123456789".Contains(split[0][0]) || RegisterToId.ContainsKey(labelName)) {
                            Console.WriteLine(LineNum + ": Labels cannot start with numbers or be a register");
                            return;
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
                                return;
                            }
                            
                            LocalLabels[labelName] = (uint)File.Position;
                            break;
                        }
                        
                        if (Labels.ContainsKey(labelName)) {
                            Console.WriteLine(LineNum + $": label {labelName} already exists!");
                            return;
                        }

                        if (NeededLabels.Keys.Any(label => label[0] == '.')) {
                            Console.WriteLine(LineNum + $": local label {labelName} does not exist!");
                            return;
                        }
                        
                        Labels[labelName] = (uint)File.Position;
                        LocalLabels.Clear();
                        break;
                    }

                    Console.WriteLine(LineNum + ": Invalid token: " + line);
                    return;
                }
            }
        }

        if (NeededLabels.Count != 0) {
            Console.WriteLine("There are jumps with unknown labels:\n" + string.Join("\n", 
                NeededLabels.Select(label => 
                    $"{label.Key}: [{string.Join(", ", label.Value.Select(l => l.LineNum))}]")));
            return;
        }
        
        File.Close();
    }
}
