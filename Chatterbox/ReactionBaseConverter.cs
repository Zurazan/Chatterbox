using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Chatterbox;

public class ReactionBaseConverter : JsonConverter
{
	public override bool CanConvert(Type objectType)
	{
		return objectType == typeof(ReactionBase);
	}

	public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
	{
		if ((int)reader.TokenType == 11)
		{
			return null;
		}
		JObject jo = JObject.Load(reader);
		JToken typeToken = jo["ObjType"];
		if (typeToken == null || !uint.TryParse(((object)typeToken).ToString(), out var typeValue))
		{
			return null;
		}
		ReactionBase target = typeValue switch
		{
			2u => new TextReaction(), 
			1u => new EmoteReaction(), 
			_ => null, 
		};
		if (target != null)
		{
			serializer.Populate(((JToken)jo).CreateReader(), (object)target);
		}
		return target;
	}

	public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
	{
		if (value == null)
		{
			writer.WriteNull();
		}
		else
		{
			((JToken)JObject.FromObject(value, serializer)).WriteTo(writer, Array.Empty<JsonConverter>());
		}
	}
}
