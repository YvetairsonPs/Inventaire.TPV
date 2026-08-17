using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Inventory.TPV.Services;

/// <summary>
/// Une session d'appairage entre une caisse (PC) et un téléphone servant de douchette.
/// Vit en mémoire : un redémarrage du site oblige simplement à réappairer.
/// </summary>
public sealed class SessionScan
{
    public required string Code { get; init; }
    public required string Proprietaire { get; init; }
    public DateTime CreeeUtc { get; init; } = DateTime.UtcNow;
    public DateTime DerniereActiviteUtc { get; set; } = DateTime.UtcNow;
    public bool TelephoneConnecte { get; set; }
    public ConcurrentQueue<string> File { get; } = new();
}

/// <summary>
/// Relais mémoire entre le téléphone-douchette et la caisse.
/// Le téléphone pousse les codes-barres, la caisse les récupère en attente longue (long polling).
/// </summary>
public sealed class ScanRelay
{
    /// <summary>Durée de vie d'une session sans aucune activité.</summary>
    public static readonly TimeSpan DureeVie = TimeSpan.FromHours(12);

    private readonly ConcurrentDictionary<string, SessionScan> _sessions = new(StringComparer.Ordinal);

    /// <summary>Ouvre une session pour la caisse et retourne son code d'appairage à 6 chiffres.</summary>
    public SessionScan Ouvrir(string proprietaire)
    {
        Purger();

        string code;
        do { code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString(); }
        while (_sessions.ContainsKey(code));

        var session = new SessionScan { Code = code, Proprietaire = proprietaire };
        _sessions[code] = session;
        return session;
    }

    /// <summary>Retourne la session si elle existe et appartient bien à l'utilisateur courant.</summary>
    public SessionScan? Trouver(string? code, string proprietaire)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        if (!_sessions.TryGetValue(code.Trim(), out var s)) return null;
        if (!string.Equals(s.Proprietaire, proprietaire, StringComparison.OrdinalIgnoreCase)) return null;
        if (DateTime.UtcNow - s.DerniereActiviteUtc > DureeVie) { _sessions.TryRemove(code.Trim(), out _); return null; }
        return s;
    }

    /// <summary>Le téléphone dépose un code-barres pour la caisse.</summary>
    public bool Pousser(SessionScan session, string codeBarres)
    {
        if (string.IsNullOrWhiteSpace(codeBarres)) return false;
        session.File.Enqueue(codeBarres.Trim());
        session.TelephoneConnecte = true;
        session.DerniereActiviteUtc = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// La caisse attend le prochain lot de codes scannés. Retourne dès qu'il y a quelque chose,
    /// ou une liste vide au bout du délai (la caisse relance alors une attente).
    /// </summary>
    public async Task<List<string>> AttendreAsync(SessionScan session, TimeSpan delai, CancellationToken ct)
    {
        var limite = DateTime.UtcNow + delai;
        while (true)
        {
            session.DerniereActiviteUtc = DateTime.UtcNow;

            var lot = new List<string>();
            while (session.File.TryDequeue(out var c)) lot.Add(c);
            if (lot.Count > 0) return lot;

            if (DateTime.UtcNow >= limite || ct.IsCancellationRequested) return lot;

            try { await Task.Delay(250, ct); }
            catch (OperationCanceledException) { return lot; }
        }
    }

    public void Fermer(SessionScan session) => _sessions.TryRemove(session.Code, out _);

    private void Purger()
    {
        var maintenant = DateTime.UtcNow;
        foreach (var kv in _sessions)
            if (maintenant - kv.Value.DerniereActiviteUtc > DureeVie)
                _sessions.TryRemove(kv.Key, out _);
    }
}
