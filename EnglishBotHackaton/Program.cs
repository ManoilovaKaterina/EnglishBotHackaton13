﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using DotNetEnv;
using System.Text.Json;
using Telegram.Bot.Types.ReplyMarkups;
using System.Diagnostics.Eventing.Reader;

class Program
{
    private static readonly HttpClient HttpClient = new HttpClient();
    private static TelegramBotClient Client;
    private static ConcurrentDictionary<long, string> _userFileRequests = new ConcurrentDictionary<long, string>();
    private static CancellationTokenSource _cts = new CancellationTokenSource();
    private static string CorrectAnswer = "A";

    static async Task Main(string[] args)
    {
        Env.Load();
        string botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        Client = new TelegramBotClient(botToken, cancellationToken: _cts.Token);
        var me = await Client.GetMeAsync();

        Console.WriteLine($"@{me.Username} is running...");

        await SetBotCommandsAsync();

        Client.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            },
            cancellationToken: _cts.Token
        );

        Console.ReadLine();
        _cts.Cancel();
    }

    private static async Task SetBotCommandsAsync()
    {
        var commands = new List<BotCommand>
        {
            new BotCommand { Command = "start", Description = "Старт" },
            new BotCommand { Command = "test", Description = "Test Translation Question" }
        };

        await Client.SetMyCommandsAsync(commands);
        Console.WriteLine("Commands set");
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.Message && update.Message is { } message)
        {
            await OnMessage(message);
        }
    }

    private static async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine(exception);
    }

    private static async Task OnMessage(Message msg)
    {
        if (msg.Text == "/start")
        {
            await Client.SendTextMessageAsync(msg.Chat.Id, "Привіт! Я - твій бот для навчання англійської");
        }
        else if (msg.Text == "/test")
        {
            await DefinitionQuestion(msg);
        }
        else if (msg.Text == "1" || msg.Text == "2" || msg.Text == "3" || msg.Text == "4")
        {
            if (msg.Text.Equals(CorrectAnswer, StringComparison.OrdinalIgnoreCase))
            {
                await Client.SendTextMessageAsync(msg.Chat.Id, "Correct!");
            }
            else
            {
                await Client.SendTextMessageAsync(msg.Chat.Id, $"Wrong, the correct answer is {CorrectAnswer}");
            }
        }
    }

    private static async Task DefinitionQuestion(Message msg)
    {
        string randomWordUrl = "https://random-word-api.herokuapp.com/word?number=4";
        HttpResponseMessage response = await HttpClient.GetAsync(randomWordUrl);
        response.EnsureSuccessStatusCode();

        string responseBody = await response.Content.ReadAsStringAsync();
        string[] Words = new string[4];
        List<string> Definitions = new List<string> { };

        using (JsonDocument doc = JsonDocument.Parse(responseBody))
        {
            JsonElement root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                Words = root.EnumerateArray().Select(e => e.GetString()).ToArray();
            }
            else
            {
                await Client.SendTextMessageAsync(msg.Chat.Id, "Failed to get a random word.");
            }
        }

        foreach (var word in Words)
        {
            string definitionUrl = $"https://api.dictionaryapi.dev/api/v2/entries/en/{word}";
            HttpResponseMessage definitionResponse = await HttpClient.GetAsync(definitionUrl);
            if (!definitionResponse.IsSuccessStatusCode)
            {
                await Client.SendTextMessageAsync(msg.Chat.Id, "Failed to get word definition.");
                return;
            }

            string definitionBody = await definitionResponse.Content.ReadAsStringAsync();
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(definitionBody))
                {
                    JsonElement root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                    {
                        var firstEntry = root[0];

                        if (firstEntry.TryGetProperty("meanings", out JsonElement meanings) && meanings.ValueKind == JsonValueKind.Array && meanings.GetArrayLength() > 0)
                        {
                            var firstMeaning = meanings[0];

                            if (firstMeaning.TryGetProperty("definitions", out JsonElement definitions) && definitions.ValueKind == JsonValueKind.Array && definitions.GetArrayLength() > 0)
                            {
                                var firstDefinition = definitions[0];
                                Definitions.Add(firstDefinition.GetProperty("definition").GetString());
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await Client.SendTextMessageAsync(msg.Chat.Id, "Failed to parse word definition.");
                return;
            }
        }

        string CorrectDef = Definitions[0];

        System.Random random = new System.Random();
        Definitions.OrderBy(x => random.Next()).ToArray();

        CorrectAnswer = (Definitions.IndexOf(CorrectDef)+1).ToString();
        Console.WriteLine(CorrectAnswer);

        string QuesString = "";
        for(int i = 0; i<4; i++)
        {
            QuesString += $"\n{i+1}) {Definitions[i]}";
        }

        await Client.SendTextMessageAsync(
            msg.Chat.Id,
            $"Define '{Words[0]}' and choose the correct definition:"+QuesString
        );
    }
}

