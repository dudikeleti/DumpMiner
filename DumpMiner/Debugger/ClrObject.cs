using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Runtime;

namespace DumpMiner.Debugger
{
    class ClrObject
    {
        private readonly ulong _objRef;
        private readonly ClrType _type;
        public Lazy<List<ClrObjectModel>> Fields { get; private set; }

        public ClrObject(ulong objRef, ClrType type)
        {
            _objRef = objRef;
            _type = type;
            Fields = new Lazy<List<ClrObjectModel>>(ValueFactory, false);
        }

        private List<ClrObjectModel> ValueFactory()
        {
            return GetValues(_objRef, _type, "", 0, false, new List<ClrObjectModel>());
        }

        private List<ClrObjectModel> GetValues(ulong obj, ClrType type, string baseName, int offset, bool inner, List<ClrObjectModel> values)
        {
            if (type.Name == "System.String")
            {
                object value;
                try
                {
                    value = type.GetValue(obj);
                }
                catch (Exception ex)
                {
                    value = ex.Message;
                }
                values.Add(new ClrObjectModel { Address = obj, BaseName = baseName, TypeName = type.Name, Value = value });
                values.AddRange(type.Fields.Select(field => new ClrObjectModel { Address = obj, BaseName = baseName, FieldName = field.Name, Offset = field.Offset + offset, TypeName = field.Type.Name, Value = field.GetValue(obj, inner).ToString() }));
            }
            else if (type.IsArray)
            {
                int len = type.GetArrayLength(obj);

                if (type.ComponentType == null)
                {
                    try
                    {
                        for (int i = 0; i < len; i++)
                            values.Add(new ClrObjectModel { Address = obj, BaseName = baseName, TypeName = type.Name, Value = type.GetArrayElementValue(obj, i) });
                    }
                    catch{ }
                }
                else if (type.ComponentType.HasSimpleValue)
                {
                    for (int i = 0; i < len; i++)
                        values.Add(new ClrObjectModel { Address = obj, BaseName = baseName, TypeName = type.Name, Value = type.GetArrayElementValue(obj, i) });
                }
                else
                {
                    for (int i = 0; i < len; i++)
                    {
                        ulong arrAddress = type.GetArrayElementAddress(obj, i);

                        foreach (var field in type.ComponentType.Fields)
                        {
                            string value;
                            if (field.HasSimpleValue)
                                value = field.GetValue(arrAddress, inner).ToString();   // an embedded struct
                            else
                                value = field.GetAddress(arrAddress, inner).ToString();

                            values.Add(new ClrObjectModel { Address = obj, BaseName = baseName, FieldName = field.Name, Offset = field.Offset + offset, TypeName = field.Type.Name, Value = value });

                            if (field.ElementType == ClrElementType.Struct)
                                values.AddRange(GetValues(arrAddress, field.Type, baseName + field.Name, offset + field.Offset, true, new List<ClrObjectModel>()));
                        }
                    }
                }
            }
            else
            {
                foreach (var field in type.Fields)
                {
                    ulong addr = field.GetAddress(obj, inner);

                    object value;
                    if (field.HasSimpleValue)
                        try
                        {
                            value = field.GetValue(obj, inner);
                        }
                        catch (Exception)
                        {
                            value = "{Unknown}";
                        }
                    else
                        value = addr;

                    string sValue = value?.ToString() ?? "{Null}";
                    values.Add(new ClrObjectModel { Address = obj, BaseName = baseName, FieldName = field.Name, Offset = field.Offset + offset, TypeName = field.Type.Name, Value = sValue });

                    if (field.ElementType == ClrElementType.Struct)
                        values.AddRange(GetValues(addr, field.Type, baseName + field.Name, offset + field.Offset, true, new List<ClrObjectModel>()));
                }
            }

            return values;
        }

        internal class ClrObjectModel
        {
            public object Address { get; set; }

            public object Value { get; set; }

            public int Offset { get; set; }

            public string TypeName { get; set; }

            public string BaseName { get; set; }

            public string FieldName { get; set; }
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
