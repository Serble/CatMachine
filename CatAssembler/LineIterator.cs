using System.Collections;

namespace CatAssembler;

public class LineIterator : IEnumerable<string>, IDisposable {
    public int LineNum => files[^1].LineNum;
    
    private List<InternalLineIterator> files = [];
    
    public LineIterator(string fileName) {
        AddFile(fileName);
    }
    
    public IEnumerator<string> GetEnumerator() {
        InternalLineIterator enumerator = files[^1];
        while (enumerator.File.MoveNext()) {
            yield return enumerator.File.Current;
            enumerator.LineNum++;
            
            enumerator = files[^1];
        }
        
        files.RemoveAt(files.Count - 1);
        enumerator.File.Dispose();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    public void AddFile(string fileName) {
        IEnumerable<string> enumerable = File.ReadLines(fileName);
        
        // ReSharper disable once GenericEnumeratorNotDisposed
        files.Add(new InternalLineIterator(enumerable.GetEnumerator(), 1));
    }
    
    public void Dispose() {
        foreach (InternalLineIterator file in files) {
            file.File.Dispose();
        }
    }
    
    private class InternalLineIterator(IEnumerator<string> file, int lineNum) {
        public readonly IEnumerator<string> File = file;
        public int LineNum = lineNum;
    }
}