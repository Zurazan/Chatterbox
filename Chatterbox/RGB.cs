using System;

namespace Chatterbox;

public record RGB(byte R, byte G, byte B)
{
	public string ToHexColorCode()
	{
		return $"#{R:X2}{G:X2}{B:X2}";
	}

	public static RGB? FromHexColourCode(string hexColourCode)
	{
		if (string.IsNullOrWhiteSpace(hexColourCode))
		{
			return null;
		}
		string hex = hexColourCode.Trim();
		if (hex.StartsWith('#'))
		{
			hex = hex.Substring(1);
		}
		if (hex.Length == 3)
		{
			string value = new string(hex[0], 2);
			string g = new string(hex[1], 2);
			return new RGB(B: Convert.ToByte(new string(hex[2], 2), 16), R: Convert.ToByte(value, 16), G: Convert.ToByte(g, 16));
		}
		if (hex.Length == 6)
		{
			byte r = Convert.ToByte(hex.Substring(0, 2), 16);
			byte g2 = Convert.ToByte(hex.Substring(2, 2), 16);
			byte b = Convert.ToByte(hex.Substring(4, 2), 16);
			return new RGB(r, g2, b);
		}
		return null;
	}

	public uint ToUInt(byte alpha = byte.MaxValue)
	{
		return (uint)((alpha << 24) | (B << 16) | (G << 8) | R);
	}
}
