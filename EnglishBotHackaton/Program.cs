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
using Telegram.Bot.Types.ReplyMarkups;
using Quartz.Impl;
using Quartz;
using EnglishBotHackaton;

class Program
{
    private static readonly HttpClient HttpClient = new HttpClient();
    private static TelegramBotClient Client;
    private static CancellationTokenSource _cts = new CancellationTokenSource();

    private static List<WordEntry> wordList = WordListProvider.WordList;
    private static List<(string question, string[] options, string correctAnswer)> dataForFillIn = WordListProvider.QuestList;

    private static Dictionary<string, TimeSpan> userPreferredTimes = new Dictionary<string, TimeSpan>();
    private static Dictionary<string, string> userCorrectAnswers = new Dictionary<string, string>();
    private static Dictionary<string, int> userQuestionIndexes = new Dictionary<string, int>();
    private static Dictionary<string, List<(string question, string[] options, string correctAnswer)>> userCurrentQuestions = new Dictionary<string, List<(string question, string[] options, string correctAnswer)>>();

    static async Task Main(string[] args)
    {
        Env.Load();
        var botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        Client = new TelegramBotClient(botToken);
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
            new BotCommand { Command = "definition", Description = "Check your knowledge of word definitions" },
            new BotCommand { Command = "translation", Description = "Check your knowledge of word translations" },
            new BotCommand { Command = "fillintheblanks", Description = "Check your knowledge of words in context" },
            new BotCommand { Command = "reminder", Description = "Set a reminder time" },
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
            .WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(sendTime.Hours, sendTime.Minutes))
            .Build();

        await scheduler.ScheduleJob(job, trigger);
    }

    public class SendMessageJob : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var chatId = context.JobDetail.JobDataMap.GetString("chatId");
            await Client.SendTextMessageAsync(chatId, "Reminder: time to study English!");
        }
    }

    private static async Task OnMessage(Message msg)
    {
        string chatId = msg.Chat.Id.ToString();

        if (msg.Text == "/start")
        {
            await Client.SendTextMessageAsync(msg.Chat.Id, "Hello! I am your English learning bot.\n" +
                "This bot provides exercises to test your knowledge of definitions and translations of words, as well as their use in sentences.");
            userCorrectAnswers[chatId] = "N";
            userQuestionIndexes[chatId] = 0;
        }
        else if (userCorrectAnswers.ContainsKey(chatId) && userCorrectAnswers[chatId] != "N")
        {
            if (msg.Text.StartsWith('/'))
            {
                await Client.SendTextMessageAsync(msg.Chat.Id, "Please answer the question.");
            }
            else
            {
                if (msg.Text.Equals(userCorrectAnswers[chatId], StringComparison.OrdinalIgnoreCase))
                {
                    await Client.SendTextMessageAsync(msg.Chat.Id, "Correct!", replyMarkup: new ReplyKeyboardRemove());
                }
                else
                {
                    await Client.SendTextMessageAsync(msg.Chat.Id, $"Incorrect. The correct answer is: \n{userCorrectAnswers[chatId]}", replyMarkup: new ReplyKeyboardRemove());
                }
                userCorrectAnswers[chatId] = "N";
                userQuestionIndexes[chatId]++;

                if (userQuestionIndexes[chatId] < 5)
                {
                    await AskNextQuestion(chatId);
                }
                else
                {
                    userQuestionIndexes[chatId] = 0;
                    await Client.SendTextMessageAsync(msg.Chat.Id, "You have completed the questions!");
                }
            }
        }
        else if (msg.Text == "/definition")
        {
            await StartQuestionSession(chatId, "definition");
        }
        else if (msg.Text == "/translation")
        {
            await StartQuestionSession(chatId, "translation");
        }
        else if (msg.Text == "/fillintheblanks")
        {
            await StartQuestionSession(chatId, "fillintheblanks");
        }
        else if (msg.Text == "/reminder")
        {
            var replyKeyboard = new ReplyKeyboardMarkup(new[] {
                new[] { new KeyboardButton("9:00"), new KeyboardButton("10:00") },
                new[] { new KeyboardButton("18:00"), new KeyboardButton("20:00") } });

            await Client.SendTextMessageAsync(msg.Chat.Id, "Please choose a time:", replyMarkup: replyKeyboard);
        }
        else if (TimeSpan.TryParse(msg.Text, out TimeSpan preferredTime))
        {
            userPreferredTimes[chatId] = preferredTime;
            await ScheduleDailyMessage(chatId, preferredTime);

            await Client.SendTextMessageAsync(msg.Chat.Id, $"You will receive a reminder at {preferredTime}", replyMarkup: new ReplyKeyboardRemove());
        }
    }

    private static async Task StartQuestionSession(string chatId, string type)
    {
        userQuestionIndexes[chatId] = 0;

        List<(string question, string[] options, string correctAnswer)> questions;

        Random rand = new Random();
        switch (type)
        {
            case "definition":
                var definitionWords = wordList.OrderBy(x => rand.Next()).Take(5).ToList();
                questions = definitionWords.Select(word => (
                    question: $"What is the definition of '{word.Word}'?",
                    options: wordList
                    .Where(w => w.Definition != word.Definition)
                    .OrderBy(x => rand.Next())
                    .Take(3)
                    .Select(w => w.Definition)
                    .Concat(new[] { word.Definition })
                    .OrderBy(x => rand.Next())
                    .ToArray(),
                    correctAnswer: word.Definition
                )).ToList();
                break;

            case "translation":
                var translationWords = wordList.OrderBy(x => rand.Next()).Take(5).ToList();
                questions = translationWords.Select(word => (
                    question: $"What is the translation of '{word.Word}'?",
                    options: wordList
                    .Where(w => w.Translation != word.Translation)
                    .OrderBy(x => rand.Next())
                    .Take(3)
                    .Select(w => w.Translation)
                    .Concat(new[] { word.Translation })
                    .OrderBy(x => rand.Next())
                    .ToArray(),
                    correctAnswer: word.Translation
                )).ToList();
                break;

            case "fillintheblanks":
                questions = dataForFillIn.OrderBy(x => rand.Next()).Take(5).ToList();
                break;

            default:
                throw new ArgumentException("Invalid question type");
        }

        userCurrentQuestions[chatId] = questions;
        await AskNextQuestion(chatId);
    }


    private static async Task AskNextQuestion(string chatId)
    {
        if (userCurrentQuestions.ContainsKey(chatId))
        {
            var questions = userCurrentQuestions[chatId];
            var currentIndex = userQuestionIndexes[chatId];

            if (currentIndex >= questions.Count)
            {
                await Client.SendTextMessageAsync(chatId, "You have completed all the questions.");
                userCurrentQuestions.Remove(chatId);
                userQuestionIndexes.Remove(chatId);
                return;
            }

            var question = questions[currentIndex];

            userCorrectAnswers[chatId] = question.correctAnswer;

            var buttons = question.options.Select(option => new KeyboardButton(option)).ToArray();
            var rows = new[] { buttons.Take(2).ToArray(), buttons.Skip(2).Take(2).ToArray() };

            var replyKeyboard = new ReplyKeyboardMarkup(rows)
            {
                ResizeKeyboard = true
            };

            string questionText = question.question;

            await Client.SendTextMessageAsync(chatId, questionText, replyMarkup: replyKeyboard);
        }
    }
}