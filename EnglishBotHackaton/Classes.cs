using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

}
