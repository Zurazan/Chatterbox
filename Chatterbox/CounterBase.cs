using Newtonsoft.Json;

namespace Chatterbox;

public abstract class CounterBase
{
	public abstract CounterType ObjType { get; }

	[JsonIgnore]
	public bool IsEditing { get; set; }
}
