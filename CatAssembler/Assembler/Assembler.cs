namespace CatAssembler.Assembler;

public class Assembler(IOutputSegment[] segments, Dictionary<string, string> constants) {

    public void WriteTo(Stream stream) {
        int startPos = (int)stream.Position;
        foreach (IOutputSegment segment in segments) {
            stream.Write(segment.GetBytes(constants));
        }

        stream.Flush();
        Console.WriteLine("Assembled " + (stream.Position - startPos) + $" bytes from {segments.Length} segments.");
    }
}
