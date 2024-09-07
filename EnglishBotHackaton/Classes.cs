using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace EnglishBotHackaton
{
    public class WordEntry
    {
        public string Word { get; set; }
        public string Translation { get; set; }
        public string Definition { get; set; }

        public WordEntry(string word, string translation, string definition)
        {
            Word = word;
            Translation = translation;
            Definition = definition;
        }
    }

    public static class WordListProvider
    {
        public static List<WordEntry> WordList { get; private set; } = LoadWordList();
        public static List<(string question, string[] options, string correctAnswer)> QuestList { get; private set; } = LoadQuestionList();

        private static List<WordEntry> LoadWordList()
        {
            string dictPath = "C:\\Users\\User\\source\\repos\\EnglishBotHackaton13.5\\EnglishBotHackaton\\dictionary.txt";

            var wordList = new List<WordEntry>();

            foreach (var line in File.ReadLines(dictPath))
            {
                var parts = line.Split('|');
                if (parts.Length == 3)
                {
                    string word = parts[0].Trim();
                    string translation = parts[1].Trim();
                    string definition = parts[2].Trim();

                    wordList.Add(new WordEntry(word, translation, definition));
                }
            }

            return wordList;
        }
        private static List<(string question, string[] options, string correctAnswer)> LoadQuestionList()
        {
            string questPath = "C:\\Users\\User\\source\\repos\\EnglishBotHackaton13.5\\EnglishBotHackaton\\fillin.txt";

            var list = new List<(string question, string[] options, string correctAnswer)>();

            foreach (var line in File.ReadLines(questPath))
            {
                var parts = line.Split('|');

                if (parts.Length >= 5)
                {
                    string question = parts[0].Trim();
                    string[] options = parts[1..^1];
                    string correctAnswer = parts[^1].Trim();

                    list.Add((question, options, correctAnswer));
                }
            }

            return list;
        }
    }
}