using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenAI.Chat;

namespace GneissBooks;

internal class OpenAiApi
{
    private readonly ChatClient client = new(model: "gpt-4o", apiKey: App.UserData["OpenAiApiKey"]);

    public void Initialize()
    {
        /*if (openAiApi != null)
            return;

        OpenAiService.Instance.AddOpenAi(settings =>
        {
            settings.ApiKey = App.UserData["OpenAiApiKey"];
        },
       "gneissbooks");

        openAiApi = OpenAiService.Factory.Create("gneissbooks");*/
    }

    public async Task<string> ChatAndReceiveResponse(string message, string systemMessage = "")
    {
        var chatCompletion = await client.CompleteChatAsync([new SystemChatMessage(systemMessage), new UserChatMessage(message)], new ChatCompletionOptions()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
            EndUserId = "gneissbooks",
            Temperature = 0,
        });

        if (chatCompletion == null || chatCompletion.Value.FinishReason != ChatFinishReason.Stop)
            throw new Exception("Failed to query OpenAI: " + ((chatCompletion == null) ? "NULL" : (chatCompletion.Value.FinishReason.ToString() + " " + chatCompletion.Value.Refusal)));

        return chatCompletion.Value.Content.First().Text;

        /*if (openAiApi == null)
            throw new Exception("OpenAI API not initialized");

        return (await openAiApi.Chat.Request(new ChatMessage { Role = ChatRole.System, Content = systemMessage},
                                            new ChatMessage { Role = ChatRole.User, Content = message })
                                            .WithModel(ChatModelType.Gpt4)
                                            .WithTemperature(0)
                                            .SetMaxTokens(300)
                                            .ExecuteAsync()).Choices!.First().Message!.Content!.ToString() ?? "";*/
    }
}
