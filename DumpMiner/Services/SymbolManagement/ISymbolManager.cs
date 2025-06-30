using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Runtime;

namespace DumpMiner.Services.SymbolManagement
{
    /// <summary>
    /// Interface for advanced symbol management capabilities
    /// </summary>
    public interface ISymbolManager
    {
        /// <summary>
        /// Resolves symbols for a CLR module using symbol servers
        /// </summary>
        Task<SymbolResolutionResult> ResolveSymbolsAsync(ClrModule module, CancellationToken cancellationToken = default);

        /// <summary>
        /// Maps a CLR method to its source code location
        /// </summary>
        Task<SourceMappingResult> MapToSourceAsync(ClrMethod method, CancellationToken cancellationToken = default);

        /// <summary>
        /// Analyzes a PDB file for debugging information
        /// </summary>
        Task<PdbAnalysisResult> AnalyzePdbAsync(string pdbPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Configures symbol search paths
        /// </summary>
        void ConfigureSymbolPaths(IEnumerable<string> symbolPaths);

        /// <summary>
        /// Gets the current status of the symbol cache
        /// </summary>
        SymbolCacheStatus GetCacheStatus();
    }
} 