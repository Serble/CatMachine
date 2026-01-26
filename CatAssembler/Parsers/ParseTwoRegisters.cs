namespace CatAssembler.Parsers;

public class ParseTwoRegisters {
    public static bool Parse(string[] split, byte opCode) {
        if (!Program.RegisterToId.TryGetValue(split[1].ToLower(), out byte r0)) {
            Console.WriteLine($"{Program.LineNum}: First argument must be a register");
            return false;
        }
        
        if (!Program.RegisterToId.TryGetValue(split[2].ToLower(), out byte r1)) {
            Console.WriteLine($"{Program.LineNum}: Second argument must be a register");
            return false;
        }
            
        Program.File.WriteByte(opCode);
        Program.File.WriteByte(r0);
        Program.File.WriteByte(r1);
        return true;
    }
}