using System;
using System.Collections.Generic;
using System.IO;

namespace LTXDiff
{
    class Routines
    {
        static public void MakeDiffFromMod(string BaseDir, string ModDir, string RootFileName)
        {
            //Build LTX database
            LTXDB BaseDataBase = new LTXDB(RootFileName);

            //Compare and extract mod changes
            string[] AllModFileNames = Directory.GetFiles(ModDir, "*", SearchOption.AllDirectories);

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
                            Helpers.Print("");
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
                            Helpers.Print((Result.bWasSectionFound ? "![" : "[") + CurrentSectionName + "]" + (!Result.bWasSectionFound && CurrentSectionParent.Length > 0 ? ":" + CurrentSectionParent : ""));
                        }

                        Helpers.Print(CurrentModData.Key + " = " + CurrentModData.Value);
                    }
                }
            }
        }

        static public string FindRootFile(string BaseDir, string ModDir, string FileName)
        {
            string CurrentDirectory = Path.GetDirectoryName(FileName);
            
            while (true)
            {
                string FileNameRelativeToCurrentDir = Path.GetRelativePath(CurrentDirectory, FileName);

                HashSet<string> AllSearchedFiles = new HashSet<string>();
                
                Action<string> AddFilesToSet = DirName =>
                {
                    string[] FileNameArray = Directory.GetFiles(Path.GetFullPath(CurrentDirectory, DirName), "*", SearchOption.TopDirectoryOnly);

                    foreach (string File in FileNameArray)
                    {
                        AllSearchedFiles.Add(Helpers.FindFileFromMod(File, BaseDir, ModDir));
                    }
                };

                AddFilesToSet(BaseDir);
                AddFilesToSet(ModDir);

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

                        string IncludeString = Helpers.GetRegexMatch(CurrentLine, "(?<=^#include\\s+\").+(?=\"$)");
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

            Helpers.Print(FileName);
            return FileName;
        }
    }
}
