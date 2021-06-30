using System;

using System.IO;

namespace LTXDiff
{
    class ArgumentTracker
    {
        string[] args;
        int CurrentArg = 0;

        public ArgumentTracker(string[] args)
        {
            this.args = args;
        }

        public string GetNext()
        {
            if (args.Length <= CurrentArg)
            {
                Program.PrintManual();
            }

            CurrentArg++;
            return args[CurrentArg - 1];
        }
    }

    class Program
    {
        public static void PrintManual()
        {
            string ProgramName = "LTXDiff";

            Helpers.PrintC(ProgramName + " diff [base directory] [mod directory] [relative path to root file]");
            Helpers.PrintC(ProgramName + " findroot [base directory] [mod directory] [relative path to file]");
            Helpers.PrintC(ProgramName + " dltxify [base directory] [mod directory] [mod name]");

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

        static void Main(string[] args)
        {
            ArgumentTracker Args = new ArgumentTracker(args);

            string Command = Args.GetNext().ToLower();

            string BaseDir, ModDir, RootFileName, FileName, ModName;

            switch (Command)
            {
                case "diff":
                    BaseDir = Helpers.ParseCommandLinePath(Args.GetNext());
                    ModDir = Helpers.ParseCommandLinePath(Args.GetNext());
                    RootFileName = Path.GetFullPath(Args.GetNext(), BaseDir);

                    ContinueExecutionIfTrue(VerifyValidPath(BaseDir, false) &&
                                            VerifyValidPath(ModDir, false) &&
                                            VerifyValidPath(RootFileName, true));

                    Routines.MakeDiffFromMod(BaseDir, ModDir, RootFileName);

                    break;
                case "findroot":
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

                    ContinueExecutionIfTrue(VerifyValidPath(BaseDir, false) &&
                                            VerifyValidPath(ModDir, false));

                    Helpers.Print(Routines.FindRootFile(BaseDir, ModDir, FileName));

                    break;

                case "dltxify":
                    BaseDir = Helpers.ParseCommandLinePath(Args.GetNext());
                    ModDir = Helpers.ParseCommandLinePath(Args.GetNext());
                    ModName = Args.GetNext();

                    bool bIsModNameAppropriate = true;

                    if (!Helpers.IsRegexMatching(ModName, "^[\\w\\d_]+$"))
                    {
                        Helpers.PrintC("Mod name may only contain letters, digits and underscores");
                        bIsModNameAppropriate = false;
                    }

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
