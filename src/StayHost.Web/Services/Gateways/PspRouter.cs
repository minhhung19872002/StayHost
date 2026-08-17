using Microsoft.Extensions.Options;

namespace StayHost.Web.Services.Gateways;

/// <summary>
/// Which gateway, if any, serves the method the guest picked.
///
/// A method with no gateway behind it is not an error — it is the stand-in
/// gateway keeping the demo checkout working. The one thing that must never
/// happen is the opposite: a method wired to a real gateway being charged by the
/// stand-in, which would confirm a stay nobody paid for. That is why the pay
/// endpoint asks this router before it charges anything.
/// </summary>
public class PspRouter(IOptions<PspSettings> options, IEnumerable<IPspProvider> providers)
{
    private readonly PspSettings _psp = options.Value;

    /// <summary>The gateway for a method, or null when the stand-in still owns it.</summary>
    public IPspProvider? For(string? method)
    {
        var key = (method ?? "").Trim().ToLowerInvariant();
        if (key.Length == 0) return null;
        if (!_psp.Methods.TryGetValue(key, out var provider)) return null;

        return providers.FirstOrDefault(
            p => p.Key.Equals(provider, StringComparison.OrdinalIgnoreCase) && p.IsConfigured);
    }

    public IPspProvider? ByKey(string? providerKey) =>
        providers.FirstOrDefault(
            p => p.Key.Equals(providerKey, StringComparison.OrdinalIgnoreCase) && p.IsConfigured);

    /// <summary>True when this method leaves the site instead of being charged in place.</summary>
    public bool IsLive(string? method) => For(method) is not null;

    /// <summary>
    /// Whether the public URL is one a gateway could reach. On a laptop it is
    /// not, so the IPN never arrives and the self-check of docs/07 §5 is the only
    /// thing that settles a payment. Worth saying out loud in the log rather than
    /// leaving as a mystery.
    /// </summary>
    public bool PublicUrlIsReachable =>
        _psp.PublicUrl.Length > 0
        && !_psp.PublicUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)
        && !_psp.PublicUrl.Contains("127.0.0.1", StringComparison.Ordinal);

    public IReadOnlyList<string> LiveMethods =>
        _psp.Methods.Keys.Where(IsLive).ToList();
}
