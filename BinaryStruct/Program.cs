using static BinaryStruct.ParserBuilder;
using Array = System.Array;

namespace BinaryStruct;


class Program
{
    private static readonly Struct PlayreadyObject = new(
        Int16ul("type"),
        Int16ul("length"),
        Switch("data", ctx => ctx["type"], i => i switch
        {
            1 => UTF16String(string.Empty, ctx => ctx["length"]),
            2 => Bytes(string.Empty, ctx => ctx["length"]),
            3 => Bytes(string.Empty, ctx => ctx["length"]),
            _ => throw new ArgumentOutOfRangeException(nameof(i), i, null)
        })
    );
    
    private static readonly Struct PlayreadyHeader = new(
        Int32ul("length"),
        Int16ul("record_count"),
        Array("records", Child("playreadyObject", PlayreadyObject), ctx => ctx["record_count"])
    );
    
    private static readonly Struct PsshBox = new(
        Int32ub("length"),
        Const("pssh", "pssh"u8.ToArray()),
        Int32ub("fullbox"),
        Bytes("system_id", 16),
        Int32ub("data_length"),
        Child("playreadyHeader", PlayreadyHeader)
    );

    private static readonly Struct SubStruct = new(
        Int16ub("size"),
        ASCIIString("text", ctx => ctx["size"])
    );
    
    private static readonly Struct ExampleStruct = new(
        Int8ub("Int8ub"),
        Int8ul("Int8ul"),
        Int8sb("Int8sb"),
        Int8sl("Int8sl"),
        Int16ub("Int16ub"),
        Int16ul("Int16ul"),
        Int16sb("Int16sb"),
        Int16sl("Int16sl"),
        Int32ub("Int32ub"),
        Int32ul("Int32ul"),
        Int32sb("Int32sb"),
        Int32sl("Int32sl"),
        Int64ub("Int64ub"),
        Int64ul("Int64ul"),
        Int64sb("Int64sb"),
        Int64sl("Int64sl"),
        Bytes("data1", 16),
        Bytes("data2", ctx => ctx["Int8sl"]),
        Const("const", "sometext"u8.ToArray()),
        ASCIIString("asciiString1", 16),
        ASCIIString("asciiString2", ctx => ctx["Int8sl"]),
        UTF8String("utf8String1", 16),
        UTF8String("utf8String2", ctx => ctx["Int8sl"]),
        UTF16String("utf16String1", 18),
        UTF16String("utf16String2", ctx => (sbyte)ctx["Int8sl"] + 2),
        Array("array", Int16ub(string.Empty), ctx =>
        {
            var bytes = ((byte[])ctx["data1"])[^2..];
            Array.Reverse(bytes);
            return BitConverter.ToUInt16(bytes[^2..], 0);
        }),
        Array("array2", Int16ub(string.Empty), 2),
        Child("subStruct", SubStruct),
        Child("subStruct2", () => SubStruct),
        Int8ub("type"),
        Switch("switch", ctx => ctx["type"], i => i switch
        {
            1 => Child(string.Empty, SubStruct),
            _ => throw new ArgumentOutOfRangeException(nameof(i), i, null)
        }),
        Switch("switch2", 2, i => i switch
        {
            1 => Child(string.Empty, SubStruct),
            _ => Bytes(string.Empty, 4)
        }),
        IfThenElse("ifThenElse", ctx => ctx["type"], Bytes(string.Empty, 6), Bytes(string.Empty, 4)),
        IfThenElse("ifThenElse2", false, Bytes(string.Empty, 6), Bytes(string.Empty, 4)),
        If("if", ctx => ctx["type"], Bytes(string.Empty, 6)),
        If("if2", false, Bytes(string.Empty, 4)),
        Range("range", Child(string.Empty, SubStruct), 1, 2),
        Int8ub("min"),
        Int8ub("max"),
        Range("range2", Child(string.Empty, SubStruct), ctx => ctx["min"], ctx => ctx["max"]),
        GreedyRange("greedy", Const(string.Empty, "string"u8.ToArray()))
    );
    
    public static void Main(string[] args)
    {
        using var stream = File.OpenRead(args[0]);
        var result = ExampleStruct.Parse(stream);
        
        Utils.PrintObject(result);

        // using var writeStream = File.Create("output.bin");
        
        var outBytes = ExampleStruct.Build(result);
        Console.WriteLine(outBytes.ToHex());
    }
}