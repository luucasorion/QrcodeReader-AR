namespace QRReader.Classification
{
    /// <summary>
    /// What kind of content the classifier decided the downloaded bytes are (architecture.md §3.5,
    /// ADR 0003). Drives which decode/render path the pipeline takes.
    /// </summary>
    public enum ContentKind
    {
        /// <summary>A still image (PNG/JPEG) — routes to the texture decode path (M3-T2).</summary>
        Image,

        /// <summary>An animated GIF — routes to the mgGif decode path (M3-T3).</summary>
        Gif,

        /// <summary>
        /// Anything else — routes to the shared error state. This is the documented seam for future
        /// website support (architecture.md §3.5): a <c>text/html</c> payload lands here today.
        /// </summary>
        Unsupported,
    }
}
