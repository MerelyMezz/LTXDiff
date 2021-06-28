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

            string Command = Args.GetNext();

            string BaseDir, ModDir, RootFileName, FileName;

            switch (Command)
            {
                case "diff":
                    BaseDir = Helpers.FullPath(Args.GetNext());
                    ModDir = Helpers.FullPath(Args.GetNext());
                    RootFileName = Path.GetFullPath(Args.GetNext(), BaseDir);

                    ContinueExecutionIfTrue(VerifyValidPath(BaseDir, false) &&
                                            VerifyValidPath(ModDir, false) &&
                                            VerifyValidPath(RootFileName, true));

                    Routines.MakeDiffFromMod(BaseDir, ModDir, RootFileName);

                    break;
                case "findroot":
                    BaseDir = Helpers.FullPath(Args.GetNext());
                    ModDir = Helpers.FullPath(Args.GetNext());
                    FileName = Helpers.GetRegexReplacement(Args.GetNext(), "^\\\\", "");

                    string FullFileName = Helpers.FindFileFromMod(FileName, BaseDir, ModDir);

                    ContinueExecutionIfTrue(VerifyValidPath(BaseDir, false) &&
                                            VerifyValidPath(ModDir, false) &&
                                            VerifyValidPath(FullFileName, true));

                    Routines.FindRootFile(BaseDir, ModDir, FileName);

                    break;
                default:
                    PrintManual();
                    break;
            }
        }
    }
}
