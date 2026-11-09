using MoaKnights.Localization;
using NUnit.Framework;

namespace MoaKnights.Tests
{
    public sealed class TextTableTests
    {
        private const string Csv = "key,ko,en\nui.a,가,A\nui.b,나,\nui.c,\"쉼표, 포함\",\"say \"\"hi\"\"\"\n";

        [Test]
        public void TryGet_ReturnsLanguageColumn()
        {
            var table = TextTable.Parse(Csv);
            Assert.IsTrue(table.TryGet("ui.a", "en", out var value));
            Assert.AreEqual("A", value);
        }

        [Test]
        public void TryGet_FallsBackToDefaultLanguage_WhenEmpty()
        {
            var table = TextTable.Parse(Csv);
            Assert.IsTrue(table.TryGet("ui.b", "en", out var value));
            Assert.AreEqual("나", value);
        }

        [Test]
        public void Parse_HandlesQuotedCommasAndQuotes()
        {
            var table = TextTable.Parse(Csv);
            table.TryGet("ui.c", "ko", out var ko);
            table.TryGet("ui.c", "en", out var en);
            Assert.AreEqual("쉼표, 포함", ko);
            Assert.AreEqual("say \"hi\"", en);
        }

        [Test]
        public void Parse_RejectsDuplicateKeys()
        {
            Assert.Throws<System.FormatException>(() => TextTable.Parse("key,ko\nui.a,가\nui.a,나\n"));
        }

        [Test]
        public void Get_MissingKey_ShowsKeyInBrackets()
        {
            Text.Load(TextTable.Parse(Csv), "ko");
            Assert.AreEqual("[ui.none]", Text.Get("ui.none"));
        }

        [Test]
        public void Format_AllowsReorderedPlaceholders()
        {
            Text.Load(TextTable.Parse("key,ko\nui.f,{1}에게 {0}\n"), "ko");
            Assert.AreEqual("적에게 5", Text.Format("ui.f", 5, "적"));
        }
    }
}
