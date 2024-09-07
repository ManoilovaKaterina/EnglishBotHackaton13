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

        private static List<WordEntry> LoadWordList()
        {
            string filePath = "dictionary.txt";

            var wordList = new List<WordEntry>();

            foreach (var line in File.ReadLines(filePath))
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
    }
}