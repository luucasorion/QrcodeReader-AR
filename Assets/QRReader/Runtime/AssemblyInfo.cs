using System.Runtime.CompilerServices;

// Expose internal implementation details (e.g. DownloadSizeGuard) to the EditMode test assembly so
// safety-critical guard logic can be unit-tested off device (M2-T7) without widening the public API.
[assembly: InternalsVisibleTo("QRReader.Tests.EditMode")]
