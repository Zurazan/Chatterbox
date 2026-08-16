using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Chatterbox;

public static class GradientBuilder
{
	public class FixedColour(ushort position, uint colour)
	{
		public ushort Position = position;

		public uint Colour = colour;

		public Guid Guid { get; init; } = Guid.NewGuid();
	}

	public record Pair(FixedColour Begin, FixedColour End)
	{
		public int Length => End.Position - Begin.Position;

		public FixedColour ColourAt(float t)
		{
			ushort position = (ushort)MathF.Round((float)(int)Begin.Position + t * (float)Length);
			return new FixedColour(position, Mode switch
			{
				0 => LerpOpaque(Begin.Colour, End.Colour, t), 
				1 => LerpHueOpaque(Begin.Colour, End.Colour, t), 
				_ => 0u, 
			});
		}
	}

	public static int Length = 64;

	public static readonly List<FixedColour> FixedColours;

	public static readonly List<Pair> Pairs;

	public static Guid Editing;

	public static int Mode;

	public static GradientAnimationStyle AnimationStyle;

	public static string PreviewText;

	public static Vector3 PreviewTextColour;

	public static GradientStyle? GeneratedStyle;

	public static void UpdatePairs()
	{
		Pairs.Clear();
		FixedColour start = FixedColours.Find((FixedColour f) => f.Position == 0);
		if (start == null)
		{
			start = new FixedColour(0, uint.MaxValue);
			FixedColours.Insert(0, start);
		}
		if (FixedColours.Find((FixedColour f) => f.Position == ushort.MaxValue) == null)
		{
			FixedColours.Add(new FixedColour(ushort.MaxValue, start.Colour));
		}
		List<FixedColour> colours = FixedColours.OrderBy((FixedColour f) => f.Position).ToList();
		for (int i = 0; i < colours.Count - 1; i++)
		{
			FixedColour a = colours[i];
			FixedColour b = colours[i + 1];
			Pairs.Add(new Pair(a, b));
		}
	}

	public static void GenerateStyle(int? steps = null)
	{
		int valueOrDefault = steps.GetValueOrDefault();
		if (!steps.HasValue)
		{
			valueOrDefault = Length;
			steps = valueOrDefault;
		}
		if (steps < 2)
		{
			steps = 2;
		}
		if (steps > 1024)
		{
			steps = 1024;
		}
		UpdatePairs();
		List<RGB> l = new List<RGB>();
		double step = 65535.0 / (double)((steps ?? Length) - 1);
		for (int i = 0; i < steps; i++)
		{
			float pos = (float)step * (float)i;
			FixedColour fixedColour = FixedColours.Find((FixedColour f) => f.Position == (ushort)MathF.Round(pos));
			uint c = 0u;
			if (fixedColour != null)
			{
				c = fixedColour.Colour;
			}
			else
			{
				Pair pair = Pairs.Find((Pair p) => (float)(int)p.Begin.Position < pos && (float)(int)p.End.Position > pos);
				if (pair == null)
				{
					throw new Exception($"Failed to get pair at position: {pos}");
				}
				float pairPos = (pos - (float)(int)pair.Begin.Position) / (float)(pair.End.Position - pair.Begin.Position);
				c = pair.ColourAt(pairPos).Colour;
			}
			l.Add(UintToRGB(c));
		}
		byte[,] bytes = new byte[l.Count, 3];
		for (int i2 = 0; i2 < l.Count; i2++)
		{
			bytes[i2, 0] = l[i2].R;
			bytes[i2, 1] = l[i2].G;
			bytes[i2, 2] = l[i2].B;
		}
		GeneratedStyle = new GradientStyle("Generated Style", bytes, AnimationStyle);
	}

	public static uint LerpOpaque(uint start, uint end, float t)
	{
		return ImGui.ColorConvertFloat4ToU32(LerpOpaque(ImGui.ColorConvertU32ToFloat4(start), ImGui.ColorConvertU32ToFloat4(end), t));
	}

	public static Vector4 LerpOpaque(Vector4 start, Vector4 end, float t)
	{
		t = Math.Clamp(t, 0f, 1f);
		Vector4 result = start + (end - start) * t;
		result.W = 1f;
		return result;
	}

	private static Vector3 GetHSV(Vector4 v)
	{
		Vector3 hsv = default(Vector3);
		ImGui.ColorConvertRGBtoHSV(v.X, v.Y, v.Z, ref hsv.X, ref hsv.Y, ref hsv.Z);
		return hsv;
	}

	private static float DeltaAngle(float a, float b)
	{
		float diff = (b - a) % 360f;
		if (diff > 180f)
		{
			diff -= 360f;
		}
		if (diff < -180f)
		{
			diff += 360f;
		}
		return diff;
	}

	public static float Lerp(float a, float b, float t)
	{
		return a + (b - a) * t;
	}

	public static uint LerpHueOpaque(uint start, uint end, float t)
	{
		return ImGui.ColorConvertFloat4ToU32(LerpHueOpaque(ImGui.ColorConvertU32ToFloat4(start), ImGui.ColorConvertU32ToFloat4(end), t));
	}

	public static Vector4 LerpHueOpaque(Vector4 start, Vector4 end, float t)
	{
		Vector3 hSV = GetHSV(start);
		Vector3 endHsv = GetHSV(end);
		float deltaH = DeltaAngle(hSV.X * 360f, endHsv.X * 360f) / 360f;
		float h = hSV.X + deltaH * t;
		if (h < 0f)
		{
			h++;
		}
		else if (h > 1f)
		{
			h--;
		}
		float s = Lerp(hSV.Y, endHsv.Y, t);
		float v = Lerp(hSV.Z, endHsv.Z, t);
		return ImGui.HSV(h, s, v, 1f).Value;
	}

	public static RGB UintToRGB(uint color)
	{
		return new RGB((byte)(color & 0xFF), (byte)((color >> 8) & 0xFF), (byte)((color >> 16) & 0xFF));
	}

	public static void Draw()
	{
		var _ = ImRaii.PushId(new ImU8String("GradientBuilder"), true);
		try
		{
			if (ImGui.SmallButton(new ImU8String("Spread")) && FixedColours.Count > 2)
			{
				double step = 65535.0 / (double)(FixedColours.Count - 1);
				int i = 0;
				foreach (FixedColour item in FixedColours.OrderBy((FixedColour fixedColour) => fixedColour.Position))
				{
					item.Position = (ushort)Math.Round(step * (double)i++);
				}
				UpdatePairs();
				GenerateStyle();
			}
			ImGui.SameLine();
			ImGui.SetNextItemWidth(100f);
			ImU8String val = new ImU8String("Mode");
			ImU8String val2 = default(ImU8String);
			if (ImGui.SliderInt(val, ref Mode, 0, 1, val2, (ImGuiSliderFlags)0))
			{
				GenerateStyle();
			}
			ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 32f));
			ImGui.Dummy(new Vector2(16f));
			ImGui.SameLine();
			ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X - 16f, 100f));
			ImDrawListPtr dl = ImGui.GetWindowDrawList();
			Vector2 tl = ImGui.GetItemRectMin();
			Vector2 size = ImGui.GetItemRectSize();
			UpdatePairs();
			for (int i2 = 0; (float)i2 < size.X; i2++)
			{
				ushort pos = (ushort)MathF.Round((float)i2 / size.X * 65535f);
				Vector2 startPos = tl + new Vector2(i2, 0f);
				Vector2 endPos = tl + new Vector2(i2, size.Y);
				Pair p = Pairs.Find((Pair pair2) => pair2.Begin.Position <= pos && pair2.End.Position > pos);
				if (!(p == null))
				{
					float pPct = (float)(pos - p.Begin.Position) / (float)(p.End.Position - p.Begin.Position);
					dl.AddLine(startPos, endPos, p.ColourAt(pPct).Colour);
				}
			}
			foreach (FixedColour f in FixedColours)
			{
				Vector2 pos2 = tl + new Vector2(size.X * (float)(int)f.Position / 65535f, -16f);
				Vector2 pos3 = pos2 + new Vector2(0f, size.Y + 32f);
				dl.AddLine(pos2, pos3, f.Colour, 4f);
				dl.AddCircleFilled(pos2, 10f, f.Colour, 16);
				dl.AddCircleFilled(pos3, 10f, f.Colour, 16);
				if (ImGui.IsMouseHoveringRect(pos2 - new Vector2(10f), pos2 + new Vector2(10f)) || ImGui.IsMouseHoveringRect(pos3 - new Vector2(10f), pos3 + new Vector2(10f)))
				{
					dl.AddCircle(pos2, 10f, 4294967040u, 16, 2f);
					dl.AddCircle(pos3, 10f, 4294967040u, 16, 2f);
					ImGuiIOPtr iO = ImGui.GetIO();
					if (iO.MouseClicked[0])
					{
						if (Editing == f.Guid)
						{
							Editing = Guid.Empty;
						}
						else
						{
							Editing = f.Guid;
						}
					}
				}
				else if (Editing == f.Guid)
				{
					dl.AddCircle(pos2, 10f, 4278190335u, 16, 2f);
					dl.AddCircle(pos3, 10f, 4278190335u, 16, 2f);
				}
				else
				{
					dl.AddCircle(pos2, 10f, uint.MaxValue, 16);
					dl.AddCircle(pos3, 10f, uint.MaxValue, 16);
				}
			}
			ImDrawListPtr windowDrawList = ImGui.GetWindowDrawList();
			windowDrawList.AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), uint.MaxValue);
			if (ImGui.IsItemHovered())
			{
				float hoverPos = (ImGui.GetMousePos() - tl).X / size.X;
				val2 = new ImU8String(3, 1);
				val2.AppendLiteral("@ ");
				val2.AppendFormatted<float>(MathF.Round(hoverPos * 100f, 1));
				val2.AppendLiteral("%");
				ImGui.SetTooltip(val2);
				if (ImGui.IsMouseClicked((ImGuiMouseButton)0))
				{
					float pos4 = hoverPos * 65535f;
					ushort posShort = (ushort)pos4;
					bool flag = ((pos4 == 0f || pos4 == 65535f) ? true : false);
					if (flag || FixedColours.All((FixedColour fixedColour) => fixedColour.Position != posShort))
					{
						Pair pair = Pairs.Find((Pair pair2) => (float)(int)pair2.Begin.Position < pos4 && (float)(int)pair2.End.Position > pos4);
						if (pair != null)
						{
							float pairPos = (pos4 - (float)(int)pair.Begin.Position) / (float)(pair.End.Position - pair.Begin.Position);
							FixedColour newColour = pair.ColourAt(pairPos);
							FixedColours.Add(newColour);
							Editing = newColour.Guid;
						}
					}
				}
			}
			ImGui.Dummy(new Vector2(32f));
			ImGui.SameLine();
			var val3 = ImRaii.Group();
			try
			{
				ImGui.Dummy(new Vector2(32f));
				FixedColour? selected = FixedColours.Find((FixedColour fixedColour) => fixedColour.Guid == Editing);
				if (selected == null)
				{
					Editing = Guid.Empty;
				}
				var val4 = ImRaii.Disabled(selected == null);
				try
				{
					FixedColour editing = selected ?? new FixedColour(32767, 0u);
					ushort originalPosition = editing!.Position;
					float position = (float)(int)originalPosition * 100f / 65535f;
					Vector4 colour = ImGui.ColorConvertU32ToFloat4(editing.Colour);
					bool edited = false;
					ImGui.SetNextItemWidth(300f);
					var val5 = ImRaii.Disabled(selected == null || originalPosition == 0 || originalPosition == ushort.MaxValue);
					try
					{
						if (ImGui.SmallButton(new ImU8String("Delete Node")) && selected != null)
						{
							FixedColours.Remove(selected);
						}
					}
					finally
					{
						((IDisposable)val5)?.Dispose();
					}
					bool flag = selected == null;
					if (!flag)
					{
						bool flag2 = ((originalPosition == 0 || originalPosition == ushort.MaxValue) ? true : false);
						flag = flag2;
					}
					val5 = ImRaii.Disabled(flag);
					try
					{
						edited |= ImGui.SliderFloat(new ImU8String("Position"), ref position, 0f, 100f, new ImU8String("%.1f"), (ImGuiSliderFlags)0);
					}
					finally
					{
						((IDisposable)val5)?.Dispose();
					}
					ImGui.SetNextItemWidth(300f);
					if ((edited | ImGui.ColorPicker4(new ImU8String("Colour"), ref colour, (ImGuiColorEditFlags)2)) && selected != null && Editing != Guid.Empty)
					{
						FixedColours.Remove(editing);
						if (originalPosition == 0)
						{
							FixedColours.RemoveAll((FixedColour fixedColour) => fixedColour.Position == ushort.MaxValue);
						}
						ushort newPos = (ushort)(position / 100f * 65535f);
						if ((originalPosition != 0 && originalPosition != ushort.MaxValue) || 1 == 0)
						{
							newPos = ushort.Clamp(newPos, 1, 65534);
						}
						FixedColours.Add(new FixedColour(newPos, ImGui.ColorConvertFloat4ToU32(colour))
						{
							Guid = selected.Guid
						});
						GenerateStyle();
					}
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
				ImGui.Separator();
				var val6 = ImRaii.Group();
				try
				{
					ImGui.SetNextItemWidth(200f);
					ImU8String val7 = new ImU8String("Export Steps");
					ImU8String val8 = default(ImU8String);
					if (ImGui.SliderInt(val7, ref Length, 32, 512, val8, (ImGuiSliderFlags)0))
					{
						GenerateStyle();
					}
					ImGui.SetNextItemWidth(200f);
					ImU8String val9 = new ImU8String("Preview Animation Style");
					val8 = new ImU8String(0, 1);
					val8.AppendFormatted<GradientAnimationStyle>(AnimationStyle);
					if (ImGui.BeginCombo(val9, val8, (ImGuiComboFlags)0))
					{
						GradientAnimationStyle[] values = Enum.GetValues<GradientAnimationStyle>();
						foreach (GradientAnimationStyle e in values)
						{
							ImU8String val10 = new ImU8String(25, 2);
							val10.AppendFormatted<GradientAnimationStyle>(e);
							val10.AppendLiteral("##gradientAnimationStyle+");
							val10.AppendFormatted<GradientAnimationStyle>(e);
							if (ImGui.Selectable(val10, AnimationStyle == e, (ImGuiSelectableFlags)0, default(Vector2)))
							{
								AnimationStyle = e;
								GenerateStyle();
							}
						}
						ImGui.EndCombo();
					}
					ImGui.SetNextItemWidth(200f);
					if (ImGui.InputText(new ImU8String("Preview Text"), ref PreviewText, 32, (ImGuiInputTextFlags)0, (ImGui.ImGuiInputTextCallbackDelegate)null))
					{
						GenerateStyle();
					}
					ImGui.SetNextItemWidth(200f);
					if (ImGui.ColorEdit3(new ImU8String("Preview Colour"), ref PreviewTextColour, (ImGuiColorEditFlags)32))
					{
						GenerateStyle();
					}
				}
				finally
				{
					val6.Dispose();
				}
			}
			finally
			{
				val3.Dispose();
			}
			ImGui.SameLine();
			ImGui.Dummy(new Vector2(32f));
			ImGui.SameLine();
			var val11 = ImRaii.Group();
			try
			{
				ImGui.Dummy(new Vector2(32f));
				foreach (FixedColour a in FixedColours.OrderBy((FixedColour fixedColour) => fixedColour.Position))
				{
					var val12 = ImRaii.PushColor((ImGuiCol)23, a.Colour & 0x80FFFFFFu, true);
					try
					{
						var val13 = ImRaii.PushColor((ImGuiCol)22, a.Colour & 0x40FFFFFF, true);
						try
						{
							var val14 = ImRaii.PushColor((ImGuiCol)21, a.Colour, true);
							try
							{
								ImU8String val15 = new ImU8String(8, 1);
								val15.AppendLiteral("##color_");
								val15.AppendFormatted<Guid>(a.Guid);
								if (ImGui.Button(val15, new Vector2(ImGui.GetTextLineHeightWithSpacing())))
								{
									Editing = a.Guid;
								}
							}
							finally
							{
								((IDisposable)val14)?.Dispose();
							}
						}
						finally
						{
							((IDisposable)val13)?.Dispose();
						}
					}
					finally
					{
						((IDisposable)val12)?.Dispose();
					}
					ImGui.SameLine();
					ImU8String val16 = new ImU8String(3, 1);
					val16.AppendLiteral("@ ");
					val16.AppendFormatted<float>(MathF.Round((float)(a.Position * 100) / 65535f, 1));
					val16.AppendLiteral("%");
					ImGui.Text(val16);
				}
			}
			finally
			{
				val11.Dispose();
			}
		}
		finally
		{
			((IDisposable)_)?.Dispose();
		}
	}

	static GradientBuilder()
	{
		int num = 1;
		List<FixedColour> list = new List<FixedColour>(num);
		CollectionsMarshal.SetCount(list, num);
		CollectionsMarshal.AsSpan(list)[0] = new FixedColour(32767, 4278190080u);
		FixedColours = list;
		Pairs = new List<Pair>();
		Editing = Guid.Empty;
		Mode = 0;
		AnimationStyle = GradientAnimationStyle.Wave;
		PreviewText = "Preview Title";
		PreviewTextColour = Vector3.Zero;
	}
}
