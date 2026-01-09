namespace CatAssembler.Parsers;

public class ParseSingleArg {
    // Can only return false if allowImmediate is false
    public static bool Parse(string[] split, byte opCode, bool allowImmediate, int length = 4, Func<uint, ReadOnlySpan<byte>>? transformer = null) {
        if (allowImmediate) {
            if (Program.RegisterToId.TryGetValue(split[1].ToLower(), out byte register)) {
                Program.File.WriteByte(opCode);
                Program.File.WriteByte(register);
            }
            else {
                Program.File.WriteByte((byte)(opCode + 1));
                Util.WriteParsed(split[1], length, transformer);
            }            
        }
        else {
            if (!Program.RegisterToId.TryGetValue(split[1].ToLower(), out byte register)) {
                Console.WriteLine($"{Program.LineNum}: First argument must be a register");
                return false;
            }
            
            Program.File.WriteByte(opCode);
            Program.File.WriteByte(register);
        }

        return true;
    }
}