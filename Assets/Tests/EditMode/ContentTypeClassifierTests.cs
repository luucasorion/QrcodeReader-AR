using NUnit.Framework;
using QRReader.Classification;

namespace QRReader.Tests.EditMode
{
    /// <summary>
    /// Precedence tests for <see cref="ContentTypeClassifier"/> (M2-T7): the URL extension hint wins
    /// over the <c>Content-Type</c> header, and the header is used only when the extension can't
    /// decide (architecture.md §3.5, ADR 0003).
    /// </summary>
    public class ContentTypeClassifierTests
    {
        // A recognized media extension is decisive and overrides a conflicting/absent header.
        [TestCase("https://cdn.example.com/pic.gif", "image/png", ContentKind.Gif)]
        [TestCase("https://cdn.example.com/pic.png", "image/gif", ContentKind.Image)]
        [TestCase("https://cdn.example.com/pic.jpg", "text/html", ContentKind.Image)]
        [TestCase("https://cdn.example.com/pic.jpeg", null, ContentKind.Image)]
        [TestCase("https://cdn.example.com/PIC.GIF", null, ContentKind.Gif)] // case-insensitive extension
        public void Extension_Hint_Wins_When_Recognized(string url, string contentType, ContentKind expected)
        {
            Assert.That(ContentTypeClassifier.Classify(url, contentType), Is.EqualTo(expected));
        }

        // No usable extension (missing, or an unrecognized one) → fall back to the Content-Type header.
        [TestCase("https://example.com/download", "image/gif", ContentKind.Gif)]
        [TestCase("https://example.com/download", "image/png", ContentKind.Image)]
        [TestCase("https://example.com/asset.bin", "image/jpeg", ContentKind.Image)]
        [TestCase("https://example.com/asset.bin", "image/webp", ContentKind.Image)] // any image/* → texture path
        [TestCase("https://example.com/page.html", "text/html", ContentKind.Unsupported)]
        [TestCase("https://example.com/data.txt", "application/octet-stream", ContentKind.Unsupported)]
        public void ContentType_Header_Used_When_Extension_Cannot_Decide(
            string url, string contentType, ContentKind expected)
        {
            Assert.That(ContentTypeClassifier.Classify(url, contentType), Is.EqualTo(expected));
        }

        // The extension is read from the URL path only, so a query/fragment can't spoof a type.
        [TestCase("https://example.com/download?file=cat.gif", "image/png", ContentKind.Image)]
        [TestCase("https://example.com/get?name=a.png#frag", "image/gif", ContentKind.Gif)]
        [TestCase("https://example.com/real.png?v=cat.gif", "text/html", ContentKind.Image)]
        public void Query_And_Fragment_Do_Not_Spoof_The_Extension(
            string url, string contentType, ContentKind expected)
        {
            Assert.That(ContentTypeClassifier.Classify(url, contentType), Is.EqualTo(expected));
        }

        // Header matching is case-insensitive and ignores media-type parameters.
        [TestCase("https://example.com/download", "image/jpeg; charset=binary", ContentKind.Image)]
        [TestCase("https://example.com/download", "IMAGE/GIF", ContentKind.Gif)]
        [TestCase("https://example.com/download", "Image/Png", ContentKind.Image)]
        [TestCase("https://example.com/download", "  image/gif  ", ContentKind.Gif)]
        public void ContentType_Matching_Is_Normalized(string url, string contentType, ContentKind expected)
        {
            Assert.That(ContentTypeClassifier.Classify(url, contentType), Is.EqualTo(expected));
        }

        // Neither signal decides → Unsupported (the future-website seam).
        [TestCase("https://example.com/download", null)]
        [TestCase("https://example.com/download", "")]
        [TestCase("https://example.com/download", "   ")]
        [TestCase(null, null)]
        [TestCase("", "")]
        public void Unsupported_When_Nothing_Identifies_Media(string url, string contentType)
        {
            Assert.That(ContentTypeClassifier.Classify(url, contentType), Is.EqualTo(ContentKind.Unsupported));
        }

        // A null/extensionless URL still classifies from the header alone.
        [TestCase(null, "image/gif", ContentKind.Gif)]
        [TestCase(null, "image/png", ContentKind.Image)]
        [TestCase(null, "text/html", ContentKind.Unsupported)]
        public void Header_Alone_Classifies_When_Url_Is_Null(string url, string contentType, ContentKind expected)
        {
            Assert.That(ContentTypeClassifier.Classify(url, contentType), Is.EqualTo(expected));
        }
    }
}
