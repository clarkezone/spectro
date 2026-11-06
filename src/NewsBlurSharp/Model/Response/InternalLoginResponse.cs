using System.Text.Json.Serialization;

namespace NewsBlurSharp.Model.Response
{

    internal class InternalLoginResponse
    {
        [JsonPropertyName("authenticated")]
        public bool Authenticated { get; set; }

        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("errors")]
        public object Errors { get; set; }

        [JsonPropertyName("result")]
        public string Result { get; set; }
    }
}
