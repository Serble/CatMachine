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
                case "mov": {
                    if (!ParseMov.Parse(File, split, LineNum)) {
                        return;
                    }
                    break;
                }

                case "jmp": {
                    if (split.Length != 2 && split.Length != 3) {
                        Console.WriteLine(LineNum + ": Wrong argument count for jmp");
                        return;
                    }
                    
                    File.WriteByte(0x26);

                    if (RegisterToId.TryGetValue(split[1].ToLower(), out byte register)) {
                        File.WriteByte(register);
                        
                        // has register and offset
                        if (split.Length == 3) {
                            Util.WriteParsed(split[2]);
                        }
                        else {
                            File.Write(BitConverter.GetBytes((uint)0));
                        }
                    }
                    else {
                        if (split.Length == 3) {
                            Console.WriteLine(LineNum + ": register must be before immediate");
                            return;
                        }
                        
                        File.WriteByte(0xFF); // no register
                        Util.WriteParsed(split[1]);
                    }
                    break;
                }

                case "int": {
                    File.WriteByte(0x1b);
                    Util.WriteParsed(split[1]);
                    break;
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
