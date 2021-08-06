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
                bool bIsCurrentSectionListed = false;

                HashSet<string> BaseParents = BaseDataBase.GetSectionParent(Section);
                HashSet<string> ModParents = ModDataBase.GetSectionParent(Section);

                Action PrintSectionHeader = () =>
                {
                    Action<bool, HashSet<string>> PrintHeader = (bOverrideFlag, Parents) =>
                    {
                        string ParentString = "";
                        bool bFirstEntry = true;

                        if (Parents != null && Parents.Count > 0)
                        {
                            ParentString = ":";

                            foreach (string CurrentParent in Parents)
                            {
                                ParentString += (!bFirstEntry ? ", " : "") + CurrentParent;
                                bFirstEntry = false;
                            }
                        }

                        Helpers.Print((bOverrideFlag ? "![" : "[") + Section + "]" + ParentString);
                    };

                    if (!bIsCurrentSectionListed)
                    {
                        bIsCurrentSectionListed = true;

                        if (!BaseDataBase.HasSection(Section))
                        {
                            PrintHeader(false, ModParents);
                            return;
                        }

                        bool bHasModifiedParents = !Helpers.AreSetsEqual<string>(BaseParents, ModParents);

                        HashSet<string> OutputParents = new HashSet<string>();

                        if (bHasModifiedParents)
                        {
                            if (ModParents != null)
                            {
                                foreach (string CurrentModParent in ModParents)
                                {
                                    if (!BaseParents.Contains(CurrentModParent))
                                    {
                                        OutputParents.Add(CurrentModParent);
                                    }
                                }
                            }

                            foreach (string CurrentBaseParent in BaseParents)
                            {
                                if (ModParents == null || !ModParents.Contains(CurrentBaseParent))
                                {
                                    OutputParents.Add("!" + CurrentBaseParent);
                                }
                            }
                        }

                        PrintHeader(true, OutputParents);
                    }
                };

                if (!Helpers.AreSetsEqual<string>(BaseParents, ModParents))
                {
                    PrintSectionHeader();
                }

                foreach (LTXData CurrentModData in ModDataBase.GetLTXData(Section))
                {
                    LTXDiffResult Result = BaseDataBase.GetDiff(CurrentModData);

                    if (!Result.bWasMatchFound || Result.bWasDifferenceFound)
                    {
                        PrintSectionHeader();

                        Helpers.Print(CurrentModData.Key + (CurrentModData.Value != null ? " = " + CurrentModData.Value : ""));
                    }
                }

                foreach (LTXData CurrentBaseData in BaseDataBase.GetLTXData(Section))
                {
                    LTXDiffResult Result = ModDataBase.GetDiff(CurrentBaseData);

                    if (!Result.bWasMatchFound)
                    {
                        PrintSectionHeader();

                        Helpers.Print("!" + CurrentBaseData.Key);
                    }
                }

                if (bIsCurrentSectionListed)
                {
                    Helpers.Print("");
                }
            }

            //Completely deleted sections
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

        static public HashSet<string> FindRootFile(string BaseDir, string ModDir, string FileName)
        {
            HashSet<string> RootFiles = new HashSet<string>();

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

                        RootFiles.UnionWith(FindRootFile(BaseDir, ModDir, Helpers.GetRelativePath(BaseDir, ModDir, CurrentFileName)));
                    }
                }

                if (CurrentDirectory == ".")
                {
                    break;
                }

                CurrentDirectory = Helpers.GetUpperDirectory(CurrentDirectory);
            }

            if (RootFiles.Count == 0)
            {
                RootFiles.Add(FileName);
            }

            return RootFiles;
        }

        public static void DLTXify(string BaseDir, string ModDir, string ModName)
        {
            bool bCopyNonLTX = Program.Options.IsFlagSet("copy-all");
            bool bForceOverwrite = Program.Options.IsFlagSet("force-overwrite");

            //Associate all mod files with a root file
            string[] AllModFiles = Directory.GetFiles(ModDir, "*", SearchOption.AllDirectories);

            string ModDirUpper = Path.Combine(ModDir, "..");
            string ModDirName = Path.GetRelativePath(ModDirUpper, ModDir);
            string OutputModDir = Path.GetFullPath(ModDirName + "_DLTX", ModDirUpper);

            HashSet<string> AllRootFiles = new HashSet<string>();

            foreach (string FileName in AllModFiles)
            {
                string RelativeFileName = Helpers.GetRelativePath(BaseDir, ModDir, FileName);

                if (Path.GetExtension(FileName) != ".ltx")
                {
                    if (bCopyNonLTX)
                    {
                        string FileDestination = Path.GetFullPath(RelativeFileName, OutputModDir);

                        if (File.Exists(FileDestination) && !bForceOverwrite)
                        {
                            Helpers.PrintC("File '" + FileDestination + "' already exists.");
                            Environment.Exit(1);
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(FileDestination));
                        File.Copy(FileName, FileDestination, true);
                    }

                    continue;
                }

                AllRootFiles.UnionWith(FindRootFile(BaseDir, ModDir, RelativeFileName));
            }

            TextWriter OldConsole = Console.Out;

            foreach (string RootFileName in AllRootFiles)
            {
                bool bBaseRootFileExists = File.Exists(Path.GetFullPath(RootFileName, BaseDir));
                string PureFileName = Path.GetFileNameWithoutExtension(RootFileName);

                string ModFileName = (bBaseRootFileExists ? "mod_" + PureFileName + "_" + ModName : PureFileName)  + ".ltx";
                string ModFileDir = Path.GetFullPath(Path.GetDirectoryName(RootFileName), OutputModDir);
                ModFileName = Path.GetFullPath(ModFileName, ModFileDir);

                if (File.Exists(ModFileName) && !bForceOverwrite)
                {
                    Helpers.PrintC("File '" + ModFileName + "' already exists.");
                    Environment.Exit(1);
                }

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
