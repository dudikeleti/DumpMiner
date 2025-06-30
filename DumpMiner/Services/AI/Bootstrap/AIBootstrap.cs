using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Common;
using DumpMiner.Models;
using DumpMiner.Services.AI.Caching;
using DumpMiner.Services.AI.Context;
using DumpMiner.Services.AI.Functions;
using DumpMiner.Services.AI.Interfaces;
using DumpMiner.Services.AI.Orchestration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DumpMiner.Services.AI.Bootstrap
{
    /// <summary>
    /// Bootstrap class for AI services initialization
    /// </summary>
    public static class AIBootstrap
    {
        /// <summary>
        /// Initializes AI services and registers all operations as AI functions
        /// </summary>
        public static void Initialize(CompositionContainer container, ILogger logger = null)
        {
            try
            {
                logger?.LogInformation("Initializing AI services...");

                // Register the AI function registry first
                RegisterAIFunctionRegistry(container, logger);

                // Register the AI orchestrator factory in the MEF container
                RegisterAIOrchestrator(container, logger);

                // Get the function registry
                var functionRegistry = container.GetExportedValue<IAIFunctionRegistry>();
                if (functionRegistry == null)
                {
                    logger?.LogWarning("AI Function Registry not found, skipping AI function registration");
                    return;
                }

                // Register all available operations as AI functions
                RegisterOperationsAsAIFunctions(container, functionRegistry, logger);

                // Quick verification - check if functions were registered
                var functionCount = functionRegistry.GetAvailableFunctions()?.Count() ?? 0;
                logger?.LogInformation("AI services initialized successfully. Total functions registered: {FunctionCount}", functionCount);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error initializing AI services");
            }
        }

        /// <summary>
        /// Diagnostic method to show which operations are defined vs implemented
        /// </summary>
        public static void DiagnoseOperationImplementation(CompositionContainer container, ILogger logger = null)
        {
            try
            {
                logger?.LogInformation("=== OPERATION IMPLEMENTATION DIAGNOSIS ===");

                var allOperationNames = GetAllOperationNames();
                var implementedOperationNames = GetImplementedOperationNames(container, logger);

                logger?.LogInformation("Total operation names defined: {Count}", allOperationNames.Count);
                logger?.LogInformation("Total operations implemented: {Count}", implementedOperationNames.Count);

                var notImplemented = allOperationNames.Except(implementedOperationNames).ToList();
                if (notImplemented.Any())
                {
                    logger?.LogInformation("Operations defined but not implemented: {Count}", notImplemented.Count);
                    foreach (var op in notImplemented.Take(10)) // Show first 10
                    {
                        logger?.LogInformation("  - {OperationName}", op);
                    }
                    if (notImplemented.Count > 10)
                    {
                        logger?.LogInformation("  ... and {Count} more", notImplemented.Count - 10);
                    }
                }

                logger?.LogInformation("=== DIAGNOSIS COMPLETE ===");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error during operation implementation diagnosis");
            }
        }

        /// <summary>
        /// Diagnostic method to verify AI function registration
        /// </summary>
        public static void VerifyRegistration(CompositionContainer container, ILogger logger = null)
        {
            try
            {
                logger?.LogInformation("=== AI FUNCTION REGISTRATION VERIFICATION ===");

                // Check if container has operations
                var operationNames = GetAllOperationNames();
                logger?.LogInformation("Operation names found: {Count}", operationNames.Count);

                foreach (var operationName in operationNames.Take(5)) // Show first 5 for brevity
                {
                    logger?.LogInformation("  - {OperationName}", operationName);
                }

                // Check if function registry exists
                try
                {
                    var functionRegistry = container.GetExportedValue<IAIFunctionRegistry>();
                    if (functionRegistry != null)
                    {
                        logger?.LogInformation("Function registry retrieved (Instance: {InstanceId})", functionRegistry.GetHashCode());

                        var availableFunctions = functionRegistry.GetAvailableFunctions()?.ToList();
                        logger?.LogInformation("AI Functions registered: {Count}", availableFunctions?.Count ?? 0);

                        foreach (var func in availableFunctions?.Take(5) ?? Enumerable.Empty<AIFunctionDefinition>())
                        {
                            logger?.LogInformation("  - {FunctionName}: {Description}", func.Name, func.Description);
                        }
                    }
                    else
                    {
                        logger?.LogWarning("AI Function Registry not found in container");
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Error getting function registry: {Error}", ex.Message);
                }

                logger?.LogInformation("=== VERIFICATION COMPLETE ===");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error during verification");
            }
        }

        private static void RegisterAIFunctionRegistry(CompositionContainer container, ILogger logger)
        {
            try
            {
                // Create and register the AI function registry
                var registry = new AIFunctionRegistry();

                // Register in the container using explicit export
                var batch = new CompositionBatch();
                batch.AddExportedValue<IAIFunctionRegistry>(registry);
                container.Compose(batch);

                logger?.LogInformation("AI Function Registry registered successfully (Instance: {InstanceId})", registry.GetHashCode());
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error registering AI Function Registry");
            }
        }

        private static void RegisterAIOrchestrator(CompositionContainer container, ILogger logger)
        {
            try
            {
                // Create a MEF factory for AI orchestrator that handles the DI dependencies
                var factory = new AIOrchestratorFactory();

                // Register the factory in the container
                var batch = new CompositionBatch();
                batch.AddPart(factory);
                container.Compose(batch);

                logger?.LogInformation("AI Orchestrator factory registered successfully");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error registering AI Orchestrator factory");
            }
        }

        private static void RegisterOperationsAsAIFunctions(
            CompositionContainer container,
            IAIFunctionRegistry functionRegistry,
            ILogger logger)
        {
            try
            {
                logger?.LogInformation("Starting AI function registration (Using instance: {InstanceId})...", functionRegistry.GetHashCode());

                // Verify container is not null
                if (container == null)
                {
                    logger?.LogError("MEF container is null - cannot register AI functions");
                    return;
                }

                // Get only implemented operation names to avoid exceptions
                var operationNames = GetImplementedOperationNames(container, logger);

                logger?.LogInformation("Found {Count} implemented operation names to register", operationNames.Count);

                var successCount = 0;
                var failureCount = 0;
                var notImplementedCount = 0;

                foreach (var operationName in operationNames)
                {
                    try
                    {
                        // Try to get the operation by its contract name (how they are actually exported)
                        var operation = container.GetExportedValue<IDebuggerOperation>(operationName);

                        if (operation != null)
                        {
                            // Create AI function definition
                            var functionDefinition = CreateFunctionDefinition(operation, operationName);

                            // Register the function
                            functionRegistry.RegisterFunction(operationName, functionDefinition);

                            successCount++;
                            logger?.LogDebug("Successfully registered AI function for operation: {OperationName}", operationName);
                        }
                        else
                        {
                            failureCount++;
                            logger?.LogWarning("Operation {OperationName} returned null from container", operationName);
                        }
                    }
                    catch (ImportCardinalityMismatchException)
                    {
                        // This is expected for operations that are defined but not yet implemented
                        notImplementedCount++;
                        logger?.LogDebug("Operation {OperationName} not implemented yet (no export found)", operationName);
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        logger?.LogWarning(ex, "Failed to register AI function for operation {OperationName}: {Error}",
                            operationName, ex.Message);
                    }
                }

                logger?.LogInformation("AI function registration completed: {SuccessCount} successful, {FailureCount} failed, {NotImplementedCount} not implemented (Instance: {InstanceId})",
                    successCount, failureCount, notImplementedCount, functionRegistry.GetHashCode());
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error during AI function registration");
            }
        }

        private static List<string> GetAllOperationNames()
        {
            // Get all operation names from the OperationNames class using reflection
            var operationNamesType = typeof(OperationNames);
            var fields = operationNamesType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            var operationNames = new List<string>();

            foreach (var field in fields)
            {
                if (field.FieldType == typeof(string) && field.IsLiteral && !field.IsInitOnly)
                {
                    var value = (string)field.GetValue(null);
                    if (!string.IsNullOrEmpty(value))
                    {
                        operationNames.Add(value);
                    }
                }
            }

            return operationNames;
        }

        /// <summary>
        /// Gets only the operation names that have actual implementations exported to MEF
        /// </summary>
        private static List<string> GetImplementedOperationNames(CompositionContainer container, ILogger logger)
        {
            var allOperationNames = GetAllOperationNames();
            var implementedOperationNames = new List<string>();

            foreach (var operationName in allOperationNames)
            {
                try
                {
                    // Check if the operation is actually exported
                    var export = container.GetExport<IDebuggerOperation>(operationName);
                    if (export != null)
                    {
                        implementedOperationNames.Add(operationName);
                    }
                }
                catch (ImportCardinalityMismatchException)
                {
                    // Operation not implemented, skip it
                }
                catch (Exception ex)
                {
                    logger?.LogDebug("Error checking operation {OperationName}: {Error}", operationName, ex.Message);
                }
            }

            return implementedOperationNames;
        }

        private static AIFunctionDefinition CreateFunctionDefinition(IDebuggerOperation operation, string operationName)
        {
            // If operation implements IAIEnabledOperation, use its definition
            if (operation is IAIEnabledOperation aiEnabledOp)
            {
                return aiEnabledOp.GetAIFunctionDefinition();
            }

            // Otherwise create a generic definition
            var descriptions = OperationNames.GetOperationDescriptions();
            var description = descriptions.TryGetValue(operationName, out var desc)
                ? desc
                : $"Executes {operationName} operation on memory dump data";

            return new AIFunctionDefinition
            {
                Name = operationName,
                Description = description,
                OperationName = operationName,
                Parameters = new System.Collections.Generic.Dictionary<string, AIFunctionParameter>
                {
                    ["objectAddress"] = new AIFunctionParameter
                    {
                        Type = "string",
                        Description = "Memory address of the object to analyze (in hex format)",
                        Required = false
                    },
                    ["types"] = new AIFunctionParameter
                    {
                        Type = "string",
                        Description = "Type filter to limit results to specific object types",
                        Required = false
                    },
                    ["numOfResults"] = new AIFunctionParameter
                    {
                        Type = "integer",
                        Description = "Maximum number of results to return",
                        Required = false,
                        DefaultValue = 100
                    }
                }
            };
        }
    }

    /// <summary>
    /// MEF factory for AI orchestrator that handles DI dependencies
    /// </summary>
    [Export(typeof(IAIOrchestrator))]
    public class AIOrchestratorFactory : IAIOrchestrator
    {
        private IAIOrchestrator _orchestrator;
        private readonly object _lock = new object();
        private readonly ILogger<AIOrchestratorFactory> _logger = LoggingExtensions.CreateLogger<AIOrchestratorFactory>();

        private IAIOrchestrator GetOrchestrator()
        {
            if (_orchestrator == null)
            {
                lock (_lock)
                {
                    if (_orchestrator == null)
                    {
                        try
                        {
                            // Use the existing AIHelper's service manager if available
                            if (App.AIHelper?.ServiceManager != null)
                            {
                                // Create orchestrator with the existing service manager
                                var serviceProvider = ServiceRegistration.CreateServiceProvider();
                                var contextBuilder = serviceProvider.GetRequiredService<IOperationContextBuilder>();
                                var cacheService = serviceProvider.GetRequiredService<IAICacheService>();

                                // CRITICAL FIX: Get function registry from MEF container, not DI service provider
                                var functionRegistry = App.Container.GetExportedValue<IAIFunctionRegistry>();

                                var logger = serviceProvider.GetRequiredService<ILogger<AIOrchestrator>>();

                                _orchestrator = new AIOrchestrator(
                                    App.AIHelper.ServiceManager,
                                    contextBuilder,
                                    cacheService,
                                    functionRegistry,
                                    logger);
                            }
                            else
                            {
                                // Fallback to creating from service provider
                                var serviceProvider = ServiceRegistration.CreateServiceProvider();
                                _orchestrator = serviceProvider.GetService<IAIOrchestrator>();
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error creating AI orchestrator: {Error}", ex.Message);
                            throw;
                        }
                    }
                }
            }

            return _orchestrator;
        }

        public async Task<AIAnalysisResult> AnalyzeOperationAsync(string operationName, OperationModel operationModel, Collection<object> operationResults, string userQuery = null, CancellationToken cancellationToken = default)
        {
            var orchestrator = GetOrchestrator();
            return await orchestrator.AnalyzeOperationAsync(operationName, operationModel, operationResults, userQuery, cancellationToken);
        }

        public async Task<AIFunctionCallResult> ExecuteFunctionCallAsync(AIFunctionCall functionCall, CancellationToken cancellationToken = default)
        {
            var orchestrator = GetOrchestrator();
            return await orchestrator.ExecuteFunctionCallAsync(functionCall, cancellationToken);
        }

        public async Task<System.Collections.Generic.List<ConversationMessage>> GetConversationHistoryAsync(string operationName, OperationModel operationModel)
        {
            var orchestrator = GetOrchestrator();
            return await orchestrator.GetConversationHistoryAsync(operationName, operationModel);
        }

        public async Task ClearConversationHistoryAsync(string operationName, OperationModel operationModel)
        {
            var orchestrator = GetOrchestrator();
            await orchestrator.ClearConversationHistoryAsync(operationName, operationModel);
        }

        public async Task<bool> IsAvailableAsync()
        {
            var orchestrator = GetOrchestrator();
            return await orchestrator.IsAvailableAsync();
        }

        public System.Collections.Generic.IEnumerable<AIFunctionDefinition> GetAvailableFunctions()
        {
            var orchestrator = GetOrchestrator();
            return orchestrator.GetAvailableFunctions();
        }
    }
}