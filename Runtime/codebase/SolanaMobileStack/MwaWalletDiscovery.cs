using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

// ReSharper disable once CheckNamespace

namespace Solana.Unity.SDK
{
    /// <summary>
    /// Resolves which installed wallet to target for MWA association.
    /// </summary>
    /// <remarks>
    /// The SDK stays platform-agnostic, so it does NOT query the Android
    /// PackageManager for installed wallets — that would require a
    /// <c>&lt;queries&gt;</c> declaration in the consuming app's manifest, which
    /// the SDK has no business shipping. Instead: the first association fires an
    /// untargeted intent and lets the OS show its wallet chooser; the chosen
    /// wallet is then identified from the authorization — account label first,
    /// then the auth-token type as a fallback (see <see cref="ResolvePackage"/>) —
    /// and cached, so subsequent associations can target it directly.
    /// </remarks>
    public static class MwaWalletDiscovery
    {
        /// <summary>
        /// Known mapping of MWA account labels to Android package names.
        /// Used to identify the wallet after a successful authorization.
        /// </summary>
        private static readonly Dictionary<string, string> KnownWalletPackages = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Solana.Unity", "com.solanamobile.wallet"},       // Seeker wallet
            { "phantom-wallet", "app.phantom" },                // Phantom
            { "Main Wallet", "com.solflare.mobile" },           // Solflare
            { "backpack", "app.backpack.mobile.standalone" },   // Backpack
            { "jupiter-wallet", "ag.jup.jupiter.android" },     // Jupiter
            { "mwallet", "com.solana.mwallet" },                // MWA mock wallet
        };
        
        /// <summary>
        /// Fallback mapping of MWA auth-token <c>typ</c> to Android package name,
        /// used when the account label does not resolve. The <c>typ</c> names the
        /// auth backend, not the app, so it cannot distinguish wallets that share
        /// one (e.g. the Seeker native wallet and the Solflare app both emit
        /// <c>solflare-auth-token</c>). In practice the label resolves the Solflare
        /// app (label "Main Wallet"), so this fallback handles the Seeker case,
        /// where the label is the account's own name.
        /// </summary>
        private static readonly Dictionary<string, string> KnownAuthTokenTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            { "solflare-auth-token", "com.solanamobile.wallet" }, // Seeker native wallet
        };

        /// <summary>
        /// Resolves the wallet package after a successful authorization, trying the
        /// account label first and falling back to the auth-token <c>typ</c>.
        /// Returns <c>null</c> when neither resolves.
        /// </summary>
        public static string ResolvePackage(string accountLabel, string authToken)
        {
            var fromLabel = ResolvePackageFromLabel(accountLabel);
            if (!string.IsNullOrEmpty(fromLabel))
                return fromLabel;

            return ResolvePackageFromAuthToken(authToken);
        }

        /// <summary>
        /// Attempts to map an MWA account label (from
        /// <see cref="AuthorizationResult.AccountLabel"/>) to an Android package
        /// name using the known-wallets table. Returns <c>null</c> for
        /// unrecognised labels.
        /// </summary>
        public static string ResolvePackageFromLabel(string accountLabel)
        {
            if (string.IsNullOrEmpty(accountLabel))
                return null;

            if (KnownWalletPackages.TryGetValue(accountLabel, out var package))
            {
                Debug.Log($"[MWA][Discovery] Mapped label \"{accountLabel}\" → {package}");
                return package;
            }
            
            Debug.Log($"[MWA][Discovery] Unknown wallet label: \"{accountLabel}\"");
            return null;
        }

        /// <summary>
        /// Attempts to map a wallet by the <c>typ</c> embedded in its auth_token.
        /// Returns <c>null</c> when the type can't be read or isn't known.
        /// </summary>
        public static string ResolvePackageFromAuthToken(string authToken)
        {
            var type = ExtractAuthTokenType(authToken);
            if (string.IsNullOrEmpty(type))
            {
                Debug.Log("[MWA][Discovery] No auth-token type could be read.");
                return null;
            }

            if (KnownAuthTokenTypes.TryGetValue(type, out var package))
            {
                Debug.Log($"[MWA][Discovery] Mapped auth-token type \"{type}\" → {package}");
                return package;
            }

            Debug.Log($"[MWA][Discovery] Unknown auth-token type: \"{type}\"");
            return null;
        }

        /// <summary>
        /// Reads the <c>typ</c> field from an MWA auth_token. The token is opaque
        /// per the spec, but known wallets prefix it with a base64 JSON header that
        /// contains <c>typ</c>. A 4-aligned leading slice (enough to cover the
        /// header) is decoded so trailing signature bytes never affect parsing.
        /// Returns <c>null</c> on any failure.
        /// </summary>
        private static string ExtractAuthTokenType(string authToken)
        {
            if (string.IsNullOrEmpty(authToken))
                return null;

            try
            {
                var normalized = authToken.Replace('-', '+').Replace('_', '/');
                var take = Math.Min(normalized.Length, 256);
                take -= take % 4;
                if (take == 0)
                    return null;

                var bytes = Convert.FromBase64String(normalized.Substring(0, take));
                var text = Encoding.UTF8.GetString(bytes);
                Debug.Log($"[MWA][Discovery] Auth token extrated text: {text}");
                
                var match = Regex.Match(text, "\"typ\"\\s*:\\s*\"([^\"]+)\"");
                return match.Success ? match.Groups[1].Value : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the cached wallet package to target, or <c>null</c> when none
        /// is cached — in which case the caller should fire an untargeted intent
        /// so the OS shows its wallet chooser.
        /// </summary>
        public static async Task<string> ResolveWalletPackage(IMwaWalletSelectionCache cache)
        {
            if (cache == null)
                return null;

            var cached = await cache.GetSelectedWalletPackage();
            if (!string.IsNullOrEmpty(cached))
            {
                Debug.Log($"[MWA][Discovery] Using cached wallet: {cached}");
                return cached;
            }

            Debug.Log("[MWA][Discovery] No cached wallet package; intent will be untargeted (OS chooser).");
            return null;
        }
    }
}
