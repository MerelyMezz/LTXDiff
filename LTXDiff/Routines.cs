using System;
using System.Collections.Generic;
using System.IO;

namespace LTXDiff
{
    class Routines
    {
        static public void MakeDiffFromMod(string BaseDir, string ModDir, string RootFileName)
        {
            LTXDB BaseDataBase = new LTXDB(RootFileName, BaseDir);
            LTXDB ModDataBase = new LTXDB(RootFileName, BaseDir, ModDir);

            foreach (string Section in ModDataBase.GetSections())
            {
                string Parent = ModDataBase.GetSectionParent(Section);
                bool bIsCurrentSectionListed = false;

                foreach (LTXData CurrentModData in ModDataBase.GetLTXData(Section))
                {
                    LTXDiffResult Result = BaseDataBase.GetDiff(CurrentModData);

                    if (!Result.bWasMatchFound || Result.bWasDifferenceFound)
                    {
                        if (!bIsCurrentSectionListed)
                        {
                            bIsCurrentSectionListed = true;
                            Helpers.Print((Result.bWasSectionFound ? "![" : "[") + Section + "]" + (!Result.bWasSectionFound && Parent.Length > 0 ? ":" + Parent : ""));
                        }

                        Helpers.Print(CurrentModData.Key + " = " + CurrentModData.Value);
                    }
                }

                foreach (LTXData CurrentBaseData in BaseDataBase.GetLTXData(Section))
                {
                    LTXDiffResult Result = ModDataBase.GetDiff(CurrentBaseData);

                    if (!Result.bWasMatchFound)
                    {
                        Helpers.Print("!" + CurrentBaseData.Key);
                    }
                }

                if (bIsCurrentSectionListed)
                {
                    Helpers.Print("");
                }
            }

            foreach (string Section in BaseDataBase.GetSections())
            {
                if (ModDataBase.HasSection(Section))
                {
                    continue;
                }

                bool bIsCurrentSectionListed = false;

                foreach (LTXData CurrentBaseData in BaseDataBase.GetLTXData(Section))
                {
                    if (!bIsCurrentSectionListed)
                    {
                        bIsCurrentSectionListed = true;
                        Helpers.Print("!![" + Section + "]");
                    }

                    Helpers.Print("!" + CurrentBaseData.Key);
                }

                if (bIsCurrentSectionListed)
                {
                    Helpers.Print("");
                }
            }
        }

        static public string FindRootFile(string BaseDir, string ModDir, string FileName)
        {
            string CurrentDirectory = Path.GetDirectoryName(FileName);
            
            while (true)
            {
                string FileNameRelativeToCurrentDir = Path.GetRelativePath(CurrentDirectory, FileName).ToLower();

                HashSet<string> AllSearchedFiles = Helpers.GetFileNamesFromDirs(CurrentDirectory, "*", BaseDir, ModDir);

                foreach (string CurrentFileName in AllSearchedFiles)
                {
                    StreamReader SR = new StreamReader(File.OpenRead(CurrentFileName));

                    while (!SR.EndOfStream)
                    {
                        string CurrentLine = SR.ReadLine().Trim();

                        if (!Helpers.IsRegexMatching(CurrentLine, "^#include\\s+\".+\"$"))
                        {
                            continue;
                        }

                        string IncludeString = Helpers.GetRegexMatch(CurrentLine, "(?<=^#include\\s+\").+(?=\"$)").ToLower();
                        IncludeString = Helpers.GetRegexReplacement(IncludeString, "\\*", ".+");                        //Replace ltx wildcard with regex wildcard
                        IncludeString = Helpers.GetRegexReplacement(IncludeString, "\\\\", "\\\\");                     //Adding Regex escape sequences

                        if (!Helpers.IsRegexMatching(FileNameRelativeToCurrentDir, IncludeString))
                        {
                            continue;
                        }

                        return FindRootFile(BaseDir, ModDir, Helpers.GetRelativePath(BaseDir, ModDir, CurrentFileName));
                    }
                }

                if (CurrentDirectory == ".")
                {
                    break;
                }

                CurrentDirectory = Helpers.GetUpperDirectory(CurrentDirectory);
            }

            return FileName;
        }

        public static void DLTXify(string BaseDir, string ModDir, string ModName)
        {
            //Associate all mod files with a root file
            string[] AllModFiles = Directory.GetFiles(ModDir, "*", SearchOption.AllDirectories);

            Dictionary<string, List<string>> ModFilesByRootFile = new Dictionary<string, List<string>>();

            foreach (string FileName in AllModFiles)
            {
                if (Path.GetExtension(FileName) != ".ltx")
                {
                    continue;
                }

                string CurrentRootFile = FindRootFile(BaseDir, ModDir, Helpers.GetRelativePath(BaseDir, ModDir, FileName));

                if (!ModFilesByRootFile.ContainsKey(CurrentRootFile))
                {
                    ModFilesByRootFile[CurrentRootFile] = new List<string>();
                }

                ModFilesByRootFile[CurrentRootFile].Add(FileName);
            }

            TextWriter OldConsole = Console.Out;

            string ModDirUpper = Path.Combine(ModDir, "..");
            string ModDirName = Path.GetRelativePath(ModDirUpper, ModDir);
            string OutputModDir = Path.GetFullPath(ModDirName + "_DLTX", ModDirUpper);

            foreach (string RootFileName in ModFilesByRootFile.Keys)
            {
                string ModFileName = "mod_" + Path.GetFileNameWithoutExtension(RootFileName) + "_" + ModName + ".ltx";
                string ModFileDir = Path.GetFullPath(Path.GetDirectoryName(RootFileName), OutputModDir);
                ModFileName = Path.GetFullPath(ModFileName, ModFileDir);

                if (File.Exists(ModFileName))
                {
                    throw new Exception();
                }

                //TODO: add overwriting option to prevent accidental overwriting
                Directory.CreateDirectory(ModFileDir);
                StreamWriter SW = new StreamWriter(File.OpenWrite(ModFileName));
                Console.SetOut(SW);

                string BaseRootFileName = Path.GetFullPath(RootFileName, BaseDir);

                MakeDiffFromMod(BaseDir, ModDir, BaseRootFileName);

                SW.Close();
            }
        }
    }
}
