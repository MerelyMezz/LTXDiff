using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using System.IO;


namespace LTXDiff
{
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
                throw new Exception();
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
                LTXIdentifier Identifier = new LTXIdentifier(Data.Section, Data.Key);

                VariableValues[Identifier] = Data.Value;
            }
        }

        public LTXDiffResult GetDiff(LTXData InputData)
        {
            LTXIdentifier Identifier = new LTXIdentifier(InputData.Section, InputData.Key);
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

                //Including other LTX file
                if (CurrentLine.Contains("#include"))
                {
                    if (bIgnoreIncludes)
                    {
                        continue;
                    }

                    Regex IncludeRegex = new Regex("(?<=#include\\s+\").+(?=\")");    //(?<=#include\s+").+(?=")
                    Match IncludeMatch = IncludeRegex.Match(CurrentLine);

                    if (!IncludeMatch.Success)
                    {
                        throw new Exception();
                    }

                    string IncludeFileName = Path.GetFullPath(IncludeMatch.Value, Path.GetDirectoryName(Filename));

                    foreach (LTXData IncludeData in LTXDataFromFile(IncludeFileName, false))
                    {
                        yield return IncludeData;
                    }
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

                    yield return new LTXData(CurrentSectionName, CurrentSectionParent, Key, Value);
                }
            }
        }
    }
}
