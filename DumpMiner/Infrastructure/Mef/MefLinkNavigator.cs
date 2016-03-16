using System;
using System.ComponentModel.Composition;
using System.Windows.Input;
using FirstFloor.ModernUI.Windows.Navigation;

namespace DumpMiner.Infrastructure.Mef
{
    /// <summary>
    /// Extends the default link navigator by adding exported ICommands.
    /// </summary>
    [Export]
    public class MefLinkNavigator : DefaultLinkNavigator, IPartImportsSatisfiedNotification
    {
        [ImportMany]
        private Lazy<ICommand, ICommandMetadata>[] ImportedCommands { get; set; }

        public void OnImportsSatisfied()
        {
            foreach (var c in ImportedCommands)
            {
                var commandUri = new Uri(c.Metadata.CommandUri, UriKind.RelativeOrAbsolute);
                Commands.Add(commandUri, c.Value);
            }
        }
    }
}
