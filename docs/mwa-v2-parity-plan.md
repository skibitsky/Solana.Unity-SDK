# MWA 2.0 Parity Plan — `feat/mwa-v2`

Working plan for bringing this adapter to **Mobile Wallet Adapter 2.0** protocol
parity, rebuilt cleanly rather than ported from PR #285.

- **Branch:** `feat/mwa-v2`, started from `90dab97` (the `#284` auth-cache merge —
  before any v2 lifecycle work).
- **Reference:** the MWA 2.0 protocol/spec
  (<https://solana-mobile.github.io/mobile-wallet-adapter/spec/spec.html>), **not**
  #285's specific implementation. We care about the required behavior, not their files.
- **Ground rules:** each capability lands as its own small, reviewed change; the
  hardened association transport stays untouched (no rewrites); discuss approach
  before implementing.
- A reference implementation ("based off #285") exists on branch
  `feat/mwa-v2-on-main` @ `6f55d3a` — useful to crib from, **not** to copy wholesale.
  The reverted/cleaned session work is preserved there @ `8f410f4`.

## What the base (`90dab97`) already has

Hardened association transport, wallet **chooser capture**, `.skr` resolution, the
`#284` **auth-token cache**, and a partial surface: `Logout()`, `Deauthorize()`,
`ReconnectWallet()`, `GetCapabilities()` (v1), `SignMessage(byte[])`,
`SignTransactions`, `SignMessages`. So we are not starting from zero.

## Capability gap (what our implementation must do for 2.0)

Each item: **2.0 requires → where we are → the gap.** Check off as completed.

- [x] **1. `authorize` (foundation).** *Done — see Notes (approach A: enriched typed
  model).* Sends CAIP-2 `chain`; `AuthorizationResult` enriched with per-account
  display_address/icon/chains/features, `wallet_icon`, and nested `SignInResult`.
  Deferred to their own items: `sign_in_payload` request (#8), `features`/`addresses`
  request params (#4-adjacent).

- [x] **2. `reauthorize` vs 2.0 token-auth.** *Done — see Notes.* Dropped `reauthorize`
  entirely; single `authorize` path passing the cached `auth_token` (silent reauth).
  Verify-on-device caveat: relies on the wallet re-prompting on a stale token.

- [x] **3. `deauthorize`.** Revoke an `auth_token` wallet-side. *Done (already
  existed at base, now correctly named).* The revoke flow is the adapter's
  `Deauthorize()` (renamed from `DisconnectWallet()`): resolve the token, target the
  exact issuing wallet package (skip the remote revoke rather than pop the OS chooser
  when none is targetable), call `client.Deauthorize(authToken)` best-effort, then
  `Logout()` to clear local state and fire `OnWalletDisconnected`. Client layer sends
  the real `deauthorize` RPC. **Verify on device.**

- [x] **4. `get_capabilities`.** *Done (model + predicates; gating lands with #5/#7).*
  Spec verified verbatim (see Notes). `CapabilitiesResult` now parses `features[]` and
  exposes `HasFeature(id)` + `SupportsCloneAuthorization` / `SupportsSignInWithSolana`
  predicates (each = 2.0 feature id OR the 1.x legacy bool, kept as a fallback). Dropped
  reliance on the removed `supports_clone_authorization` bool as the source of truth;
  `supported_transaction_versions` now tolerates the mixed string/number array via a
  converter. Feature-id constants live on `CapabilitiesResult`. The actual *gating* (offer
  sign-and-send / clone) is wired when #5/#7 consume these predicates.

- [x] **5. `sign_and_send_transactions`.** *Built (no fallback). Verify on device.*
  New `SignAndSendTransactions(Transaction[], options)` on the adapter + wrapper, plus
  `IAdapterOperations`/client method, `SignAndSendResult` (signatures), and
  `SignAndSendTransactionsOptions` (nested `options`). On `-32601` it throws
  `NotSupportedException` (no fallback to `sign_transactions`, per decision). Error code
  surfaced via `MwaRpcException` thrown from `RunPrivileged` (adapter-level — the code was
  already in `result.Error.Code`; transport untouched). **Not yet handled:** `-4`
  NOT_SUBMITTED partial signatures (needs `data` on the response-error model) — follow-up.

- [ ] **6. `sign_transactions` / `sign_messages`.** Present already. *Gap:* minor —
  confirm correct signature handling (trailing 64-byte extraction).

- [ ] **7. `clone_authorization`** *(optional in 2.0)*. *We have:* nothing. *Gap:*
  add it, gated on capabilities.

- [ ] **8. Sign-In-With-Solana.** `sign_in_payload` in authorize → `sign_in_result`;
  for wallets without native SIWS, construct the SIWS message and `sign_messages` as
  a fallback. *We have:* nothing. *Gap:* message construction + native path +
  fallback + result normalization (address as base58).

- [x] **9. CAIP-2 chain identifiers.** *Done — request side was already correct; edges
  tightened.* Spec verified (see Notes): the `chain` param accepts exactly the aliases we
  send (`solana:mainnet|devnet|testnet`); genesis-hash forms are **not** required;
  `cluster` is an ignored alias when `chain` is present (we send both for 1.x back-compat).
  Mapping is centralized in `MobileWalletAdapterClient.ResolveChain`. Tightened: localnet
  (no chain → wallet defaults to mainnet) now logs a **warning** instead of silently
  authorizing on mainnet; `ERROR_CHAIN_NOT_SUPPORTED` (-7) surfaces as a clear
  `NotSupportedException` on login; and login now logs the wallet-reported
  `accounts[].chains` so the returned format can be confirmed on device. **Verify on
  device:** what chain string Seed Vault reports.

- [x] **10. Session lifecycle semantics.** *Done — three behaviors (no explicit soft
  disconnect).* On mobile the OS app lifecycle **is** the soft disconnect: closing the
  app drops the in-memory session, the cached token survives in PlayerPrefs, and
  reopening reconnects silently. So a standalone soft-disconnect button is redundant and
  was dropped. The set:
  - `Login()` — authorize (silent when the cached token is valid, else wallet prompt).
  - `ReconnectWallet()` — silent local rehydrate via the `Login()` fast path (cached
    pubkey + token, no wallet launch); used by focus-resume; fires `OnWalletReconnected`.
  - `Logout()` — clears local creds (pubkey + token cache + wallet selection), no wallet
    revoke → next login re-prompts.
  - `Deauthorize()` — **hard**: revoke wallet-side (`deauthorize` RPC, best-effort,
    targeted) then `Logout()`; fires `OnWalletDisconnected`.
  **Verify on device.**

- [x] **11. Auth cache shape.** *Closed — single global slot is sufficient.* The
  existing `#284` cache (pubkey + token, one slot) already carries enough to rebuild a
  session offline for reconnect (#10); chain is derived from `RpcCluster` at
  construction, so it need not be cached. **Per-wallet keying (token/pubkey scoped to the
  wallet package) was considered and rejected:** it would make `Logout()` per-wallet
  (no single "log out everywhere", no visibility into what's still authorized) — a worse
  UX than the shared slot. Keep the single slot.

## Suggested build order

Bottom-up so each layer compiles before the one above leans on it:

```
#1 authorize  →  #2 reauthorize/token-auth  →  #9 chain mapping  →  #4 capabilities
   →  #5–#8 signing capabilities (sign_and_send, sign, messages, clone, SIWS)
   →  #10–#11 lifecycle + cache shape
```

## Verify against the spec BEFORE building the relevant item

These are details we've gotten wrong before — pin them down first:

- ~~The exact `features[]` identifier strings (item #4).~~ **Verified — see #4 note.**
- ~~The exact error codes for `sign_and_send_transactions` (item #5).~~ **Verified — see #5 note.**
- The exact chain format Seed Vault / Seeker returns in `accounts[].chains` (item #9) —
  now **logged on login** so it can be read off device; spec doesn't pin the format.

## Notes / decisions

- **REBASE 2026-06-11: branch re-based onto `main` @ `8fc607a` (PR #288 fix).** Main shipped
  the real auth fix and it **reverses our #2 decision**: it KEEPS `Reauthorize` and sends the
  CAIP-2 `chain` on it (the `Reauthorize` C# method issues RPC `authorize`+`auth_token`+`chain`;
  device evidence showed Seed Vault rejects token re-auth when the chain is absent → "Network
  mismatch"/"-1"). It also invalidates pre-fix cached tokens. On rebase we **dropped our
  obsolete #1/#2 auth rework and #9 chain edges/diagnostics** (main supersedes them) and
  **re-layered only the additive work** on top of main's auth: #3 deauth rename
  (`DisconnectWallet`→`Deauthorize`), #4 capabilities `features[]`+predicates, #5
  `sign_and_send_transactions` (+`MwaRpcException` thrown from `RunPrivileged` so `-32601` is
  catchable). The #1/#2/#9 notes below are historical — main's reauthorize+chain is the
  current auth path. **Needs a Unity compile + device re-test.**

- **#1 (authorize) — approach A chosen (enrich typed model, no hand-written parser).**
  Landed (additive only, no transport touched):
  - `JsonRequest.JsonRequestParams.Chain` added; client derives CAIP-2 `chain` from the
    cluster string and sends it alongside `cluster` (localnet → chain omitted).
  - `AuthorizationResult` enriched: per-account `display_address`,
    `display_address_format`, `icon`, `chains`, `features`; plus `wallet_icon` and a
    nested `SignInResult` (`address`/`signed_message`/`signature`/`signature_type`).
  - Existing `PublicKey`/`AccountLabel` helpers and adapter usage unchanged.
  - **Deferred to their own items:** `features`/`addresses` request params (#4),
    `sign_in_payload` request + consumption (#8), unified authorize+token routing (#2).
- **#2 (reauthorize) — decided: drop `reauthorize` entirely, single `authorize` path.**
  Rather than reauthorize-first-with-fallback (which forces a double wallet launch on
  2.0 wallets and rests on the unverified assumption that current Phantom prompts on
  authorize+token), we always call `authorize`, passing the cached token when we have
  one — the 2.0-spec intent (reauthorize is deprecated; authorize+token = silent reauth).
  Landed:
  - Removed `IAdapterOperations.Reauthorize`, the client `Reauthorize` method, and the
    `ReauthorizeOperation` class.
  - `Authorize` / `AuthorizeOperation` take an optional `authToken` (sets `auth_token`).
  - `RunPrivileged` collapsed to one association: `[authorize(token), op]`; no fallback.
  - **Known trade-off / verify on device:** relies on the wallet showing a fresh prompt
    for an invalid token (spec behavior). If a wallet instead *errors* on
    authorize+stale-token, sign ops fail until a fresh `Login`. No error-code self-heal
    added yet — add only if a real wallet misbehaves.
- **#3 (deauthorize) — found already implemented at base; only renamed.** The revoke
  flow predates the v2 work (the plan's "we have nothing" note was stale). No new logic;
  renamed the public adapter method `DisconnectWallet()` → `Deauthorize()` (correct MWA
  term) on both `SolanaMobileWalletAdapter` and the `SolanaWalletAdapter` wrapper, and
  updated doc/log/test comments. No clash with the client-level
  `IAdapterOperations.Deauthorize(string authToken)` (adapter method is parameterless).
  **Breaking rename** for external callers of `DisconnectWallet()`; no `[Obsolete]` alias
  added. The `OnWalletDisconnected` event name was left unchanged. **Verify on device.**
- **#4 (get_capabilities) — spec verified verbatim, model + predicates landed.**
  From the 2.0 spec (`get_capabilities` result + feature-identifiers sections):
  - Result fields: `max_transactions_per_request` (opt num), `max_messages_per_request`
    (opt num), `supported_transaction_versions` (**required, MIXED** string/number array,
    e.g. `"legacy"` and `0`), `features` (**required**, string[] of OPTIONAL feature ids).
  - **`supports_clone_authorization` was REMOVED in 2.0** ("does not appear anywhere").
  - Feature ids (exact casing): mandatory & NOT in `features[]` →
    `solana:signMessages`, `solana:signAndSendTransaction`; optional & in `features[]` →
    `solana:signInWithSolana`, `solana:cloneAuthorization`; deprecated →
    `solana:signTransactions`. (Note: feature is `signAndSendTransaction` singular; RPC
    method is `sign_and_send_transactions` plural.)
  - Build consequences flagged: **#5 can't be feature-gated** (sign-and-send is mandatory,
    absent from `features[]`; the real problem is detecting 1.x wallets that lack it).
    **#6 `sign_transactions` is the deprecated path** (2.0 prefers sign-and-send).
  - Landed: `CapabilitiesResult` adds `Features`, `HasFeature(id)`,
    `SupportsCloneAuthorization`/`SupportsSignInWithSolana` (feature OR 1.x bool fallback),
    `Feature*` id constants, and a `MixedStringArrayConverter` for the version array.
    Old bool kept only as `SupportsCloneAuthorizationLegacy` (1.x fallback). Tests updated.
  - **Observed on device (2026-06-11):** Phantom returns a non-null result with
    `features == null` (1.x behavior — no optional features, no required `features`
    array; predicates correctly degrade to `false`, no throw). Seed Vault returns
    `features == ["solana:signTransactions"]` — i.e. it opts into advertising the
    **deprecated** method for back-compat. Neither lists `solana:signAndSendTransaction`,
    so **capabilities cannot tell you whether sign-and-send works** on either wallet.
    Confirms #5 must **attempt + catch the not-supported error**, not feature-gate;
    `sign_transactions` (#6) remains the path that works on both today.
- **#5 (sign_and_send_transactions) — spec verified; NOT yet built.** Design: no
  fallback. It signs+sends; if the wallet can't, catch and surface it (per user).
  - **Spec self-contradiction (verbatim):** `solana:signAndSendTransaction` is in the
    **Mandatory Features** list ("assume always available"), yet the method section says
    *"Implementation of this method by a wallet endpoint is optional."* Non-supporting
    wallets return `-32601`. Net: can't feature-detect, can genuinely be absent →
    **attempt + catch `-32601`**.
  - **Request:** `payloads` (base64[]) + nested `options` { `min_context_slot` (int),
    `commitment` ("finalized"|"confirmed"|"processed"), `skip_preflight` (bool),
    `max_retries` (int), `wait_for_commitment_to_send_next_transaction` (bool) }.
  - **Result:** `signatures` (base64[]).
  - **Error set:** `-32601` Method not found (unsupported → "that's that"); `-32602`
    Invalid params; `-1` AUTHORIZATION_FAILED; `-2` INVALID_PAYLOADS (nested `valid[]`);
    `-3` NOT_SIGNED (declined); `-4` NOT_SUBMITTED (**partial success** — `data.signatures[]`
    with `null` for unsubmitted); `-6` TOO_MANY_PAYLOADS.
  - **Transport gap found:** `JsonRpc20Client.Receiver` throws `new Exception(error.Message)`
    and **drops `error.code`**. Catching `-32601` and handling `-4` partial success need the
    code. Requires enriching the thrown exception with the code (transport-adjacent — get
    buy-in before touching, per [[mwa-v2-no-transport-rewrites]]).
- **#9 (CAIP-2 chain) — spec verified; request side already done by #1, edges tightened.**
  Verbatim spec facts: `chain` accepts `solana:mainnet|testnet|devnet` **and** the bare
  `mainnet-beta|testnet|devnet`, defaulting to `solana:mainnet` when unset; genesis-hash
  CAIP-2 forms are NOT listed as accepted authorize values; `cluster` is "an alias for
  `chain` … ignored if the `chain` parameter is present." Error constant
  `ERROR_CHAIN_NOT_SUPPORTED = -7` (also `INVALID_SIWS_PAYLOAD` exists, code TBD — for #8).
  So we keep sending both `chain` (2.0) and `cluster` (1.x back-compat). Landed: centralized
  `ResolveChain`; localnet warns instead of silently defaulting to mainnet; `-7` → clear
  `NotSupportedException`; login logs `accounts[].chains` for on-device format confirmation.
  - **Observed on device (Seed Vault, 2026-06-11):** `accounts[].chains` is **empty**
    (`<none reported>`) — Seed Vault returns no chain list at all, neither alias nor
    genesis-hash. We don't branch on it, so nothing to handle. Verify-item closed.
- **#10 (lifecycle) — soft disconnect built, then dropped as redundant on mobile.**
  Briefly added a soft `DisconnectWallet()` (clears in-memory session, keeps cache) but
  removed it: on mobile, "disconnect" is just closing the app — the OS lifecycle drops the
  in-memory session while the PlayerPrefs token persists, so reopening reconnects silently
  via focus-resume. A dedicated button adds nothing. Also surfaced a real `Web3` wiring
  bug it would have needed: `Web3.OnLogin` only fires on the `currentWallet == null →
  Account != null` transition (`Web3.cs`), so an in-session disconnect that left
  `Web3._wallet` non-null would suppress `OnLogin` on the next login. Moot now that the
  in-session disconnect flow is gone; revisit `Web3.cs` only if a future flow swaps the
  wallet without nulling it first. Final lifecycle: **login / logout / deauthorize** +
  silent reconnect-on-focus.
- **#11 (cache shape) — decided: keep the single global slot, no per-wallet keying.**
  Reusing the `PlayerPrefsAuthCache` `scope` to key token+pubkey by wallet package was
  considered (so switching wallets wouldn't clobber the previous session) but rejected:
  it makes `Logout()` per-wallet, with no global "log out everywhere" and no visibility
  into which wallets remain authorized. The shared slot is the better UX. The existing
  pubkey+token cache is already enough for reconnect.
