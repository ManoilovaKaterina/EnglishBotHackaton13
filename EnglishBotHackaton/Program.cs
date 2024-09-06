using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using DotNetEnv;
using System.Text.Json;

class Program
{
    private static readonly HttpClient HttpClient = new HttpClient();
    private static TelegramBotClient Client;
    private static ConcurrentDictionary<long, string> _userFileRequests = new ConcurrentDictionary<long, string>();
    private static CancellationTokenSource _cts = new CancellationTokenSource();

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
            new BotCommand { Command = "start", Description = "Старт" }
        };

        await Client.SetMyCommandsAsync(commands);
        Console.WriteLine("Команди виставлені");
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
            await GetTranslationQuestion(msg);
        }
    }

    private static async Task GetTranslationQuestion(Message msg)
    {
        string url = "https://random-word-api.herokuapp.com/word";
        HttpResponseMessage response = await HttpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        string responseBody = await response.Content.ReadAsStringAsync();

        using (JsonDocument doc = JsonDocument.Parse(responseBody))
        {
            JsonElement root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() == 1)
            {
                string value = root[0].GetString();
                await Client.SendTextMessageAsync(msg.Chat.Id, value);
            }
        }
    }
}
