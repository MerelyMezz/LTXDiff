using System;
using System.IO;

using System.Collections.Generic;

namespace LTXDiff
{
    struct SectionKeyValueTriple
    {
        public string Section;
        public string SectionParent;
        public string Key;
        public string Value;

        public SectionKeyValueTriple(string Section, string SectionParent, string Key, string Value)
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

        static public IEnumerable<SectionKeyValueTriple> SectionKeyValueTripleFromFile(string Filename)
        {
            if (Path.GetExtension(Filename) != ".ltx" || !File.Exists(Filename))
            {
                yield break;
            }

            StreamReader SR = new StreamReader(File.OpenRead(Filename));

            string CurrentSectionName = "";
            string CurrentSectionParent = "";

            while (!SR.EndOfStream)
            {
                string CurrentLine = SR.ReadLine();

                //Clear Comments
                int SemicolonPos = CurrentLine.IndexOf(';');
                int SlashPos = CurrentLine.IndexOf('/');

                if (SlashPos >= 0 && CurrentLine[SlashPos + 1] != '/')
                {
                    SlashPos = -1;
                }

                if (SlashPos * SemicolonPos <= 0)
                {
                    SlashPos = SlashPos >= 0 ? SlashPos : SemicolonPos + 1;
                    SemicolonPos = SemicolonPos >= 0 ? SemicolonPos : SlashPos + 1;
                }

                int CommentPos = Math.Min(SlashPos, SemicolonPos);

                if (CommentPos >= 0)
                {
                    CurrentLine = CurrentLine.Substring(0, CommentPos);
                }

                CurrentLine = CurrentLine.Trim();

                //Empty Line
                if (CurrentLine.Length == 0)
                {
                    continue;
                }

                //Including other LTX file, we don't care about this
                if (CurrentLine.Contains("#include"))
                {
                    continue;
                }

                //Defining new section
                if (CurrentLine[0] == '[' && CurrentLine.Contains(']'))
                {
                    CurrentSectionName = CurrentLine.Substring(1, CurrentLine.LastIndexOf(']') - 1).Trim();

                    int ParentPos = CurrentLine.IndexOf(':');

                    CurrentSectionParent = (ParentPos >= 0 && ParentPos <= CurrentLine.Length - 1) ? CurrentLine.Substring(ParentPos + 1, CurrentLine.Length - ParentPos - 1) : "";


                    continue;
                }

                //Key Value Pair
                string[] KeyValuePair = CurrentLine.Split("=");

                if (KeyValuePair.Length == 2)
                {
                    string Key = KeyValuePair[0].Trim();
                    string Value = KeyValuePair[1].Trim();

                    yield return new SectionKeyValueTriple(CurrentSectionName, CurrentSectionParent, Key, Value);
                }
            }
        }

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                System.Console.Error.WriteLine("LTXDiff [BaseDir] [ModDir]");
                return;
            }

            string BaseDir = FullPath(args[0]);
            string ModDir = FullPath(args[1]);

            string[] AllModFileNames = System.IO.Directory.GetFiles(ModDir, "*", SearchOption.AllDirectories);

            foreach (string Filename in AllModFileNames)
            {
                IEnumerable<SectionKeyValueTriple> ModFileData = SectionKeyValueTripleFromFile(Filename);

                string RelativePath = Path.GetRelativePath(ModDir, Filename);
                string BaseDirFilename = Path.GetFullPath(RelativePath, BaseDir);

                bool bIsCurrentSectionListed = false;
                string CurrentSectionName = "";
                string CurrentSectionParent = "";

                foreach (SectionKeyValueTriple CurrentModData in ModFileData)
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

                    //Compare Key Value pair with basefile
                    IEnumerable<SectionKeyValueTriple> BaseFileData = SectionKeyValueTripleFromFile(BaseDirFilename);

                    bool bWasSectionFound = false;
                    bool bWasMatchFound = false;
                    bool bWasDifferenceFound = false;

                    foreach (SectionKeyValueTriple CurrentBaseData in BaseFileData)
                    {
                        if (CurrentBaseData.Section == CurrentModData.Section)
                        {
                            bWasSectionFound = true;

                            if (CurrentBaseData.Key == CurrentModData.Key)
                            {
                                bWasMatchFound = true;

                                if (CurrentBaseData.Value != CurrentModData.Value)
                                {
                                    bWasDifferenceFound = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (!bWasMatchFound || bWasDifferenceFound)
                    {
                        if (!bIsCurrentSectionListed)
                        {
                            bIsCurrentSectionListed = true;
                            Print((bWasSectionFound ? "![" : "[") + CurrentSectionName + "]" + (!bWasSectionFound && CurrentSectionParent.Length > 0 ? ":" + CurrentSectionParent : ""));
                        }

                        Print(CurrentModData.Key + " = " + CurrentModData.Value);
                    }
                }
            }
        }
    }
}
