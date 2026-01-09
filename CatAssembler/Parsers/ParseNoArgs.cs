namespace CatAssembler.Parsers;

public class ParseNoArgs {
    public static bool Parse(string[] split, byte opCode) {
        if (split.Length != 1) {
            Console.WriteLine(Program.LineNum + ": ret takes 0 arguments");
            return false;
        }
        
        Program.File.WriteByte(opCode);
        return true;
    }
}