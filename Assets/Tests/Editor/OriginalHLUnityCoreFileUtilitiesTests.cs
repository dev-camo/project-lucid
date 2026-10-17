using System;
using System.Collections.Generic;
using System.IO;
using Hardlight;
using Hardlight.Utils;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // Behaviour of the original HLUnityCore.Runtime FileUtilities (020002c8) and
    // DirectoryUtilities (02000268) against their 1.10.1 x86-64 bodies. Disk work
    // stays inside a directory this fixture creates and deletes.
    [TestFixture]
    public sealed class OriginalHLUnityCoreFileUtilitiesTests
    {
        private string m_root;

        [SetUp]
        public void SetUp()
        {
            m_root = Path.Combine(Path.GetTempPath(), "ProjectLucid-HLUnityCoreFiles-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_root))
            {
                Directory.Delete(m_root, true);
            }
        }

        // GetLocalFileUri 0x0600120a (0x1b34410) returns only the prefix and
        // GetPathWithLocalFileUri 0x0600120b (0x1b34490) appends the path to it,
        // so blank input comes back doubled.
        [Test]
        public void LocalFileUriPrefixDependsOnTheLeadingSlash()
        {
            Assert.That(FileUtilities.GetLocalFileUri("/tmp/a"), Is.EqualTo("file://"));
            Assert.That(FileUtilities.GetLocalFileUri("C:/a"), Is.EqualTo("file:///"));
            Assert.That(FileUtilities.GetLocalFileUri(" "), Is.EqualTo(" "));
            Assert.That(FileUtilities.GetPathWithLocalFileUri("/tmp/a"), Is.EqualTo("file:///tmp/a"));
            Assert.That(FileUtilities.GetPathWithLocalFileUri("C:/a"), Is.EqualTo("file:///C:/a"));
            Assert.That(FileUtilities.GetPathWithLocalFileUri("  "), Is.EqualTo("    "));
        }

        // SanitiseUnixPath 0x06001214 (0x1b35160), string literals "../", "/..",
        // "./", "/.", "://" and "/" resolved from shipped metadata.
        [Test]
        public void SanitiseUnixPathFoldsSlashesAndResolvesDotSegments()
        {
            Assert.That(FileUtilities.SanitiseUnixPath(null), Is.Null);
            Assert.That(FileUtilities.SanitiseUnixPath("  "), Is.EqualTo("  "));
            Assert.That(FileUtilities.SanitiseUnixPath(" ../ "), Is.EqualTo("/"));
            Assert.That(FileUtilities.SanitiseUnixPath("/.."), Is.EqualTo("/"));
            Assert.That(FileUtilities.SanitiseUnixPath("./"), Is.EqualTo("/"));
            Assert.That(FileUtilities.SanitiseUnixPath("a//b"), Is.EqualTo("a/b"));
            Assert.That(FileUtilities.SanitiseUnixPath("/a/./b/../c/"), Is.EqualTo("/a/c/"));
            Assert.That(FileUtilities.SanitiseUnixPath("http://host//x/../y"), Is.EqualTo("http://host/y"));
            Assert.That(FileUtilities.SanitiseUnixPath("../../a"), Is.EqualTo("/a"), "an unmatched ../ falls back to the prefix");
            Assert.That(FileUtilities.SanitiseUnixPath("/a/b/."), Is.EqualTo("/a/b/"));
            Assert.That(FileUtilities.SanitiseUnixPath("/a/b/.."), Is.EqualTo("/a/"), "the trailing /.. pops twice");
        }

        // WriteAllLinesWithoutFinalNewLine 0x0600120c (0x1b34520): WriteLine for all
        // but the last line, Write for the last; an empty list creates the file
        // and then fails reading lines[-1].
        [Test]
        public void WriteAllLinesWithoutFinalNewLineOmitsOnlyTheLastNewLine()
        {
            string path = Path.Combine(m_root, "lines.txt");
            FileUtilities.WriteAllLinesWithoutFinalNewLine(path, new List<string> { "x", "y" });
            Assert.That(File.ReadAllText(path), Is.EqualTo("x" + Environment.NewLine + "y"));

            string emptyPath = Path.Combine(m_root, "empty.txt");
            Assert.Throws<ArgumentOutOfRangeException>(() => FileUtilities.WriteAllLinesWithoutFinalNewLine(emptyPath, new List<string>()));
            Assert.That(File.Exists(emptyPath), Is.True);
            Assert.That(new FileInfo(emptyPath).Length, Is.EqualTo(0));
        }

        // WriteAllLinesWithoutFinalNewLine 0x0600120c (0x1b34520): a null or empty
        // path and then a null list each log through HLOutput.LogError and return
        // before File.Create.
        [Test]
        public void WriteAllLinesWithoutFinalNewLineReturnsBeforeCreatingOnMissingInput()
        {
            Assert.DoesNotThrow(() => FileUtilities.WriteAllLinesWithoutFinalNewLine(null, new List<string> { "x" }));
            Assert.DoesNotThrow(() => FileUtilities.WriteAllLinesWithoutFinalNewLine(string.Empty, new List<string> { "x" }));

            string path = Path.Combine(m_root, "unwritten.txt");
            Assert.DoesNotThrow(() => FileUtilities.WriteAllLinesWithoutFinalNewLine(path, null));
            Assert.That(File.Exists(path), Is.False);
        }

        // ReadWriteLines 0x0600120f, 0x06001210 and 0x06001211: each path's lines go
        // to the callback parameter in path order and are all written back.
        [Test]
        public void ReadWriteLinesPassesEachPathsLinesInParameterOrder()
        {
            string[] paths = new string[5];
            for (int i = 0; i < paths.Length; i++)
            {
                paths[i] = Path.Combine(m_root, "file" + i + ".txt");
                File.WriteAllText(paths[i], "line" + i);
            }

            FileUtilities.ReadWriteLines(paths[0], paths[1], paths[2], (lines1, lines2, lines3) =>
            {
                lines1.Add("a");
                lines2.Add("b");
                lines3.Add("c");
            });
            Assert.That(File.ReadAllLines(paths[0]), Is.EqualTo(new[] { "line0", "a" }));
            Assert.That(File.ReadAllLines(paths[1]), Is.EqualTo(new[] { "line1", "b" }));
            Assert.That(File.ReadAllLines(paths[2]), Is.EqualTo(new[] { "line2", "c" }));

            FileUtilities.ReadWriteLines(paths[0], paths[1], paths[2], paths[3], (lines1, lines2, lines3, lines4) =>
            {
                lines4.Add(lines1[0]);
                lines1.Clear();
            });
            Assert.That(File.ReadAllLines(paths[0]), Is.Empty);
            Assert.That(File.ReadAllLines(paths[3]), Is.EqualTo(new[] { "line3", "line0" }));

            FileUtilities.ReadWriteLines(paths[0], paths[1], paths[2], paths[3], paths[4], (lines1, lines2, lines3, lines4, lines5) =>
            {
                lines5.Add(lines2[0]);
                lines5.Add(lines3[0]);
            });
            Assert.That(File.ReadAllLines(paths[4]), Is.EqualTo(new[] { "line4", "line1", "line2" }));
            Assert.That(File.ReadAllLines(paths[1]), Is.EqualTo(new[] { "line1", "b" }));
        }

        // ReadWriteLines 0x0600120d/0e with MultiReadLines 0x06001212 (Dictionary.Add)
        // and MultiWriteLines 0x06001213 (File.WriteAllLines).
        [Test]
        public void ReadWriteLinesRoundTripsTheCallbackEdits()
        {
            string first = Path.Combine(m_root, "first.txt");
            string second = Path.Combine(m_root, "second.txt");
            File.WriteAllText(first, "a" + Environment.NewLine + "b");
            File.WriteAllText(second, "c");

            FileUtilities.ReadWriteLines(first, lines => lines.Add("tail"));
            Assert.That(File.ReadAllText(first), Is.EqualTo("a" + Environment.NewLine + "b" + Environment.NewLine + "tail" + Environment.NewLine));

            FileUtilities.ReadWriteLines(first, second, (lines1, lines2) =>
            {
                lines2.Add(lines1[0]);
                lines1.Clear();
            });
            Assert.That(File.ReadAllText(first), Is.EqualTo(string.Empty));
            Assert.That(File.ReadAllText(second), Is.EqualTo("c" + Environment.NewLine + "a" + Environment.NewLine));

            Assert.Throws<ArgumentException>(() => FileUtilities.MultiReadLines(second, second));
        }

        // DirectoryUtilities.Clear 0x06000f3b (0x1b1ce60): subdirectories are deleted
        // before files, so a non-recursive failure leaves the files in place.
        [Test]
        public void ClearDeletesSubdirectoriesBeforeFilesAndKeepsTheDirectory()
        {
            string nested = Path.Combine(m_root, "nested");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested, "inner.txt"), "inner");
            string topFile = Path.Combine(m_root, "top.txt");
            File.WriteAllText(topFile, "top");

            Assert.Throws<IOException>(() => DirectoryUtilities.Clear(m_root));
            Assert.That(File.Exists(topFile), Is.True);
            Assert.That(Directory.Exists(nested), Is.True);

            DirectoryUtilities.Clear(m_root, true);
            Assert.That(Directory.Exists(m_root), Is.True);
            Assert.That(Directory.GetFileSystemEntries(m_root), Is.Empty);
        }
    }
}
