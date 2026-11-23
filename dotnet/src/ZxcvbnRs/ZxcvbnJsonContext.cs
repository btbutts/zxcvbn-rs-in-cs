using System.Text.Json.Serialization;

namespace ZxcvbnRs;

[JsonSerializable(typeof(ZxcvbnResult))]
internal sealed partial class ZxcvbnJsonContext : JsonSerializerContext;
