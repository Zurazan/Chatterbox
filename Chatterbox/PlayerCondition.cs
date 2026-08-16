using System;

namespace Chatterbox;

[Flags]
public enum PlayerCondition
{
	None = 0,
	Friend = 1,
	Party = 2,
	MareSynced = 4
}
