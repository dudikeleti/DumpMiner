using System.Collections.ObjectModel;
using System.Text;

namespace DumpMiner.Models
{
    public class OperationModel
    {
        public int NumOfResults { get; set; }
        public string Types { get; set; }
        public ulong ObjectAddress { get; set; }
        public StringBuilder GptPrompt { get; set; }
        public ObservableCollection<GptChat> Chat { get; set; }

        public OperationModel()
        {
            GptPrompt = new StringBuilder();
            Chat = new ObservableCollection<GptChat>();
        }
    }

    public class GptChat
    {
        public string Text { get; set; }
        public string Type { get; set; }
    }
}