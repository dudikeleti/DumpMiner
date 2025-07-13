using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;

namespace DumpMiner.Common
{
    /// <summary>
    /// Service providing comprehensive help content for all operations
    /// </summary>
    [Export(typeof(IHelpService))]
    public class HelpService : IHelpService
    {
        private readonly Dictionary<string, OperationHelpContent> _helpContent;

        public HelpService()
        {
            _helpContent = BuildHelpContent();
        }

        public OperationHelpContent GetOperationHelp(string operationName)
        {
            return _helpContent.TryGetValue(operationName, out var content) 
                ? content 
                : new OperationHelpContent 
                { 
                    OperationName = operationName,
                    DisplayName = operationName,
                    Description = "Help content not available for this operation."
                };
        }

        public Dictionary<string, OperationHelpContent> GetAllOperationHelp()
        {
            return new Dictionary<string, OperationHelpContent>(_helpContent);
        }

        public List<OperationHelpContent> SearchHelp(string keywords)
        {
            if (string.IsNullOrWhiteSpace(keywords))
                return new List<OperationHelpContent>();

            var searchTerms = keywords.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            return _helpContent.Values.Where(help =>
                searchTerms.Any(term =>
                    help.DisplayName.ToLower().Contains(term) ||
                    help.Description.ToLower().Contains(term) ||
                    help.WhenToUse?.ToLower().Contains(term) == true ||
                    help.CommonScenarios.Any(scenario => scenario.ToLower().Contains(term))
                )).ToList();
        }

        public string GetGettingStartedGuide()
        {
            return @"# 🚀 Getting Started with DumpMiner

## 1. Loading a Dump or Attaching to a Process

### Loading a Memory Dump
- Go to the **Attach/Detach** tab
- Click **Load Dump** and select your .dmp file
- Wait for the analysis to complete

### Attaching to a Live Process
- Go to the **Attach/Detach** tab
- Find your process in the list
- Click **Attach** to connect to the live process

## 2. Understanding Operation Categories

### Core Analysis Operations
- **DumpHeap**: Shows all objects in memory - start here for memory analysis
- **DumpClrStack**: Shows call stacks for all threads - essential for crash analysis
- **DumpExceptions**: Lists all exceptions - crucial for debugging crashes

### Object Investigation
- **DumpObject**: Examine specific objects in detail
- **GetObjectRoot**: Find what's keeping objects alive (memory leaks)
- **DumpTypeInfo**: Get detailed type information

### Advanced Analysis
- **AutomatedAnalysis**: Let AI analyze your dump automatically
- **DeadlockDetection**: Find threading issues
- **MemoryLeakDetection**: Identify memory leaks

## 3. Basic Workflow

### For Crash Analysis:
1. **DumpExceptions** → Find the exception
2. **DumpClrStack** → Examine the call stack
3. **DumpSourceCode** → Look at the failing code
4. **Ask AI** → Get intelligent analysis

### For Memory Issues:
1. **DumpHeap** → See memory usage patterns
2. **DumpHeapStat** → Get memory statistics
3. **GetObjectRoot** → Find memory leak sources
4. **DumpLargeObjects** → Check for large objects

### For Performance Issues:
1. **DumpClrStack** → Check for blocked threads
2. **DumpSyncBlock** → Look for lock contention
3. **DeadlockDetection** → Find deadlocks
4. **JitAnalysis** → Analyze JIT compilation

## 4. Using AI Features

- **Run any operation** and results will appear on the left
- **Ask questions** in the text box on the right
- **Get intelligent insights** from AI analysis
- **Follow AI suggestions** for deeper investigation

## 5. Tips for Success

- **Start with AutomatedAnalysis** for an overview
- **Use AI liberally** - ask ""What's wrong?"" or ""What should I investigate?""
- **Follow related operations** - the help system suggests next steps
- **Check performance notes** - some operations are expensive on large dumps

## 6. Common Issues

- **""Process is detached""** → Load a dump or attach to a process first
- **""No results""** → Check if your filters are too restrictive
- **""Operation timeout""** → Increase timeout in settings or use filters
- **""Symbol loading failed""** → Configure symbol paths in settings

Ready to start? Try **AutomatedAnalysis** or **DumpHeap** as your first operation!";
        }

        public string GetTroubleshootingGuide()
        {
            return @"# 🔧 Troubleshooting Guide

## Common Issues and Solutions

### Connection Issues
**""Process is detached""**
- Ensure you have loaded a dump file or attached to a process
- Check that the target process is still running (for live debugging)
- Verify you have sufficient permissions

**""Failed to load dump file""**
- Ensure the dump file is not corrupted
- Check that the dump is from a .NET application
- Verify the dump file is not currently in use by another process

### Performance Issues
**""Operation is taking too long""**
- Increase timeout in General Settings
- Use type filters to reduce the scope
- Try a more specific operation instead of broad ones
- Check available memory - large dumps need more RAM

**""Out of memory errors""**
- Close other applications to free up memory
- Use filters to reduce data being processed
- Try processing smaller chunks of data
- Restart DumpMiner and try again

### Analysis Issues
**""No results returned""**
- Check if your address is valid (use hex format: 0x12345678)
- Verify type names are correct and fully qualified
- Remove or adjust filters that might be too restrictive
- Ensure the target process actually contains the objects you're looking for

**""AI analysis not working""**
- Check your AI provider configuration in Settings
- Verify you have a valid API key configured
- Ensure you have internet connectivity
- Check if you've exceeded API rate limits

### Symbol and Source Code Issues
**""Source code not available""**
- Configure symbol paths in General Settings
- Ensure PDB files are available
- Check that the assemblies haven't been optimized heavily
- Verify the code is .NET managed code

**""Symbol loading failed""**
- Add Microsoft symbol server to symbol path
- Check local symbol cache directory
- Verify network connectivity to symbol servers
- Try clearing the symbol cache

### Data Issues
**""Invalid object address""**
- Ensure addresses are in hexadecimal format (0x12345678)
- Verify the object hasn't been garbage collected
- Check that the address is within valid memory regions
- Use DumpHeap to find valid object addresses

**""Type not found""**
- Use fully qualified type names (namespace.class)
- Check spelling and case sensitivity
- Verify the type is loaded in the target process
- Use DumpModules to see available types

### UI Issues
**""Interface not responding""**
- Wait for long operations to complete
- Use the Cancel button to stop operations
- Check if the operation is actually running
- Restart DumpMiner if necessary

**""Results not updating""**
- Ensure you clicked the Execute button
- Check if the operation completed successfully
- Verify there are no error messages
- Try refreshing the view or running again

### Advanced Troubleshooting
**""Deadlock in analysis""**
- Use Cancel button to stop the operation
- Check if multiple operations are running simultaneously
- Restart DumpMiner and try a different approach
- Report the issue if it persists

**""Corrupted results""**
- Verify the dump file integrity
- Check if the target process was in a stable state
- Try a different analysis approach
- Use multiple operations to cross-verify results

## Getting Help
- Use the AI assistant - describe your problem and ask for help
- Check the operation-specific help for detailed guidance
- Look at the About section for version information
- Report bugs with specific error messages and steps to reproduce

## Best Practices
- Always start with AutomatedAnalysis for an overview
- Use specific operations based on your investigation goals
- Apply filters to improve performance on large dumps
- Save important findings before switching operations
- Keep DumpMiner updated for the latest features and fixes";
        }

        private Dictionary<string, OperationHelpContent> BuildHelpContent()
        {
            var content = new Dictionary<string, OperationHelpContent>();

            // Core Operations
            content[OperationNames.DumpHeap] = new OperationHelpContent
            {
                OperationName = OperationNames.DumpHeap,
                DisplayName = "Dump Heap",
                Description = "Lists all objects on the managed heap with their addresses, sizes, and types",
                WinDbgEquivalent = "!dumpheap",
                WhenToUse = "Use when you need to see all objects in memory, analyze memory usage patterns, or find specific object instances",
                Parameters = new List<ParameterInfo>
                {
                    new ParameterInfo { Name = "Types", Description = "Filter by object type name (optional)", IsRequired = false, ExampleValue = "System.String;MyApp.Customer", Notes = "Use semicolon to separate multiple types" },
                    new ParameterInfo { Name = "Generation", Description = "Filter by GC generation (0, 1, 2, or -1 for all)", IsRequired = false, ExampleValue = "2", Notes = "Generation 2 contains long-lived objects" }
                },
                Examples = new List<UsageExample>
                {
                    new UsageExample
                    {
                        Title = "Find all String objects",
                        Scenario = "You suspect a memory leak with string objects",
                        Steps = { "Enter 'System.String' in the Types field", "Click Execute", "Review the results for large or numerous strings" },
                        ExpectedOutput = "List of all string objects with addresses and sizes",
                        AnalysisTips = "Look for unexpectedly large strings or many similar strings that might indicate a leak"
                    },
                    new UsageExample
                    {
                        Title = "Analyze Generation 2 objects",
                        Scenario = "You want to find long-lived objects that might be causing memory issues",
                        Steps = { "Set Generation to 2", "Leave Types empty", "Click Execute", "Sort by size to find largest objects" },
                        ExpectedOutput = "All Generation 2 objects sorted by size",
                        AnalysisTips = "Generation 2 objects are expensive to collect - large objects here may indicate memory pressure"
                    }
                },
                CommonScenarios = { "Memory leak investigation", "Finding specific object instances", "Analyzing memory usage patterns", "Identifying large objects" },
                OutputExplanation = "Each row shows: Object Address (hex), Object Size (bytes), Type Name, and Generation",
                TroubleshootingTips = { "Use type filters on large dumps to improve performance", "If no results appear, check if the process is still attached", "Large dumps may take time to process - be patient" },
                RelatedOperations = { OperationNames.DumpHeapStat, OperationNames.DumpObject, OperationNames.GetObjectRoot, OperationNames.DumpLargeObjects },
                PerformanceNotes = "Can be slow on large dumps. Use type filters to improve performance. Consider using DumpHeapStat first for overview.",
                BestPractices = { "Use type filters on large dumps", "Start with DumpHeapStat for overview", "Use with DumpObject to investigate specific instances", "Check generation patterns for GC behavior" }
            };

            content[OperationNames.DumpClrStack] = new OperationHelpContent
            {
                OperationName = OperationNames.DumpClrStack,
                DisplayName = "CLR Stack",
                Description = "Shows managed call stacks for all threads with method names, parameters, and local variables",
                WinDbgEquivalent = "!clrstack",
                WhenToUse = "Essential for crash analysis, understanding what threads were doing when the dump was taken, and finding the root cause of exceptions",
                Parameters = new List<ParameterInfo>
                {
                    new ParameterInfo { Name = "Thread Filter", Description = "Focus on specific threads (optional)", IsRequired = false, ExampleValue = "Main thread, Background threads", Notes = "Useful for large applications with many threads" }
                },
                Examples = new List<UsageExample>
                {
                    new UsageExample
                    {
                        Title = "Analyze application crash",
                        Scenario = "Your application crashed and you need to find the root cause",
                        Steps = { "Run DumpClrStack", "Look for threads with exceptions", "Examine the call stack leading to the crash", "Check local variables and method parameters" },
                        ExpectedOutput = "Call stacks for all threads showing method calls and local variables",
                        AnalysisTips = "Focus on threads with exceptions first, then look at the main thread. Check method parameters for null values or invalid state"
                    },
                    new UsageExample
                    {
                        Title = "Find deadlock causes",
                        Scenario = "Application is hanging and you suspect a deadlock",
                        Steps = { "Run DumpClrStack", "Look for threads blocked on locks", "Check what methods are waiting", "Compare with DumpSyncBlock results" },
                        ExpectedOutput = "Thread stacks showing blocked threads and their waiting locations",
                        AnalysisTips = "Look for threads waiting on Monitor.Enter, lock statements, or sync primitives"
                    }
                },
                CommonScenarios = { "Crash analysis", "Deadlock investigation", "Performance bottleneck identification", "Understanding application flow" },
                OutputExplanation = "Shows Thread ID, Stack Frames (method calls), Local Variables, Method Parameters, and Exception information",
                TroubleshootingTips = { "If stacks are empty, check if symbols are loaded", "Native frames may not show managed information", "Use DumpExceptions alongside for complete crash analysis" },
                RelatedOperations = { OperationNames.DumpExceptions, OperationNames.DumpSyncBlock, OperationNames.DeadlockDetection, OperationNames.DumpSourceCode },
                PerformanceNotes = "Generally fast operation. Symbol loading may add some delay initially.",
                BestPractices = { "Always check this for crash analysis", "Use with DumpExceptions for complete picture", "Pay attention to local variables and parameters", "Look for patterns across multiple threads" }
            };

            content[OperationNames.DumpExceptions] = new OperationHelpContent
            {
                OperationName = OperationNames.DumpExceptions,
                DisplayName = "Dump Exceptions",
                Description = "Lists all exception objects currently in the managed heap with their messages and stack traces",
                WinDbgEquivalent = "!dumpheap -type Exception",
                WhenToUse = "Essential for crash analysis - shows you what exceptions were thrown and their details",
                Parameters = new List<ParameterInfo>
                {
                    new ParameterInfo { Name = "Exception Type", Description = "Filter by specific exception type (optional)", IsRequired = false, ExampleValue = "System.NullReferenceException", Notes = "Leave empty to see all exceptions" }
                },
                Examples = new List<UsageExample>
                {
                    new UsageExample
                    {
                        Title = "Analyze application crash",
                        Scenario = "Your application crashed and you need to find the root cause exception",
                        Steps = { "Run DumpExceptions", "Look for recent exceptions with stack traces", "Check exception messages for clues", "Use addresses to examine with DumpObject" },
                        ExpectedOutput = "List of all exceptions with their messages, types, and stack traces",
                        AnalysisTips = "Focus on the most recent exceptions. Check inner exceptions for root causes. Look for patterns in exception messages."
                    },
                    new UsageExample
                    {
                        Title = "Find specific exception type",
                        Scenario = "You want to find all NullReferenceExceptions to identify a pattern",
                        Steps = { "Enter 'System.NullReferenceException' in the filter", "Click Execute", "Review all instances and their stack traces", "Look for common code paths" },
                        ExpectedOutput = "All NullReferenceException instances with their details",
                        AnalysisTips = "Check if multiple exceptions occur in the same method or code path - this indicates a systematic issue"
                    }
                },
                CommonScenarios = { "Crash analysis", "Exception pattern identification", "Root cause analysis", "Understanding error conditions" },
                OutputExplanation = "Shows Exception Type, Message, Stack Trace, Inner Exception details, and Object Address",
                TroubleshootingTips = { "If no exceptions found, the crash might be due to access violations or other native issues", "Check inner exceptions for root causes", "Use exception addresses with DumpObject for more details" },
                RelatedOperations = { OperationNames.DumpClrStack, OperationNames.DumpObject, OperationNames.DumpSourceCode, OperationNames.AutomatedAnalysis },
                PerformanceNotes = "Fast operation that scans the heap for exception objects.",
                BestPractices = { "Always run this for crash analysis", "Check inner exceptions", "Use with DumpClrStack for complete picture", "Look for exception patterns" }
            };

            content[OperationNames.DumpObject] = new OperationHelpContent
            {
                OperationName = OperationNames.DumpObject,
                DisplayName = "Dump Object",
                Description = "Shows detailed information about a specific object including all its fields and their values",
                WinDbgEquivalent = "!do",
                WhenToUse = "When you need to examine a specific object in detail, investigate its field values, or understand its state",
                Parameters = new List<ParameterInfo>
                {
                    new ParameterInfo { Name = "Object Address", Description = "Memory address of the object to examine", IsRequired = true, ExampleValue = "0x12345678", Notes = "Use hex format. Get addresses from DumpHeap or other operations" }
                },
                Examples = new List<UsageExample>
                {
                    new UsageExample
                    {
                        Title = "Examine exception object",
                        Scenario = "You found an exception in DumpExceptions and want to see its details",
                        Steps = { "Copy the exception address from DumpExceptions", "Paste it into Object Address field", "Click Execute", "Examine the Message and StackTrace fields" },
                        ExpectedOutput = "All fields of the exception object with their values",
                        AnalysisTips = "Look at the Message field for the error description and StackTrace for where it occurred"
                    },
                    new UsageExample
                    {
                        Title = "Investigate suspicious object",
                        Scenario = "You found a large object in DumpHeap and want to understand what it contains",
                        Steps = { "Copy the object address from DumpHeap", "Run DumpObject with that address", "Examine all field values", "Check for unexpected data or references" },
                        ExpectedOutput = "Complete object layout with all field names and values",
                        AnalysisTips = "Look for null references, unexpected values, or circular references that might indicate issues"
                    }
                },
                CommonScenarios = { "Examining exception details", "Understanding object state", "Debugging field values", "Investigating large objects" },
                OutputExplanation = "Shows Field Names, Field Values, Field Types, and Object References",
                TroubleshootingTips = { "Ensure address is in hex format (0x12345678)", "If object shows as null, it may have been garbage collected", "Use DumpHeap first to find valid object addresses" },
                RelatedOperations = { OperationNames.DumpHeap, OperationNames.GetObjectRoot, OperationNames.DumpTypeInfo, OperationNames.DumpExceptions },
                PerformanceNotes = "Fast operation for individual objects. May be slower for objects with many fields.",
                BestPractices = { "Always use hex addresses", "Use with DumpHeap to find objects", "Check field values for null references", "Look for circular references" }
            };

            content[OperationNames.AutomatedAnalysis] = new OperationHelpContent
            {
                OperationName = OperationNames.AutomatedAnalysis,
                DisplayName = "Automated Analysis",
                Description = "Performs comprehensive automated analysis with intelligent issue detection and AI-powered root cause identification",
                WinDbgEquivalent = "N/A (Advanced automated analysis)",
                WhenToUse = "Perfect starting point for any dump analysis. Automatically detects common issues and provides actionable recommendations",
                Parameters = new List<ParameterInfo>
                {
                    new ParameterInfo { Name = "Analysis Depth", Description = "How deep to analyze (automatic)", IsRequired = false, ExampleValue = "Comprehensive", Notes = "Automatically determines optimal depth" }
                },
                Examples = new List<UsageExample>
                {
                    new UsageExample
                    {
                        Title = "Quick dump analysis",
                        Scenario = "You have a dump file and want to quickly understand what went wrong",
                        Steps = { "Run AutomatedAnalysis", "Review the detected issues", "Follow the recommended actions", "Use suggested operations for deeper investigation" },
                        ExpectedOutput = "Comprehensive analysis report with issues, recommendations, and next steps",
                        AnalysisTips = "Start with high-priority issues first. Follow the recommended operations for deeper investigation."
                    },
                    new UsageExample
                    {
                        Title = "Performance issue investigation",
                        Scenario = "Application is slow and you need to identify bottlenecks",
                        Steps = { "Run AutomatedAnalysis", "Look for threading or GC issues", "Check memory usage patterns", "Follow performance-related recommendations" },
                        ExpectedOutput = "Analysis highlighting performance bottlenecks and optimization opportunities",
                        AnalysisTips = "Focus on high-severity issues first. Check for deadlocks, memory leaks, and GC pressure."
                    }
                },
                CommonScenarios = { "Initial dump analysis", "Performance troubleshooting", "Memory leak detection", "Crash investigation" },
                OutputExplanation = "Provides categorized issues with severity levels, detailed descriptions, and actionable recommendations",
                TroubleshootingTips = { "If analysis takes too long, check dump file size and available memory", "Review all categories of issues", "Follow recommended operations for deeper investigation" },
                RelatedOperations = { OperationNames.DumpHeap, OperationNames.DumpClrStack, OperationNames.DumpExceptions, OperationNames.DeadlockDetection },
                PerformanceNotes = "Comprehensive analysis may take several minutes on large dumps. Results are cached for subsequent queries.",
                BestPractices = { "Always start with this operation", "Review all issue categories", "Follow recommended next steps", "Use AI for detailed explanations" }
            };

            content[OperationNames.GetObjectRoot] = new OperationHelpContent
            {
                OperationName = OperationNames.GetObjectRoot,
                DisplayName = "Get Object Root",
                Description = "Finds the root references that are keeping a specific object alive, essential for memory leak analysis",
                WinDbgEquivalent = "!gcroot",
                WhenToUse = "When investigating memory leaks - shows you what's preventing an object from being garbage collected",
                Parameters = new List<ParameterInfo>
                {
                    new ParameterInfo { Name = "Object Address", Description = "Address of the object to find roots for", IsRequired = true, ExampleValue = "0x12345678", Notes = "Get from DumpHeap or other object analysis operations" }
                },
                Examples = new List<UsageExample>
                {
                    new UsageExample
                    {
                        Title = "Find memory leak cause",
                        Scenario = "You have objects that should be garbage collected but aren't",
                        Steps = { "Find the object address using DumpHeap", "Run GetObjectRoot with that address", "Examine the reference chain", "Identify what's holding the reference" },
                        ExpectedOutput = "Chain of references from GC roots to your object",
                        AnalysisTips = "Look for static variables, event handlers, or long-lived objects that are preventing garbage collection"
                    },
                    new UsageExample
                    {
                        Title = "Investigate large object retention",
                        Scenario = "Large objects are not being freed and consuming memory",
                        Steps = { "Use DumpLargeObjects to find large objects", "Run GetObjectRoot on suspicious objects", "Trace the reference path", "Identify the root cause of retention" },
                        ExpectedOutput = "Reference path showing what's keeping the large object alive",
                        AnalysisTips = "Check for event handler leaks, static collections, or circular references"
                    }
                },
                CommonScenarios = { "Memory leak investigation", "Understanding object lifetime", "Debugging garbage collection issues", "Analyzing object retention" },
                OutputExplanation = "Shows the complete reference path from GC roots to the target object",
                TroubleshootingTips = { "If no roots found, object may have been garbage collected", "Check multiple objects of the same type for patterns", "Use with DumpHeap to find problematic objects" },
                RelatedOperations = { OperationNames.DumpHeap, OperationNames.DumpObject, OperationNames.MemoryLeakDetection, OperationNames.DumpLargeObjects },
                PerformanceNotes = "Can be slow on large heaps as it traverses reference chains. More efficient on smaller, focused dumps.",
                BestPractices = { "Use on objects you suspect are leaking", "Check multiple instances for patterns", "Focus on large or numerous objects", "Combine with memory leak detection" }
            };

            // Add more operations...
            content[OperationNames.DumpHeapStat] = new OperationHelpContent
            {
                OperationName = OperationNames.DumpHeapStat,
                DisplayName = "Heap Statistics",
                Description = "Provides statistical summary of objects on the heap grouped by type, showing counts and total memory usage",
                WinDbgEquivalent = "!dumpheap -stat",
                WhenToUse = "Perfect for getting an overview of memory usage and identifying types that consume the most memory",
                Parameters = new List<ParameterInfo>
                {
                    new ParameterInfo { Name = "Sort Order", Description = "How to sort results (by size, count, or type)", IsRequired = false, ExampleValue = "Size", Notes = "Default sorts by total memory usage" }
                },
                Examples = new List<UsageExample>
                {
                    new UsageExample
                    {
                        Title = "Memory usage overview",
                        Scenario = "You need to understand what's using the most memory",
                        Steps = { "Run DumpHeapStat", "Sort by total size", "Identify the largest memory consumers", "Use DumpHeap on specific types for details" },
                        ExpectedOutput = "Summary table showing object types, counts, and total memory usage",
                        AnalysisTips = "Look for unexpectedly large counts or sizes. Focus on application-specific types rather than system types."
                    }
                },
                CommonScenarios = { "Memory usage analysis", "Finding memory-heavy types", "Getting heap overview", "Identifying leak candidates" },
                OutputExplanation = "Shows Type Name, Object Count, Total Size, and Average Size per object type",
                TroubleshootingTips = { "Large counts of small objects can indicate issues", "Focus on application types", "Use with DumpHeap for detailed investigation" },
                RelatedOperations = { OperationNames.DumpHeap, OperationNames.DumpObject, OperationNames.MemoryLeakDetection },
                PerformanceNotes = "Fast operation that provides good overview without detailed object enumeration.",
                BestPractices = { "Use for initial memory analysis", "Sort by size to find biggest consumers", "Focus on your application's types", "Use before detailed heap analysis" }
            };

            content[OperationNames.DeadlockDetection] = new OperationHelpContent
            {
                OperationName = OperationNames.DeadlockDetection,
                DisplayName = "Deadlock Detection",
                Description = "Advanced deadlock detection using thread state analysis and lock chain examination",
                WinDbgEquivalent = "!syncblk + manual analysis",
                WhenToUse = "When your application hangs or becomes unresponsive, and you suspect thread synchronization issues",
                Parameters = new List<ParameterInfo>
                {
                    new ParameterInfo { Name = "Analysis Depth", Description = "How deep to analyze thread dependencies", IsRequired = false, ExampleValue = "Deep", Notes = "Automatic depth selection based on thread count" }
                },
                Examples = new List<UsageExample>
                {
                    new UsageExample
                    {
                        Title = "Analyze hanging application",
                        Scenario = "Your application stops responding and you suspect a deadlock",
                        Steps = { "Run DeadlockDetection", "Review detected circular wait conditions", "Examine the threads involved", "Check lock acquisition order" },
                        ExpectedOutput = "Detailed deadlock analysis with thread dependencies and lock chains",
                        AnalysisTips = "Look for circular wait conditions where Thread A waits for Thread B, and Thread B waits for Thread A"
                    }
                },
                CommonScenarios = { "Application hanging", "Thread synchronization issues", "Performance bottlenecks", "Lock contention analysis" },
                OutputExplanation = "Shows deadlock patterns, involved threads, lock objects, and circular dependencies",
                TroubleshootingTips = { "If no deadlocks found, check for other blocking conditions", "Use with DumpSyncBlock for additional lock info", "Check thread states in DumpClrStack" },
                RelatedOperations = { OperationNames.DumpSyncBlock, OperationNames.DumpClrStack, OperationNames.AutomatedAnalysis },
                PerformanceNotes = "Analysis time depends on thread count and lock complexity. Can be intensive on highly threaded applications.",
                BestPractices = { "Use when application hangs", "Check lock acquisition patterns", "Review thread synchronization code", "Use with other threading analysis tools" }
            };

            // Add remaining operations with comprehensive help content...
            AddRemainingOperations(content);

            return content;
        }

        private void AddRemainingOperations(Dictionary<string, OperationHelpContent> content)
        {
            // Add remaining operations with focused help content
            content[OperationNames.DumpLargeObjects] = new OperationHelpContent
            {
                OperationName = OperationNames.DumpLargeObjects,
                DisplayName = "Large Object Heap",
                Description = "Lists objects in the Large Object Heap (LOH) - typically objects larger than 85KB",
                WinDbgEquivalent = "!dumpheap -large",
                WhenToUse = "When investigating memory issues with large objects that don't get garbage collected frequently",
                CommonScenarios = { "Memory pressure investigation", "Large object analysis", "GC performance issues" },
                BestPractices = { "Check for unnecessary large objects", "Look for fragmentation issues", "Use with GetObjectRoot for retention analysis" }
            };

            content[OperationNames.DumpSyncBlock] = new OperationHelpContent
            {
                OperationName = OperationNames.DumpSyncBlock,
                DisplayName = "Synchronization Blocks",
                Description = "Shows synchronization block information for objects with locks",
                WinDbgEquivalent = "!syncblk",
                WhenToUse = "When investigating thread synchronization issues, deadlocks, or lock contention",
                CommonScenarios = { "Deadlock investigation", "Lock contention analysis", "Thread synchronization issues" },
                BestPractices = { "Use with DeadlockDetection", "Check for lock ownership patterns", "Identify contention hotspots" }
            };

            content[OperationNames.DumpModules] = new OperationHelpContent
            {
                OperationName = OperationNames.DumpModules,
                DisplayName = "Loaded Modules",
                Description = "Lists all loaded modules (assemblies) in the process",
                WinDbgEquivalent = "!lm",
                WhenToUse = "When you need to see what assemblies are loaded, check versions, or investigate loading issues",
                CommonScenarios = { "Assembly loading issues", "Version conflicts", "Module dependency analysis" },
                BestPractices = { "Check for version conflicts", "Verify expected assemblies are loaded", "Use for dependency analysis" }
            };

            content[OperationNames.DumpTypeInfo] = new OperationHelpContent
            {
                OperationName = OperationNames.DumpTypeInfo,
                DisplayName = "Type Information",
                Description = "Displays detailed type information including method tables and type metadata",
                WinDbgEquivalent = "!dumpmt",
                WhenToUse = "When you need detailed information about a specific type's structure and metadata",
                CommonScenarios = { "Type structure analysis", "Method table investigation", "Type metadata review" },
                BestPractices = { "Use with DumpObject for complete analysis", "Check type hierarchy", "Verify method table information" }
            };

            content[OperationNames.DumpMethods] = new OperationHelpContent
            {
                OperationName = OperationNames.DumpMethods,
                DisplayName = "Type Methods",
                Description = "Shows method information for a specific type including signatures and compilation status",
                WinDbgEquivalent = "!dumpmt -md",
                WhenToUse = "When analyzing method-level issues, JIT compilation problems, or method-specific debugging",
                CommonScenarios = { "Method analysis", "JIT compilation issues", "Performance investigation" },
                BestPractices = { "Check method compilation status", "Use with DumpSourceCode", "Analyze method signatures" }
            };

            content[OperationNames.DumpSourceCode] = new OperationHelpContent
            {
                OperationName = OperationNames.DumpSourceCode,
                DisplayName = "Source Code",
                Description = "Retrieves and displays decompiled source code for methods when debugging information is available",
                WinDbgEquivalent = "N/A (requires decompilation)",
                WhenToUse = "When you need to see the actual code that was executing, especially useful for crash analysis",
                CommonScenarios = { "Crash analysis", "Code review", "Understanding execution flow" },
                BestPractices = { "Use with stack traces", "Check for obvious code issues", "Analyze exception paths" }
            };

            content[OperationNames.MemoryLeakDetection] = new OperationHelpContent
            {
                OperationName = OperationNames.MemoryLeakDetection,
                DisplayName = "Memory Leak Detection",
                Description = "Sophisticated memory leak detection using reference graph analysis and generation patterns",
                WinDbgEquivalent = "N/A (advanced analysis)",
                WhenToUse = "When you suspect memory leaks and need automated detection of problematic patterns",
                CommonScenarios = { "Memory leak investigation", "Memory usage analysis", "Performance troubleshooting" },
                BestPractices = { "Use with heap analysis", "Check reference patterns", "Focus on Generation 2 objects" }
            };

            content[OperationNames.JitAnalysis] = new OperationHelpContent
            {
                OperationName = OperationNames.JitAnalysis,
                DisplayName = "JIT Analysis",
                Description = "Analyzes JIT compilation statistics, method compilation status, and optimization patterns",
                WinDbgEquivalent = "N/A (advanced analysis)",
                WhenToUse = "When investigating performance issues related to JIT compilation or method optimization",
                CommonScenarios = { "Performance analysis", "JIT compilation issues", "Optimization investigation" },
                BestPractices = { "Check compilation statistics", "Look for optimization issues", "Use with method analysis" }
            };

            // Add more operations as needed...
        }
    }
} 