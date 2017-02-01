using Newtonsoft.Json;
using System;

namespace Example1
{
  internal class TextMessage
  {
    [JsonProperty ("user_id")]
    public uint? UserID {
      get; set;
    }

    [JsonProperty ("name")]
    public string Name {
      get; set;
    }

    [JsonProperty ("type")]
    public string Type {
      get; set;
    }

    [JsonProperty ("message")]
    public string Message {
      get; set;
    }

    public override string ToString ()
    {
      return JsonConvert.SerializeObject (this);
    }
  }
}
