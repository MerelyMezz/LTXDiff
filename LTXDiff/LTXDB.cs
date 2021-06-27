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

                //Get rid of comments
                CurrentLine = new Regex("^(?=;|//|$)|(^.+?(?=;|//|$))").Match(CurrentLine).Value;
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

                    continue;
                }

                //Defining new section
                if (new Regex("^\\[[^\\[\\]:\\s]+\\](:[^\\[\\]:]+)?$").Match(CurrentLine).Success)                                         //  ^\[[^\[\]:\s]+\](:[^\[\]:]+)?$                  i.e. is it in the form "[some_section]:some_parent"
                {
                    CurrentSectionName = new Regex("(?<=^\\[)[^\\[\\]:\\s]+(?=\\](:[^\\[\\]:]+)?$)").Match(CurrentLine).Value.Trim();      //  (?<=^\[)[^\[\]:\s]+(?=\](:[^\[\]:]+)?$)         i.e. extract sector name
                    CurrentSectionParent = new Regex("(?<=^\\[[^\\[\\]:\\s]+\\]:)[^\\[\\]:]+$").Match(CurrentLine).Value.Trim();           //  (?<=^\[[^\[\]:\s]+\]:)[^\[\]:]+$                i.e. extract parent name

                    continue;
                }

                //Key Value Pair
                if (new Regex("^[^\\[\\]:]+(\\s+)?=(\\s+)?.+$").Match(CurrentLine).Success)                                                //   ^[^\[\]:]+(\s+)?=(\s+)?.+$                     i.e. is it in the form "some_variable = some_value"
                {
                    string Key = new Regex("^[^\\[\\]:]+(?=(\\s+)?=(\\s+)?.+$)").Match(CurrentLine).Value.Trim();                          //   ^[^\[\]:]+(?=(\s+)?=(\s+)?.+$)                 i.e. extract variable name
                    string Value = new Regex("(?<=^[^\\[\\]:]+(\\s+)?=(\\s+)?).+$").Match(CurrentLine).Value.Trim();                       //   (?<=^[^\[\]:]+(\s+)?=(\s+)?).+$                i.e. extract variable value

                    yield return new LTXData(CurrentSectionName, CurrentSectionParent, Key, Value);

                    continue;
                }

                //Key Value Pair with empty value
                if (new Regex("^[^\\[\\]:]+((\\s+)?=)?$").Match(CurrentLine).Success)                                                       //   ^[^\[\]:]+((\s+)?=)?$                         i.e. is it in the form "some_variable =" or "some_variable"
                {
                    string Key = new Regex("^[^\\[\\]:]+(?=((\\s+)?=)?$)").Match(CurrentLine).Value.Trim();                                 //   ^[^\[\]:]+(?=((\s+)?=)?$)                     i.e. extract variable name

                    yield return new LTXData(CurrentSectionName, CurrentSectionParent, Key, "");

                    continue;
                }

                throw new Exception();
            }
        }
    }
}
