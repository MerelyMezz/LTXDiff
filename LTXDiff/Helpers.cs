using System.IO;
using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;

namespace LTXDiff
{
    class Helpers
    {
        public static HashSet<string> GetFileNamesFromDirs(string DirPath, string Pattern, string BaseDir, string ModDir, bool bRecursive = false)
        {
            ModDir = ModDir == null ? BaseDir : ModDir;

            DirPath = Path.Combine(DirPath, Helpers.GetRegexMatch(Pattern, "^.+(?=\\\\[^\\\\]+$)"));
            Pattern = Path.GetFileName(Pattern);

            Func<string, string[]> GetFilesFromDir = Dir =>
            {
                if (!Directory.Exists(Dir))
                {
                    return new string[0];
                }

                return Directory.GetFiles(Dir, Pattern, bRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            };

            HashSet<string> AllFiles = new HashSet<string>();

            string[] AllModDirFiles = GetFilesFromDir(Path.GetFullPath(DirPath, ModDir));

            foreach (string ModDirFile in AllModDirFiles)
            {
                AllFiles.Add(ModDirFile);
            }

            string[] AllBaseDirFiles = GetFilesFromDir(Path.GetFullPath(DirPath, BaseDir));

            foreach (string BaseDirFile in AllBaseDirFiles)
            {
                //Don't include file if a mod file overrides it
                string RelativePath = Path.GetRelativePath(BaseDir, BaseDirFile);

                if (AllFiles.Contains(Path.GetFullPath(RelativePath, ModDir)))
                {
                    continue;
                }

                AllFiles.Add(BaseDirFile);
            }

            return AllFiles;
        }
        public static string FindFileFromMod(string FileName, string BaseDir, string ModDir)
        {
            HashSet<string> MatchingFiles = GetFileNamesFromDirs(Path.GetDirectoryName(GetRelativePath(BaseDir, ModDir, FileName)), Path.GetFileName(FileName), BaseDir, ModDir);

            if (MatchingFiles.Count != 1)
            {
                throw new Exception();
            }

            HashSet<string>.Enumerator E = MatchingFiles.GetEnumerator();

            E.MoveNext();
            return E.Current;
        }
        public static string GetRelativePath(string BaseDir, string ModDir, string FileName)
        {
            if (ModDir != null)
            {
                string RelativePath = Path.GetRelativePath(ModDir, FileName);

                if (RelativePath != FileName && !Helpers.IsRegexMatching(RelativePath, "^\\.\\."))
                {
                    return RelativePath;
                }
            }

            return Path.GetRelativePath(BaseDir, FileName);
        }

        public static string GetUpperDirectory(string Input)
        {
            string FakeBaseDir = "C:\\";

            return Path.GetRelativePath(FakeBaseDir, Path.GetFullPath(Path.Combine(Input, ".."), FakeBaseDir));
        }

        public static string ParseCommandLinePath(string Input)
        {
            if (Path.IsPathFullyQualified(Input))
            {
                return Input;
            }

            return Path.GetFullPath(Input, Directory.GetCurrentDirectory());
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
