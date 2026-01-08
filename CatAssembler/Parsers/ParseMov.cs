namespace CatAssembler.Parsers;

public static class ParseMov {
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
    
    public static bool Parse(FileStream file, string[] split, int lineNum) {
        if (split.Length != 3) {
            Console.WriteLine("Wrong arg count for mov instruction at line " + lineNum);
            return false;
        }

        // store start index and write to just go forward
        long startIndex = file.Position;
        file.WriteByte(0);
        
        // write the arguments
        (int, int) typeHash = (ParseArg(file, split[1]), ParseArg(file, split[2]));
        if (typeHash.Item1 == -1 || typeHash.Item2 == -1) {
            return false;
        }
        
        // get opcode
        if (!HashToOpcode.TryGetValue(typeHash, out byte opCode)) {
            Console.WriteLine(lineNum + ": not a valid mov operation");
            return false;
        }
        
        // write opcode
        long endIndex = file.Position;
        file.Position = startIndex;
        file.WriteByte(opCode);
        file.Position = endIndex;
        return true;
    }

    private static int ParseArg(FileStream file, string arg) {
        int typeHash;
        
        if (arg[0] != '@') {
            if (Program.RegisterToId.TryGetValue(arg.ToLower(), out byte regId)) {
                typeHash = 0;
                file.WriteByte(regId);
            }
            else {
                typeHash = 1;
                Util.WriteParsed(arg);
            }
        }
        else {
            arg = arg[1..];
            if (Program.RegisterToId.TryGetValue(arg.ToLower(), out byte regId)) {
                typeHash = 2;
                file.WriteByte(regId);
            }
            else {
                typeHash = 3;
                Util.WriteParsed(arg);
            }
        }

        return typeHash;
    }
}