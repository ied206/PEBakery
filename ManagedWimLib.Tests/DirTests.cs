﻿/*
    Licensed under LGPLv3

    Derived from wimlib's original header files
    Copyright (C) 2012, 2013, 2014 Eric Biggers

    C# Wrapper written by Hajin Jang
    Copyright (C) 2017-2018 Hajin Jang

    This file is free software; you can redistribute it and/or modify it under
    the terms of the GNU Lesser General Public License as published by the Free
    Software Foundation; either version 3 of the License, or (at your option) any
    later version.

    This file is distributed in the hope that it will be useful, but WITHOUT
    ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
    FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public License for more
    details.

    You should have received a copy of the GNU Lesser General Public License
    along with this file; if not, see http://www.gnu.org/licenses/.
*/

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ManagedWimLib;
using System.IO;
using System.Threading;
using System.Collections.Generic;

namespace ManagedWimLib.Tests
{
    [TestClass]
    public class DirTests
    {
        #region DirProgress
        // TODO: Enable unit test
        // Strange enough, Wim.IterateDirTree kills process because of FatalExecutionEngineError.
        // But that function works well in real apps, which is very confusing.
        [TestMethod]
        [TestCategory("WimLib")]
        public void Dir()
        {
            Dir_Template("XPRESS.wim");
            Dir_Template("LZX.wim");
            Dir_Template("LZMS.wim");
            Dir_Template("BootLZX.wim");
            Dir_Template("BootXPRESS.wim");
        }

        public CallbackStatus IterateDirTree_Callback(DirEntry dentry, object userData)
        {
            List<string> entries = userData as List<string>;

            entries.Add(dentry.FullPath);
            Console.WriteLine(dentry.FullPath);

            return CallbackStatus.CONTINUE;
        }

        public void Dir_Template(string fileName)
        {
            List<string> entries = new List<string>();

            string wimFile = Path.Combine(TestSetup.SampleDir, fileName);
            using (Wim wim = Wim.OpenWim(wimFile, OpenFlags.DEFAULT))
            {
                wim.IterateDirTree(1, @"\", IterateFlags.RECURSIVE, IterateDirTree_Callback, entries);
            }

            TestHelper.CheckList_Src01(entries);
        }
        #endregion
    }
}
