using OpenAI.GPT3.Managers;
using OpenAI.GPT3.ObjectModels.RequestModels;
using OpenAI.GPT3;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DumpMiner.Common;

namespace DumpMiner.Operations
{
    internal static class Gpt
    {
         internal class Variables
        {
            internal const string values = "{values}";
            internal const string callstack = "{callstack}";
            internal const string assembly = "{assembly}";
            internal const string csharp = "{c#}";
        }

        private static readonly OpenAIService OpenAiService;

        static Gpt()
        {
            OpenAiService = new OpenAIService(new OpenAiOptions
            {
                ApiKey = "your api key"
            });

            OpenAiService.SetDefaultModelId(OpenAI.GPT3.ObjectModels.Models.Gpt_4_32k);
        }

        public static async Task<string> Ask(string[] system, string[] user)
        {
            var message = new List<ChatMessage>();
            message.AddRange(system.Select(s => ChatMessage.FromSystem(s)));
            message.AddRange(user.Select(u => ChatMessage.FromUser(u)));

            var completionResult = await OpenAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = message
            });

            if (!completionResult.Successful)
            {
                App.Container.GetExport<IDialogService>().Value.ShowDialog($"Gpt error: {completionResult.Error?.Message ?? "n/a"}");
                return null;
            }

            return completionResult.Choices.FirstOrDefault()?.Message.Content;
        }
    }
}
