using IWshRuntimeLibrary;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace BirthdayCommander
{
    class Program
    {
        private static string _saveFilePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\BirthdayCommander\\";

        private static string _programName = "BirthdayCommander";

        private static string _saveFileName = "BirthdayReminderSettings.txt";

        private static string _birthdayInputFormat = "\tFull name +\n\tday they were born +\n\thow many days to alert you in advance +\n\thow often (in days)\n\texample: Jakub Rak+29.10.1991+30+5";

        private static char _inputFormatDelimiter = '+';

        private static string _quietModeArgument = "-quiet";

        private static string _deleteInputKeyword = "DELETE";

        private static string _addToStartupKeyword = "AUTORUN";


        static void Main(string[] args)
        {
            Console.Title = _programName;
            List<BirthdateEntry> allWatchedBirthdays = LoadSettings();
            if (args.Contains(_quietModeArgument))
            {
                List<BirthdateEntry> birthdayBoys = FindImpendingBirthdays(allWatchedBirthdays);
                if (birthdayBoys.Count > 0)
                {
                    PrintBirthdayBoys(birthdayBoys);
                    Console.WriteLine("\nPress any key to confirm");
                    Console.ReadLine();
                }
            }
            else
            {                
                EditBirthdateEntries(allWatchedBirthdays);
            }
        }

        //FRONT END

        private static void PrintBirthdayBoys(List<BirthdateEntry> birthdayBoys)
        {
            if (birthdayBoys.Count == 0)
            {
                Console.WriteLine("No birthdays to show");
            } else {
                BubbleSortBirthdayBoysByETA(birthdayBoys);
                for (int entryIndex = 0; entryIndex < birthdayBoys.Count; entryIndex++)
                {
                    BirthdateEntry birthdayBoy = birthdayBoys[entryIndex];
                    Console.WriteLine("{0}: {1} is turning {2} in {3} days on {4} (born {5})\n\t-reminding you {6} days in advance every {7} days\n",
                        entryIndex,
                        birthdayBoy.fullName,
                        GetNextAge(birthdayBoy),
                        GetETAInDays(birthdayBoy),
                        GetNextBirthday(birthdayBoy).ToShortDateString(),
                        birthdayBoy.birthdate.ToShortDateString(),
                        birthdayBoy.checkFutureXDays,
                        birthdayBoy.remindEveryXDays);
                }
            }
        }  

        private static void PrintFirstEditHelp()
        {
            Console.WriteLine("Today is " + DateTime.Now + "\n");
            Console.WriteLine("How to use this editor:\n\n");
            Console.WriteLine("NEW: You may add new watched birthdays by typing commands using this format: \n" + _birthdayInputFormat);
            Console.WriteLine("\nDELETE: You can DELETE an entry by typing DELETE+n where n is the index number of the entry");
            Console.WriteLine("\nAUTORUN: You can set the app to AUTORUN when computer turns on with the AUTORUN command. \n\tIt will stay hidden if no birthdays are coming up.");
            Console.WriteLine("\nEXIT: Exit the program by pressing the cute X in the top right corner");
            Console.WriteLine("\n------------------------------------------------------------------------------------------------------\n\n");
        }

        private static void PrintEditHelp(List<BirthdateEntry> allBirthdays)
        {
            if (allBirthdays.Count > 0)
            {
                Console.WriteLine("Currently watched birthdays:\n");
                PrintBirthdayBoys(allBirthdays);
            }
            Console.WriteLine("------------------------------------------------------------------------------------------------------\n");
            Console.WriteLine("\nEnter new command and confirm with ENTER:");
        }

        //USER INPUT

        private static void EditBirthdateEntries(List<BirthdateEntry> allBirthdays)
        {
            PrintFirstEditHelp();
            PrintEditHelp(allBirthdays);
            while (true)
            {
                string[] command = Console.ReadLine().Split(_inputFormatDelimiter);
                if (command[0] == _deleteInputKeyword)
                {
                    int indexToRemove = Convert.ToInt32(command[1]);
                    allBirthdays.RemoveAt(indexToRemove);
                    SaveSettings(allBirthdays);
                }else if(command[0] == _addToStartupKeyword){
                    AddShortcutToStartup();
                }
                else if (command.Length == 4)
                {
                    BirthdateEntry newBirthday = new BirthdateEntry()
                    {
                        fullName = command[0],
                        lastShown = DateTime.Now.AddYears(-1),
                        checkFutureXDays = Convert.ToInt32(command[2]),
                        remindEveryXDays = Convert.ToInt32(command[3])
                    };
                    bool parseResult = DateTime.TryParse(
                        command[1],
                        CultureInfo.CurrentCulture.DateTimeFormat,
                        DateTimeStyles.None,
                        out newBirthday.birthdate);
                    if (!parseResult)
                    {
                        Console.Write("Could not understand the date. Try using the following format: " + CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern + "\n\n-----------------------------------\n\n");
                    }
                    else
                    {
                        allBirthdays.Add(newBirthday);
                        SaveSettings(allBirthdays);
                    }
                }
                PrintEditHelp(allBirthdays);
            }
        }


        //LOGIC

        private static List<BirthdateEntry> FindImpendingBirthdays(List<BirthdateEntry> allKnownBirthdays)
        {
            List<BirthdateEntry> birthdaysComingUp = new List<BirthdateEntry>();
            foreach (BirthdateEntry birthdayBoyCandidate in allKnownBirthdays)
            {
                if (GetETAInDays(birthdayBoyCandidate) < birthdayBoyCandidate.checkFutureXDays)
                {
                    if(!WasThisBirthdayRemindedRecently(birthdayBoyCandidate))
                    {
                        birthdaysComingUp.Add(birthdayBoyCandidate);
                        birthdayBoyCandidate.lastShown = DateTime.Now;
                    }
                }
            }
            if (birthdaysComingUp.Count > 0)
            {
                SaveSettings(allKnownBirthdays);
            }
            return birthdaysComingUp;
        }

        private static bool WasThisBirthdayRemindedRecently(BirthdateEntry birthdayBoy)
        {
            int daysAgoReminded = DateTime.Now.Subtract(birthdayBoy.lastShown).Days;
            if(daysAgoReminded > birthdayBoy.remindEveryXDays)
            {
                return false;
            }
            return true;
        }

        private static int GetETAInDays(BirthdateEntry birthdayBoy)
        {
            DateTime bigDay = GetNextBirthday(birthdayBoy);
            int estimatedTimeOfArrival = bigDay.Subtract(DateTime.Now).Days;
            return estimatedTimeOfArrival;
        }

        private static int GetNextAge(BirthdateEntry birthdayBoy)
        {
            DateTime bigDay = GetNextBirthday(birthdayBoy);
            int difference = birthdayBoy.birthdate.Year - bigDay.Year;
            return -difference;
        }

        private static DateTime GetNextBirthday(BirthdateEntry birthdayBoy)
        {
            DateTime birthday = birthdayBoy.birthdate;
            birthday = birthday.AddYears(DateTime.Now.Year - birthday.Year);
            if (birthday < DateTime.Now)
            {
                birthday = birthday.AddYears(1);
            }
            return birthday;
        }

        private static List<BirthdateEntry> BubbleSortBirthdayBoysByETA(List<BirthdateEntry> birthdayBoys)
        {
            bool flag = true;
            BirthdateEntry temp;
            int numLength = birthdayBoys.Count;

            for (int i = 1; (i <= (numLength - 1)) && flag; i++)
            {
                flag = false;
                for (int j = 0; j < (numLength - 1); j++)
                {
                    if (GetNextBirthday(birthdayBoys[j + 1]) < GetNextBirthday(birthdayBoys[j]))
                    {
                        temp = birthdayBoys[j];
                        birthdayBoys[j] = birthdayBoys[j + 1];
                        birthdayBoys[j + 1] = temp;
                        flag = true;
                    }
                }
            }
            return birthdayBoys;
        }

        public static void AddShortcutToStartup()
        {
            string filepathShortcut = Environment.GetFolderPath(Environment.SpecialFolder.Startup) + "\\" + _programName + ".lnk";
            string filepathThis = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\" + _programName + ".exe";
            
            bool success = false;
            try
            {
                WshShell shell = new WshShell();                
                IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(filepathShortcut);
                shortcut.Description = "Shortcut to quietly check whether any known birthdays are coming up and if so - alert the user";
                shortcut.TargetPath = filepathThis;
                shortcut.Arguments= _quietModeArgument;
                shortcut.Save();

                success = true;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            if (success)
            {
                Console.WriteLine(_programName + " copied successfully to startup folder. If you move this exe, run the AUTORUN command again afterwards.");
            }
            else
            {
                Console.WriteLine(_programName + " could not be copied into startup folder. Try adding it manually with the \"-quiet\" argument after the target path.");
            }
            Process.Start(Environment.GetFolderPath(Environment.SpecialFolder.Startup));
        }

        //INPUT OUTPUT OPERATIONS

        private static void SaveSettings(List<BirthdateEntry> allBirthdays)
        {
            if (!Directory.Exists(_saveFilePath + _saveFileName))
            {
                Directory.CreateDirectory(_saveFilePath);
            }
            string json = JsonConvert.SerializeObject(allBirthdays, Formatting.Indented);
            System.IO.File.WriteAllLines(_saveFilePath + _saveFileName, new String[] { json }, Encoding.UTF8);
            Console.WriteLine("Settings saved successfully to "  + _saveFilePath + _saveFileName + "\n");
        }

        private static List<BirthdateEntry> LoadSettings()
        {
            if (System.IO.File.Exists(_saveFilePath + _saveFileName))
            {
                string json = System.IO.File.ReadAllText(_saveFilePath + _saveFileName);
                List<BirthdateEntry> items = JsonConvert.DeserializeObject<List<BirthdateEntry>>(json);
                return items;
            }
            else
            {
                return new List<BirthdateEntry>();
            }

        }
    }
}
