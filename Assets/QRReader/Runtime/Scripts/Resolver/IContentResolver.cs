using System.Threading.Tasks;

namespace QRReader.Resolver
{
    /// <summary>
    /// The resolve stage the per-QR pipeline depends on (architecture.md §3.4, §4): download the
    /// content for a QR payload URL and return a typed <see cref="ResolveResult"/>. Abstracts
    /// <see cref="ContentResolver"/> so the <see cref="Lifecycle.TrackableLifecycleManager"/> depends on
    /// the contract, not the concrete (sealed) network implementation.
    /// </summary>
    /// <remarks>
    /// The contract is deliberately <b>reentrant</b>: one resolver instance is shared across every
    /// tracked QR, so <see cref="GetAsync"/> may be in flight for several payloads at once and must hold
    /// no per-request mutable state (each call owns its own request/handler). That reentrancy is what
    /// lets N QRs run independent pipelines with no cross-QR shared state (§4, M5-T3). The seam also
    /// lets EditMode tests drive the pipeline with a fake resolver, off device (§8).
    /// </remarks>
    public interface IContentResolver
    {
        /// <summary>
        /// Resolves the content for <paramref name="url"/> and returns the bytes + headers on success,
        /// or a <see cref="ResolveFailure"/> reason otherwise. Never throws into the pipeline — every
        /// failure is a value the caller branches on (§3.4).
        /// </summary>
        Task<ResolveResult> GetAsync(string url);
    }
}
