using System;
using System.Collections.Generic;
using System.IO;

namespace LTXDiff
{
    class Program
    {
        public class ArgumentTracker
        {
            string[] args;
            int CurrentArg = 0;

            public ArgumentTracker(string[] args)
            {
                this.args = args;
            }

            public bool HasNext()
            {
                return CurrentArg < args.Length;
            }

            public string GetNext()
            {
                if (args.Length <= CurrentArg)
                {
                    PrintManual();
                }

                CurrentArg++;
                return args[CurrentArg - 1];
            }
        }

        public enum RoutineType
        {
            Diff,
            FindRoot,
            DLTXify
        }

        public class OptionTracker
        {
            struct OptionData
            {
                public bool bIsStringValue;

                public bool bFlagSet;
                public string StringValue;

                public HashSet<RoutineType> RoutineFilter;

                public OptionData(RoutineType[] RoutineTypes, bool bIsStringValue)
                {
                    this.bIsStringValue = bIsStringValue;
                    this.bFlagSet = false;
                    this.StringValue = null;

                    RoutineFilter = new HashSet<RoutineType>();

                    foreach (RoutineType Type in RoutineTypes)
                    {
                        RoutineFilter.Add(Type);
                    }
                }
            }

            Dictionary<char, string> OptionAbbreviations = new Dictionary<char, string>();
            Dictionary<string, OptionData> Options = new Dictionary<string, OptionData>();

            public void AddOption(string OptionName, RoutineType[] RoutineTypes, bool bIsStringValue = false, char OptionChar = (char)0)
            {
                Options.Add(OptionName, new OptionData(RoutineTypes, bIsStringValue));

                if (OptionChar != 0)
                {
                    OptionAbbreviations.Add(OptionChar, OptionName);
                }
            }

            public void SetFlag(string OptionName)
            {
                if (!Options.ContainsKey(OptionName))
                {
                    PrintManual();
                }

                OptionData Data = Options[OptionName];

                if (!Data.RoutineFilter.Contains(ExecutedRoutine))
                {
                    PrintManual();
                }

                Data.bFlagSet = true;
                Options[OptionName] = Data;
            }

            public void SetStringValue(string OptionName, string Value)
            {
                if (!Options.ContainsKey(OptionName))
                {
                    PrintManual();
                }

                OptionData Data = Options[OptionName];

                if (!Data.RoutineFilter.Contains(ExecutedRoutine))
                {
                    PrintManual();
                }

                Data.StringValue = Value;
                Options[OptionName] = Data;
            }

            public bool IsFlagSet(string OptionName)
            {
                return Options[OptionName].bFlagSet;
            }

            public string GetStringValue(string OptionName)
            {
                return Options[OptionName].StringValue;
            }

            public void ProcessOptions(ArgumentTracker Tracker)
            {
                while (Tracker.HasNext())
                {
                    string CurrentOption = Tracker.GetNext();

                    if (CurrentOption[0] != '-')
                    {
                        Program.PrintManual();
                    }

                    if (CurrentOption[1] == '-')
                    {
                        string OptionName = CurrentOption.Substring(2);

                        if (!Options.ContainsKey(OptionName))
                        {
                            PrintManual();
                        }

                        if (Options[OptionName].bIsStringValue)
                        {
                            SetStringValue(OptionName, Tracker.GetNext());
                        }
                        else
                        {
                            SetFlag(OptionName);
                        }
                    }
                    else
                    {
                        string OptionFlags = CurrentOption.Substring(1);

                        foreach (char Flag in OptionFlags)
                        {
                            if (!OptionAbbreviations.ContainsKey(Flag))
                            {
                                PrintManual();
                            }

                            SetFlag(OptionAbbreviations[Flag]);
                        }
                    }
                }
            }
        }

        public static void PrintManual()
        {
            string ProgramName = "LTXDiff";

            Helpers.PrintC(ProgramName + " diff [base directory] [mod directory] [relative path to root file] [options]");
            Helpers.PrintC("Prints a list of differences between the base and the mod directory, formatted to be ready to be used by DLTX");
            Helpers.PrintC("");
            Helpers.PrintC(ProgramName + " findroot [base directory] [mod directory] [relative path to file]");
            Helpers.PrintC("Determines the root ltx file that a given file belongs to");
            Helpers.PrintC("");
            Helpers.PrintC(ProgramName + " dltxify [base directory] [mod directory] [mod name] [options]");
            Helpers.PrintC("Writes a fully usable DLTX-usable version of a mod");
            Helpers.PrintC("");
            Helpers.PrintC("Options:");
            Helpers.PrintC("[dltxify] --force-overwrite, -f: If files that need to be written are already present, overwrite them without warning.");
            Helpers.PrintC("[diff, dltxify] --no-typo-tolerance, -t: Typos in LTX files will not be corrected");

            Environment.Exit(1);
        }

        static bool VerifyValidPath(string Path, bool bIsFile)
        {
            string PathObjectName = bIsFile ? "File" : "Directory";

            if ((bIsFile && !File.Exists(Path)) || (!bIsFile && !Directory.Exists(Path)))
            {
                Helpers.PrintC(PathObjectName + " " + Path + " doesn't exist.");
                return false;
            }

            return true;
        }

        static void ContinueExecutionIfTrue(bool bContinue)
        {
            if (!bContinue)
            {
                Environment.Exit(1);
            }
        }

        static RoutineType ExecutedRoutine;
        public static OptionTracker Options = new OptionTracker();

        static void Main(string[] args)
        {
            ArgumentTracker Args = new ArgumentTracker(args);

            Options.AddOption("no-typo-tolerance", new RoutineType[]{ RoutineType.Diff, RoutineType.DLTXify }, false, 't');
            Options.AddOption("force-overwrite", new RoutineType[]{ RoutineType.DLTXify }, false, 'f');

            string Command = Args.GetNext().ToLower();

            string BaseDir, ModDir, RootFileName, FileName, ModName;

            switch (Command)
            {
                case "diff":
                    ExecutedRoutine = RoutineType.Diff;

                    BaseDir = Helpers.ParseCommandLinePath(Args.GetNext());
                    ModDir = Helpers.ParseCommandLinePath(Args.GetNext());
                    RootFileName = Path.GetFullPath(Args.GetNext(), BaseDir);

                    Options.ProcessOptions(Args);

                    ContinueExecutionIfTrue(VerifyValidPath(BaseDir, false) &&
                                            VerifyValidPath(ModDir, false) &&
                                            VerifyValidPath(RootFileName, true));

                    Routines.MakeDiffFromMod(BaseDir, ModDir, RootFileName);

                    break;
                case "findroot":
                    ExecutedRoutine = RoutineType.FindRoot;

                    BaseDir = Helpers.ParseCommandLinePath(Args.GetNext());
                    ModDir = Helpers.ParseCommandLinePath(Args.GetNext());
                    FileName = Helpers.GetRegexReplacement(Args.GetNext(), "^\\\\", "");

                    string FullFileNameBase = Path.GetFullPath(FileName, BaseDir);
                    string FullFileNameMod = Path.GetFullPath(FileName, ModDir);

                    if (!File.Exists(FullFileNameBase) && !File.Exists(FullFileNameMod))
                    {
                        Helpers.PrintC("File " + FileName + " doesn't exist.");
                        return;
                    }

                    Options.ProcessOptions(Args);

                    ContinueExecutionIfTrue(VerifyValidPath(BaseDir, false) &&
                                            VerifyValidPath(ModDir, false));

                    HashSet<string> RootFiles = Routines.FindRootFile(BaseDir, ModDir, FileName);

                    foreach (string File in RootFiles)
                    {
                        Helpers.Print(File);
                    }

                    break;

                case "dltxify":
                    ExecutedRoutine = RoutineType.DLTXify;

                    BaseDir = Helpers.ParseCommandLinePath(Args.GetNext());
                    ModDir = Helpers.ParseCommandLinePath(Args.GetNext());
                    ModName = Args.GetNext();

                    bool bIsModNameAppropriate = true;

                    if (!Helpers.IsRegexMatching(ModName, "^[\\w\\d_]+$"))
                    {
                        Helpers.PrintC("Mod name may only contain letters, digits and underscores");
                        bIsModNameAppropriate = false;
                    }

                    Options.ProcessOptions(Args);

                    ContinueExecutionIfTrue(VerifyValidPath(BaseDir, false) &&
                                            VerifyValidPath(ModDir, false) &&
                                            bIsModNameAppropriate);

                    Routines.DLTXify(BaseDir, ModDir, ModName);

                    break;
                default:
                    PrintManual();
                    break;
            }
        }
    }
}
