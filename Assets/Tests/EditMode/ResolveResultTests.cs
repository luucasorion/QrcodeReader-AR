using System;
using System.Collections.Generic;
using NUnit.Framework;
using QRReader.Classification;
using QRReader.Resolver;

namespace QRReader.Tests.EditMode
{
    /// <summary>
    /// Tests for the typed <see cref="ResolveResult"/> (M2-T5): success/failure shape, null
    /// normalization, case-insensitive header lookup, and the <see cref="ResolveFailure.None"/> guard.
    /// </summary>
    public class ResolveResultTests
    {
        [Test]
        public void Succeeded_Carries_Bytes_And_Headers()
        {
            var bytes = new byte[] { 1, 2, 3 };
            var headers = new Dictionary<string, string> { ["Content-Type"] = "image/png" };

            ResolveResult result = ResolveResult.Succeeded(bytes, headers);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Failure, Is.EqualTo(ResolveFailure.None));
            Assert.That(result.Bytes, Is.EqualTo(bytes));
            Assert.That(result.TryGetContentType(out string contentType), Is.True);
            Assert.That(contentType, Is.EqualTo("image/png"));
        }

        [Test]
        public void Succeeded_Normalizes_Nulls_To_Empty()
        {
            ResolveResult result = ResolveResult.Succeeded(null, null);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Bytes, Is.Not.Null.And.Empty);
            Assert.That(result.Headers, Is.Not.Null.And.Empty);
            Assert.That(result.TryGetContentType(out _), Is.False);
        }

        [Test]
        public void Succeeded_Header_Lookup_Is_Case_Insensitive()
        {
            var headers = new Dictionary<string, string> { ["content-TYPE"] = "image/gif" };

            ResolveResult result = ResolveResult.Succeeded(Array.Empty<byte>(), headers);

            Assert.That(result.TryGetContentType(out string contentType), Is.True);
            Assert.That(contentType, Is.EqualTo("image/gif"));
        }

        [Test]
        public void TryGetContentType_Is_False_For_Empty_Header_Value()
        {
            var headers = new Dictionary<string, string> { ["Content-Type"] = "" };

            ResolveResult result = ResolveResult.Succeeded(Array.Empty<byte>(), headers);

            Assert.That(result.TryGetContentType(out _), Is.False);
        }

        [TestCase(ResolveFailure.BlockedByPolicy)]
        [TestCase(ResolveFailure.Timeout)]
        [TestCase(ResolveFailure.ExceededSizeCap)]
        [TestCase(ResolveFailure.NetworkError)]
        public void Failed_Sets_Reason_And_Has_No_Payload(ResolveFailure reason)
        {
            ResolveResult result = ResolveResult.Failed(reason);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Failure, Is.EqualTo(reason));
            Assert.That(result.Bytes, Is.Not.Null.And.Empty);
            Assert.That(result.Headers, Is.Not.Null.And.Empty);
            Assert.That(result.TryGetContentType(out _), Is.False);
        }

        [Test]
        public void Failed_Rejects_None_Reason()
        {
            Assert.That(() => ResolveResult.Failed(ResolveFailure.None), Throws.ArgumentException);
        }

        [Test]
        public void Classifier_Reads_ContentType_From_Result_Overload()
        {
            var headers = new Dictionary<string, string> { ["Content-Type"] = "image/gif" };
            ResolveResult result = ResolveResult.Succeeded(Array.Empty<byte>(), headers);

            // Extensionless URL forces the classifier onto the header carried by the result.
            Assert.That(
                ContentTypeClassifier.Classify("https://example.com/download", result),
                Is.EqualTo(ContentKind.Gif));
        }
    }
}
