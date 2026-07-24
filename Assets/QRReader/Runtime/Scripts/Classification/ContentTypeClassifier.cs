using System;
using System.IO;
using QRReader.Resolver;

namespace QRReader.Classification
{
    /// <summary>
    /// Decides whether resolved bytes are an <see cref="ContentKind.Image"/>, a
    /// <see cref="ContentKind.Gif"/>, or <see cref="ContentKind.Unsupported"/> (architecture.md §3.5,
    /// ADR 0003). It classifies by <b>URL file-extension hint first</b>, then falls back to the
    /// <c>Content-Type</c> response header — matching ADR 0003 so a mislabeled server header can't
    /// override a clear extension, while an extensionless URL still classifies from the header.
    /// </summary>
    /// <remarks>
    /// Pure and stateless (no config, no I/O) so it stays EditMode-unit-testable off device (plan R1,
    /// §8) — M2-T7 covers the extension-vs-header precedence. It only categorizes; whether the bytes
    /// actually decode is the decoder's concern, and a decode failure routes to the same error state
    /// (M3-T5). Unsupported is not an error here — it's a routing decision the pipeline turns into the
    /// shared error visual.
    /// </remarks>
    public static class ContentTypeClassifier
    {
        /// <summary>
        /// Classifies from the payload <paramref name="url"/> (extension hint) and the response
        /// <paramref name="contentType"/> header (fallback), in that precedence order. A null/empty
        /// header simply means "no fallback"; a URL whose extension isn't a recognized media type
        /// defers to the header.
        /// </summary>
        public static ContentKind Classify(string url, string contentType)
        {
            // Extension hint first: a recognized media extension is decisive (ADR 0003).
            if (TryClassifyByExtension(url, out ContentKind byExtension))
            {
                return byExtension;
            }

            // Fallback: no usable extension → trust the server's Content-Type.
            return ClassifyByContentType(contentType);
        }

        /// <summary>
        /// Convenience overload that reads the <c>Content-Type</c> from a resolver
        /// <see cref="ResolveResult"/> (M2-T5) — the classifier's real input in the pipeline. The
        /// <paramref name="url"/> is still passed separately because it's the QR payload the pipeline
        /// holds, not part of the download result.
        /// </summary>
        public static ContentKind Classify(string url, in ResolveResult result) =>
            Classify(url, result.TryGetContentType(out string contentType) ? contentType : null);

        // Recognized media extensions are decisive; anything else (including no extension) returns
        // false so the caller falls back to the Content-Type header.
        private static bool TryClassifyByExtension(string url, out ContentKind kind)
        {
            kind = ContentKind.Unsupported;

            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            // Use only the path so query strings/fragments (e.g. "?token=…") don't spoof an extension.
            string path = url;
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                path = uri.AbsolutePath;
            }
            else
            {
                int cut = path.IndexOfAny(new[] { '?', '#' });
                if (cut >= 0)
                {
                    path = path.Substring(0, cut);
                }
            }

            string extension = Path.GetExtension(path).ToLowerInvariant();
            switch (extension)
            {
                case ".gif":
                    kind = ContentKind.Gif;
                    return true;
                case ".png":
                case ".jpg":
                case ".jpeg":
                    kind = ContentKind.Image;
                    return true;
                default:
                    return false;
            }
        }

        private static ContentKind ClassifyByContentType(string contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                return ContentKind.Unsupported;
            }

            // Drop any parameters ("image/jpeg; charset=binary") and normalize before matching.
            string mediaType = contentType;
            int semicolon = mediaType.IndexOf(';');
            if (semicolon >= 0)
            {
                mediaType = mediaType.Substring(0, semicolon);
            }

            mediaType = mediaType.Trim().ToLowerInvariant();

            if (mediaType == "image/gif")
            {
                return ContentKind.Gif;
            }

            // Any other image/* is the texture path; the decoder is the authority on whether the
            // specific format actually decodes (M3-T2). Non-image types are the website seam.
            if (mediaType.StartsWith("image/", StringComparison.Ordinal))
            {
                return ContentKind.Image;
            }

            return ContentKind.Unsupported;
        }
    }
}
