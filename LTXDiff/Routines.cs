using System;
using System.Collections.Generic;
using System.IO;

namespace LTXDiff
{
    class Routines
    {
        static public void MakeDiffFromMod(string BaseDir, string ModDir, string RootFileName)
        {
            string[] AllModFileNames = Directory.GetFiles(ModDir, "*", SearchOption.AllDirectories);

            MakeDiffFromMod(BaseDir, ModDir, RootFileName, AllModFileNames);
        }

        static public void MakeDiffFromMod(string BaseDir, string ModDir, string RootFileName, string[] AllModFileNames)
        {
            LTXDB BaseDataBase = new LTXDB(RootFileName);

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
                string FileNameRelativeToCurrentDir = Path.GetRelativePath(CurrentDirectory, FileName).ToLower();

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

                MakeDiffFromMod(BaseDir, ModDir, BaseRootFileName, ModFilesByRootFile[RootFileName].ToArray());

                SW.Close();
            }
        }
    }
}
