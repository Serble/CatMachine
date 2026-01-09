using System.Net.Http.Headers;

namespace CatAssembler;

public class Util {
    public static bool Parse32Int(string text, out uint value) {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) {
            return false;
        }

        text = text.Trim();

        if (text.Length >= 3 && text[0] == '\'') {
            if (text[1] == '\\') {
                if (text is not ['\'', '\\', _, '\'']) {
                    return false;
                }
                
                value = text[2] switch {
                    '\'' => '\'',
                    '\"' => '\"',
                    '\\' => '\\',
                    '0'  => '\0',
                    'a'  => '\a',
                    'b'  => '\b',
                    'f'  => '\f',
                    'n'  => '\n',
                    'r'  => '\r',
                    't'  => '\t',
                    'v'  => '\v',
                    's'  => ' ',
                    _ => uint.MaxValue
                };
                
                return value != uint.MaxValue;
            }
            
            if (text is not ['\'', _, '\'']) {
                return false;
            }
            
            value = text[1];
            return true;
        }

        bool negative = text.StartsWith("-");
        if (negative) {
            text = text.Substring(1);
        }

        int numberBase = 10;

        if (text.StartsWith("0b", StringComparison.OrdinalIgnoreCase)) {
            numberBase = 2;
            text = text.Substring(2);
        }
        else if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
            numberBase = 16;
            text = text.Substring(2);
        }
        else if (text.StartsWith("0o", StringComparison.OrdinalIgnoreCase)) {
            numberBase = 8;
            text = text.Substring(2);
        }
        else if (text.StartsWith("0") && text.Length > 1) {
            // legacy octal like 0755
            numberBase = 8;
            text = text.Substring(1);
        }

        try {
            if (negative) {
                value = (uint)(-Convert.ToInt32(text, numberBase));
            }
            else {
                value = Convert.ToUInt32(text, numberBase);
            }
        }
        catch {
            return false;
        }
        
        return true;
    }
    
    public static void WriteParsed(string text, int length = 4, Func<uint, ReadOnlySpan<byte>>? transformer = null) {
        transformer ??= length switch {
            1 => u => (byte[])[(byte)u],
            2 => u => BitConverter.GetBytes((ushort)u),
            4 => u => BitConverter.GetBytes(u),
            _ => throw new ArgumentException("Must have transformer if length is not 1 2 or 4")
        };

        if (!Parse32Int(text, out uint value)) {
            // is a label or invalid
            string labelName = text;
            if (Program.LocalLabels.TryGetValue(labelName, out uint labelAddress) || 
                    Program.Labels.TryGetValue(labelName, out labelAddress)) {
                Program.File.Write(transformer(labelAddress));
                return;
            }

            NeededLabel needed = new((uint)Program.File.Position, transformer, Program.LineNum);
            if (Program.NeededLabels.TryGetValue(labelName, out List<NeededLabel>? labels)) {
                labels.Add(needed);
            }
            else {
                Program.NeededLabels[labelName] = [needed];
            }
            
            Program.File.Write(new byte[length]); // will be replaced later
            return;
        }
        
        Program.File.Write(transformer(value));
    }
}