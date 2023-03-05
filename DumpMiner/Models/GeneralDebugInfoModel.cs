namespace DumpMiner.Models
{
    class TargetProcessInfoOperationModel
    {
        public string AppDomains { get; set; }
        public int AppDomainsCount { get; set; }
        public int ModulesCount { get; set; }
        public int ThreadsCount { get; set; }
        public string ClrVersions { get; set; }
        public string DacInfo { get; set; }
        public string CreatedTime { get; set; }
        public string SymbolPath { get; set; }
        public string Architecture { get; set; }
        public bool IsGcServer { get; set; }
        public int HeapCount { get; set; }
        public int PointerSize { get; set; }
    }
}
