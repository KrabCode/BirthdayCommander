using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace BirthdayCommander
{
    class Program
    {
        private static string _saveFilePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\BirthdayReminder\\";

        private static string _programName = "BIRTHDAY COMMANDER";

        private static string _saveFileName = "BirthdayReminderSettings.txt";

        private static string _birthdayInputFormat = "Full name +\nday they were born +\nhow many days to alert you in advance +\nhow often (in days)\nexample: Jakub Rak+29.10.1991+30+2";

        private static char _inputFormatDelimiter = '+';

        private static string _deleteInputKeyword = "DELETE";


        static void Main(string[] args)
        {
            Console.Title = _programName;
            List<BirthdateEntry> allWatchedBirthdays = loadSettings();
            if (args.Contains("-edit"))
            {
                editBirthdateEntries(allWatchedBirthdays);
            }

            List<BirthdateEntry> birthdayBoys = FindImpendingBirthdaysForTheNext_N_Days(30, allWatchedBirthdays);
            if (birthdayBoys.Count > 0)
            {
                printBirthdayBoys(birthdayBoys);
                Console.WriteLine("\nPress any key to confirm");
                Console.ReadLine();
            }
        }

        //FRONT END

        private static void printBirthdayBoys(List<BirthdateEntry> birthdayBoys)
        {
            if (birthdayBoys.Count == 0)
            {
                Console.WriteLine("No birthdays to show");
            } else {
                bubbleSortBirthdayBoys(birthdayBoys);
                for (int i = 0; i < birthdayBoys.Count; i++)
                {
                    BirthdateEntry birthdayBoy = birthdayBoys[i];
                    Console.WriteLine(i + ": " + birthdayBoy.fullName+
                        " is turning " + GetNextAge(birthdayBoy) +
                        " in " + GetETAInDays(birthdayBoy) + " days on " +
                        GetNextBirthday(birthdayBoy).ToShortDateString() + " (born " + birthdayBoy.birthdate.ToShortDateString() + ")");
                }
            }
        }  

        private static void printFirstEditHelp()
        {
            Console.WriteLine("You may add new watched birthdays by typing commands using this format: \n\n" + _birthdayInputFormat);
            Console.WriteLine("\nIf you wish to delete a watched birthday, type DELETE+n where n is the index number of the entry");
            Console.WriteLine("\n------------------------------------------------------------------------------------------------------\n\n");
        }

        private static void printEditHelp(List<BirthdateEntry> allBirthdays)
        {
            Console.WriteLine("Today is " + DateTime.Now + "\n");
            if (allBirthdays.Count > 0)
            {
                Console.WriteLine("Currently watched birthdays:\n");
                printBirthdayBoys(allBirthdays);
            }
            Console.WriteLine("\n------------------------------------------------------------------------------------------------------\n\n");
            Console.WriteLine("\nEnter new command and confirm with ENTER:");
        }

        //USER INPUT OPERATIONS

        private static void editBirthdateEntries(List<BirthdateEntry> allBirthdays)
        {
            printFirstEditHelp();
            printEditHelp(allBirthdays);
            while (true)
            {
                string[] command = Console.ReadLine().Split(_inputFormatDelimiter);
                if (command[0] == _deleteInputKeyword)
                {
                    int indexToRemove = Convert.ToInt32(command[1]);
                    allBirthdays.RemoveAt(indexToRemove);
                    saveSettings(allBirthdays);
                }
                else if (command.Length == 4)
                {
                    BirthdateEntry newBirthday = new BirthdateEntry()
                    {
                        fullName = command[0],
                        lastShown = DateTime.Now.AddDays(-30),
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
                        Console.Write("Unable to parse the date. Try using the following format: " + CultureInfo.CurrentCulture.DateTimeFormat.LongDatePattern + "\n\n-----------------------------------\n\n");
                    }
                    else
                    {
                        allBirthdays.Add(newBirthday);
                        saveSettings(allBirthdays);
                    }
                }
                printEditHelp(allBirthdays);
            }
        }


        //LOGIC

        private static List<BirthdateEntry> FindImpendingBirthdaysForTheNext_N_Days(int numberOfFollowingDaysToCheck, List<BirthdateEntry> allKnownBirthdays)
        {
            List<BirthdateEntry> birthdaysComingUp = new List<BirthdateEntry>();
            foreach (BirthdateEntry birthdayBoyCandidate in allKnownBirthdays)
            {
                if (GetETAInDays(birthdayBoyCandidate) < numberOfFollowingDaysToCheck)
                {
                    if(!wasThisBirthdayRemindedRecently(birthdayBoyCandidate))
                    {
                        birthdaysComingUp.Add(birthdayBoyCandidate);
                        birthdayBoyCandidate.lastShown = DateTime.Now;
                    }
                }
            }
            if (birthdaysComingUp.Count > 0)
            {
                saveSettings(allKnownBirthdays);
            }
            return birthdaysComingUp;
        }

        private static bool wasThisBirthdayRemindedRecently(BirthdateEntry birthdayBoy)
        {
            //TODO
            return false;
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

        private static List<BirthdateEntry> bubbleSortBirthdayBoys(List<BirthdateEntry> birthdayBoys)
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

        //INPUT OUTPUT OPERATIONS

        private static void saveSettings(List<BirthdateEntry> allBirthdays)
        {
            if (!Directory.Exists(_saveFilePath + _saveFileName))
            {
                Directory.CreateDirectory(_saveFilePath);
            }
            string json = JsonConvert.SerializeObject(allBirthdays, Formatting.Indented);
            File.WriteAllLines(_saveFilePath + _saveFileName, new String[] { json }, Encoding.UTF8);
            Console.WriteLine("Settings saved successfully to "  + _saveFilePath + _saveFileName + "\n");
        }

        private static List<BirthdateEntry> loadSettings()
        {
            if (File.Exists(_saveFilePath + _saveFileName))
            {
                string json = File.ReadAllText(_saveFilePath + _saveFileName);
                List<BirthdateEntry> items = JsonConvert.DeserializeObject<List<BirthdateEntry>>(json);
                Console.WriteLine("Settings loaded successfully.\n");
                return items;
            }
            else
            {
                return new List<BirthdateEntry>();
            }

        }
    }
}
