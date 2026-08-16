using System.Linq;

namespace Chatterbox;

public static class StringEx
{
	public static string GetForename(this string nameWorld)
	{
		if (!nameWorld.Contains(' '))
		{
			return nameWorld;
		}
		return nameWorld.Split(' ')[0];
	}

	public static (string, string?) GetSurnameWorld(this string nameWorld)
	{
		string surname = (nameWorld.Contains(' ') ? nameWorld.Split(' ')[1] : nameWorld);
		string world = string.Empty;
		if (!Plugin.Worlds.Any((string x) => x == surname))
		{
			world = Plugin.Worlds.FirstOrDefault((string x) => surname.EndsWith(x));
			if (world != null)
			{
				surname = surname.Substring(0, surname.Length - world.Length);
			}
		}
		if (string.IsNullOrWhiteSpace(world))
		{
			EntityInfo player = PlayerManager.NearbyPlayers.FirstOrDefault((EntityInfo x) => x.Name == nameWorld);
			if (player != null)
			{
				world = player.HomeWorld;
			}
		}
		return (surname, world);
	}
}
