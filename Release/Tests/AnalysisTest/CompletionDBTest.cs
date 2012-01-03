﻿/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace AnalysisTest {
    [TestClass]
    [DeploymentItem(@"..\\PythonTools\\CompletionDB\\", "CompletionDB")]
    [DeploymentItem(@"..\\PythonTools\\PythonScraper.py")]
    [DeploymentItem(@"..\\PythonTools\\BuiltinScraper.py")]
    [DeploymentItem(@"..\\PythonTools\\IronPythonScraper.py")]
    [DeploymentItem(@"..\\PythonTools\\ExtensionScraper.py")]
    [DeploymentItem("PyDebugAttach.dll")]
    [DeploymentItem("Microsoft.PythonTools.Analyzer.exe")]
    [DeploymentItem(@"Python.VS.TestData\", "Python.VS.TestData")]
    public class CompletionDBTest {
        
        [TestMethod]
        public void TestOpen() {
            foreach (var path in PythonPaths.Versions) {
                Console.WriteLine(path.Path);

                Guid testId = Guid.NewGuid();
                var testDir = Path.Combine(Path.GetTempPath(), testId.ToString());
                Directory.CreateDirectory(testDir);

                // run the scraper
                var startInfo = new ProcessStartInfo(path.Path,
                    String.Format("\"{2}\" \"{0}\" \"{1}\"", 
                        testDir, 
                        Path.Combine(Directory.GetCurrentDirectory(), "CompletionDB"), 
                        Path.Combine(Directory.GetCurrentDirectory(), "PythonScraper.py")
                    )
                );

                var process = Process.Start(startInfo);
                process.WaitForExit();

                // it should succeed
                Assert.AreEqual(process.ExitCode, 0);

                // perform some basic validation
                dynamic builtinDb = Unpickle.Load(new FileStream(Path.Combine(testDir, path.Version.Is3x() ? "builtins.idb" : "__builtin__.idb"), FileMode.Open, FileAccess.Read));
                if (path.Version.Is2x()) { // no open in 3.x
                    foreach (var overload in builtinDb["members"]["open"]["value"]["overloads"]) {
                        Assert.AreEqual(overload["ret_type"][0], "__builtin__");
                        Assert.AreEqual(overload["ret_type"][1], "file");
                    }
                }
            }
        }

        [TestMethod]
        public void TestPthFiles() {
            var outputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(outputPath);

            string args = String.Join(" ",
                "/dir", '"' + Path.Combine(Environment.CurrentDirectory, "Python.VS.TestData", "PathStdLib") + '"',
                "/version", "V27",
                "/outdir", '"' + outputPath + '"',
                "/indir", '"' + Path.Combine(Environment.CurrentDirectory, "CompletionDB") + '"'
            );

            // run the analyzer
            var startInfo = new ProcessStartInfo("Microsoft.PythonTools.Analyzer.exe", args);
            var proc = Process.Start(startInfo);
            proc.WaitForExit();
            Assert.AreEqual(proc.ExitCode, 0);

            File.Copy(Path.Combine(Environment.CurrentDirectory, "CompletionDB", "__builtin__.idb"), Path.Combine(outputPath, "__builtin__.idb"));

            var typeDb = new PythonTypeDatabase(outputPath, new Version(2, 7));
            var module = typeDb.GetModule("SomeLib");
            Assert.AreNotEqual(null, module);
            var fooMod = module.GetMember(null, "foo");
            Assert.AreNotEqual(null, fooMod);

            var cClass = ((IPythonModule)fooMod).GetMember(null, "C");
            Assert.AreNotEqual(null, cClass);

            Assert.AreEqual(PythonMemberType.Class, cClass.MemberType);
        }

        /// <summary>
        /// Checks that members removed or introduced in later versions show up or don't in
        /// earlier versions as appropriate.
        /// </summary>
        [TestMethod]
        public void VersionedSharedDatabase() {
            var twoFive = PythonTypeDatabase.CreateDefaultTypeDatabase(new Version(2, 5));
            var twoSix = PythonTypeDatabase.CreateDefaultTypeDatabase(new Version(2, 6));
            var twoSeven = PythonTypeDatabase.CreateDefaultTypeDatabase(new Version(2, 7));
            var threeOh = PythonTypeDatabase.CreateDefaultTypeDatabase(new Version(3, 0));
            var threeOne = PythonTypeDatabase.CreateDefaultTypeDatabase(new Version(3, 1));
            var threeTwo = PythonTypeDatabase.CreateDefaultTypeDatabase(new Version(3, 2));

            // new in 2.6
            Assert.AreEqual(null, twoFive.BuiltinModule.GetAnyMember("bytearray"));
            foreach (var version in new[] { twoSix, twoSeven, threeOh, threeOne, threeTwo }) {
                Assert.AreNotEqual(version, version.BuiltinModule.GetAnyMember("bytearray"));
            }

            // new in 2.7
            Assert.AreEqual(null, twoSix.BuiltinModule.GetAnyMember("memoryview"));
            foreach (var version in new[] { twoSeven, threeOh, threeOne, threeTwo }) {
                Assert.AreNotEqual(version, version.BuiltinModule.GetAnyMember("memoryview"));
            }

            // not in 3.0
            foreach (var version in new[] { twoFive, twoSix, twoSeven }) {
                Assert.AreNotEqual(null, version.BuiltinModule.GetAnyMember("StandardError"));
            }

            foreach (var version in new[] { threeOh, threeOne, threeTwo }) {
                Assert.AreEqual(null, version.BuiltinModule.GetAnyMember("StandardError"));
            }

            // new in 3.0
            foreach (var version in new[] { twoFive, twoSix, twoSeven }) {
                Assert.AreEqual(null, version.BuiltinModule.GetAnyMember("exec"));
                Assert.AreEqual(null, version.BuiltinModule.GetAnyMember("print"));
            }

            foreach (var version in new[] { threeOh, threeOne, threeTwo }) {
                Assert.AreNotEqual(null, version.BuiltinModule.GetAnyMember("exec"));
                Assert.AreNotEqual(null, version.BuiltinModule.GetAnyMember("print"));
            }


            // new in 3.1
            foreach (var version in new[] { twoFive, twoSix, twoSeven, threeOh }) {
                Assert.AreEqual(null, version.GetModule("sys").GetMember(null, "int_info"));
            }

            foreach (var version in new[] { threeOne, threeTwo }) {
                Assert.AreNotEqual(null, version.GetModule("sys").GetMember(null, "int_info"));
            }

            // new in 3.2
            foreach (var version in new[] { twoFive, twoSix, twoSeven, threeOh, threeOne }) {
                Assert.AreEqual(null, version.GetModule("sys").GetMember(null, "setswitchinterval"));
            }

            foreach (var version in new[] { threeTwo }) {
                Assert.AreNotEqual(null, version.GetModule("sys").GetMember(null, "setswitchinterval"));
            }
        }
    }
}
