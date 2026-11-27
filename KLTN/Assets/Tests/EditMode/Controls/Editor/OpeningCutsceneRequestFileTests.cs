using System;
using System.IO;
using System.Text;
using EchoProtocol.Editor.Cinematics;
using NUnit.Framework;

namespace EchoProtocol.Tests.EditMode.Controls
{
    public sealed class OpeningCutsceneRequestFileTests
    {
        private string _directory, _path;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine("Temp", "OpeningCutsceneRequestTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _path = Path.Combine(_directory, "command.request");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) File.Delete(_path);
            if (Directory.Exists(_directory)) Directory.Delete(_directory);
        }

        [Test]
        public void ExclusiveWriter_DefersConsumptionAndPreservesCommandUntilWriterCloses()
        {
            using (var writer = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var data = Encoding.UTF8.GetBytes("tests");
                writer.Write(data, 0, data.Length);
                writer.Flush();
                Assert.That(OpeningCutsceneSceneSetup.TryConsumeRequest(_path, out var pending), Is.False);
                Assert.That(pending, Is.Null);
                Assert.That(File.Exists(_path), Is.True);
            }

            Assert.That(OpeningCutsceneSceneSetup.TryConsumeRequest(_path, out var command), Is.True);
            Assert.That(command, Is.EqualTo("tests"));
            Assert.That(File.Exists(_path), Is.False);
        }

        [Test]
        public void WriterSharingReads_StillCannotExposePartiallyWrittenCommand()
        {
            using (var writer = new FileStream(_path, FileMode.Create, FileAccess.Write,
                       FileShare.ReadWrite | FileShare.Delete))
            {
                var partial = Encoding.UTF8.GetBytes("pre");
                writer.Write(partial, 0, partial.Length);
                writer.Flush();
                Assert.That(OpeningCutsceneSceneSetup.TryConsumeRequest(_path, out var pending), Is.False);
                Assert.That(pending, Is.Null);

                var remaining = Encoding.UTF8.GetBytes("view");
                writer.Write(remaining, 0, remaining.Length);
            }

            Assert.That(OpeningCutsceneSceneSetup.TryConsumeRequest(_path, out var command), Is.True);
            Assert.That(command, Is.EqualTo("preview"));
        }

        [Test]
        public void ReaderPreventingDeletion_DefersWithoutLosingRequest()
        {
            File.WriteAllText(_path, "survey");
            using (var reader = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                Assert.That(OpeningCutsceneSceneSetup.TryConsumeRequest(_path, out var pending), Is.False);
                Assert.That(pending, Is.Null);
                Assert.That(File.Exists(_path), Is.True);
            }

            Assert.That(OpeningCutsceneSceneSetup.TryConsumeRequest(_path, out var command), Is.True);
            Assert.That(command, Is.EqualTo("survey"));
        }

        [Test]
        public void CompleteUtf8Request_IsTrimmedAndConsumedExactlyOnce()
        {
            File.WriteAllText(_path, " \r\ntests\r\n ", new UTF8Encoding(true));
            Assert.That(OpeningCutsceneSceneSetup.TryConsumeRequest(_path, out var command), Is.True);
            Assert.That(command, Is.EqualTo("tests"));
            Assert.That(OpeningCutsceneSceneSetup.TryConsumeRequest(_path, out var repeated), Is.False);
            Assert.That(repeated, Is.Null);
        }

        [Test]
        public void EmptyFile_IsKeptUntilProducerPublishesContent()
        {
            File.WriteAllText(_path, "\r\n ");
            Assert.That(OpeningCutsceneSceneSetup.TryConsumeRequest(_path, out var pending), Is.False);
            Assert.That(pending, Is.Null);
            Assert.That(File.Exists(_path), Is.True);

            File.WriteAllText(_path, "refresh");
            Assert.That(OpeningCutsceneSceneSetup.TryConsumeRequest(_path, out var command), Is.True);
            Assert.That(command, Is.EqualTo("refresh"));
        }

        [Test]
        public void MissingFile_IsANormalRetryWithoutException()
        {
            Assert.That(OpeningCutsceneSceneSetup.TryConsumeRequest(_path, out var command), Is.False);
            Assert.That(command, Is.Null);
        }
    }
}
