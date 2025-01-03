namespace BinaryStruct;

public abstract class Field(string name)
{
    public string Name { get; } = name;

    protected static void Assert(int length, int size) => _ = length < size ? throw new EndOfStreamException($"Expected {size}, got {length}") : true;
    public abstract object Read(BinaryReader reader, Context context);
    public abstract void Write(BinaryWriter writer, Context context, object value);
}

public class Context
{
    private readonly Dictionary<string, object> _values = new();
        
    public void SetValue(string name, object value) => _values[name] = value;
    public T GetValue<T>(string fieldName)
    {
        if (_values.TryGetValue(fieldName, out var value))
        {
            return (T)value;
        }
        throw new KeyNotFoundException($"Field '{fieldName}' not found in context");
    }
}

public class ContextAccess
{
    private readonly Context _context;

    public ContextAccess(Context context)
    {
        _context = context;
    }

    public object this[string fieldName] => _context.GetValue<object>(fieldName);
}

public class IntField : Field
{
    private readonly int _size;
    private readonly bool _bigEndian;
    private readonly bool _signed;
 
    public IntField(string name, int size, bool bigEndian, bool signed) : base(name)
    {
        _size = size;
        _bigEndian = bigEndian;
        _signed = signed;
    }

    public override object Read(BinaryReader reader, Context context)
    {
        var bytes = reader.ReadBytes(_size);
        Assert(bytes.Length, _size);
        
        if (_bigEndian)
            Array.Reverse(bytes);

        object value = (_size, _signed) switch
        {
            (1, true) => (sbyte)bytes[0],
            (1, false) => bytes[0],
            (2, true) => BitConverter.ToInt16(bytes, 0),
            (2, false) => BitConverter.ToUInt16(bytes, 0),
            (4, true) => BitConverter.ToInt32(bytes, 0),
            (4, false) => BitConverter.ToUInt32(bytes, 0),
            (8, true) => BitConverter.ToInt64(bytes, 0),
            (8, false) => BitConverter.ToUInt64(bytes, 0),
            _ => throw new ArgumentException($"Unsupported byte count: {_size}")
        };
        
        context.SetValue(Name, value);
        return value;
    }
    
    public override void Write(BinaryWriter writer, Context context, object value)
    {
        byte[] bytes = (_size, _signed) switch
        {
            (1, true) => [(byte)Convert.ToSByte(value)],
            (1, false) => [Convert.ToByte(value)],
            (2, true) => BitConverter.GetBytes(Convert.ToInt16(value)),
            (2, false) => BitConverter.GetBytes(Convert.ToUInt16(value)),
            (4, true) => BitConverter.GetBytes(Convert.ToInt32(value)),
            (4, false) => BitConverter.GetBytes(Convert.ToUInt32(value)),
            (8, true) => BitConverter.GetBytes(Convert.ToInt64(value)),
            (8, false) => BitConverter.GetBytes(Convert.ToUInt64(value)),
            _ => throw new ArgumentException($"Unsupported byte count: {_size}")
        };
        
        if (_bigEndian)
            Array.Reverse(bytes);

        writer.Write(bytes);
        context.SetValue(Name, value);
    }
}


public class BytesField : Field
{
    private protected readonly int? Length;
    private protected readonly Func<ContextAccess, object>? LengthExpression;

    public BytesField(string name, int length) : base(name)
    {
        Length = length;
    }

    public BytesField(string name, Func<ContextAccess, object> lengthExpression) : base(name)
    {
        LengthExpression = lengthExpression;
    }

    public override object Read(BinaryReader reader, Context context)
    {
        var length = Length ?? Convert.ToInt32(LengthExpression!(new ContextAccess(context)));
        var value = reader.ReadBytes(length);

        Assert(value.Length, length);
        context.SetValue(Name, value);
        
        return value;
    }

    public override void Write(BinaryWriter writer, Context context, object value)
    {
        var bytes = (byte[])value;
        var length = Length ?? Convert.ToInt32(LengthExpression!(new ContextAccess(context)));

        if (bytes.Length != length)
            throw new InvalidDataException($"Expected {length}, got {bytes.Length}");
        
        writer.Write(bytes);
        context.SetValue(Name, value);
    }
}

public class StringField : BytesField
{
    private readonly Encoding _encoding;
    
    public StringField(string name, int length, Encoding encoding) : base(name, length)
    {
        _encoding = encoding;
    }

    public StringField(string name, Func<ContextAccess, object> lengthExpression, Encoding encoding) : base(name, lengthExpression)
    {
        _encoding = encoding;
    }
    
    public override object Read(BinaryReader reader, Context context)
    {
        var bytes = (byte[])base.Read(reader, context);

        return _encoding switch
        {
            Encoding.Ascii => System.Text.Encoding.ASCII.GetString(bytes),
            Encoding.Utf8 => System.Text.Encoding.UTF8.GetString(bytes),
            Encoding.Utf16 => System.Text.Encoding.Unicode.GetString(bytes),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public override void Write(BinaryWriter writer, Context context, object value)
    {
        var text = (string)value;
        var length = Length ?? Convert.ToInt32(LengthExpression!(new ContextAccess(context)));

        byte[] bytes = _encoding switch
        {
            Encoding.Ascii => System.Text.Encoding.ASCII.GetBytes(text),
            Encoding.Utf8 => System.Text.Encoding.UTF8.GetBytes(text),
            Encoding.Utf16 => System.Text.Encoding.Unicode.GetBytes(text),
            _ => throw new ArgumentOutOfRangeException()
        };
            
        if (bytes.Length != length)
            throw new InvalidDataException($"Expected {length}, got {bytes.Length}");
        
        writer.Write(bytes);
        context.SetValue(Name, value);
    }
}


public class ConstField : Field
{
    private readonly byte[] _expected;

    public ConstField(string name, byte[] expected) : base(name)
    {
        _expected = expected;
    }

    public override object Read(BinaryReader reader, Context context)
    {
        var actual = reader.ReadBytes(_expected.Length);

        Assert(actual.Length, _expected.Length);
        if (!actual.SequenceEqual(_expected))
            throw new InvalidDataException($"Expected constant {_expected.ToHex()} but got {actual.ToHex()}");
            
        context.SetValue(Name, actual);
        return actual;
    }

    public override void Write(BinaryWriter writer, Context context, object value)
    {
        var bytes = (byte[])value;

        Assert(bytes.Length, _expected.Length);
        if (!bytes.SequenceEqual(_expected))
            throw new InvalidDataException($"Expected constant {_expected.ToHex()} but got {bytes.ToHex()}");
        
        writer.Write(bytes);
        context.SetValue(Name, value);
    }
}

public class ArrayField : Field
{
    private readonly Field _field;
    private readonly int? _count;
    private readonly Func<ContextAccess, object>? _countExpression;

    public ArrayField(string name, Field field, int count) : base(name)
    {
        _field = field;
        _count = count;
    }

    public ArrayField(string name, Field field, Func<ContextAccess, object> countExpression) : base(name)
    {
        _field = field;
        _countExpression = countExpression;
    }

    public override object Read(BinaryReader reader, Context context)
    {
        var count = _count ?? Convert.ToInt32(_countExpression!(new ContextAccess(context)));
        var results = new List<object>();

        for (var i = 0; i < count; i++)
        {
            var value = _field.Read(reader, context);
            results.Add(value);
        }
        Assert(results.Count, count);
        
        context.SetValue(Name, results);
        return results;
    }

    public override void Write(BinaryWriter writer, Context context, object value)
    {
        var count = _count ?? Convert.ToInt32(_countExpression!(new ContextAccess(context)));
        var results = new List<object>();

        for (var i = 0; i < count; i++)
        {
            var item = ((List<object>)value)[i];
            _field.Write(writer, context, item);
            results.Add(item);
        }
        Assert(results.Count, count);
        
        context.SetValue(Name, value);
    }
}


public class StructField : Field
{
    private readonly Struct? _nestedStruct;
    private readonly Func<Struct>? _nestedStructExpression;

    public StructField(string name, Struct nestedStruct) : base(name)
    {
        _nestedStruct = nestedStruct;
    }
    
    public StructField(string name, Func<Struct> nestedStructExpression) : base(name)
    {
        _nestedStructExpression = nestedStructExpression;
    }

    public override object Read(BinaryReader reader, Context context)
    {
        var nestedStruct = _nestedStruct ?? _nestedStructExpression!();
        
        var nestedData = nestedStruct.Parse(reader, context);
        context.SetValue(Name, nestedData);
        return nestedData;
    }
    
    public override void Write(BinaryWriter writer, Context context, object value)
    {
        var nestedStruct = _nestedStruct ?? _nestedStructExpression!();

        nestedStruct.Build(writer, context, (Dictionary<string, object>)value);
        context.SetValue(Name, value);
    }
}


public class SwitchField : Field
{
    private readonly int? _index;
    private readonly Func<ContextAccess, object>? _indexExpression;
    private readonly Func<int, Field> _switchExpression;

    public SwitchField(string name, int index, Func<int, Field> switchExpression) : base(name)
    {
        _index = index;
        _switchExpression = switchExpression;
    }

    public SwitchField(string name, Func<ContextAccess, object> indexExpression, Func<int, Field> switchExpression) : base(name)
    {
        _indexExpression = indexExpression;
        _switchExpression = switchExpression;
    }

    public override object Read(BinaryReader reader, Context context)
    {
        var index = _index ?? Convert.ToInt32(_indexExpression!(new ContextAccess(context)));
        var field = _switchExpression(index);
        
        if(field == null)
            throw new InvalidOperationException($"No case found for index {index} in Switch {Name}");

        var value = field.Read(reader, context);
        context.SetValue(Name, value);
        return value;
    }

    public override void Write(BinaryWriter writer, Context context, object value)
    {
        var index = _index ?? Convert.ToInt32(_indexExpression!(new ContextAccess(context)));
        var field = _switchExpression(index);
        
        if(field == null)
            throw new InvalidOperationException($"No case found for index {index} in Switch {Name}");

        field.Write(writer, context, value);
        context.SetValue(Name, value);
    }
}


public class ConditionalField : Field
{
    private readonly Field _thenField;
    private readonly Field _elseField;
    private readonly bool? _condition;
    private readonly Func<ContextAccess, object>? _conditionExpression;

    public ConditionalField(string name, bool condition, Field thenField, Field elseField) : base(name)
    {
        _condition = condition;
        _thenField = thenField;
        _elseField = elseField;
    }

    public ConditionalField(string name, Func<ContextAccess, object> conditionExpression, Field thenField, Field elseField) : base(name)
    {
        _conditionExpression = conditionExpression;
        _thenField = thenField;
        _elseField = elseField;
    }

    public override object Read(BinaryReader reader, Context context)
    {
        var condition = _condition ?? Convert.ToBoolean(_conditionExpression!(new ContextAccess(context)));
        var value = condition ? _thenField.Read(reader, context) : _elseField.Read(reader, context);
        
        context.SetValue(Name, value);
        return value;
    }

    public override void Write(BinaryWriter writer, Context context, object value)
    {
        var condition = _condition ?? Convert.ToBoolean(_conditionExpression!(new ContextAccess(context)));

        if (condition)
        {
            _thenField.Write(writer, context, value);
        }
        else
        {
            _elseField.Write(writer, context, value);
        }

        context.SetValue(Name, value);
    }
}


public class RangeField : Field
{
    private readonly Field _field;
    private readonly int? _min;
    private readonly Func<ContextAccess, object>? _minExpression;
    private readonly int? _max;
    private readonly Func<ContextAccess, object>? _maxExpression;
    
    public RangeField(string name, Field field, int min, int max) : base(name)
    {
        _field = field;
        _min = min;
        _max = max;
    }
    
    public RangeField(string name, Field field, Func<ContextAccess, object> minExpression, int max) : base(name)
    {
        _field = field;
        _minExpression = minExpression;
        _max = max;
    }
    
    public RangeField(string name, Field field, int min, Func<ContextAccess, object> maxExpression) : base(name)
    {
        _field = field;
        _min = min;
        _maxExpression = maxExpression;
    }
    
    public RangeField(string name, Field field, Func<ContextAccess, object> minExpression, Func<ContextAccess, object> maxExpression) : base(name)
    {
        _field = field;
        _minExpression = minExpression;
        _maxExpression = maxExpression;
    }

    public override object Read(BinaryReader reader, Context context)
    {
        var min = _min ?? Convert.ToInt32(_minExpression!(new ContextAccess(context)));
        var max = _max ?? Convert.ToInt32(_maxExpression!(new ContextAccess(context)));

        var results = new List<object>();
        while (results.Count < max)
        {
            var pos = reader.BaseStream.Position;
            try
            {
                var value = _field.Read(reader, context);
                results.Add(value);
            }
            catch (Exception)
            {
                reader.BaseStream.Position = pos;
                break;
            }
        }
        
        Assert(results.Count, min);
        context.SetValue(Name, results);

        return results;
    }

    public override void Write(BinaryWriter writer, Context context, object value)
    {
        var min = _min ?? Convert.ToInt32(_minExpression!(new ContextAccess(context)));
        var max = _max ?? Convert.ToInt32(_maxExpression!(new ContextAccess(context)));
        var items = (List<object>)value;
        
        if (!(min <= items.Count && items.Count <= max))
            throw new Exception($"Expected from {min} to {max} elements, found {items.Count}");
        
        var results = new List<object>();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[results.Count];
            _field.Write(writer, context, item);
            results.Add(value);
        }

        Assert(results.Count, min);
        context.SetValue(Name, results);
    }
}


public class PassField(string name) : Field(name)
{
    public override object Read(BinaryReader reader, Context context)
    {
        return new object();
    }
    
    public override void Write(BinaryWriter writer, Context context, object value) { } 
}

public class Struct
{
    private readonly List<Field> _fields = [];

    public Struct(params Field[] fields)
    {
        _fields.AddRange(fields);
    }

    public Dictionary<string, object> Parse(byte[] data)
    {
        using var stream = new MemoryStream(data);
        return Parse(stream);
    }

    public Dictionary<string, object> Parse(Stream stream)
    {
        using var reader = new BinaryReader(stream);
        return Parse(reader, new Context());
    }
    
    public Dictionary<string, object> Parse(BinaryReader reader, Context context)
    {
        var result = new Dictionary<string, object>();

        foreach (var field in _fields)
        {
            var value = field.Read(reader, context);
            result[field.Name] = value;
        }

        return result;
    }

    public byte[] Build(Dictionary<string, object> values)
    {
        using var memoryStream = new MemoryStream();
        Build(memoryStream, values);
        return memoryStream.ToArray();
    }
    
    public void Build(Stream stream, Dictionary<string, object> values)
    {
        using var writer = new BinaryWriter(stream);
        Build(writer, new Context(), values);
    }
    
    public void Build(BinaryWriter writer, Context context, Dictionary<string, object> values)
    {
        foreach (Field field in _fields.Where(field => !string.IsNullOrEmpty(field.Name)))
        {
            if (!values.TryGetValue(field.Name, out var value))
                throw new KeyNotFoundException($"No value provided for Field '{field.Name}'");

            field.Write(writer, context, value);
        }
        writer.Flush();
    }
}

public static class ParserBuilder
{
    public static IntField Int8sb(string name) => new(name, 1, true, true);
    public static IntField Int8sl(string name) => new(name, 1, false, true);
    public static IntField Int16sb(string name) => new(name, 2, true, true);
    public static IntField Int16sl(string name) => new(name, 2, false, true);
    public static IntField Int32sb(string name) => new(name, 4, true, true);
    public static IntField Int32sl(string name) => new(name, 4, false, true);
    public static IntField Int64sb(string name) => new(name, 8, true, true);
    public static IntField Int64sl(string name) => new(name, 8, false, true);
    
    public static IntField Int8ub(string name) => new(name, 1, true, false);
    public static IntField Int8ul(string name) => new(name, 1, false, false);
    public static IntField Int16ub(string name) => new(name, 2, true, false);
    public static IntField Int16ul(string name) => new(name, 2, false, false);
    public static IntField Int32ub(string name) => new(name, 4, true, false);
    public static IntField Int32ul(string name) => new(name, 4, false, false);
    public static IntField Int64ub(string name) => new(name, 8, true, false);
    public static IntField Int64ul(string name) => new(name, 8, false, false);
    
    public static BytesField Bytes(string name, int length) => new(name, length);
    public static BytesField Bytes(string name, Func<ContextAccess, object> lengthExpression) => new(name, lengthExpression);
    
    public static ConstField Const(string name, byte[] expected) => new(name, expected);
    
    public static StringField ASCIIString(string name, int length) => new(name, length, Encoding.Ascii);
    public static StringField ASCIIString(string name, Func<ContextAccess, object> lengthExpression) => new(name, lengthExpression, Encoding.Ascii);
    public static StringField UTF8String(string name, int length) => new(name, length, Encoding.Utf8);
    public static StringField UTF8String(string name, Func<ContextAccess, object> lengthExpression) => new(name, lengthExpression, Encoding.Utf8);
    public static StringField UTF16String(string name, int length) => new(name, length, Encoding.Utf16);
    public static StringField UTF16String(string name, Func<ContextAccess, object> lengthExpression) => new(name, lengthExpression, Encoding.Utf16);

    public static ArrayField Array(string name, Field element, int count) => new(name, element, count);
    public static ArrayField Array(string name, Field element, Func<ContextAccess, object> countExpression) => new(name, element, countExpression);

    public static StructField Child(string name, Struct nestedStruct) => new(name, nestedStruct);
    public static StructField Child(string name, Func<Struct> nestedStructExpression) => new(name, nestedStructExpression);

    public static SwitchField Switch(string name, int index, Func<int, Field> switchExpression) => new(name, index, switchExpression);
    public static SwitchField Switch(string name, Func<ContextAccess, object> indexExpression, Func<int, Field> switchExpression) => new(name, indexExpression, switchExpression);

    public static ConditionalField IfThenElse(string name, bool condition, Field thenField, Field elseField) => new(name, condition, thenField, elseField);
    public static ConditionalField IfThenElse(string name, Func<ContextAccess, object> conditionExpression, Field thenField, Field elseField) => new(name, conditionExpression, thenField, elseField);
    
    public static ConditionalField If(string name, bool condition, Field thenField) => new(name, condition, thenField, new PassField(thenField.Name));
    public static ConditionalField If(string name, Func<ContextAccess, object> conditionExpression, Field thenField) => new(name, conditionExpression, thenField, new PassField(thenField.Name));

    public static RangeField Range(string name, Field field, int min, int max) => new(name, field, min, max);
    public static RangeField Range(string name, Field field, Func<ContextAccess, object> minExpression, int max) => new(name, field, minExpression, max);
    public static RangeField Range(string name, Field field, int min, Func<ContextAccess, object> maxExpression) => new(name, field, min, maxExpression);
    public static RangeField Range(string name, Field field, Func<ContextAccess, object> minExpression, Func<ContextAccess, object> maxExpression) => new(name, field, minExpression, maxExpression);

    public static RangeField GreedyRange(string name, Field field) => new(name, field, 0, int.MaxValue);
}
