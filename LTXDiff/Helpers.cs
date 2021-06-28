using System;
using System.Collections.Generic;
using System.Text;

using System.Text.RegularExpressions;

namespace LTXDiff
{
    class Helpers
    {
        public static bool IsRegexMatching(string Input, string Pattern)
        {
            return new Regex(Pattern).Match(Input).Success;
        }

        public static string GetRegexMatch(string Input, string Pattern)
        {
            return new Regex(Pattern).Match(Input).Value.Trim();
        }
    }
}
