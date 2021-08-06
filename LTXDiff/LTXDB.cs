using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using System.IO;

namespace LTXDiff
{
    struct LTXData
    {
        public string Section;
        public HashSet<string> SectionParent;
        public string Key;
        public string Value;
        public bool bIsSectionOnly;

        public LTXData(string Section, HashSet<string> SectionParent, string Key, string Value, bool bIsSectionOnly = false)
        {
            this.Section = Section;
            this.SectionParent = SectionParent;
            this.Key = Key;
            this.Value = Value;
            this.bIsSectionOnly = bIsSectionOnly;
        }

        public LTXIdentifier GetIdentifier()
        {
            return new LTXIdentifier(Section, Key);
        }
    }
    struct LTXIdentifier
    {
        string SectionName;
        string VariableName;

        public LTXIdentifier(string SectionName, string VariableName)
        {
            this.SectionName = SectionName;
            this.VariableName = VariableName;
        }
    }

    struct LTXDiffResult
    {
        public bool bWasSectionFound;
        public bool bWasMatchFound;
        public bool bWasDifferenceFound;
    }

    class LTXDB
    {
        Dictionary<string, HashSet<string>> SectionParents = new Dictionary<string, HashSet<string>>();
        Dictionary<string, Dictionary<string, string>> VariablesBySections = new Dictionary<string, Dictionary<string, string>>();

        public LTXDB(string RootFilePath, string BaseDir, string ModDir = null)
        {
            if (BaseDir == null)
            {
                throw new Exception();
            }

            IEnumerable<LTXData> FileData = LTXDataFromFile(RootFilePath, false, BaseDir, ModDir);

            foreach (LTXData Data in FileData)
            {
                //Record Section Parent
                HashSet<string> CurrentSectionParent;
                bool bHasSectionParent = SectionParents.TryGetValue(Data.Section, out CurrentSectionParent);

                if (!bHasSectionParent)
                {
                    SectionParents.Add(Data.Section, Data.SectionParent);
                }
                else if (CurrentSectionParent != Data.SectionParent)
                {
                    throw new Exception();
                }

                //Record Value
                if (!VariablesBySections.ContainsKey(Data.Section))
                {
                    VariablesBySections.Add(Data.Section, new Dictionary<string, string>());
                }

                if (Data.bIsSectionOnly)
                {
                    continue;
                }

                VariablesBySections[Data.Section][Data.Key] = Data.Value;
            }
        }

        public LTXDiffResult GetDiff(LTXData InputData)
        {
            LTXDiffResult Result;
            Result.bWasSectionFound = VariablesBySections.ContainsKey(InputData.Section);
            Result.bWasMatchFound = Result.bWasSectionFound && VariablesBySections[InputData.Section].ContainsKey(InputData.Key);
            Result.bWasDifferenceFound = Result.bWasMatchFound ? VariablesBySections[InputData.Section][InputData.Key] != InputData.Value : false;

            return Result;
        }

        public bool HasSection(string Section)
        {
            return VariablesBySections.ContainsKey(Section);
        }

        public HashSet<string> GetSectionParent(string Section)
        {
            if (!SectionParents.ContainsKey(Section))
            {
                return null;
            }

            return SectionParents[Section];
        }

        public IEnumerable<string> GetSections()
        {
            foreach (string Section in VariablesBySections.Keys)
            {
                yield return Section;
            }
        }

        public IEnumerable<LTXData> GetLTXData(string Section)
        {
            if (!VariablesBySections.ContainsKey(Section))
            {
                yield break;
            }

            Dictionary<string, string> Variables = VariablesBySections[Section];

            foreach (string Key in Variables.Keys)
            {
                yield return new LTXData(Section, SectionParents[Section], Key, Variables[Key]);
            }
        }

        public static IEnumerable<LTXData> LTXDataFromFile(string Filename, bool bIgnoreIncludes = true, string BaseDir = null, string ModDir = null)
        {
            if (ModDir != null && BaseDir == null)
            {
                throw new Exception();
            }

            BaseDir = BaseDir == null ? Path.GetDirectoryName(Filename) : BaseDir;

            Filename = Helpers.FindFileFromMod(Filename, BaseDir, ModDir);

            if (Filename == null)
            {
                yield break;
            }

            string FileDir = Helpers.GetRelativePath(BaseDir, ModDir, Path.GetDirectoryName(Filename));

            if (Path.GetExtension(Filename) != ".ltx" || !File.Exists(Filename))
            {
                yield break;
            }

            StreamReader SR = new StreamReader(File.OpenRead(Filename));

            string CurrentSectionName = "";
            HashSet<string> CurrentSectionParent = null;

            while (!SR.EndOfStream)
            {
                string CurrentLine = SR.ReadLine();

                //Get rid of comments, ignore comments that are in quotes
                bool bInQuotes = false;

                for (int i = 0; i < CurrentLine.Length; i++)
                {
                    char CurrentChar = CurrentLine[i];

                    if (CurrentChar == '"')
                    {
                        bInQuotes = !bInQuotes;
                        continue;
                    }

                    if (bInQuotes)
                    {
                        continue;
                    }

                    switch (CurrentChar)
                    {
                        case '/':
                            if (i >= CurrentLine.Length - 1 || CurrentLine[i+1] != '/')
                            {
                                continue;
                            }

                            goto case ';';
                        case ';':
                            CurrentLine = CurrentLine.Substring(0, i);
                            break;
                        default:
                            break;
                    }
                }

                CurrentLine = CurrentLine.Trim();

                //Including other LTX file
                if (Helpers.IsRegexMatching(CurrentLine, "^#include\\s+\".+\"$"))                                                   //i.e. is it in the form "#include "somefile.ltx""
                {
                    if (bIgnoreIncludes)
                    {
                        continue;
                    }

                    string IncludeFilePattern = Helpers.GetRegexMatch(CurrentLine, "(?<=^#include\\s+\").+(?=\"$)");                //i.e. extract include file name

                    HashSet<string> AllMatchingIncludeFiles = Helpers.GetFileNamesFromDirs(FileDir, IncludeFilePattern, BaseDir, ModDir);

                    foreach (string IncludeFileName in AllMatchingIncludeFiles)
                    {
                        foreach (LTXData IncludeData in LTXDataFromFile(IncludeFileName, false, BaseDir, ModDir))
                        {
                            yield return IncludeData;
                        }
                    }

                    continue;
                }

                //Defining new section

                //Typo'd variant found in gamedata/configs/scripts/generators/smart/gen_smart_terrain_urod.ltx
                //We correct the line so that it can be read as a normal section header
                if (!Program.Options.IsFlagSet("no-typo-tolerance") && Helpers.IsRegexMatching(CurrentLine, "^\\[.*\\]\\[.*\\].*$"))
                {
                    string SectionName = Helpers.GetRegexMatch(CurrentLine, "(?<=^\\[)[^\\]]*(?=\\])");
                    string PostSectionStuff = Helpers.GetRegexMatch(CurrentLine, "(?<=^\\[.*\\]\\[.*\\]).*");

                    CurrentLine = "[" + SectionName + "]" + PostSectionStuff;
                }

                if (Helpers.IsRegexMatching(CurrentLine, "^\\[[^\\[\\]:\\s]+\\](:[^\\[\\]:]+)?$"))                                 //i.e. is it in the form "[some_section]:some_parent"
                {
                    CurrentSectionName = Helpers.GetRegexMatch(CurrentLine, "(?<=^\\[)[^\\[\\]:\\s]+(?=\\](:[^\\[\\]:]+)?$)");     //i.e. extract sector name

                    string CurrentSectionParentString = Helpers.GetRegexMatch(CurrentLine, "(?<=^\\[[^\\[\\]:\\s]+\\]:)[^\\[\\]:]+$");          //i.e. extract parent name

                    CurrentSectionParent = new HashSet<string>();

                    if (CurrentSectionParentString.Length > 0)
                    {
                        string[] ParentEntries = CurrentSectionParentString.Split(',', StringSplitOptions.RemoveEmptyEntries);

                        foreach (string Parent in ParentEntries)
                        {
                            CurrentSectionParent.Add(Parent.Trim());
                        }
                    }

                    yield return new LTXData(CurrentSectionName, CurrentSectionParent, null, null, true);

                    continue;
                }

                //Key Value Pair
                if (Helpers.IsRegexMatching(CurrentLine, "^[^\\[\\]:]+(\\s+)?=(\\s+)?[^\"]+$"))                                    //i.e. is it in the form "some_variable = some_value"
                {
                    string Key = Helpers.GetRegexMatch(CurrentLine, "^[^\\[\\]:]+(?=(\\s+)?=(\\s+)?.+$)");                         //i.e. extract variable name
                    string Value = Helpers.GetRegexMatch(CurrentLine, "(?<=^[^\\[\\]:]+(\\s+)?=(\\s+)?).+$");                      //i.e. extract variable value

                    yield return new LTXData(CurrentSectionName, CurrentSectionParent, Key, Value);

                    continue;
                }

                if (Helpers.IsRegexMatching(CurrentLine, "^[^\\[\\]:\"]+(\\s+)?=(\\s+)?\"[^\"]+\"$"))                              //i.e. is it in the form "some_variable = "some_value""
                {
                    string Key = Helpers.GetRegexMatch(CurrentLine, "^[^\\[\\]:\"]+[^\\s](?=(\\s+)?=(\\s+)?\"[^\"]+\"$)");         // i.e. extract variable name
                    string Value = Helpers.GetRegexMatch(CurrentLine, "(?<=^[^\\[\\]:\"]+(\\s+)?=(\\s+)?\")[^\"]+(?=\"$)");        // i.e. extract variable value

                    yield return new LTXData(CurrentSectionName, CurrentSectionParent, Key, Value);

                    continue;
                }

                //Key Value Pair with empty value
                if (Helpers.IsRegexMatching(CurrentLine, "^[^\\[\\]]+((\\s+)?=)?$"))                                              //i.e. is it in the form "some_variable =" or "some_variable"
                {
                    string Key = Helpers.GetRegexMatch(CurrentLine, "^[^\\[\\]:]+(?=((\\s+)?=)?$)");                               //i.e. extract variable name

                    yield return new LTXData(CurrentSectionName, CurrentSectionParent, Key, null);

                    continue;
                }

                //TODO: make all this regex stuff easier to read and maintain somehow

                //TODO: multiline variable values surrounded by quotes
                //as far as I can tell the game doesn't seem to actually make use of it, but code for it is present
                //in the engine, so we should support it

                //Empty Line
                if (CurrentLine.Length == 0)
                {
                    continue;
                }

                throw new Exception();
            }
        }
    }
}
