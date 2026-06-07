using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.Scripting;

// ReSharper disable once CheckNamespace

[Preserve]
public interface IAdapterOperations
{
    [Preserve]
    // chain: optional CAIP-2 identifier (e.g. "solana:devnet") for MWA 2.0 wallets.
    public Task<AuthorizationResult> Authorize(Uri identityUri, Uri iconUri, string identityName, string rpcCluster, string chain = null);
    [Preserve]
    public Task<AuthorizationResult> Reauthorize(Uri identityUri, Uri iconUri, string identityName, string authToken);
    [Preserve]
    public Task Deauthorize(string authToken);
    [Preserve]
    public Task<CapabilitiesResult> GetCapabilities();
    [Preserve]
    public Task<SignedResult> SignTransactions(IEnumerable<byte[]> transactions);
    [Preserve]
    public Task<SignedResult> SignMessages(IEnumerable<byte[]> messages, IEnumerable<byte[]> addresses);
}