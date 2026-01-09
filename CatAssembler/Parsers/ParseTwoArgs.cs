namespace CatAssembler.Parsers;

public class ParseTwoArgs {
    public static bool Parse(string[] split, byte opCode, bool allowFirstImmediate) {
        if (split.Length != 3) {
            Console.WriteLine($"{Program.LineNum}: Wrong argument count for {split[0]}");
            return false;
        }
        
        byte opCodeOffset = 0;
        long startPos = Program.File.Position;
        Program.File.WriteByte(0); // will be set later

        byte register;
        if (allowFirstImmediate) {
            if (Program.RegisterToId.TryGetValue(split[1].ToLower(), out register)) {
                Program.File.WriteByte(register);
            }
            else {
                opCodeOffset = 0b10;
                Util.WriteParsed(split[1]);
            }            
        }
        else {
            if (!Program.RegisterToId.TryGetValue(split[1].ToLower(), out register)) {
                Console.WriteLine($"{Program.LineNum}: First argument must be a register");
                return false;
            }
            
            Program.File.WriteByte(register);
        }
        
        if (Program.RegisterToId.TryGetValue(split[2].ToLower(), out register)) {
            Program.File.WriteByte(register);
        }
        else {
            opCodeOffset |= 0b01;
            Util.WriteParsed(split[2]);
        }
        
        // write opcode
        long endPos = Program.File.Position;
        Program.File.Position = startPos;
        Program.File.WriteByte((byte)(opCode + opCodeOffset));
        Program.File.Position = endPos;
        return true;
    }
}