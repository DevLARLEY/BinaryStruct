using System.Collections;
using System.Text;

namespace BinaryStruct;

public static class Utils
{
    public static string ToHex(this byte[] bytes) => string.Concat(bytes.Select(b => b.ToString("x2")));
    
    static string FormatBytes(byte[] data)
    {
        StringBuilder builder = new StringBuilder();

        foreach (byte b in data)
        {
            if (b >= 32 && b <= 126) // Printable ASCII range
            {
                builder.Append((char)b); // Append as character
            }
            else
            {
                builder.AppendFormat("\\x{0:X2}", b); // Append as hex escape
            }
        }

        return builder.ToString();
    }
    
    public static void PrintObject(object? obj, int indentLevel = 0)
    {
        var indent = new string(' ', indentLevel * 2);

        switch (obj)
        {
            case Dictionary<string, object> dictionary:
                Console.WriteLine($"{indent}Dictionary({dictionary.Count}):");
                foreach (var kvp in dictionary)
                {
                    Console.WriteLine($"{indent}  {kvp.Key}:");
                    PrintObject(kvp.Value, indentLevel + 2);
                }
                break;

            case byte[] bytes:
                Console.WriteLine($"{indent}byte[{bytes.Length}]: \"{FormatBytes(bytes)}\"");
                break;
                
            case IList list:
                Console.WriteLine($"{indent}List({list.Count}):");
                for (var i = 0; i < list.Count; i++)
                {
                    Console.WriteLine($"{indent} --- {i} ---");
                    PrintObject(list[i], indentLevel + 1);
                }
                break;

            default:
                Console.WriteLine($"{indent}{obj}");
                break;
        }
    }
}