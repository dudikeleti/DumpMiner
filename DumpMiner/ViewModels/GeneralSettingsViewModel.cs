using DumpMiner.Common;
using DumpMiner.Infrastructure;

namespace DumpMiner.ViewModels
{
    /// <summary>
    /// A simple view model for configuring theme, font and accent colors.
    /// </summary>
    public class GeneralSettingsViewModel : BaseViewModel
    {
        private string _symbolCache;
        private uint _defaultTimeout;

        public GeneralSettingsViewModel()
        {
            var symbolCache = SettingsManager.Instance.ReadSettingValue(SettingsManager.SymbolCache);
            symbolCache = symbolCache?.Substring(symbolCache.IndexOf(',') + 1); //todo: remove
            SymbolCache = symbolCache;
        }

        public string SymbolCache
        {
            get { return this._symbolCache; }
            set
            {
                if (this._symbolCache != value)
                {
                    this._symbolCache = value;
                    SettingsManager.Instance.SaveSettings(nameof(SymbolCache), value);
                    OnPropertyChanged(nameof(SymbolCache));
                }
            }
        }

        public uint DefaultTimeout
        {
            get { return this._defaultTimeout; }
            set
            {
                if (this._defaultTimeout != value)
                {
                    this._defaultTimeout = value;
                    SettingsManager.Instance.SaveSettings(nameof(DefaultTimeout), value.ToString());
                    OnPropertyChanged(nameof(DefaultTimeout));
                }
            }
        }
    }
}
