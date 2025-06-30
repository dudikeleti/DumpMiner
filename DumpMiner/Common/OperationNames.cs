using System.Collections.Generic;
using System.Linq;

namespace DumpMiner.Common
{
    public class OperationNames
    {
        public const string DumpArrayItem = "DumpArrayItemOperation";
        public const string DumpClrStack = "DumpClrStackOperation";
        public const string DumpDelegateMethod = "DumpDelegateMethodOperation";
        public const string DumpDisassemblyMethod = "DumpDisassemblyMethodOperation";
        public const string DumpExceptions = "DumpExceptionsOperation";
        public const string DumpFinalizerQueue = "DumpFinalizerQueueOperation";
        public const string DumpGcHandles = "DumpGcHandlesOperation";
        public const string DumpHeap = "DumpHeapOperation";
        public const string DumpHeapSegments = "DumpHeapSegmentsOperation";
        public const string DumpHeapStat = "DumpHeapStatOperation";
        public const string DumpLargeObjects = "DumpLargeObjectsOperation";
        public const string DumpMemoryRegions = "DumpMemoryRegionsOperation";
        public const string DumpMethods = "DumpMethodsOperation";
        public const string DumpModules = "DumpModulesOperation";
        public const string DumpObject = "DumpObjectOperation";
        public const string DumpObjectToDisk = "DumpObjectToDiscOperation";
        public const string DumpSourceCode = "DumpSourceCodeOperation";
        public const string DumpSyncBlock = "DumpSyncBlockOperation";
        public const string DumpTypeInfo = "DumpTypeInfoOperation";
        public const string GetObjectRoot = "GetObjectRootOperation";
        public const string GetObjectSize = "GetObjectSizeOperation";
        public const string TargetProcessInfo = "TargetProcessInfoOperation";
        public const string TypeFromHandle = "TypeFromHandleOperation";

        // === NEW STATE-OF-THE-ART OPERATIONS ===

        // Advanced Symbol & Source Analysis
        public const string SymbolAnalysis = "SymbolAnalysisOperation";
        public const string SourceMapping = "SourceMappingOperation";
        public const string PdbAnalysis = "PdbAnalysisOperation";

        // Advanced Memory Analysis
        public const string MemoryLeakDetection = "MemoryLeakDetectionOperation";
        public const string MemoryFragmentation = "MemoryFragmentationOperation";
        public const string GenerationAnalysis = "GenerationAnalysisOperation";
        public const string MemoryPressureAnalysis = "MemoryPressureAnalysisOperation";
        public const string VirtualMemoryAnalysis = "VirtualMemoryAnalysisOperation";

        // Advanced Threading & Synchronization
        public const string DeadlockDetection = "DeadlockDetectionOperation";
        public const string ThreadContentionAnalysis = "ThreadContentionAnalysisOperation";
        public const string LockAnalysis = "LockAnalysisOperation";
        public const string ThreadPerformanceAnalysis = "ThreadPerformanceAnalysisOperation";

        // Performance Analysis
        public const string JitAnalysis = "JitAnalysisOperation";
        public const string HotspotAnalysis = "HotspotAnalysisOperation";
        public const string AssemblyAnalysis = "AssemblyAnalysisOperation";
        public const string GCAnalysis = "GCAnalysisOperation";

        // Advanced Heap Analysis
        public const string ReferenceGraphAnalysis = "ReferenceGraphAnalysisOperation";
        public const string MemoryUsagePattern = "MemoryUsagePatternOperation";
        public const string ObjectLifecycleAnalysis = "ObjectLifecycleAnalysisOperation";

        // Comparison & Reporting
        public const string DumpComparison = "DumpComparisonOperation";
        public const string TrendAnalysis = "TrendAnalysisOperation";
        public const string PerformanceReport = "PerformanceReportOperation";

        // Automation & Scripting
        public const string AutomatedAnalysis = "AutomatedAnalysisOperation";
        public const string ScriptExecution = "ScriptExecutionOperation";
        public const string BatchAnalysis = "BatchAnalysisOperation";

        // Visualization
        public const string MemoryMapVisualization = "MemoryMapVisualizationOperation";
        public const string ReferenceGraphVisualization = "ReferenceGraphVisualizationOperation";
        public const string TimelineAnalysis = "TimelineAnalysisOperation";

        private static readonly System.Lazy<Dictionary<string, string>> _operationDescriptions =
            new System.Lazy<Dictionary<string, string>>(BuildOperationDescriptions);

        /// <summary>
        /// Gets a dictionary containing operation names and their descriptions.
        /// </summary>
        /// <returns>Dictionary mapping operation names to their descriptions</returns>
        public static Dictionary<string, string> GetOperationDescriptions() => _operationDescriptions.Value;

        /// <summary>
        /// Formats the operation descriptions as structured text for AI model consumption.
        /// This provides context about available debugging operations and their purposes.
        /// </summary>
        /// <returns>Formatted text describing all available operations</returns>
        public static string GetOperationDescriptionsAsText()
        {
            var descriptions = GetOperationDescriptions();
            var text = new System.Text.StringBuilder();

            text.AppendLine("=== AVAILABLE DEBUGGING OPERATIONS ===");
            text.AppendLine();
            text.AppendLine("The following operations are available for memory dump analysis:");
            text.AppendLine();

            foreach (var kvp in descriptions.OrderBy(x => x.Key))
            {
                text.AppendLine($"• {kvp.Key}:");
                text.AppendLine($"  {kvp.Value}");
                text.AppendLine();
            }

            text.AppendLine("=== OPERATION USAGE NOTES ===");
            text.AppendLine("- Operations marked 'similar to WinDbg' correspond to equivalent WinDbg SOS extension commands");
            text.AppendLine("- Memory addresses should be provided in hexadecimal format (0x...)");
            text.AppendLine("- Some operations require specific object types or memory addresses as input");
            text.AppendLine("- Large heap operations may take considerable time depending on dump size");

            return text.ToString();
        }

        /// <summary>
        /// Builds the operation descriptions dictionary
        /// </summary>
        private static Dictionary<string, string> BuildOperationDescriptions()
        {
            return new Dictionary<string, string>
            {
                // === EXISTING OPERATIONS ===
                [DumpArrayItem] = "Dumps the contents of an array item at a specific index, showing individual elements and their values",
                [DumpClrStack] = "Displays the managed call stack for all threads, similar to WinDbg's !clrstack command",
                [DumpDelegateMethod] = "Examines delegate objects and displays information about the methods they point to",
                [DumpDisassemblyMethod] = "Shows the disassembled native or JIT-compiled code for a specific method",
                [DumpExceptions] = "Lists all exception objects currently in the managed heap, similar to !dumpheap -type Exception",
                [DumpFinalizerQueue] = "Displays objects waiting in the finalization queue, similar to WinDbg's !finalizequeue command",
                [DumpGcHandles] = "Lists all GC handles (strong, weak, pinned references), similar to WinDbg's !gchandles command",
                [DumpHeap] = "Displays objects on the managed heap with addresses, sizes, and types, similar to WinDbg's !dumpheap command",
                [DumpHeapSegments] = "Shows the layout and information about heap segments, similar to WinDbg's !eeheap command",
                [DumpHeapStat] = "Provides statistics about objects on the heap grouped by type, similar to !dumpheap -stat",
                [DumpLargeObjects] = "Lists objects in the Large Object Heap (LOH), typically objects larger than 85KB",
                [DumpMemoryRegions] = "Displays information about different memory regions and their usage, similar to !address",
                [DumpMethods] = "Shows method information including method tables and IL code, similar to !dumpmt -md",
                [DumpModules] = "Lists all loaded modules in the process, similar to WinDbg's !lm command",
                [DumpObject] = "Displays detailed information about a specific object instance, similar to WinDbg's !do command",
                [DumpObjectToDisk] = "Exports object data to disk for further analysis or backup purposes",
                [DumpSourceCode] = "Retrieves and displays the source code for methods when debugging information is available",
                [DumpSyncBlock] = "Shows synchronization block information for objects with locks, similar to !syncblk",
                [DumpTypeInfo] = "Displays detailed type information including method tables, similar to !dumpmt",
                [GetObjectRoot] = "Finds the root references keeping an object alive, similar to WinDbg's !gcroot command",
                [GetObjectSize] = "Calculates the size of an object including referenced objects, similar to !objsize",
                [TargetProcessInfo] = "Provides general information about the target process being analyzed",
                [TypeFromHandle] = "Resolves type information from a runtime type handle or method table pointer",

                // === NEW STATE-OF-THE-ART OPERATIONS ===

                // Advanced Symbol & Source Analysis
                [SymbolAnalysis] = "Analyzes symbol information, PDB files, and debugging metadata for comprehensive source mapping",
                [SourceMapping] = "Maps IL code to source code using available debugging information and symbol servers",
                [PdbAnalysis] = "Analyzes PDB files for debugging information, source mappings, and symbol resolution",

                // Advanced Memory Analysis
                [MemoryLeakDetection] = "Sophisticated memory leak detection using reference graph analysis and generation comparison",
                [MemoryFragmentation] = "Analyzes heap fragmentation patterns and memory layout inefficiencies",
                [GenerationAnalysis] = "Detailed analysis of GC generations and object promotion patterns",
                [MemoryPressureAnalysis] = "Analyzes memory pressure indicators and GC stress patterns",
                [VirtualMemoryAnalysis] = "Comprehensive virtual memory analysis including reserved, committed, and free regions",

                // Advanced Threading & Synchronization
                [DeadlockDetection] = "Advanced deadlock detection using thread state analysis and lock chain examination",
                [ThreadContentionAnalysis] = "Analyzes thread contention patterns and synchronization bottlenecks",
                [LockAnalysis] = "Comprehensive lock analysis including lock hierarchy and potential deadlock scenarios",
                [ThreadPerformanceAnalysis] = "Analyzes thread performance patterns including CPU usage and blocking time",

                // Performance Analysis
                [JitAnalysis] = "Analyzes JIT compilation statistics, method compilation status, and optimization patterns",
                [HotspotAnalysis] = "Identifies performance hotspots and frequently executed code paths",
                [AssemblyAnalysis] = "Analyzes assembly loading patterns, dependencies, and version conflicts",
                [GCAnalysis] = "Comprehensive garbage collection analysis including pressure, frequency, and impact",

                // Advanced Heap Analysis
                [ReferenceGraphAnalysis] = "Analyzes object reference graphs to identify circular references and memory retention patterns",
                [MemoryUsagePattern] = "Identifies memory usage patterns and allocation behaviors for different object types",
                [ObjectLifecycleAnalysis] = "Analyzes object lifecycle patterns including creation, usage, and disposal",

                // Comparison & Reporting
                [DumpComparison] = "Compares multiple memory dumps to identify changes, trends, and progressive issues",
                [TrendAnalysis] = "Analyzes trends across multiple dumps to identify progressive memory leaks or performance degradation",
                [PerformanceReport] = "Generates comprehensive performance reports with actionable recommendations",

                // Automation & Scripting
                [AutomatedAnalysis] = "Performs automated analysis with intelligent issue detection and root cause identification",
                [ScriptExecution] = "Executes custom analysis scripts with full access to dump data and operations",
                [BatchAnalysis] = "Performs batch analysis of multiple dumps with comparative reporting",

                // Visualization
                [MemoryMapVisualization] = "Creates visual representations of memory layout and usage patterns",
                [ReferenceGraphVisualization] = "Generates visual graphs of object references and memory relationships",
                [TimelineAnalysis] = "Creates timeline visualizations of application behavior and memory usage"
            };
        }
    }
}
