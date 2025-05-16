using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Diagnostics.Runtime;

namespace DumpMiner.Debugger
{
    class ClrObject
    {
        private readonly ulong _objRef;
        private readonly ClrType _type;
        private readonly CancellationToken _cancellationToken;
        public Lazy<List<ClrObjectModel>> Fields { get; }

        public ClrObject(ulong objRef, ClrType type, /*appDomain, threadId,*/ CancellationToken cancellationToken)
        {
            _objRef = objRef;
            _type = type;
            _cancellationToken = cancellationToken;
            Fields = new Lazy<List<ClrObjectModel>>(ValueFactory, false);
        }

        private List<ClrObjectModel> ValueFactory()
        {
            return GetValues(_objRef, _type, "", 0, false, new List<ClrObjectModel>());
        }

        private List<ClrObjectModel> GetValues(ulong obj, ClrType type, string baseName, ulong offset, bool inner, List<ClrObjectModel> values)
        {
            if (type == null)
            {
                throw new ArgumentException("type is null");
            }

            var firstAppDomain = type.Heap?.Runtime?.AppDomains[0];

            if (type.Name == "System.String")
            {
                object value;
                try
                {
                    value = type.Heap.GetObject(obj).AsString();
                }
                catch (Exception ex)
                {
                    value = ex.Message;
                }

                values.Add(new ClrObjectModel { Address = obj, BaseName = baseName, TypeName = type.Name, Value = value, MetadataToken = type.MetadataToken });
                values.AddRange(type.Fields.Select(field => new ClrObjectModel { Address = field.GetAddress(obj), BaseName = baseName, FieldName = field.Name, Offset = (ulong)field.Offset + offset, TypeName = field.Type?.Name ?? "Unknown", Value = field.ReadObject(obj, inner).ToString(), MetadataToken = field.Token }));
            }
            else if (type.IsArray)
            {
                var array = type.Heap.GetObject(obj).AsArray();
                int len = Math.Min(array.Length, 1000000);
                if (type.ComponentType == null || type.ComponentType.IsPrimitive)
                {
                    var typeName = type.ComponentType?.ElementType.ToString();
                    for (int i = 0; i < len; i++)
                    {
                        if (_cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        try
                        {
                            ulong address = type.GetArrayElementAddress(obj, i);
                            values.Add(new ClrObjectModel
                            {
                                Address = address,
                                BaseName = baseName,
                                TypeName = typeName ?? DebuggerSession.Instance.Heap.GetObjectType(address).Name,
                                Value = array.GetObjectValue(i),
                                Offset = address - obj,
                                MetadataToken = type.ComponentType.MetadataToken
                            });

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

                            foreach (var field in type.ComponentType.Fields)
                            {
                                if (_cancellationToken.IsCancellationRequested)
                                {
                                    break;
                                }

                                string value;
                                if (field.IsPrimitive)
                                    value = field.ReadObject(arrAddress, inner).ToString() ?? "null";
                                else
                                    value = field.GetAddress(arrAddress, inner).ToString();

                                values.Add(new ClrObjectModel { Address = obj, BaseName = baseName, FieldName = field.Name, Offset = (ulong)field.Offset + offset, TypeName = field.Type?.Name ?? "Unknown", Value = value });

                                if (field.ElementType == ClrElementType.Struct)
                                    values.AddRange(GetValues(arrAddress, field.Type, baseName + field.Name, offset + (ulong)field.Offset, true, new List<ClrObjectModel>()));
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
                values.Add(new ClrObjectModel { Address = obj, BaseName = baseName, FieldName = string.Empty, Offset = offset, TypeName = type.Name, Value = $"0x{obj:X8}", MetadataToken = type.MetadataToken });

                foreach (var field in type.Fields)
                {
                    try
                    {
                        ulong addr = field.GetAddress(obj, inner);

                        object value;
                        if (field.IsPrimitive)
                            try
                            {
                                value = field.ReadObject(obj, inner);
                                if (!field.IsPrimitive && field.Type?.Name != "System.String" && field.IsObjectReference)
                                {
                                    value = $"0x{(ulong)value:X8}";
                                }
                            }
                            catch (Exception e)
                            {
                                value = $"Error: {e.Message}";
                            }
                        else
                            value = $"0x{addr:X8}";

                        string sValue = value?.ToString() ?? "{Null}";
                        values.Add(new ClrObjectModel { Address = addr, BaseName = baseName, FieldName = field.Name, Offset = (ulong)field.Offset + offset, TypeName = field.Type?.Name ?? "Unknown", Value = sValue, MetadataToken = field.Token });

                        if (field.ElementType == ClrElementType.Struct)
                            values.AddRange(GetValues(addr, field.Type, baseName + field.Name, offset + (ulong)field.Offset, true, new List<ClrObjectModel>()));
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
                    values.AddRange(type.StaticFields.Select(field => new ClrObjectModel { IsStatic = true, Address = field.GetAddress(firstAppDomain), BaseName = baseName, FieldName = field.Name, Offset = (ulong)field.Offset + offset, TypeName = field.Type?.Name ?? "n/a", Value = field.ReadObject(firstAppDomain).ToString() ?? "null", MetadataToken = field.Token }));
                    values.AddRange(type.ThreadStaticFields.Select(field => new ClrObjectModel { IsThreadStatic = true, Address = obj, BaseName = baseName, FieldName = field.Name, Offset = (ulong)field.Offset + offset, TypeName = field.Type?.Name ?? "n/a", Value = field.ReadObject(type.Heap.Runtime.Threads.First()).ToString() ?? "null", MetadataToken = field.Token }));

                }
                catch (OutOfMemoryException)
                {
                }
            }

            return values;
        }

        internal class ClrObjectModel
        {
            public object Address { get; set; }

            public object Value { get; set; }

            public ulong Offset { get; set; }

            public string TypeName { get; set; }

            public string BaseName { get; set; }

            public string FieldName { get; set; }

            public int MetadataToken { get; set; }

            public bool IsStatic { get; set; }

            public bool IsThreadStatic { get; set; }
        }

        //private static ClrInstanceField _stringLengthField = DebuggerSession.Instance.Runtime.GetHeap().GetTypeByName("System.String").GetFieldByName("m_stringLength");
        //    else if (_field.ElementType == ClrElementType.String)
        //    {
        //        var stringLength = (int)_stringLengthField.GetFieldValue(_address);
        //        if (stringLength == 0)
        //            return String.Empty;
        //        var content = new byte[stringLength * 2];
        //        int bytesRead;
        //        DebuggerSession.Instance.Runtime.ReadMemory(_address + 12, content, content.Length, out bytesRead);
        //        return System.Text.Encoding.Unicode.GetString(content);
        //    }
    }
}
