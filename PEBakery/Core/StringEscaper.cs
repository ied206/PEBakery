﻿using PEBakery.Exceptions;
using PEBakery.Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PEBakery.Core
{
    public static class StringEscaper
    {
        #region Static Variables and Constructor
        private static readonly List<string> forbiddenPaths = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), 
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), 
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), 
        };
        #endregion

        #region PathSecurityCheck
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns>Return false if path is forbidden</returns>
        public static bool PathSecurityCheck(string path, out string errorMsg)
        {
            bool containsInvalidChars = false;
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char ch in invalidChars)
            {
                if (path.IndexOf(ch) != -1)
                    containsInvalidChars = true;
            }

            string fullPath;
            if (containsInvalidChars)
                fullPath = Path.GetFullPath(FileHelper.GetDirNameEx(path));
            else
                fullPath = Path.GetFullPath(path);

            foreach (string f in forbiddenPaths)
            {
                if (fullPath.StartsWith(f, StringComparison.OrdinalIgnoreCase))
                {
                    errorMsg = $"Cannot write into [{path}], [{f}] is a write protected directory";
                    return false;
                }
            }
            errorMsg = string.Empty;
            return true;
        }
        #endregion

        #region EscapeString
        private static readonly Dictionary<string, string> unescapeSeqs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { @"#$c", @"," },
            { @"#$p", @"%" },
            { @"#$q", "\"" },
            { @"#$s", @" " },
            { @"#$t", "\t"},
            { @"#$x", Environment.NewLine},
        };

        public static readonly string Legend = "#$c = Comma [,]\r\n#$p = Percent [%]\r\n#$q = DoubleQuote [\"]\r\n#$s = Space [ ]\r\n#$t = Tab [\t]\r\n#$x = NewLine\r\n## = Sharp [#]";

        public static string Unescape(string str, bool escapePercent = false)
        {
            int idx = 0;
            StringBuilder b = new StringBuilder();
            while (idx < str.Length)
            {
                int hIdx = str.IndexOf('#', idx);
                if (hIdx == -1)
                { // # (X)
                    b.Append(str.Substring(idx));
                    break;
                }
                else
                { // # (O)
                    b.Append(str.Substring(idx, hIdx - idx));
                    if (hIdx + 1 < str.Length)
                    {
                        char ch1 = str[hIdx + 1];
                        if (ch1 == '#')
                        { // ## -> [#]
                            b.Append('#');
                            idx = hIdx + 2;
                        }
                        else if (ch1 == '$')
                        {
                            if (idx + 2 < str.Length)
                            {
                                char ch2 = str[hIdx + 2];
                                switch (ch2)
                                {
                                    case 'c': // #$c -> [,]
                                        b.Append(',');
                                        break;
                                    case 'p': // #$p -> [%]
                                        b.Append('%');
                                        break;
                                    case 'q': // #$q -> ["]
                                        b.Append('"');
                                        break;
                                    case 's': // #$s -> [ ]
                                        b.Append(' ');
                                        break;
                                    case 't': // #$t -> [   ]
                                        b.Append('\t');
                                        break;
                                    case 'x': // #$x -> [\r\n]
                                        b.Append("\r\n");
                                        break;
                                    default: // No escape
                                        b.Append(@"#$");
                                        idx = hIdx + 2;
                                        continue;
                                }
                                idx = hIdx + 3;
                            }
                            else
                            { // Last 2 characters of string
                                b.Append("#$");
                                idx = hIdx + 2;
                            }                               
                        }
                        else
                        {
                            b.Append('#');
                            idx = hIdx + 1;
                        }
                    }
                    else
                    { // Last character of string
                        b.Append('#');
                        idx = hIdx + 1;
                    }
                }
            }
            str = b.ToString();

            if (escapePercent)
                str = UnescapePercent(str);
            
            return str;
        }

        public static List<string> Unescape(IEnumerable<string> strs, bool escapePercent = false)
        {
            List<string> unescaped = new List<string>(strs.Count());
            foreach (string str in strs)
                unescaped.Add(Unescape(str, escapePercent));
            return unescaped;
        }

        public static string QuoteUnescape(string str, bool escapePercent = false)
        {
            return Unescape(str.Trim('\"'), escapePercent);
        }

        public static List<string> QuoteUnescape(IEnumerable<string> strs, bool escapePercent = false)
        {
            List<string> unescaped = new List<string>();
            foreach (string str in strs)
                unescaped.Add(QuoteUnescape(str, escapePercent));
            return unescaped;
        }

        public static string UnescapePercent(string str)
        {
            return str.Replace(@"#$p", @"%");
        }

        public static List<string> UnescapePercent(IEnumerable<string> strs)
        {
            List<string> unescaped = new List<string>();
            foreach (string str in strs)
                unescaped.Add(UnescapePercent(str));
            return unescaped;
        }

        private static readonly Dictionary<string, string> fullEscapeSeqs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { @",", @"#$c" },
            { "\"", @"#$q" },
            { @" ", @"#$s" },
            { "\t", @"#$t" },
            { Environment.NewLine, @"#$x" },
        };

        private static readonly Dictionary<string, string> escapeSeqs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "\"", @"#$q" },
            { "\t", @"#$t" },
            { Environment.NewLine, @"#$x" },
        };

        public static string Escape(string str, bool fullEscape = false, bool escapePercent = false)
        {
            // Escape # first
            // Keys.Aggregate를 쓰고 싶지만 그렇게 하면 #과 $가 서로를 escaping해버리는 참사가 발생한다.
            if (str.IndexOf('#') != -1)
            {
                int idx = 0;
                StringBuilder b = new StringBuilder();
                while (idx < str.Length)
                {
                    int hIdx = str.IndexOf('#', idx);

                    if (hIdx == -1)
                    { // # (X)
                        b.Append(str.Substring(idx));
                        break;
                    }
                    else
                    { // # (O)
                        b.Append(str.Substring(idx, hIdx - idx));
                        b.Append(@"##");
                        idx = hIdx += 1;
                    }
                }
                str = b.ToString();
            }

            Dictionary<string, string> dict;
            if (fullEscape)
                dict = fullEscapeSeqs;
            else
                dict = escapeSeqs;

            str = dict.Keys.Aggregate(str, (from, to) => from.Replace(to, dict[to]));

            if (escapePercent)
                str = EscapePercent(str);
            
            return str;
        }

        public static List<string> Escape(IEnumerable<string> strs, bool fullEscape = false, bool escapePercent = false)
        {
            List<string> escaped = new List<string>(strs.Count());
            foreach (string str in strs)
                escaped.Add(Escape(str, fullEscape, escapePercent));
            return escaped;
        }

        public static string EscapePercent(string str)
        {
            return StringHelper.ReplaceEx(str, @"%", @"#$p", StringComparison.Ordinal);
        }

        public static List<string> EscapePercent(IEnumerable<string> strs)
        {
            List<string> escaped = new List<string>(strs.Count());
            foreach (string str in strs)
                escaped.Add(EscapePercent(str));
            return escaped;
        }

        public static string Doublequote(string str)
        {
            if (str.Contains(' '))
                return "\"" + str + "\"";
            else
                return str;
        }

        public static string QuoteEscape(string str, bool fullEscape = false, bool escapePercent = false)
        {
            bool needQuote = false;

            // Check if str need doublequote escaping
            if (str.Contains(' ') || str.Contains(','))
                needQuote = true;

            // Escape characters
            str = Escape(str, fullEscape, escapePercent); // WB082 escape sequence
            if (needQuote)
                str = Doublequote(str); // Doublequote escape
            return str;
        }

        public static List<string> QuoteEscape(IEnumerable<string> strs, bool fullEscape = false, bool escapePercent = false)
        {
            List<string> escaped = new List<string>(strs.Count());
            foreach (string str in strs)
                escaped.Add(QuoteEscape(str, fullEscape, escapePercent));
            return escaped;
        }
        #endregion

        #region Variables
        /// <summary>
        /// Expand #n and %Var% variables.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string ExpandVariables(EngineState s, string str)
        {
            return s.Variables.Expand(ExpandSectionParams(s, str));
        }

        public static List<string> ExpandVariables(EngineState s, IEnumerable<string> strs)
        {
            List<string> list = new List<string>(strs.Count());
            foreach (string str in strs)
                list.Add(s.Variables.Expand(ExpandSectionParams(s, str)));
            return list;
        }

        public static string ExpandVariables(Variables vars, string str)
        {
            return vars.Expand(str);
        }

        public static List<string> ExpandVariables(Variables vars, IEnumerable<string> strs)
        {
            List<string> list = new List<string>(strs.Count());
            foreach (string str in strs)
                list.Add(vars.Expand(str));
            return list;
        }

        /// <summary>
        /// Expand #1, #2, #3, etc...
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string ExpandSectionParams(EngineState s, string str)
        {
            // Expand #1 into its value
            Regex regex = new Regex(@"(?<!#)(#[0-9]+)", RegexOptions.Compiled);

            MatchCollection matches = regex.Matches(str);
            while (0 < matches.Count)
            {
                StringBuilder b = new StringBuilder();
                for (int x = 0; x < matches.Count; x++)
                {
                    string pIdxStr = matches[x].Groups[1].ToString().Substring(1);
                    if (!int.TryParse(pIdxStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pIdx))
                        throw new InternalException("ExpandVariables failure");

                    if (x == 0)
                    {
                        b.Append(str.Substring(0, matches[0].Index));
                    }
                    else
                    {
                        int startOffset = matches[x - 1].Index + matches[x - 1].Value.Length;
                        int endOffset = matches[x].Index - startOffset;
                        b.Append(str.Substring(startOffset, endOffset));
                    }

                    string param;
                    if (s.CurSectionParams.ContainsKey(pIdx))
                    {
                        param = s.CurSectionParams[pIdx];
                    }
                    else
                    { 
                        if (s.CurDepth == 1) // Dirty Hack for WB082 compatibility
                            param = $"##{pIdx}"; // [Process] -> Should return #{pIdx} even it was not found
                        else
                            param = string.Empty; // Not in entry section -> return string.Empty;
                    }
                    b.Append(param);

                    if (x + 1 == matches.Count) // Last iteration
                    {
                        b.Append(str.Substring(matches[x].Index + matches[x].Value.Length));
                    }
                }
                str = b.ToString();

                matches = regex.Matches(str);
            }

            // Escape #a (Current Argument Count)
            if (str.IndexOf("#a", StringComparison.Ordinal) != -1)
                str = StringHelper.ReplaceEx(str, "#a", s.CurSectionParamsCount.ToString(), StringComparison.Ordinal);

            // Escape #r (Return Value)
            if (str.IndexOf("#r", StringComparison.Ordinal) != -1)
                str = StringHelper.ReplaceEx(str, "#r", s.SectionReturnValue, StringComparison.Ordinal);

            // Escape #c (Loop Counter)
            if (s.LoopRunning) 
                str = StringHelper.ReplaceEx(str, "#c", s.LoopCounter.ToString(), StringComparison.Ordinal);

            return str;
        }

        public static List<string> ExpandSectionParams(EngineState s, IEnumerable<string> strs)
        {
            List<string> list = new List<string>(strs.Count());
            foreach (string str in strs)
                list.Add(ExpandSectionParams(s, str));
            return list;
        }
        #endregion

        #region Preprocess
        public static string Preprocess(EngineState s, string str, bool escapePercent = true)
        {
            return Unescape(ExpandVariables(s, str), escapePercent);
        }

        public static List<string> Preprocess(EngineState s, IEnumerable<string> strs, bool escapePercent = true)
        {
            return Unescape(ExpandVariables(s, strs), escapePercent);
        }

        public static string Preprocess(Variables vars, string str, bool escapePercent = true)
        {
            return Unescape(ExpandVariables(vars, str), escapePercent);
        }

        public static List<string> Preprocess(Variables vars, IEnumerable<string> strs, bool escapePercent = true)
        {
            return Unescape(ExpandVariables(vars, strs), escapePercent);
        }
        #endregion

        #region Registry
        public static string PackRegBinary(byte[] bin, bool escape = false)
        { // Ex) 43,00,3A,00,5C,00,55,00,73,00,65,00,72,00,73,00,5C,00,4A,00,6F,00,76,00,65,00,6C,00,65,00,72,00,5C,00,4F,00,6E,00,65,00,44,00,72,00,69,00,76,00,65,00,00,00
            string seperator =  ",";
            if (escape)
                seperator = "#$c";

            StringBuilder b = new StringBuilder();
            for (int i = 0; i < bin.Length; i++)
            {
                b.Append(bin[i].ToString("X2"));
                if (i + 1 < bin.Length)
                    b.Append(seperator);
            }

            return b.ToString();
        }

        public static string PackRegBinary(string[] strs, bool escape = false)
        { // Ex) 43,00,3A,00,5C,00,55,00,73,00,65,00,72,00,73,00,5C,00,4A,00,6F,00,76,00,65,00,6C,00,65,00,72,00,5C,00,4F,00,6E,00,65,00,44,00,72,00,69,00,76,00,65,00,00,00
            string seperator = ",";
            if (escape)
                seperator = "#$c";

            StringBuilder b = new StringBuilder();
            for (int i = 0; i < strs.Length; i++)
            {
                b.Append(strs[i]);
                if (i + 1 < strs.Length)
                    b.Append(seperator);
            }

            return b.ToString();
        }

        public static bool UnpackRegBinary(string packStr, out byte[] bin)
        { // Ex) 43,00,3A,00,5C,00,55,00,73,00,65,00,72,00,73,00,5C,00,4A,00,6F,00,76,00,65,00,6C,00,65,00,72,00,5C,00,4F,00,6E,00,65,00,44,00,72,00,69,00,76,00,65,00,00,00
            int count = (packStr.Length + 1) / 3;
            bin = new byte[count]; // 3n-1

            for (int i = 0; i < count; i++)
            {
                if (!byte.TryParse(packStr.Substring(i * 3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bin[i]))
                    return false;
            }

            return true;
        }

        public static bool UnpackRegBinary(string[] packStrs, out byte[] bin)
        { // Ex) 43,00,3A,00,5C,00,55,00,73,00,65,00,72,00,73,00,5C,00,4A,00,6F,00,76,00,65,00,6C,00,65,00,72,00,5C,00,4F,00,6E,00,65,00,44,00,72,00,69,00,76,00,65,00,00,00
            bin = new byte[packStrs.Length];

            for (int i = 0; i < packStrs.Length; i++)
            {
                if (!byte.TryParse(packStrs[i], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bin[i]))
                    return false;
            }

            return true;
        }

        public static string PackRegMultiBinary(IEnumerable<string> multiStrs)
        {
            StringBuilder b = new StringBuilder();

            string[] list = multiStrs.ToArray();
            for (int i = 0; i < list.Length; i++)
            {
                byte[] bin = Encoding.Unicode.GetBytes(list[i]);
                b.Append(StringEscaper.PackRegBinary(bin));
                if (i + 1 < list.Length)
                    b.Append(",00,00,");
            }

            return b.ToString();
        }

        public static string PackRegMultiString(IEnumerable<string> multiStrs)
        {
            // RegRead,HKLM,SOFTWARE\Microsoft\Windows NT\CurrentVersion\FontLink\SystemLink,Batang,%A%
            // MSMINCHO.TTC,MS PMincho#$zMINGLIU.TTC,PMingLiU#$zSIMSUN.TTC,SimSun#$zMALGUN.TTF,Malgun Gothic#$zYUGOTHM.TTC,Yu Gothic UI#$zMSJH.TTC,Microsoft JhengHei UI#$zMSYH.TTC,Microsoft YaHei UI#$zSEGUISYM.TTF,Segoe UI Symbol

            string[] list = multiStrs.ToArray();

            StringBuilder b = new StringBuilder();
            for (int i = 0; i < list.Length; i++)
            {
                b.Append(list[i]);
                if (i + 1 < list.Length)
                    b.Append("#$z");
            }
            return b.ToString();
        }

        public static List<string> UnpackRegMultiString(string packStr)
        {
            // RegRead,HKLM,SOFTWARE\Microsoft\Windows NT\CurrentVersion\FontLink\SystemLink,Batang,%A%
            // MSMINCHO.TTC,MS PMincho#$zMINGLIU.TTC,PMingLiU#$zSIMSUN.TTC,SimSun#$zMALGUN.TTF,Malgun Gothic#$zYUGOTHM.TTC,Yu Gothic UI#$zMSJH.TTC,Microsoft JhengHei UI#$zMSYH.TTC,Microsoft YaHei UI#$zSEGUISYM.TTF,Segoe UI Symbol

            List<string> list = new List<string>();

            string next = packStr;
            while (next != null)
            {
                int pIdx = next.IndexOf("#$z", StringComparison.Ordinal);
                if (pIdx != -1)
                { // Not Last One
                    string now = next.Substring(0, pIdx);
                    next = next.Substring(pIdx + 3);

                    list.Add(now);
                }
                else
                { // Last One
                    list.Add(next);

                    next = null;
                }
            }

            return list;
        }
        #endregion
    }
}
