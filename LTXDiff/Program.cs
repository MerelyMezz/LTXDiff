using System;

using System.Collections.Generic;
using System.IO;

namespace LTXDiff
{
    struct LTXData
    {
        public string Section;
        public string SectionParent;
        public string Key;
        public string Value;

        public LTXData(string Section, string SectionParent, string Key, string Value)
        {
            this.Section = Section;
            this.SectionParent = SectionParent;
            this.Key = Key;
            this.Value = Value;
        }
    }

    class Program
    {
        static public string FullPath(string Input)
        {
            if (Path.IsPathFullyQualified(Input))
            {
                return Input;
            }

            return Path.GetFullPath(Input, Directory.GetCurrentDirectory());
        }
        static void Print(string Input)
        {
            System.Console.WriteLine(Input);
        }

        static void Main(string[] args)
        {
            if (args.Length != 3)
            {
                System.Console.Error.WriteLine("LTXDiff [BaseDir] [ModDir] [RootFile]");
                return;
            }

            string BaseDir = FullPath(args[0]);
            string ModDir = FullPath(args[1]);
            string RootFileName = Path.GetFullPath(args[2], BaseDir);

            //Build LTX database
            LTXDB BaseDataBase = new LTXDB(RootFileName);

            //Compare and extract mod changes
            string[] AllModFileNames = System.IO.Directory.GetFiles(ModDir, "*", SearchOption.AllDirectories);

            foreach (string Filename in AllModFileNames)
            {
                IEnumerable<LTXData> ModFileData = LTXDB.LTXDataFromFile(Filename);

                string RelativePath = Path.GetRelativePath(ModDir, Filename);
                string BaseDirFilename = Path.GetFullPath(RelativePath, BaseDir);

                bool bIsCurrentSectionListed = false;
                string CurrentSectionName = "";
                string CurrentSectionParent = "";

                foreach (LTXData CurrentModData in ModFileData)
                {
                    //Update section if need be
                    if (CurrentSectionName != CurrentModData.Section)
                    {
                        if (bIsCurrentSectionListed)
                        {
                            Print("");
                        }

                        bIsCurrentSectionListed = false;
                        CurrentSectionName = CurrentModData.Section;
                        CurrentSectionParent = CurrentModData.SectionParent;
                    }

                    LTXDiffResult Result = BaseDataBase.GetDiff(CurrentModData);

                    if (!Result.bWasMatchFound || Result.bWasDifferenceFound)
                    {
                        if (!bIsCurrentSectionListed)
                        {
                            bIsCurrentSectionListed = true;
                            Print((Result.bWasSectionFound ? "![" : "[") + CurrentSectionName + "]" + (!Result.bWasSectionFound && CurrentSectionParent.Length > 0 ? ":" + CurrentSectionParent : ""));
                        }

                        Print(CurrentModData.Key + " = " + CurrentModData.Value);
                    }
                }
            }
        }
    }
}
