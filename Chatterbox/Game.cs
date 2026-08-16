using Hypostasis.Dalamud;

namespace Chatterbox;

[HypostasisInjection]
public static class Game
{
	[HypostasisSignatureInjection("F3 0F 10 05 ?? ?? ?? ?? 0F 2E C7", Offset = 4, Static = true, Required = true)]
	private static nint forceDisableMovementPtr = 0;

	public unsafe static ref int ForceDisableMovement => ref *(int*)forceDisableMovementPtr;
}
