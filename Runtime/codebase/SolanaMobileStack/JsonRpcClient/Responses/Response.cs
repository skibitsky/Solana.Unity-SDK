using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Scripting;

// ReSharper disable once CheckNamespace

namespace Solana.Unity.SDK
{

    [Preserve]
    public class Response<T>
    {
        [Preserve]
        public class ResponseError
        {
            [JsonProperty("code")]
            [RequiredMember]
            public long Code { get; set; }

            [JsonProperty("message")]
            [RequiredMember]
            public string Message { get; set; }

            // Optional structured error payload (e.g. sign_and_send's -4 NOT_SUBMITTED
            // partial `signatures`, or -2 INVALID_PAYLOADS `valid` array).
            [JsonProperty("data")]
            [RequiredMember]
            public JToken Data { get; set; }
        }


        [JsonProperty("jsonrpc")] 
        [RequiredMember]
        public string JsonRpc { get; set; }

        [JsonProperty("result")]
        [RequiredMember]
        
        public T Result { get; set; }

        [JsonProperty("id")] 
        [RequiredMember]
        public long Id { get; set; }

        [JsonProperty("error")] 
        [RequiredMember]
        public ResponseError Error { get; set; }
        
        [RequiredMember]
        public bool WasSuccessful => Error is null;
        
        [RequiredMember]
        public bool Failed => Error is not null;
    }
}