using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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
        Dictionary<string, string> SectionParents = new Dictionary<string, string>();
        Dictionary<LTXIdentifier, string> VariableValues = new Dictionary<LTXIdentifier, string>();

        public LTXDB(string RootFilePath)
        {
            if (!File.Exists(RootFilePath))
            {
                return;
            }

            IEnumerable<LTXData> FileData = LTXDataFromFile(RootFilePath, false);

            foreach (LTXData Data in FileData)
            {
                //Record Section Parent
                string CurrentSectionParent;
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
                VariableValues[Data.GetIdentifier()] = Data.Value;
            }
        }

        public LTXDiffResult GetDiff(LTXData InputData)
        {
            LTXIdentifier Identifier = InputData.GetIdentifier();
            LTXDiffResult Result;
            Result.bWasSectionFound = SectionParents.ContainsKey(InputData.Section);
            Result.bWasMatchFound = VariableValues.ContainsKey(Identifier);

            if (Result.bWasMatchFound)
            {
                string Value;
                VariableValues.TryGetValue(Identifier, out Value);

                Result.bWasDifferenceFound = Value != InputData.Value;
            }
            else
            {
                Result.bWasDifferenceFound = false;
            }

            return Result;
        }

        public static IEnumerable<LTXData> LTXDataFromFile(string Filename, bool bIgnoreIncludes = true)
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

                //Get rid of comments
                CurrentLine = Helpers.GetRegexMatch(CurrentLine, "^(?=;|//|$)|(^.+?(?=;|//|$))").Trim();                            //  ^(?=;|//|$)|(^.+?(?=;|//|$))                    i.e. remove everything after and including ; and //

                //Including other LTX file
                if (Helpers.IsRegexMatching(CurrentLine, "^#include\\s+\".+\"$"))                                                   //  ^#include\s+".+"$                               i.e. is it in the form "#include "somefile.ltx""
                {
                    if (bIgnoreIncludes)
                    {
                        continue;
                    }

                    string IncludeFileName = Helpers.GetRegexMatch(CurrentLine, "(?<=^#include\\s+\").+(?=\"$)");                   //  (?<=^#include\s+").+(?="$)                      i.e. extract include file name
                    IncludeFileName = Path.GetFullPath(IncludeFileName, Path.GetDirectoryName(Filename));

                    foreach (LTXData IncludeData in LTXDataFromFile(IncludeFileName, false))
                    {
                        yield return IncludeData;
                    }

                    continue;
                }

                //Defining new section
                if (Helpers.IsRegexMatching(CurrentLine, "^\\[[^\\[\\]:\\s]+\\](:[^\\[\\]:]+)?$"))                                  //  ^\[[^\[\]:\s]+\](:[^\[\]:]+)?$                  i.e. is it in the form "[some_section]:some_parent"
                {
                    CurrentSectionName = Helpers.GetRegexMatch(CurrentLine, "(?<=^\\[)[^\\[\\]:\\s]+(?=\\](:[^\\[\\]:]+)?$)");      //  (?<=^\[)[^\[\]:\s]+(?=\](:[^\[\]:]+)?$)         i.e. extract sector name
                    CurrentSectionParent = Helpers.GetRegexMatch(CurrentLine, "(?<=^\\[[^\\[\\]:\\s]+\\]:)[^\\[\\]:]+$");           //  (?<=^\[[^\[\]:\s]+\]:)[^\[\]:]+$                i.e. extract parent name

                    continue;
                }

                //Key Value Pair
                if (Helpers.IsRegexMatching(CurrentLine, "^[^\\[\\]:]+(\\s+)?=(\\s+)?.+$"))                                         //   ^[^\[\]:]+(\s+)?=(\s+)?.+$                     i.e. is it in the form "some_variable = some_value"
                {
                    string Key = Helpers.GetRegexMatch(CurrentLine, "^[^\\[\\]:]+(?=(\\s+)?=(\\s+)?.+$)");                          //   ^[^\[\]:]+(?=(\s+)?=(\s+)?.+$)                 i.e. extract variable name
                    string Value = Helpers.GetRegexMatch(CurrentLine, "(?<=^[^\\[\\]:]+(\\s+)?=(\\s+)?).+$");                       //   (?<=^[^\[\]:]+(\s+)?=(\s+)?).+$                i.e. extract variable value

                    yield return new LTXData(CurrentSectionName, CurrentSectionParent, Key, Value);

                    continue;
                }

                //Key Value Pair with empty value
                if (Helpers.IsRegexMatching(CurrentLine, "^[^\\[\\]:]+((\\s+)?=)?$"))                                                //   ^[^\[\]:]+((\s+)?=)?$                         i.e. is it in the form "some_variable =" or "some_variable"
                {
                    string Key = Helpers.GetRegexMatch(CurrentLine, "^[^\\[\\]:]+(?=((\\s+)?=)?$)");                                 //   ^[^\[\]:]+(?=((\s+)?=)?$)                     i.e. extract variable name

                    yield return new LTXData(CurrentSectionName, CurrentSectionParent, Key, "");

                    continue;
                }

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
