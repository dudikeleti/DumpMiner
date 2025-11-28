using Microsoft.Diagnostics.Runtime;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using DumpMiner.Common;

namespace DumpMiner.Debugger
{
    /// <summary>
    /// Enhanced CLR object inspector with comprehensive logging and specialized type handling
    /// </summary>
    class ClrObject
    {
        private readonly ILogger<ClrObject> _logger;
        private readonly ulong _objRef;
        private readonly ClrType _type;
        private readonly CancellationToken _cancellationToken;
        private readonly ClrObjectConfiguration _config;
        private readonly IReadOnlyDictionary<string, ITypeSpecialization> _typeSpecializations;
        private int _currentDepth;

        public Lazy<ImmutableList<ClrObjectModel>> Fields { get; }

        public ClrObject(ulong objRef, ClrType type, CancellationToken cancellationToken, ClrObjectConfiguration config = null)
        {
            _logger = LoggingExtensions.CreateLogger<ClrObject>();
            _objRef = objRef;
            _type = type ?? throw new ArgumentNullException(nameof(type));
            _cancellationToken = cancellationToken;
            _config = config ?? ClrObjectConfiguration.Default;

            _typeSpecializations = CreateTypeSpecializations();
            Fields = new Lazy<ImmutableList<ClrObjectModel>>(ValueFactory, false);

            _logger.LogDebug("ClrObject created for type {TypeName} at address 0x{Address:X}",
                type.Name, objRef);
        }

        private ImmutableList<ClrObjectModel> ValueFactory()
        {
            using var operation = _logger.LogOperation("ClrObjectFieldExtraction", _type.Name, _objRef);

            try
            {
                var values = new List<ClrObjectModel>();
                ExtractObjectFields(_objRef, _type, string.Empty, _type.Name, 0, false, values);

                _logger.LogInformation("Successfully extracted {FieldCount} fields from {TypeName}",
                    values.Count, _type.Name);

                return values.ToImmutableList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract fields from {TypeName} at 0x{Address:X}",
                    _type.Name, _objRef);
                return ImmutableList<ClrObjectModel>.Empty;
            }
        }

        private void ExtractObjectFields(ulong obj, ClrType type, string baseName, string name,
            ulong offset, bool inner, List<ClrObjectModel> values)
        {
            if (!ValidateExtractionLimits(type, values))
                return;

            _currentDepth++;

            try
            {
                _logger.LogDebug("Extracting fields from {TypeName} at depth {Depth}", type.Name, _currentDepth);

                // Handle specialized types first
                if (TryHandleSpecializedType(obj, type, baseName, name, offset, values))
                {
                    _logger.LogDebug("Handled specialized type {TypeName}", type.Name);
                    return;
                }

                // Handle arrays
                if (type.IsArray)
                {
                    HandleArrayType(obj, type, baseName, name, offset, inner, values);
                    return;
                }

                // Handle regular object types
                HandleObjectType(obj, type, baseName, name, offset, inner, values);

                // Handle static fields
                HandleStaticFields(obj, type, baseName, offset, values);
            }
            catch (OutOfMemoryException ex)
            {
                _logger.LogWarning(ex, "Out of memory while processing {TypeName} at 0x{Address:X}",
                    type.Name, obj);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting fields from {TypeName} at 0x{Address:X}",
                    type.Name, obj);
            }
            finally
            {
                _currentDepth--;
            }
        }

        private bool ValidateExtractionLimits(ClrType type, List<ClrObjectModel> values)
        {
            if (type == null)
            {
                _logger.LogWarning("Attempted to extract fields from null type");
                return false;
            }

            if (_currentDepth >= _config.MaxDepth)
            {
                _logger.LogDebug("Reached maximum depth {MaxDepth} for {TypeName}",
                    _config.MaxDepth, type.Name);
                return false;
            }

            if (values.Count >= _config.MaxFields)
            {
                _logger.LogDebug("Reached maximum field limit {MaxFields} for {TypeName}",
                    _config.MaxFields, type.Name);
                return false;
            }

            if (_cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Operation cancelled while processing {TypeName}", type.Name);
                return false;
            }

            return true;
        }

        private bool TryHandleSpecializedType(ulong obj, ClrType type, string baseName,
            string name, ulong offset, List<ClrObjectModel> values)
        {
            if (_typeSpecializations.TryGetValue(type.Name, out var specialization))
            {
                try
                {
                    var result = specialization.ExtractValue(obj, type, _logger);
                    if (result != null)
                    {
                        values.Add(new ClrObjectModel(obj, type, result)
                        {
                            BaseName = baseName,
                            FieldName = name,
                            Offset = offset,
                        });
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Specialized handler failed for {TypeName}", type.Name);
                }
            }

            return false;
        }

        private void HandleArrayType(ulong obj, ClrType type, string baseName, string name,
            ulong offset, bool inner, List<ClrObjectModel> values)
        {
            _logger.LogDebug("Processing array type {TypeName}", type.Name);

            var clrObj = type.Heap.GetObject(obj);
            if (!clrObj.IsValid)
            {
                _logger.LogWarning("Array object os not valid {TypeName} at 0x{Address:X}", type.Name, obj);
                return;
            }

            var array = clrObj.AsArray();

            int len = Math.Min(array.Length, _config.MaxArrayElements);

            _logger.LogDebug("Array length: {Length}, processing: {ProcessingLength}",
                array.Length, len);

            if (type.ComponentType?.IsPrimitive == true)
            {
                HandlePrimitiveArray(obj, type, baseName, name, offset, len, values);
            }
            else
            {
                HandleObjectArray(obj, type, baseName, name, offset, len, values);
            }
        }

        private void HandlePrimitiveArray(ulong obj, ClrType type, string baseName, string name,
            ulong offset, int len, List<ClrObjectModel> values)
        {
            for (int i = 0; i < len && !_cancellationToken.IsCancellationRequested; i++)
            {
                try
                {
                    ulong address = type.GetArrayElementAddress(obj, i);
                    var elementType = type.Heap.GetObjectType(address) ?? type.ComponentType;
                    ExtractObjectFields(address, elementType, baseName + "." + name,
                        elementType.Name, address - obj, true, new List<ClrObjectModel>());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process primitive array element {Index} of {TypeName}",
                        i, type.Name);
                }
            }
        }

        private void HandleObjectArray(ulong obj, ClrType type, string baseName, string name,
            ulong offset, int len, List<ClrObjectModel> values)
        {
            for (int i = 0; i < len && !_cancellationToken.IsCancellationRequested; i++)
            {
                try
                {
                    ulong arrAddress = type.GetArrayElementAddress(obj, i);
                    var fieldsToProcess = type.ComponentType.Fields.Take(_config.MaxFields);

                    foreach (var field in fieldsToProcess)
                    {
                        if (_cancellationToken.IsCancellationRequested) break;

                        ProcessField(arrAddress, field, baseName + "." + name, offset, true, values);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process object array element {Index} of {TypeName}",
                        i, type.Name);
                }
            }
        }

        private void HandleObjectType(ulong obj, ClrType type, string baseName, string name,
            ulong offset, bool inner, List<ClrObjectModel> values)
        {
            if (!type.IsValueType)
            {
                values.Add(new ClrObjectModel(obj, type, $"0x{obj:X8}")
                {
                    BaseName = baseName,
                    FieldName = name,
                    Offset = offset
                });
            }

            var fieldsToProcess = type.Fields.Take(_config.MaxFields);
            foreach (var field in fieldsToProcess)
            {
                if (_cancellationToken.IsCancellationRequested) break;
                ProcessField(obj, field, baseName + "." + name, offset, inner, values);
            }
        }

        private void ProcessField(ulong obj, ClrInstanceField field, string baseName,
            ulong offset, bool inner, List<ClrObjectModel> values)
        {
            try
            {
                ulong address = field.GetAddress(obj, inner);

                if (field.IsPrimitive)
                {
                    values.Add(new ClrObjectModel(address, field.Type, field.ReadObject(obj, inner))
                    {
                        BaseName = _type.Name,
                        FieldName = field.Name,
                        MetadataToken = field.Token
                    });
                }
                else
                {
                    bool isStruct = field.ElementType == ClrElementType.Struct;
                    ExtractObjectFields(address, field.Type, baseName, field.Name,
                        offset + (ulong)field.Offset, isStruct, values);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process field {FieldName} of type {FieldType}",
                    field.Name, field.Type?.Name ?? "Unknown");
            }
        }

        private void HandleStaticFields(ulong obj, ClrType type, string baseName,
            ulong offset, List<ClrObjectModel> values)
        {
            var firstAppDomain = type.Heap.Runtime.AppDomains.FirstOrDefault();
            if (firstAppDomain == null) return;

            try
            {
                // Handle static fields
                values.AddRange(type.StaticFields.Select(field => new ClrObjectModel(
                    field.GetAddress(firstAppDomain), field.Type, field.ReadObject(firstAppDomain).ToString() ?? "null")
                {
                    IsStatic = true,
                    BaseName = baseName,
                    FieldName = field.Name,
                    Offset = (ulong)field.Offset + offset,
                    MetadataToken = field.Token
                }));

                // Handle thread static fields  
                var firstThread = type.Heap.Runtime.Threads.FirstOrDefault();
                if (firstThread != null)
                {
                    values.AddRange(type.ThreadStaticFields.Select(field => new ClrObjectModel(
                        obj, field.Type, field.ReadObject(firstThread).ToString() ?? "null")
                    {
                        IsThreadStatic = true,
                        BaseName = baseName,
                        FieldName = field.Name,
                        Offset = (ulong)field.Offset + offset,
                        MetadataToken = field.Token
                    }));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process static fields for {TypeName}", type.Name);
            }
        }

        private IReadOnlyDictionary<string, ITypeSpecialization> CreateTypeSpecializations()
        {
            return new Dictionary<string, ITypeSpecialization>
            {
                ["System.String"] = new StringSpecialization(),
                ["System.DateTime"] = new DateTimeSpecialization(),
                ["System.Guid"] = new GuidSpecialization(),
                ["System.Decimal"] = new DecimalSpecialization(),
                ["System.TimeSpan"] = new TimeSpanSpecialization(),

                // Note: Additional specializations will be added in separate files
                // to maintain clean architecture and avoid compilation issues
            };
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

    /// <summary>
    /// Advanced configuration for ClrObject field extraction with comprehensive options
    /// </summary>
    public class ClrObjectConfiguration
    {
        // Basic Limits
        public int MaxDepth { get; init; } = 10;
        public int MaxFields { get; init; } = 200;
        public int MaxArrayElements { get; init; } = 100;
        public int MaxStringLength { get; init; } = 1000;
        public int MaxCollectionSample { get; init; } = 50;

        // Performance Options
        public bool EnableParallelProcessing { get; init; } = false;
        public bool EnableFieldCaching { get; init; } = true;
        public bool EnableLazyEvaluation { get; init; } = true;
        public TimeSpan MaxProcessingTime { get; init; } = TimeSpan.FromSeconds(30);

        // Analysis Features
        public bool EnableMemoryAnalysis { get; init; } = false;
        public bool EnableReferenceLoopDetection { get; init; } = true;
        public bool EnableCollectionAnalysis { get; init; } = true;
        public bool EnableSecurityAnalysis { get; init; } = false;
        public bool DetectMemoryLeaks { get; init; } = false;

        // Formatting Options
        public ObjectFormat OutputFormat { get; init; } = ObjectFormat.Structured;
        public bool IncludeMetadata { get; init; } = true;
        public bool IncludeTypeHierarchy { get; init; } = false;
        public bool ShowMemoryAddresses { get; init; } = true;
        public bool ShowObjectSizes { get; init; } = false;
        public bool PrettifyCollections { get; init; } = true;

        // Security & Safety
        public SecurityLevel SecurityLevel { get; init; } = SecurityLevel.Standard;
        public bool AllowUnsafeOperations { get; init; } = false;
        public bool ValidateMemoryIntegrity { get; init; } = true;
        public int MaxRecursionDepth { get; init; } = 50;

        // Advanced Features
        public bool EnableAIAnalysis { get; init; } = false;
        public bool EnablePatternRecognition { get; init; } = false;
        public bool GenerateInsights { get; init; } = false;
        public HashSet<string> CustomTypeFilters { get; init; } = new();
        public Dictionary<string, object> ExtensionSettings { get; init; } = new();

        public static ClrObjectConfiguration Default => new();

        public static ClrObjectConfiguration Conservative => new()
        {
            MaxDepth = 5,
            MaxFields = 50,
            MaxArrayElements = 25,
            MaxStringLength = 200,
            EnableParallelProcessing = false,
            EnableMemoryAnalysis = false,
            SecurityLevel = SecurityLevel.Restricted,
            MaxProcessingTime = TimeSpan.FromSeconds(10)
        };

        public static ClrObjectConfiguration Detailed => new()
        {
            MaxDepth = 15,
            MaxFields = 500,
            MaxArrayElements = 200,
            MaxStringLength = 5000,
            MaxCollectionSample = 100,
            EnableParallelProcessing = true,
            EnableMemoryAnalysis = true,
            EnableCollectionAnalysis = true,
            IncludeTypeHierarchy = true,
            ShowObjectSizes = true,
            EnablePatternRecognition = true,
            SecurityLevel = SecurityLevel.Permissive,
            MaxProcessingTime = TimeSpan.FromMinutes(2)
        };

        public static ClrObjectConfiguration Performance => new()
        {
            MaxDepth = 8,
            MaxFields = 100,
            MaxArrayElements = 50,
            EnableParallelProcessing = true,
            EnableFieldCaching = true,
            EnableLazyEvaluation = true,
            OutputFormat = ObjectFormat.Compact,
            IncludeMetadata = false,
            MaxProcessingTime = TimeSpan.FromSeconds(5)
        };

        public static ClrObjectConfiguration Debug => new()
        {
            MaxDepth = 20,
            MaxFields = 1000,
            MaxArrayElements = 500,
            EnableMemoryAnalysis = true,
            EnableReferenceLoopDetection = true,
            EnableCollectionAnalysis = true,
            EnableSecurityAnalysis = true,
            IncludeTypeHierarchy = true,
            ShowMemoryAddresses = true,
            ShowObjectSizes = true,
            ValidateMemoryIntegrity = true,
            SecurityLevel = SecurityLevel.Debug,
            MaxProcessingTime = TimeSpan.FromMinutes(5)
        };
    }

    public enum ObjectFormat
    {
        Structured,
        Compact,
        JSON,
        XML,
        Custom
    }

    public enum SecurityLevel
    {
        Restricted,    // Very safe, minimal access
        Standard,      // Normal operations  
        Permissive,    // Allow most operations
        Debug          // Full access for debugging
    }

    /// <summary>
    /// Enhanced interface for type-specific value extraction with advanced capabilities
    /// </summary>
    public interface ITypeSpecialization
    {
        string? ExtractValue(ulong address, ClrType type, ILogger logger);
        bool CanHandle(ClrType type);
        int Priority { get; }
        ObjectAnalysis? AnalyzeObject(ulong address, ClrType type, ILogger logger) => null;
    }

    /// <summary>
    /// Advanced object analysis results
    /// </summary>
    public record ObjectAnalysis(
        long Size,
        int FieldCount,
        bool HasIssues,
        string[] Issues,
        Dictionary<string, object>? Metadata = null
    );

    /// <summary>
    /// Specialized handler for System.String
    /// </summary>
    public class StringSpecialization : ITypeSpecialization
    {
        private ClrInstanceField? _stringLengthField;
        private ClrInstanceField? _firstCharField;

        public bool CanHandle(ClrType type) => type.Name == "System.String";
        public int Priority => 100; // High priority for strings

        public string? ExtractValue(ulong address, ClrType type, ILogger logger)
        {
            try
            {
                // First, try to use ClrMD's built-in string reading capabilities
                var clrObject = type.Heap.GetObject(address);
                if (clrObject.IsValid)
                {
                    var stringValue = clrObject.AsString();
                    if (stringValue != null)
                    {
                        logger.LogDebug("Successfully read string using ClrMD built-in method: '{Value}'",
                            stringValue.Length > 50 ? stringValue.Substring(0, 50) + "..." : stringValue);
                        return stringValue;
                    }
                }

                // Fallback to manual field-based extraction if built-in method fails
                return ExtractStringUsingFields(address, type, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to extract string value at address 0x{Address:X}", address);
                return "null";
            }
        }

        private string? ExtractStringUsingFields(ulong address, ClrType type, ILogger logger)
        {
            try
            {
                EnsureStringFields(type, logger);

                if (_stringLengthField == null)
                {
                    logger.LogWarning("Could not locate string length field for address 0x{Address:X}", address);
                    return "null";
                }

                var stringLength = _stringLengthField.Read<int>(address, false);
                logger.LogDebug("String length at 0x{Address:X}: {Length}", address, stringLength);

                if (stringLength <= 0)
                {
                    return string.Empty;
                }

                if (stringLength > 1000000) // Sanity check
                {
                    logger.LogWarning("String length {Length} seems unreasonable at 0x{Address:X}", stringLength, address);
                    return $"<String too large: {stringLength} chars>";
                }

                // Calculate the actual offset to character data using field information
                ulong charDataOffset = CalculateCharacterDataOffset(address, type, logger);
                if (charDataOffset == 0)
                {
                    logger.LogWarning("Could not determine character data offset for string at 0x{Address:X}", address);
                    return "null";
                }

                var content = new byte[stringLength * 2]; // UTF-16, 2 bytes per character
                var dataReader = DebuggerSession.Instance.Runtime.DataTarget.DataReader;

                int bytesRead = dataReader.Read(address + charDataOffset, content);
                if (bytesRead <= 0)
                {
                    logger.LogWarning("Failed to read string content at 0x{Address:X} + {Offset}, expected {Expected} bytes",
                        address, charDataOffset, content.Length);
                    return null;
                }

                if (bytesRead < content.Length)
                {
                    logger.LogDebug("Partial string read at 0x{Address:X}: got {Actual} bytes of {Expected}",
                        address, bytesRead, content.Length);
                    // Resize array to actual bytes read
                    Array.Resize(ref content, bytesRead);
                }

                var result = System.Text.Encoding.Unicode.GetString(content);
                logger.LogDebug("Successfully extracted string: '{Value}'",
                    result.Length > 50 ? result.Substring(0, 50) + "..." : result);

                return result;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Field-based string extraction failed at 0x{Address:X}", address);
                return "null";
            }
        }

        private ulong CalculateCharacterDataOffset(ulong address, ClrType type, ILogger logger)
        {
            try
            {
                // Method 1: Use _firstChar field if available (most reliable)
                if (_firstCharField != null)
                {
                    ulong firstCharAddress = _firstCharField.GetAddress(address, false);
                    ulong offset = firstCharAddress - address;
                    logger.LogDebug("Character data offset calculated using _firstChar field: {Offset}", offset);
                    return offset;
                }

                // Method 2: Calculate based on field layout
                if (_stringLengthField != null)
                {
                    // Character data should start right after the length field
                    ulong lengthFieldEnd = _stringLengthField.GetAddress(address, false) + (ulong)_stringLengthField.Size;
                    ulong offset = lengthFieldEnd - address;
                    logger.LogDebug("Character data offset calculated from length field: {Offset}", offset);
                    return offset;
                }

                // Method 3: Use runtime architecture information
                var pointerSize = DebuggerSession.Instance.Runtime.DataTarget.DataReader.PointerSize;
                ulong estimatedOffset = (ulong)(pointerSize + 4); // Object header + int32 length
                logger.LogDebug("Using estimated offset based on pointer size {PointerSize}: {Offset}",
                    pointerSize, estimatedOffset);
                return estimatedOffset;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to calculate character data offset, using fallback");
                return 12; // Fallback to original hardcoded value as last resort
            }
        }

        private void EnsureStringFields(ClrType type, ILogger logger)
        {
            if (_stringLengthField != null && _firstCharField != null) return;

            try
            {
                var stringType = type.Heap.GetTypeByName("System.String");
                if (stringType == null)
                {
                    logger.LogWarning("Could not find System.String type in heap");
                    return;
                }

                // Find string length field
                if (_stringLengthField == null)
                {
                    _stringLengthField = stringType.GetFieldByName("_stringLength") ??
                                       stringType.GetFieldByName("m_stringLength");

                    if (_stringLengthField == null)
                    {
                        logger.LogWarning("Could not find string length field in System.String type");
                    }
                    else
                    {
                        logger.LogDebug("Found string length field: {FieldName}", _stringLengthField.Name);
                    }
                }

                // Find first character field (for offset calculation)
                if (_firstCharField == null)
                {
                    _firstCharField = stringType.GetFieldByName("_firstChar") ??
                                    stringType.GetFieldByName("m_firstChar");

                    if (_firstCharField != null)
                    {
                        logger.LogDebug("Found first char field: {FieldName}", _firstCharField.Name);
                    }
                    else
                    {
                        logger.LogDebug("Could not find first char field - will calculate offset manually");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while locating string fields");
            }
        }
    }

    /// <summary>
    /// Specialized handler for System.DateTime
    /// </summary>
    public class DateTimeSpecialization : ITypeSpecialization
    {
        public bool CanHandle(ClrType type) => type.Name == "System.DateTime";
        public int Priority => 90;

        public string? ExtractValue(ulong address, ClrType type, ILogger logger)
        {
            try
            {
                var ticksField = type.GetFieldByName("_ticks") ?? type.GetFieldByName("dateData");
                if (ticksField == null)
                {
                    logger.LogDebug("Could not find ticks field in DateTime at 0x{Address:X}", address);
                    return null;
                }

                var ticks = ticksField.Read<long>(address, false);
                var dateTime = new DateTime(ticks & 0x3FFFFFFFFFFFFFFF); // Remove DateTimeKind bits

                return dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to extract DateTime value at 0x{Address:X}", address);
                return null;
            }
        }
    }

    /// <summary>
    /// Specialized handler for System.Guid
    /// </summary>
    public class GuidSpecialization : ITypeSpecialization
    {
        public bool CanHandle(ClrType type) => type.Name == "System.Guid";
        public int Priority => 80;

        public string? ExtractValue(ulong address, ClrType type, ILogger logger)
        {
            try
            {
                // Guid is a 16-byte structure
                var guidBytes = new byte[16];
                var dataReader = DebuggerSession.Instance.Runtime.DataTarget.DataReader;
                if (dataReader.Read(address, guidBytes) <= 0)
                {
                    logger.LogWarning("Failed to read Guid bytes at 0x{Address:X}", address);
                    return null;
                }

                var guid = new Guid(guidBytes);
                return guid.ToString();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to extract Guid value at 0x{Address:X}", address);
                return null;
            }
        }
    }

    /// <summary>
    /// Specialized handler for System.Decimal
    /// </summary>
    public class DecimalSpecialization : ITypeSpecialization
    {
        public bool CanHandle(ClrType type) => type.Name == "System.Decimal";
        public int Priority => 75;

        public string? ExtractValue(ulong address, ClrType type, ILogger logger)
        {
            try
            {
                // Decimal consists of 4 int32 fields: flags, high, low, mid
                var dataReader = DebuggerSession.Instance.Runtime.DataTarget.DataReader;
                var decimalData = new int[4];

                for (int i = 0; i < 4; i++)
                {
                    if (!dataReader.Read(address + (ulong)(i * 4), out decimalData[i]))
                    {
                        logger.LogWarning("Failed to read decimal component {Index} at 0x{Address:X}", i, address);
                        return null;
                    }
                }

                var decimalValue = new decimal(decimalData);
                return decimalValue.ToString();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to extract Decimal value at 0x{Address:X}", address);
                return null;
            }
        }
    }

    /// <summary>
    /// Specialized handler for System.TimeSpan
    /// </summary>
    public class TimeSpanSpecialization : ITypeSpecialization
    {
        public bool CanHandle(ClrType type) => type.Name == "System.TimeSpan";
        public int Priority => 70;

        public string? ExtractValue(ulong address, ClrType type, ILogger logger)
        {
            try
            {
                var ticksField = type.GetFieldByName("_ticks");
                if (ticksField == null)
                {
                    logger.LogDebug("Could not find ticks field in TimeSpan at 0x{Address:X}", address);
                    return null;
                }

                var ticks = ticksField.Read<long>(address, false);
                var timeSpan = new TimeSpan(ticks);

                return timeSpan.ToString();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to extract TimeSpan value at 0x{Address:X}", address);
                return null;
            }
        }
    }
}
