using System;
using Lumina.Text;

namespace Chatterbox;

public class GradientStyle
{
	public GradientAnimationStyle AnimationStyle;

	public string Name;

	public byte[,] Colours;

	public int? ColourSet;

	public GradientStyle(string name, string b64, GradientAnimationStyle animStyle)
	{
		Name = name;
		AnimationStyle = animStyle;
		Colours = Decode(b64);
		ColourSet = null;
	}

	public GradientStyle(string name, byte[,] colours, GradientAnimationStyle animStyle)
	{
		Name = name;
		AnimationStyle = animStyle;
		Colours = colours;
		ColourSet = null;
	}

	private static byte[,] Decode(string b64)
	{
		byte[] arr = Convert.FromBase64String(b64);
		byte[,] arr2 = new byte[arr.Length / 3, 3];
		for (int i = 0; i < arr.Length; i += 3)
		{
			arr2[i / 3, 0] = arr[i];
			arr2[i / 3, 1] = arr[i + 1];
			arr2[i / 3, 2] = arr[i + 2];
		}
		return arr2;
	}

	public void Apply(SeStringBuilder builder, string title, bool animate)
	{
		if (!animate)
		{
			ApplyStatic(builder, title);
			return;
		}
		switch (AnimationStyle)
		{
		case GradientAnimationStyle.Wave:
			ApplyWave(builder, title);
			break;
		case GradientAnimationStyle.Pulse:
			ApplyPulse(builder, title);
			break;
		default:
			ApplyStatic(builder, title);
			break;
		}
	}

	private void ApplyPulse(SeStringBuilder builder, string title)
	{
		RGB glow = GradientSystem.GetColourRGB(this, 0, 5);
		builder.PushEdgeColorRgba(glow.R, glow.G, glow.B, byte.MaxValue);
		builder.Append(title);
		builder.PopEdgeColor();
	}

	private void ApplyWave(SeStringBuilder builder, string title)
	{
		if (title.Length > 25)
		{
			for (int i = 0; i < title.Length; i += 2)
			{
				RGB glow = GradientSystem.GetColourRGB(this, i, 5);
				builder.PushEdgeColorRgba(glow.R, glow.G, glow.B, byte.MaxValue);
				builder.Append(title.Substring(i, Math.Min(2, title.Length - i)));
				builder.PopEdgeColor();
			}
			return;
		}
		int i2 = 0;
		foreach (char c in title)
		{
			RGB glow2 = GradientSystem.GetColourRGB(this, i2++, 5);
			builder.PushEdgeColorRgba(glow2.R, glow2.G, glow2.B, byte.MaxValue);
			builder.AppendChar((int)c);
			builder.PopEdgeColor();
		}
	}

	private void ApplyStatic(SeStringBuilder builder, string title)
	{
		int gradientSize = Colours.GetLength(0);
		if (title.Length > 25)
		{
			for (int i = 0; i < title.Length; i += 2)
			{
				int z = (int)MathF.Round((float)i / (float)title.Length * (float)gradientSize);
				RGB glow = GradientSystem.GetColourRGB(this, z, 5, animate: false);
				builder.PushEdgeColorRgba(glow.R, glow.G, glow.B, byte.MaxValue);
				builder.Append(title.Substring(i, Math.Min(2, title.Length - i)));
				builder.PopEdgeColor();
			}
			return;
		}
		int i2 = 0;
		foreach (char c in title)
		{
			RGB glow2 = GradientSystem.GetColourRGB(this, (int)MathF.Round((float)i2++ / (float)title.Length * (float)gradientSize), 5, animate: false);
			builder.PushEdgeColorRgba(glow2.R, glow2.G, glow2.B, byte.MaxValue);
			builder.AppendChar((int)c);
			builder.PopEdgeColor();
		}
	}
}
