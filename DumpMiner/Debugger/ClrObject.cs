using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace DumpMiner.Debugger
{
    class ClrObject
    {
        private static ClrInstanceField? _stringLengthField;
        private readonly int _maxDepth = 10; // Maximum depth to prevent infinite recursion
        private readonly int _maxFields = 200; // Maximum fields to prevent out of memory
        private readonly int _maxArrayElements = 100; // Maximum array elements to prevent out of memory
        private readonly ulong _objRef;
        private readonly ClrType _type;
        private readonly CancellationToken _cancellationToken;
        private int _currentDepth;
        public Lazy<ImmutableList<ClrObjectModel>> Fields { get; }

        public ClrObject(ulong objRef, ClrType type, /*appDomain, threadId,*/ CancellationToken cancellationToken)
        {
            _objRef = objRef;
            _type = type;
            _cancellationToken = cancellationToken;
            Fields = new Lazy<ImmutableList<ClrObjectModel>>(ValueFactory, false);
            SetStringLengthField(type);
        }

        private static void SetStringLengthField(ClrType type)
        {
            if (_stringLengthField != null)
            {
                return;
            }

            var stringType = type.Heap.GetTypeByName("System.String");
            _stringLengthField = stringType?.GetFieldByName("_stringLength") ?? stringType?.GetFieldByName("m_stringLength");
        }

        private ImmutableList<ClrObjectModel> ValueFactory()
        {
            return GetValues(_objRef, _type, string.Empty, _type.Name, 0, false, []).ToImmutableList();
        }

        private IEnumerable<ClrObjectModel> GetValues(ulong obj, ClrType? type, string baseName, string name, ulong offset, bool inner, List<ClrObjectModel> values)
        {
            if (type == null)
            {
                return ImmutableList<ClrObjectModel>.Empty;
            }

            if (_currentDepth >= _maxDepth || values.Count >= _maxFields)
            {
                return values;
            }

            _currentDepth++;

            var firstAppDomain = type.Heap.Runtime.AppDomains.FirstOrDefault();

            if (type.IsArray)
            {
                var array = type.Heap.GetObject(obj).AsArray();
                int len = Math.Min(array.Length, _maxArrayElements);
                if (type.ComponentType == null || type.ComponentType.IsPrimitive)
                {
                    for (int i = 0; i < len; i++)
                    {
                        if (_cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        try
                        {
                            ulong address = type.GetArrayElementAddress(obj, i);
                            var elementType = type.Heap.GetObjectType(address) ?? type.ComponentType;
                            values.AddRange(GetValues(address, elementType, baseName + "." + name, elementType.Name, address - obj, true, []));
                        }
                        catch (OutOfMemoryException)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < len; i++)
                    {
                        if (_cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        try
                        {
                            ulong arrAddress = type.GetArrayElementAddress(obj, i);

                            foreach (var field in type.ComponentType.Fields.Take(_maxFields))
                            {
                                if (_cancellationToken.IsCancellationRequested)
                                {
                                    break;
                                }

                                var value = field.IsPrimitive ? field.ReadObject(arrAddress, inner).ToString() : field.GetAddress(arrAddress, inner).ToString();

                                values.Add(new ClrObjectModel(obj, field.Type, value)
                                {
                                    BaseName = baseName,
                                    FieldName = field.Name,
                                    Offset = (ulong)field.Offset + offset,
                                    MetadataToken = field.Token
                                });

                                if (field.ElementType == ClrElementType.Struct)
                                {
                                    values.AddRange(GetValues(arrAddress, field.Type, baseName + "." + name, field.Name, offset + (ulong)field.Offset, true, []));
                                }
                            }
                        }
                        catch (OutOfMemoryException)
                        {
                            break;
                        }
                    }
                }
            }
            else
            {
                if (type.ElementType == ClrElementType.String)
                {
                    var stringLength = _stringLengthField?.Read<int>(obj, false) ?? 0;
                    string? text = null;
                    if (stringLength > 0)
                    {
                        var content = new byte[stringLength * 2];
                        DebuggerSession.Instance.Runtime.DataTarget.DataReader.Read(obj + 12, content);
                        text = System.Text.Encoding.Unicode.GetString(content);
                    }

                    values.Add(new ClrObjectModel(obj, type, text ?? "null")
                    {
                        BaseName = baseName,
                        FieldName = name,
                        Offset = offset,
                    });
                }
                else if (!type.IsValueType)
                {
                    values.Add(new ClrObjectModel(obj, type, $"0x{obj:X8}")
                    {
                        BaseName = baseName,
                        FieldName = name,
                        Offset = offset
                    });
                }

                foreach (var field in type.Fields.Take(_maxFields))
                {
                    if (_cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        ulong address = field.GetAddress(obj, inner);

                        if (field.IsPrimitive)
                        {
                            values.Add(new ClrObjectModel(address, field.Type, field.ReadObject(obj, inner))
                            {
                                BaseName = type.Name,
                                FieldName = field.Name,
                                MetadataToken = field.Token
                            });
                        }
                        else
                        {
                            if (field.ElementType == ClrElementType.Struct)
                            {
                                values.AddRange(GetValues(address, field.Type, baseName + "." + name, field.Name, offset + (ulong)field.Offset, true, []));
                            }
                            else
                            {
                                values.AddRange(GetValues(address, field.Type, baseName + "." + name, field.Name, offset + (ulong)field.Offset, false, []));
                            }
                        }
                    }
                    catch (OutOfMemoryException)
                    {
                        break;
                    }
                }
            }

            if (firstAppDomain != null)
            {
                try
                {
                    values.AddRange(type.StaticFields.Select(field => new ClrObjectModel(field.GetAddress(firstAppDomain), field.Type, field.ReadObject(firstAppDomain).ToString())
                    {
                        IsStatic = true,
                        BaseName = baseName,
                        FieldName = field.Name,
                        Offset = (ulong)field.Offset + offset,
                        MetadataToken = field.Token
                    }));
                    values.AddRange(type.ThreadStaticFields.Select(field => new ClrObjectModel(obj, field.Type, field.ReadObject(type.Heap.Runtime.Threads.First()).ToString())
                    {
                        IsThreadStatic = true,
                        BaseName = baseName,
                        FieldName = field.Name,
                        Offset = (ulong)field.Offset + offset,
                        MetadataToken = field.Token
                    }));
                }
                catch (OutOfMemoryException)
                {
                }
            }

            return values;
        }

        internal record ClrObjectModel
        {
            public ClrObjectModel(object address, ClrType? type, object? value)
            {
                Address = address;
                Value = value;
                if (type == null)
                {
                    return;
                }

                TypeName = type.Name;
                IsValueType = type.IsValueType;
                MetadataToken = type.MetadataToken;
            }

            public object Address { get; init; }

            public object? Value { get; init; }

            public ulong Offset { get; init; }

            public string? BaseName { get; init; }

            public string? FieldName { get; init; }

            public string? TypeName { get; init; }

            public int MetadataToken { get; init; }

            public bool? IsValueType { get; init; }

            public bool IsStatic { get; init; }

            public bool IsThreadStatic { get; init; }
        }
    }
}
