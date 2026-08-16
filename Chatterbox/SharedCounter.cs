using System;

namespace Chatterbox;

public class SharedCounter : CounterBase
{
	public override CounterType ObjType => CounterType.Shared;

	public Guid? TriggerGuid { get; set; }
}
