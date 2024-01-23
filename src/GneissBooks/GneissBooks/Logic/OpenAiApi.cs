using Microsoft.Extensions.Configuration;
using Rystem.OpenAi;
using Rystem.OpenAi.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GneissBooks;

internal class OpenAiApi
{
    IOpenAi? openAiApi;

    public void Initialize()
    {
        if (openAiApi != null)
            return;

        OpenAiService.Instance.AddOpenAi(settings =>
        {
            settings.ApiKey = App.UserData["OpenAiApiKey"];
        },
       "gneissbooks");

        openAiApi = OpenAiService.Factory.Create("gneissbooks");
    }

    public async Task<string> ChatAndReceiveResponse(string message, string systemMessage = "")
    {
        if (openAiApi == null)
            throw new Exception("OpenAI API not initialized");

        return (await openAiApi.Chat.Request(new ChatMessage { Role = ChatRole.System, Content = systemMessage},
                                            new ChatMessage { Role = ChatRole.User, Content = message })
                                            .WithModel(ChatModelType.Gpt4)
                                            .WithTemperature(0)
                                            .SetMaxTokens(200)
                                            .ExecuteAsync()).Choices!.First().Message!.Content!;
    }
}
