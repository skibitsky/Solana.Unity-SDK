# Mobile Wallet Adapter — Method Reference

The MWA implementation is reached through the cross-platform **`SolanaWalletAdapter`**
wrapper (the underlying `SolanaMobileWalletAdapter` is `[Obsolete]` — use the wrapper).
On Android the wrapper routes to MWA; on WebGL/iOS the MWA-specific methods throw
`NotImplementedException`.

## Configuration

```csharp
var options = new SolanaMobileWalletAdapterOptions
{
    identityUri        = "https://yourgame.com/", // dapp identity URI
    iconUri            = "/icon.png",             // relative to identityUri
    name               = "My Game",
    keepConnectionAlive = true,                   // cache the auth token (default: true)
};

// Via Web3 (uses the default PlayerPrefsAuthCache):
var account = await Web3.Instance.LoginWalletAdapter();
var adapter = Web3.Wallet as SolanaWalletAdapter;

// Or construct directly (lets you inject a custom IMwaAuthCache — see the Cache Guide):
var direct = new SolanaWalletAdapter(
    new SolanaWalletAdapterOptions { solanaMobileWalletAdapterOptions = options },
    RpcCluster.DevNet);
```

---

## Authentication & session

### Login

```csharp
Task<Account> Login(string password = null)   // WalletBase
```

Authorize with the wallet. First call opens the OS wallet chooser and the wallet's approve
UI. Subsequent calls take the **fast path**: if a cached public key + auth token exist they
are returned immediately with no wallet launch. Returns the connected `Account`.

Token validity is re-checked lazily on the next operation that needs the wallet (see
`reauthorize` below), not during `Login`.

### LoginWithSignIn (Sign-In-With-Solana)

```csharp
Task<(Account account, SignInResult signInResult)> LoginWithSignIn(SignInPayload payload)
```

Authorizes carrying a `sign_in_payload` and returns the account plus the SIWS result. If
the wallet supports SIWS natively, the wallet's `sign_in_result` is returned. Otherwise the
SDK builds the SIWS message and signs it via `sign_messages` as a fallback. The result
`Address` is normalized to base58 in both paths.

```csharp
var (account, siws) = await adapter.LoginWithSignIn(new SignInPayload
{
    Domain    = "yourgame.com",
    Statement = "Sign in to My Game",
    Nonce     = System.Guid.NewGuid().ToString("N").Substring(0, 8),
});
// siws.Signature (base64), siws.SignatureType ("ed25519"), siws.SignedMessage (base64)
```

`SignInPayload` fields (all optional, SIWS / camelCase): `Domain`, `Address`, `Statement`,
`Uri`, `Version`, `ChainId`, `Nonce`, `IssuedAt`, `ExpirationTime`, `NotBefore`,
`RequestId`, `Resources`.

### ReconnectWallet

```csharp
Task ReconnectWallet()
```

Silently restores a cached session via `Login()`'s fast path and fires `OnWalletReconnected`.
Used by `HandleApplicationFocus` for silent resume. Throws if no session could be restored.

### Logout

```csharp
void Logout()   // WalletBase override
```

Clears local state — in-memory token, the cached public key, the auth-token cache, and the
remembered wallet package. Does **not** revoke wallet-side; the next `Login()` re-prompts.

### Deauthorize

```csharp
Task Deauthorize()
```

Revokes the authorization **wallet-side** (the `deauthorize` RPC, sent to the exact issuing
wallet) and then clears local state, firing `OnWalletDisconnected`. The remote revoke is
best-effort: if the issuing wallet can't be targeted (none cached / uninstalled) it skips
the remote call and clears local only, rather than popping the OS chooser.

### CloneAuthorization

```csharp
Task<string> CloneAuthorization()
```

Clones the current authorization into a new `auth_token` that can be transferred to another
instance of the dapp. Requires an authorized session (established internally first). Returns
the new token.

> **Optional in MWA 2.0** (`solana:cloneAuthorization`). Many wallets — including Phantom
> and the Seeker's Seed Vault — don't implement it; on those, this throws
> `NotSupportedException` (RPC `-32601`). Gate it on `GetCapabilities().SupportsCloneAuthorization`.

---

## Signing

### SignMessage

```csharp
Task<byte[]> SignMessage(byte[] message)   // WalletBase override
```

Signs an arbitrary message; returns the raw 64-byte ed25519 signature.

### SignTransaction / SignAllTransactions

```csharp
Task<Transaction> SignTransaction(Transaction transaction)
Task<Transaction[]> SignAllTransactions(Transaction[] transactions)
```

Signs locally (via `sign_transactions`); the SDK submits. Returns the signed transaction(s).

### SignAndSendTransactions

```csharp
Task<SignAndSendTxResult> SignAndSendTransactions(
    Transaction[] transactions, SignAndSendTransactionsOptions options = null)
```

Signs **and submits** to the network via the wallet (`sign_and_send_transactions`). Distinct
from `SignTransaction`, which signs locally and leaves submission to the SDK. No fallback to
local signing.

Unlike the other MWA methods (which throw), this returns a **typed result** — its outcomes
(user declined, partial submit, invalid payloads, not supported) are expected and carry data.
Pattern-match on the case:

```csharp
switch (await adapter.SignAndSendTransactions(txs))
{
    case SignAndSendTxResult.Success s:        /* s.Signatures (one per tx) */     break;
    case SignAndSendTxResult.UserDeclined:     /* user rejected the prompt */      break;
    case SignAndSendTxResult.NotSubmitted ns:  /* ns.PartialSignatures: null = not landed */ break;
    case SignAndSendTxResult.InvalidPayloads p:/* p.Valid[] per-payload */         break;
    case SignAndSendTxResult.TooManyPayloads:  /* exceeds wallet limit */          break;
    case SignAndSendTxResult.NotSupported:     /* wallet can't sign+send (-32601) */ break;
    case SignAndSendTxResult.Unauthorized:     /* session not authorized */        break;
    case SignAndSendTxResult.Failed f:         /* f.Code, f.Message */             break;
}
```

`SignAndSendTransactionsOptions` (all optional, omitted from the request when null):
`MinContextSlot` (`ulong?`), `Commitment` (`"processed"|"confirmed"|"finalized"`),
`SkipPreflight` (`bool?`), `MaxRetries` (`int?`),
`WaitForCommitmentToSendNextTransaction` (`bool?`).

---

## Capabilities

### GetCapabilities

```csharp
Task<CapabilitiesResult> GetCapabilities()
```

Queries the wallet's supported features and limits. `CapabilitiesResult`:

| Member | Notes |
|---|---|
| `Features` (`string[]`) | optional-feature identifiers the wallet advertises |
| `HasFeature(string id)` | membership test (use the `Feature*` constants) |
| `SupportsCloneAuthorization` (`bool`) | `solana:cloneAuthorization` present, or the 1.x bool |
| `SupportsSignInWithSolana` (`bool`) | `solana:signInWithSolana` present |
| `MaxTransactionsPerRequest` (`int?`) | |
| `MaxMessagesPerRequest` (`int?`) | |
| `SupportedTransactionVersions` (`string[]`) | mixed `"legacy"`/`0` normalized to strings |

> Mandatory features (`solana:signMessages`, `solana:signAndSendTransaction`) are **not**
> listed in `Features` per spec — assume them present and detect lack of support by catching
> `-32601`.

---

## Events

| Event | Fired when |
|---|---|
| `OnWalletDisconnected` | `Deauthorize()` completes |
| `OnWalletReconnected` | `ReconnectWallet()` restores a session |

---

## Session state (before login)

`MwaSession` is a static helper for the cached session, callable at app start **before any
adapter exists** — use it to decide the landing-screen UI (see
[Best Practices](mwa-best-practices.md)).

```csharp
Task<bool>   MwaSession.HasCachedSession(IMwaAuthCache authCache = null)
string       MwaSession.CachedAccountAddress()                  // base58, or null
Task         MwaSession.ClearCachedSession(IMwaAuthCache = null, IMwaWalletSelectionCache = null)
```

- `HasCachedSession` → true when the next `Login()` will restore silently (cached account
  **and** token present). Pass the same `IMwaAuthCache` you configured (defaults to
  `PlayerPrefsAuthCache`).
- `CachedAccountAddress` → the remembered address for a "Continue as …" label.
- `ClearCachedSession` → local logout (account + token + remembered wallet), no wallet-side
  revoke (same semantics as `Logout()`).

## Error handling

Wallet RPC errors surface as `MwaRpcException` (carrying the numeric `Code`; see
`MwaErrorCodes`, plus an optional `Data` payload). `CloneAuthorization` translates "method
not supported" (`-32601`) into a `NotSupportedException`. `SignAndSendTransactions` is the one
method that returns a typed `SignAndSendTxResult` instead of throwing (see above), so its
expected outcomes — including the partial-submit signatures — are handled by case rather than
by `catch`.

---

## Scope & notes

- **Local association only.** The SDK connects to a wallet on the **same device** (MWA
  `associate/local`). Remote association (desktop-dApp ↔ phone-wallet over a reflector) is a
  deliberate non-goal for on-device Unity games.
- **Silent reauth** is performed by re-sending `authorize` with the cached `auth_token` and
  the CAIP-2 `chain` (kept as the C# method `Reauthorize`). The chain **must** be re-sent or
  the wallet defaults the re-established session to `solana:mainnet`.
- **Chooser caller identity.** The wallet chooser is launched with `startActivityForResult`
  so the chosen wallet sees the app as the caller; otherwise the chooser-issued token isn't
  reauthorizable and the first sign double-prompts.
