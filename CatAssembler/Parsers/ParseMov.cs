namespace CatAssembler.Parsers;

public static class ParseMov {
    // 0: reg, 1: immediate, 2: reg pointer, 3: imm pointer
    private static Dictionary<(int, int), byte> HashToOpcode = new() {
        {(0, 0), 0},
        {(0, 1), 1},
        {(0, 2), 2},
        {(0, 3), 3},
        {(2, 0), 4},
        {(2, 1), 5},
        {(3, 0), 6},
        {(3, 1), 7}
    };
    
    private static Dictionary<(int, int), byte> HashToSmallOpcodeOffset = new() {
        {(0, 2), 0},
        {(0, 3), 1},
        {(2, 0), 2},
        {(2, 1), 3},
        {(3, 0), 4},
        {(3, 1), 5}
    };
    
    public static bool Parse(string[] split) {
        return DoParse(split, 0, 4, HashToOpcode);
    }

    public static bool ParseSmall(string[] split, byte opCode, int length) {
        return DoParse(split, opCode, length, HashToSmallOpcodeOffset);
    }
    
    private static bool DoParse(string[] split, byte opCode, int length, Dictionary<(int, int), byte> opCodeOffset) {
        if (split.Length != 3) {
            Console.WriteLine($"{Program.LineNum}: Wrong arg count for {split[0]} instruction");
            return false;
        }
        
        // store start index and write to just go forward
        long startIndex = Program.File.Position;
        Program.File.WriteByte(0);
        
        // write the arguments
        (int, int) typeHash = (ParseArg(split[1], length), ParseArg(split[2], length));
        if (typeHash.Item1 == -1 || typeHash.Item2 == -1) {
            return false;
        }
        
        // get opcode
        if (!opCodeOffset.TryGetValue(typeHash, out byte opCodeOff)) {
            Console.WriteLine(Program.LineNum + $": not a valid {split[0]} operation");
            return false;
        }
        
        // write opcode
        long endIndex = Program.File.Position;
        Program.File.Position = startIndex;
        Program.File.WriteByte((byte)(opCode + opCodeOff));
        Program.File.Position = endIndex;
        return true;
    }

    private static int ParseArg(string arg, int length) {
        int typeHash;
        
        if (arg[0] != '@') {
            if (Program.RegisterToId.TryGetValue(arg.ToLower(), out byte regId)) {
                typeHash = 0;
                Program.File.WriteByte(regId);
            }
            else {
                typeHash = 1;
                Util.WriteParsed(arg, length);
            }
        }
        else {
            arg = arg[1..];
            if (Program.RegisterToId.TryGetValue(arg.ToLower(), out byte regId)) {
                typeHash = 2;
                Program.File.WriteByte(regId);
            }
            else {
                typeHash = 3;
                Util.WriteParsed(arg, length);
            }
        }

        return typeHash;
    }
}