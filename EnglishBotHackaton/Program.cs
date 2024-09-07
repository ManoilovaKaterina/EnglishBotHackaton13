using System;
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
using Microsoft.VisualBasic;
using Quartz.Impl;
using Quartz;

class Program
{
    private static readonly HttpClient HttpClient = new HttpClient();
    private static TelegramBotClient Client;
    private static CancellationTokenSource _cts = new CancellationTokenSource();
    private static string CorrectAnswer = "N"; // Якщо у юзера зараз питання - тут правильна відповідь, якщо ні - N (як None)

    private static Dictionary<string, TimeSpan> userPreferredTimes = new Dictionary<string, TimeSpan>();//сюди з бази даних айдішки та час

    static async Task Main(string[] args)
    {
        Env.Load();
        var botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        Client = new TelegramBotClient(botToken); // .енв тупить, якщо не працює просто прямо встав токен
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
            new BotCommand { Command = "start", Description = "Start" },
            new BotCommand { Command = "definition", Description = "Test your knowlege of words\' definitions" }
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

    public static async Task ScheduleDailyMessage(string chatId, TimeSpan sendTime)
    {
        IScheduler scheduler = await StdSchedulerFactory.GetDefaultScheduler();
        await scheduler.Start();

        IJobDetail job = JobBuilder.Create<SendMessageJob>()
            .WithIdentity($"sendMessageJob_{chatId}", "group1")
            .UsingJobData("chatId", chatId)
            .Build();

        ITrigger trigger = TriggerBuilder.Create()
            .WithIdentity($"sendMessageTrigger_{chatId}", "group1")
            .StartNow()
            .WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(sendTime.Hours, sendTime.Minutes))  // Daily trigger at user-specific time
            .Build();

        await scheduler.ScheduleJob(job, trigger);
    }

    public class SendMessageJob : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var chatId = context.JobDetail.JobDataMap.GetString("chatId");
            await Client.SendTextMessageAsync(chatId, "Нагадування: час займатись англійською!");
        }
    }

    private static async Task OnMessage(Message msg)
    {
        if (msg.Text == "/start")
        {
            await Client.SendTextMessageAsync(msg.Chat.Id, "Привіт! Я - твій бот для навчання англійської");
        }
        else if (CorrectAnswer != "N")
        {
            if (msg.Text.StartsWith('/')) // щоб не можна було вийти з невідомого питання
            {
                await Client.SendTextMessageAsync(msg.Chat.Id, "Будь ласка, дайте відповідь на питання");
            }
            else
            {
                if (msg.Text.Equals(CorrectAnswer, StringComparison.OrdinalIgnoreCase))
                {
                    await Client.SendTextMessageAsync(msg.Chat.Id, "Правильно!", replyMarkup: new ReplyKeyboardRemove());
                }
                else
                {
                    await Client.SendTextMessageAsync(msg.Chat.Id, $"Неправильно. Правильна відповідь: \n{CorrectAnswer}", replyMarkup: new ReplyKeyboardRemove());
                }
                CorrectAnswer = "N";
            }
        }
        else if (msg.Text == "/definition")
        {
            await DefinitionQuestion(msg);
        }
        else if (msg.Text == "/translation")
        {
            await DefinitionQuestion(msg);
        }
        else if (msg.Text == "/complete")
        {
            await DefinitionQuestion(msg);
        }
        else if (msg.Text == "/general")
        {
            await DefinitionQuestion(msg);
        }
        else if (msg.Text == "/reminder")
        {
            var replyKeyboard = new ReplyKeyboardMarkup(new[] {
            new[] { new KeyboardButton("9:00"), new KeyboardButton("10:00") },
            new[] { new KeyboardButton("18:00"), new KeyboardButton("20:00") }});

            await Client.SendTextMessageAsync(msg.Chat.Id, "Будь ласка, оберіть час:", replyMarkup: replyKeyboard);
        }
        else if (TimeSpan.TryParse(msg.Text, out TimeSpan preferredTime))
        {
            if (!userPreferredTimes.ContainsKey(msg.Chat.Id.ToString()))
            {
                userPreferredTimes[msg.Chat.Id.ToString()] = preferredTime;
                await ScheduleDailyMessage(msg.Chat.Id.ToString(), preferredTime);
            }

            await Client.SendTextMessageAsync(msg.Chat.Id, $"Ви будете отримувати нагадування о {preferredTime}", replyMarkup: new ReplyKeyboardRemove());
        }
    }

    private static async Task DefinitionQuestion(Message msg)
    {
        string randomWordUrl = "https://random-word-api.herokuapp.com/word?number=4"; // якщо буде словник то слова та визначення з нього
        HttpResponseMessage response = await HttpClient.GetAsync(randomWordUrl);      // але поки хай це буде
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
            try // тут воно не хотіло нормально достати значення з джейсона, тому довга муть
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

        Random rand = new Random();
        int CurrentCorrect = rand.Next(4); // рандомно обирається правильний варіант

        CorrectAnswer = Definitions[CurrentCorrect];

        var replyKeyboard = new ReplyKeyboardMarkup(new[] {
            new[] { new KeyboardButton(Definitions[0]), new KeyboardButton(Definitions[1]) },
            new[] { new KeyboardButton(Definitions[2]), new KeyboardButton(Definitions[3]) }});

        await Client.SendTextMessageAsync(msg.Chat.Id, $"Дайте визначення слову '{Words[CurrentCorrect]}':", replyMarkup: replyKeyboard);
    }
}

