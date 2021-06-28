using System.IO;
using System.Text.RegularExpressions;
using System;

namespace LTXDiff
{
    class Helpers
    {
        public static string GetRelativePath(string BaseDir, string ModDir, string FileName)
        {
            string RelativePath = Path.GetRelativePath(ModDir, FileName);

            if (RelativePath != FileName && !Helpers.IsRegexMatching(RelativePath, "^\\.\\."))
            {
                return RelativePath;
            }

            return Path.GetRelativePath(BaseDir, FileName);
        }

        public static string GetUpperDirectory(string Input)
        {
            string FakeBaseDir = "C:\\";

            return Path.GetRelativePath(FakeBaseDir, Path.GetFullPath(Path.Combine(Input, ".."), FakeBaseDir));
        }

        public static string FullPath(string Input)
        {
            if (Path.IsPathFullyQualified(Input))
            {
                return Input;
            }

            return Path.GetFullPath(Input, Directory.GetCurrentDirectory());
        }

        public static string FindFileFromMod(string FileName, string BaseDir, string ModDir)
        {
            Func<string, string> FindFileInPath = DirPath =>
            {
                string FullFilePath = Path.GetFullPath(FileName, DirPath);

                if (File.Exists(FullFilePath))
                {
                    return FullFilePath;
                }

                return "";
            };

            string ModFile = FindFileInPath(ModDir);

            if (ModFile != "")
            {
                return ModFile;
            }

            return FindFileInPath(BaseDir);
        }

        public static void Print(string Input)
        {
            System.Console.WriteLine(Input);
        }

        public static void PrintC(string Input)
        {
            System.Console.Error.WriteLine(Input);
        }

        public static bool IsRegexMatching(string Input, string Pattern)
        {
            return new Regex(Pattern).Match(Input).Success;
        }

        public static string GetRegexMatch(string Input, string Pattern)
        {
            return new Regex(Pattern).Match(Input).Value.Trim();
        }

        public static string GetRegexReplacement(string Input, string Pattern, string Replacement)
        {
            return new Regex(Pattern).Replace(Input, Replacement);
        }
    }
}
