using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Scripting;


// ReSharper disable once CheckNamespace

namespace Solana.Unity.SDK
{

    [Serializable]
    [Preserve]
    public class JsonRequest
    {
        [Serializable]
        [Preserve]
        public class JsonRequestIdentity
        {
            [JsonProperty("uri", NullValueHandling = NullValueHandling.Ignore)]
            [RequiredMember]
            public Uri Uri { get; set; }

            [JsonProperty("icon", NullValueHandling = NullValueHandling.Ignore)]
            [RequiredMember]
            public Uri Icon { get; set; }

            [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
            [RequiredMember]
            public string Name { get; set; }

            [Preserve]
            [RequiredMember]
            public JsonRequestIdentity()
            {
            }
        }

        [Serializable]
        [Preserve]
        public class JsonRequestParams
        {
            [JsonProperty("identity", NullValueHandling = NullValueHandling.Ignore)]
            [RequiredMember]
            public JsonRequestIdentity Identity { get; set; }

            [JsonProperty("cluster", NullValueHandling = NullValueHandling.Ignore)]
            [RequiredMember]
            public string Cluster { get; set; }

            // MWA 2.0 renamed the network identifier from "cluster" to "chain" using
            // CAIP-2 values (e.g. "solana:devnet"). Wallets implementing the 2.0 spec
            // (e.g. Seeker Seed Vault) ignore the legacy "cluster" field and default to
            // "solana:mainnet" when "chain" is absent. Both are serialized for
            // backward/forward compatibility across MWA 1.x and 2.0 wallets.
            [JsonProperty("chain", NullValueHandling = NullValueHandling.Ignore)]
            [RequiredMember]
            public string Chain { get; set; }

            [JsonProperty("auth_token", NullValueHandling = NullValueHandling.Ignore)]
            [RequiredMember]
            
            public string AuthToken { get; set; }

            [JsonProperty("payloads", NullValueHandling = NullValueHandling.Ignore)]
            [RequiredMember]
            public List<string> Payloads { get; set; }

            [JsonProperty("addresses", NullValueHandling = NullValueHandling.Ignore)]
            [RequiredMember]
            public List<string> Addresses { get; set; }

            // sign_and_send_transactions network-submission options. Omitted when null.
            [JsonProperty("options", NullValueHandling = NullValueHandling.Ignore)]
            [RequiredMember]
            public JsonRequestOptions Options { get; set; }

            // Sign-In-With-Solana input fields on authorize. Omitted when null.
            [JsonProperty("sign_in_payload", NullValueHandling = NullValueHandling.Ignore)]
            [RequiredMember]
            public SignInPayload SignInPayload { get; set; }

            [RequiredMember]
            public JsonRequestParams()
            {
            }
        }

        [Serializable]
        [Preserve]
        public class JsonRequestOptions
        {
            [JsonProperty("min_context_slot", NullValueHandling = NullValueHandling.Ignore)]
            [RequiredMember]
            public ulong? MinContextSlot { get; set; }

            [JsonProperty("commitment", NullValueHandling = NullValueHandling.Ignore)]
            [RequiredMember]
            public string Commitment { get; set; }

            [JsonProperty("skip_preflight", NullValueHandling = NullValueHandling.Ignore)]
            [RequiredMember]
            public bool? SkipPreflight { get; set; }

            [JsonProperty("max_retries", NullValueHandling = NullValueHandling.Ignore)]
            [RequiredMember]
            public int? MaxRetries { get; set; }

            [JsonProperty("wait_for_commitment_to_send_next_transaction", NullValueHandling = NullValueHandling.Ignore)]
            [RequiredMember]
            public bool? WaitForCommitmentToSendNextTransaction { get; set; }

            [RequiredMember]
            public JsonRequestOptions()
            {
            }
        }

        [JsonProperty("jsonrpc")] 
        [RequiredMember]
        public string JsonRpc { get; set; }

        [JsonProperty("method")] 
        [RequiredMember]
        public string Method { get; set; }

        [JsonProperty("params")] 
        [RequiredMember]
        public JsonRequestParams Params { get; set; }

        [JsonProperty("id")] 
        [RequiredMember]
        public int Id { get; set; }
    }
}