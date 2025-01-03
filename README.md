# BinaryStruct
Declarative data structures that allow for binary parsing and building
Heavily based on the base functions of the python construct library.

# Examples

## General Usage

```csharp
using static BinaryStruct.ParserBuilder;

var example = new Struct(
    Int16ub("size"),
    Bytes("data", ctx => ctx["size"]),
    Int8ub("count"),
    Array("items", Int16ul(string.Empty), ctx => ctx["count"])
);

// Input data: 001000000000000000000000000000000000040100020003000400

var result = example.Parse(new byte[] { ... });
// OR
// using var stream = File.OpenRead(args[0]);
// var result = example.Parse(stream);

// Results in:
/*
 *  new Dictionary<string, object>{
 *      { "size", (ushort)16 },
 *      { "data", new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 } },
 *      { "count", (byte)4 },
 *      { "items", new List<object>{ 1, 2, 3, 4 },
 *  }
 */

var bytes = example.Build(result);
// Output data: 001000000000000000000000000000000000040100020003000400
```

## Fields

### IntField
```csharp
var example = new Struct(
    Int8ub("Int8ub"),   // big-endian byte
    Int8ul("Int8ul"),   // little-endian byte
    Int8sb("Int8sb"),   // big-endian sbyte
    Int8sl("Int8sl"),   // little-endian sbyte
    Int16ub("Int16ub"), // big-endian ushort
    Int16ul("Int16ul"), // little-endian ushort
    Int16sb("Int16sb"), // big-endian short
    Int16sl("Int16sl"), // little-endian short
    Int32ub("Int32ub"), // big-endian uint
    Int32ul("Int32ul"), // little-endian uint
    Int32sb("Int32sb"), // big-endian int
    Int32sl("Int32sl"), // little-endian int
    Int64ub("Int64ub"), // big-endian ulong
    Int64ul("Int64ul"), // little-endian ulong
    Int64sb("Int64sb"), // big-endian long
    Int64sl("Int64sl")  // little-endian long
);
```

### BytesField
```csharp
var example = new Struct(
    Bytes("data1", 16), // static length
    Bytes("data2", ctx => ctx["Int8sl"]) // get length from field named 'Int8sl'
);
```

### ConstField
```csharp
var example = new Struct(
    Const("const", "CONST"u8.ToArray()) // always expects 'CONST'
);
```

### StringField
```csharp
var example = new Struct(
    ASCIIString("asciiString1", 16), // static length ASCII string
    ASCIIString("asciiString2", ctx => ctx["Int8sl"]), // get length from field named 'Int8sl'
    UTF8String("utf8String1", 16), // static length UTF-8 string
    UTF8String("utf8String2", ctx => ctx["Int8sl"]), // get length from field named 'Int8sl'
    UTF16String("utf16String1", 18), // static length UTF-16/Unicode string
    UTF16String("utf16String2", ctx => (sbyte)ctx["Int8sl"] + 2) // cast for addition, etc...
);
```

### ArrayField
```csharp
var example = new Struct(
    Array("array", Int16ub(string.Empty), ctx => ctx["Int8sl"]), // get count from field named 'Int8sl'
    Array("array2", Int16ub(string.Empty), 2) // static count
);
```

### SwitchField
```csharp
var example = new Struct(
    Switch("switch", ctx => ctx["Int8sl"], i => i switch // switch based on value of field named 'Int8sl'
    {
        1 => Int32ub(string.Empty),
        _ => throw new ArgumentOutOfRangeException(nameof(i), i, null)
    }),
    Switch("switch2", 2, i => i switch // static value
    {
        1 => Int32ub(string.Empty),
        _ => Bytes(string.Empty, 4)
    })
);
```

### ConditionalField
```csharp
var example = new Struct(
    IfThenElse("ifThenElse", ctx => ctx["Int8sl"], Bytes(string.Empty, 6), Bytes(string.Empty, 4)), // basically ctx["Int8sl"] ? Bytes(string.Empty, 6) : Bytes(string.Empty, 4))
    IfThenElse("ifThenElse2", false, Bytes(string.Empty, 6), Bytes(string.Empty, 4)), // basically ctx["Int8sl"] ? Bytes(string.Empty, 6) : Bytes(string.Empty, 4))
    If("if", ctx => ctx["Int8sl"], Bytes(string.Empty, 6)), // writes nothing and returns new object() if ctx["Int8sl"] is false
    If("if2", false, Bytes(string.Empty, 4)) // writes nothing and returns new object()
);
```

### RangeField
```csharp
var example = new Struct(
    Range("range", Int32ub(string.Empty), 1, 2) // will repeat Int32ub two times but only throws an error if it crashes earlier and has read less than one item
    Range("range", Int32ub(string.Empty), 1, ctx => ctx["Int16ub"]) // variable counts
    Range("range", Int32ub(string.Empty), ctx => ctx["Int8sl"], 2) // variable counts
    Range("range", Int32ub(string.Empty), ctx => ctx["Int8sl"], ctx => ctx["Int16ub"]) // variable counts
    GreedyRange("greedy", Const(string.Empty, "string"u8.ToArray())) // reads as much as possible until it crashes (errors are catched, so debugging is difficult)
);
```

### StructField
```csharp
var example = new Struct(
    Child("subStruct", SubStruct) // include another Struct
);
```

```csharp
// Recursiveness
private static readonly Struct Record = new(
    Int16ub("flags"),
    Int16ub("type"),
    Int32ub("length"),
    Switch("data", ctx => ctx["type"], i => i switch 
    {
        1 => Child(string.Empty, SubStruct1),
        2 => Child(string.Empty, SubStruct2),
        _ => Child(string.Empty, () => Record!) // References itself
    })
);
```
