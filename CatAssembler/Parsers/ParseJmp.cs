namespace CatAssembler.Parsers;

public static class ParseJmp {
    public static bool Parse(string[] split, byte opCode) {
        if (split.Length != 2 && split.Length != 3) {
            Console.WriteLine(Program.LineNum + ": Wrong argument count for jmp");
            return false;
        }
                    
        Program.File.WriteByte(opCode);

        if (Program.RegisterToId.TryGetValue(split[1].ToLower(), out byte register)) {
            Program.File.WriteByte(register);
                        
            // has register and offset
            if (split.Length == 3) {
                Util.WriteParsed(split[2]);
            }
            else {
                Program.File.Write(BitConverter.GetBytes((uint)0));
            }
        }
        else {
            if (split.Length == 3) {
                Console.WriteLine(Program.LineNum + ": register must be before immediate");
                return false;
            }
                        
            Program.File.WriteByte(0xFF); // no register
            Util.WriteParsed(split[1]);
        }

        return true;
    }
}