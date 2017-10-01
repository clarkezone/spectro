using Newtonsoft.Json;

namespace NewsBlurSharp.Model.Response
{

    internal class InternalLoginResponse
    {
        [JsonProperty("authenticated")]
        public bool Authenticated { get; set; }

        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("user_id")]
        public int UserId { get; set; }

        [JsonProperty("errors")]
        public object Errors { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; }
    }
}
