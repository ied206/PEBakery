﻿/*
    Copyright (C) 2016-2018 Hajin Jang
    Licensed under GPL 3.0
 
    PEBakery is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.

    Additional permission under GNU GPL version 3 section 7

    If you modify this program, or any covered work, by linking
    or combining it with external libraries, containing parts
    covered by the terms of various license, the licensors of
    this program grant you additional permission to convey the
    resulting work. An external library is a library which is
    not derived from or based on this program. 
*/

using PEBakery.Helper;
using PEBakery.IniLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEBakery.Core.Commands
{
    public static class CommandScript
    {
        /*
         * WB082 Behavior
         * ExtractFile : DestDir must be Directory, create if not exists.
         * Ex) (...),README.txt,%BaseDir%\Temp\Hello
         *   -> No Hello : Create direcotry "Hello" and extract files into new directory.
         *   -> Hello is a file : Failure
         *   -> Hello is a directory : Extract files into directory.
         * 
         * ExtractAllFiles
         * Ex) (...),Fonts,%BaseDir%\Temp\Hello
         *   -> No Hello : Failure
         *   -> Hello is a file : Failure
         *   -> Hello is a direcotry : Extract files into directory.
         * 
         * PEBakery Behavior
         * ExtractFile/ExtractAllFiles : DestDir must be Directory, create if not exists.
         * Ex) (...),README.txt,%BaseDir%\Temp\Hello
         *   -> No Hello : Create direcotry "Hello" and extract files into new directory.
         *   -> Hello is a file : Failure
         *   -> Hello is a directory : Extract files into directory.
         */

        public static List<LogInfo> ExtractFile(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_ExtractFile));
            CodeInfo_ExtractFile info = cmd.Info as CodeInfo_ExtractFile;

            string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
            string dirName = StringEscaper.Preprocess(s, info.DirName);
            string fileName = StringEscaper.Preprocess(s, info.FileName);
            string destDir = StringEscaper.Preprocess(s, info.DestDir); // Should be directory name

            Script sc = Engine.GetScriptInstance(s, cmd, s.CurrentScript.RealPath, scriptFile, out _);

            if (StringEscaper.PathSecurityCheck(destDir, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            if (!Directory.Exists(destDir)) // DestDir already exists
            {
                if (File.Exists(destDir)) // Error, cannot proceed
                    return LogInfo.LogErrorMessage(logs, $"File [{destDir}] is not a directory.");
                
                Directory.CreateDirectory(destDir);
            }

            string destPath = Path.Combine(destDir, fileName);
            using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
            {
                EncodedFile.ExtractFile(sc, dirName, fileName, fs);
            }

            logs.Add(new LogInfo(LogState.Success, $"Encoded file [{fileName}] was extracted to [{destDir}]"));

            return logs;
        }

        public static List<LogInfo> ExtractAndRun(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_ExtractAndRun));
            CodeInfo_ExtractAndRun info = cmd.Info as CodeInfo_ExtractAndRun;

            string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
            string dirName = StringEscaper.Preprocess(s, info.DirName);
            string fileName = StringEscaper.Preprocess(s, info.FileName);
            List<string> parameters = StringEscaper.Preprocess(s, info.Params);

            Script sc = Engine.GetScriptInstance(s, cmd, s.CurrentScript.RealPath, scriptFile, out _);

            string destPath = Path.GetTempFileName();
            if (StringEscaper.PathSecurityCheck(destPath, out string errorMsg) == false)
            {
                logs.Add(new LogInfo(LogState.Error, errorMsg));
                return logs;
            }

            using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
            {
                EncodedFile.ExtractFile(sc, dirName, info.FileName, fs);
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = destPath,
                UseShellExecute = true,
            };
                /*
            if (!string.IsNullOrEmpty(info.Params))
            {
                string parameters = StringEscaper.Preprocess(s, info.Params);
                proc.StartInfo.Arguments = parameters;
                b.Append(" ");
                b.Append(parameters);
            }
            */
            Process.Start(startInfo);

            logs.Add(new LogInfo(LogState.Success, $"Encoded file [{fileName}] was extracted and executed"));

            return logs;
        }

        public static List<LogInfo> ExtractAllFiles(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_ExtractAllFiles));
            CodeInfo_ExtractAllFiles info = cmd.Info as CodeInfo_ExtractAllFiles;

            string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
            string dirName = StringEscaper.Preprocess(s, info.DirName);
            string destDir = StringEscaper.Preprocess(s, info.DestDir);

            Script sc = Engine.GetScriptInstance(s, cmd, s.CurrentScript.RealPath, scriptFile, out _);

            if (StringEscaper.PathSecurityCheck(destDir, out string errorMsg) == false)
                return LogInfo.LogErrorMessage(logs, errorMsg);

            List<string> dirs = sc.Sections["EncodedFolders"].Lines;
            bool dirNameValid = dirs.Any(d => d.Equals(dirName, StringComparison.OrdinalIgnoreCase));
            if (dirNameValid == false)
                return LogInfo.LogErrorMessage(logs, $"Directory [{dirName}] not exists in [{scriptFile}]");

            if (!Directory.Exists(destDir))
            {
                if (File.Exists(destDir))
                    return LogInfo.LogErrorMessage(logs, $"File [{destDir}] is not a directory");
                else
                    Directory.CreateDirectory(destDir);
            }

            List<string> lines = sc.Sections[dirName].Lines;
            Dictionary<string, string> fileDict = Ini.ParseIniLinesIniStyle(lines);
            foreach (string file in fileDict.Keys)
            {
                using (FileStream fs = new FileStream(Path.Combine(destDir, file), FileMode.Create, FileAccess.Write))
                {
                    EncodedFile.ExtractFile(sc, dirName, file, fs);
                }
            }

            logs.Add(new LogInfo(LogState.Success, $"Encoded folder [{dirName}] was extracted to [{destDir}]"));

            return logs;
        }

        public static List<LogInfo> Encode(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_Encode));
            CodeInfo_Encode info = cmd.Info as CodeInfo_Encode;

            string scriptFile = StringEscaper.Preprocess(s, info.ScriptFile);
            string dirName = StringEscaper.Preprocess(s, info.DirName);
            string filePath = StringEscaper.Preprocess(s, info.FilePath);

            EncodedFile.EncodeMode mode = EncodedFile.EncodeMode.ZLib;
            if (info.Compression != null)
            {
                string encodeModeStr = StringEscaper.Preprocess(s, info.Compression);
                if (encodeModeStr.Equals("None", StringComparison.OrdinalIgnoreCase))
                    mode = EncodedFile.EncodeMode.Raw;
                else if (encodeModeStr.Equals("Deflate", StringComparison.OrdinalIgnoreCase))
                    mode = EncodedFile.EncodeMode.ZLib;
                else if (encodeModeStr.Equals("LZMA", StringComparison.OrdinalIgnoreCase))
                    mode = EncodedFile.EncodeMode.XZ;
                else
                    return LogInfo.LogErrorMessage(logs, $"[{encodeModeStr}] is invalid compression");
            }
           
            Script sc = Engine.GetScriptInstance(s, cmd, s.CurrentScript.RealPath, scriptFile, out _);

            // Check srcFileName contains wildcard
            if (filePath.IndexOfAny(new char[] { '*', '?' }) == -1)
            { // No Wildcard
                EncodedFile.AttachFile(sc, dirName, Path.GetFileName(filePath), filePath, mode);
                logs.Add(new LogInfo(LogState.Success, $"[{filePath}] was encoded into [{sc.RealPath}]", cmd));
            }
            else
            { // With Wildcard
                // Use FileHelper.GetDirNameEx to prevent ArgumentException of Directory.GetFiles
                string srcDirToFind = FileHelper.GetDirNameEx(filePath);
                string[] files = Directory.GetFiles(srcDirToFind, Path.GetFileName(filePath));

                if (0 < files.Length)
                { // One or more file will be copidwed
                    logs.Add(new LogInfo(LogState.Success, $"[{filePath}] will be encoded into [{sc.RealPath}]", cmd));
                    for (int i = 0; i < files.Length; i++)
                    {
                        EncodedFile.AttachFile(sc, dirName, Path.GetFileName(files[i]), files[i], mode);
                        logs.Add(new LogInfo(LogState.Success, $"[{files[i]}] encoded ({i + 1}/{files.Length})", cmd));
                    }

                    logs.Add(new LogInfo(LogState.Success, $"[{files.Length}] files copied", cmd));
                }
                else
                { // No file will be compressed
                    logs.Add(new LogInfo(LogState.Warning, $"Files matching wildcard [{filePath}] were not found", cmd));
                }
            }

            return logs;
        }
    }
}
