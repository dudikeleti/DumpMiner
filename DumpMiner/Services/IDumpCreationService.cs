using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DumpMiner.Models;

namespace DumpMiner.Services
{
    /// <summary>
    /// Defines a contract for creating memory dumps from running processes in a cross-platform manner.
    /// </summary>
    public interface IDumpCreationService
    {
        /// <summary>
        /// Creates a memory dump from the specified process asynchronously.
        /// </summary>
        /// <param name="processId">The ID of the target process.</param>
        /// <param name="outputPath">The full path where the dump file will be created.</param>
        /// <param name="dumpType">The type of dump to create.</param>
        /// <param name="progress">Optional progress reporter for tracking dump creation progress.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation with the creation result.</returns>
        Task<DumpCreationResult> CreateDumpAsync(
            int processId,
            string outputPath,
            DumpType dumpType,
            IProgress<DumpProgress> progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines whether the specified process can have a dump created from it.
        /// </summary>
        /// <param name="processId">The process ID to check.</param>
        /// <returns>True if the process is dumpable, false otherwise.</returns>
        Task<bool> IsProcessDumpableAsync(int processId);

        /// <summary>
        /// Gets the dump types supported by this service implementation.
        /// </summary>
        /// <returns>A collection of supported dump types.</returns>
        IEnumerable<DumpType> GetSupportedDumpTypes();

        /// <summary>
        /// Validates the proposed dump file path and suggests corrections if needed.
        /// </summary>
        /// <param name="proposedPath">The path to validate.</param>
        /// <returns>A validation result with corrections if necessary.</returns>
        Task<PathValidationResult> ValidateDumpPathAsync(string proposedPath);

        /// <summary>
        /// Estimates the approximate size of a dump before creation.
        /// </summary>
        /// <param name="processId">The target process ID.</param>
        /// <param name="dumpType">The type of dump to estimate.</param>
        /// <returns>An estimation result with size information.</returns>
        Task<DumpSizeEstimation> EstimateDumpSizeAsync(int processId, DumpType dumpType);
    }
}