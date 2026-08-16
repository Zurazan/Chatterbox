using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiSeStringRenderer;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;

namespace Chatterbox;

public static class ImGuiEx
{
	private static readonly Dictionary<uint, bool> TreeOpenStates = new Dictionary<uint, bool>();

	private static Vector3 editingColour = Vector3.One;

	public static bool DrawHonorificTitle(Counter counter, string label = "##counterTemplate")
	{
		bool isEdited = false;
		var val = ImRaii.PushColor((ImGuiCol)0, 0u, !counter.IsEditing);
		try
		{
			if (InputText(label, counter.TitleTemplate, delegate(string x)
			{
				counter.TitleTemplate = x;
			}, 32))
			{
				isEdited = true;
			}
			counter.IsEditing = ImGui.IsItemActive();
			if (!counter.IsEditing)
			{
				Vector2 itemRectMin = ImGui.GetItemRectMin();
				ImGuiStylePtr style = ImGui.GetStyle();
				ImGui.SetCursorScreenPos(itemRectMin + style.FramePadding);
				ImDrawListPtr dl = ImGui.GetWindowDrawList();
				Vector2 itemRectMin2 = ImGui.GetItemRectMin();
				style = ImGui.GetStyle();
				Vector2 clipMin = itemRectMin2 + style.FramePadding;
				Vector2 itemRectMax = ImGui.GetItemRectMax();
				style = ImGui.GetStyle();
				Vector2 clipMax = itemRectMax - style.FramePadding;
				clipMin.Y = MathF.Max(clipMin.Y, ImGui.GetWindowPos().Y);
				clipMax.Y = MathF.Min(clipMax.Y, ImGui.GetWindowPos().Y + ImGui.GetWindowHeight());
				dl.PushClipRect(clipMin, clipMax);
				ReadOnlySpan<byte> readOnlySpan = counter.ToSeString(includeQuotes: false).Encode();
				SeStringDrawParams val2 = default(SeStringDrawParams);
				val2.Color = uint.MaxValue;
				val2.WrapWidth = float.MaxValue;
				val2.TargetDrawList = dl;
				val2.Font = UiBuilder.DefaultFont;
				val2.FontSize = UiBuilder.DefaultFontSizePx;
				val2.ScreenOffset = ImGui.GetCursorScreenPos();
		ImGuiHelpers.SeStringWrapped(readOnlySpan, in val2, default(ImGuiId), (ImGuiButtonFlags)1);
				dl.PopClipRect();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		return isEdited;
	}

	public static void DrawStyledLinkText(string text, string url, uint colorId, string tooltip = "")
	{
		string id = (text.Contains("##") ? text.Substring(text.IndexOf("##")) : text.Replace(" ", string.Empty));
		text = text.Replace(id, string.Empty);
		string obj = $"<colortype({colorId})><edgecolortype({colorId})>{text}<colortype(0)><edgecolortype(0)>";
		SeStringDrawParams val = default(SeStringDrawParams);
		ImGuiHelpers.CompileSeStringWrapped(obj, in val, (ImGuiId)id, (ImGuiButtonFlags)1);
		if (!StringExtensions.IsNullOrWhitespace(tooltip))
		{
			SetItemTooltip(tooltip, (ImGuiHoveredFlags)0);
		}
		if (ImGui.IsItemHovered())
		{
			ImGui.SetMouseCursor((ImGuiMouseCursor)7);
		}
		if (ImGui.IsItemClicked((ImGuiMouseButton)0))
		{
			try
			{
				Process.Start(new ProcessStartInfo(url)
				{
					UseShellExecute = true
				});
			}
			catch
			{
			}
		}
	}

	public static void DrawStyledText(string text, uint colorId, string tooltip = "", Action? action = null)
	{
		string id = (text.Contains("##") ? text.Substring(text.IndexOf("##")) : text.Replace(" ", string.Empty));
		text = text.Replace(id, string.Empty);
		string obj = $"<colortype({colorId})><edgecolortype({colorId})>{text}<colortype(0)><edgecolortype(0)>";
		SeStringDrawParams val = default(SeStringDrawParams);
		ImGuiHelpers.CompileSeStringWrapped(obj, in val, (ImGuiId)id, (ImGuiButtonFlags)1);
		if (!StringExtensions.IsNullOrWhitespace(tooltip))
		{
			SetItemTooltip(tooltip, (ImGuiHoveredFlags)0);
		}
		if (action != null)
		{
			if (ImGui.IsItemHovered())
			{
				ImGui.SetMouseCursor((ImGuiMouseCursor)7);
			}
			if (ImGui.IsItemClicked((ImGuiMouseButton)0))
			{
				action();
			}
		}
	}

	public static string EnumToString<T>(T value, string separator = "+") where T : Enum
	{
		if (Convert.ToUInt64(value) == 0L)
		{
			return "None";
		}
		List<string> selected = new List<string>();
		foreach (Enum flag in Enum.GetValues(typeof(T)))
		{
			ulong flagValue = Convert.ToUInt64(flag);
			if (flagValue != 0L && (Convert.ToUInt64(value) & flagValue) == flagValue)
			{
				selected.Add(flag.ToString());
			}
		}
		if (selected.Count <= 0)
		{
			return value.ToString();
		}
		return string.Join(separator, selected);
	}

	public static string EnumToSelectedCountString<T>(T value, string noneText = "None", string allText = "All") where T : Enum
	{
		if (Convert.ToUInt64(value) == 0L)
		{
			return noneText;
		}
		int i = 0;
		Array enumValues = Enum.GetValues(typeof(T));
		foreach (Enum item in enumValues)
		{
			ulong flagValue = Convert.ToUInt64(item);
			if (flagValue != 0L && (Convert.ToUInt64(value) & flagValue) == flagValue)
			{
				i++;
			}
		}
		if (i != 0)
		{
			if (i != enumValues.Length - 1 || string.IsNullOrWhiteSpace(allText))
			{
				return $"{i} Selected";
			}
			return allText;
		}
		return noneText;
	}

	public static TriState NextTriState(TriState current)
	{
		return current switch
		{
			TriState.Ignored => TriState.Allow, 
			TriState.Allow => TriState.Disallow, 
			TriState.Disallow => TriState.Ignored, 
			_ => TriState.Ignored, 
		};
	}

	public static bool Checkbox(string label, bool value, Action<bool> setter)
	{
		if (ImGui.Checkbox(new ImU8String(label), ref value))
		{
			setter(value);
			return true;
		}
		return false;
	}

	public static bool InputText(string label, string value, Action<string> setter, int maxLength = 256)
	{
		if (ImGui.InputText(new ImU8String(label), ref value, maxLength, (ImGuiInputTextFlags)0, (ImGui.ImGuiInputTextCallbackDelegate)null))
		{
			setter(value);
			return true;
		}
		return false;
	}

	public static bool InputTextWithHint(string label, string hint, string value, Action<string> setter, int maxLength = 256)
	{
		if (ImGui.InputTextWithHint(new ImU8String(label), new ImU8String(hint), ref value, maxLength, (ImGuiInputTextFlags)0, (ImGui.ImGuiInputTextCallbackDelegate)null))
		{
			setter(value);
			return true;
		}
		return false;
	}

	public static bool InputInt(string label, int value, Action<int> setter, int step = 1, int stepFast = 100)
	{
		if (ImGui.InputInt(new ImU8String(label), ref value, step, stepFast, default(ImU8String), (ImGuiInputTextFlags)0))
		{
			setter(value);
			return true;
		}
		return false;
	}

	public static bool DragInt(string label, object obj, string nameofProp, float spd, int min, int max)
	{
		PropertyInfo p = obj.GetType().GetProperty(nameofProp);
		int x = (int)(p?.GetValue(obj) ?? ((object)0));
		bool result = ImGui.DragInt(new ImU8String(label), ref x, spd, min, max, default(ImU8String), (ImGuiSliderFlags)0);
		p?.SetValue(obj, x);
		return result;
	}

	public static bool DragInt(string label, Func<int> getter, Action<int> setter, float speed = 1f, int min = 0, int max = 0, string format = "", ImGuiSliderFlags flags = (ImGuiSliderFlags)0)
	{
		int value = getter();
		if (ImGui.DragInt(new ImU8String(label), ref value, speed, min, max, default(ImU8String), (ImGuiSliderFlags)0))
		{
			setter(value);
			return true;
		}
		return false;
	}

	public static bool DragUInt(string label, uint value, Action<uint> setter, float speed = 1f, uint min = 0u, uint max = 0u)
	{
		if (ImGui.DragUInt(new ImU8String(label), ref value, speed, min, max, default(ImU8String), (ImGuiSliderFlags)0))
		{
			setter(value);
			return true;
		}
		return false;
	}

	public static bool DragInt(string label, int value, Action<int> setter, float speed = 1f, int min = 0, int max = 0)
	{
		if (max == 0)
		{
			max = int.MaxValue;
		}
		if (ImGui.DragInt(new ImU8String(label), ref value, speed, min, max, default(ImU8String), (ImGuiSliderFlags)0))
		{
			setter(value);
			return true;
		}
		return false;
	}

	public static bool DragFloat(string label, float value, Action<float> setter, float speed = 1f, float min = 0f, float max = 0f)
	{
		if (max == 0f)
		{
			max = float.MaxValue;
		}
		if (ImGui.DragFloat(new ImU8String(label), ref value, speed, min, max, default(ImU8String), (ImGuiSliderFlags)0))
		{
			setter(value);
			return true;
		}
		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void SetItemTooltip(string s, ImGuiHoveredFlags flags = (ImGuiHoveredFlags)0)
	{
		if (ImGui.IsItemHovered(flags))
		{
			ImGui.SetTooltip(new ImU8String(s));
		}
	}

	public static void IconTextUnformatted(FontAwesomeIcon icon)
	{
		ImGui.PushFont(UiBuilder.IconFont);
		ImGui.TextUnformatted(new ImU8String(FontAwesomeExtensions.ToIconString(icon)));
		ImGui.PopFont();
	}

	public static void IconText(FontAwesomeIcon icon, Vector4 color)
	{
		ImGui.PushFont(UiBuilder.IconFont);
		ImGui.TextColored(in color, new ImU8String(FontAwesomeExtensions.ToIconString(icon)));
		ImGui.PopFont();
	}

	public static void IconText(FontAwesomeIcon icon, ImGuiCol color)
	{
		ImGui.PushFont(UiBuilder.IconFont);
		ImGuiStylePtr style = ImGui.GetStyle();
		ImGui.TextColored(in style.Colors[(int)color], new ImU8String(FontAwesomeExtensions.ToIconString(icon)));
		ImGui.PopFont();
	}

	public static void IconWarningTooltip(string tooltip)
	{
		ImGui.PushFont(UiBuilder.IconFont);
		Vector4 dalamudYellow = ImGuiColors.DalamudYellow;
		ImGui.TextColored(in dalamudYellow, new ImU8String(FontAwesomeExtensions.ToIconString((FontAwesomeIcon)61553)));
		ImGui.PopFont();
		SetItemTooltip(tooltip, (ImGuiHoveredFlags)0);
	}

	public static void IconAlertTooltip(string tooltip)
	{
		ImGui.PushFont(UiBuilder.IconFont);
		Vector4 dalamudRed = ImGuiColors.DalamudRed;
		ImGui.TextColored(in dalamudRed, new ImU8String(FontAwesomeExtensions.ToIconString((FontAwesomeIcon)61553)));
		ImGui.PopFont();
		SetItemTooltip(tooltip, (ImGuiHoveredFlags)0);
	}

	public static void IconWarningText(string text, bool wrapped = false)
	{
		ImGui.PushFont(UiBuilder.IconFont);
		Vector4 dalamudYellow = ImGuiColors.DalamudYellow;
		ImGui.TextColored(in dalamudYellow, new ImU8String(FontAwesomeExtensions.ToIconString((FontAwesomeIcon)61553)));
		ImGui.PopFont();
		ImGui.SameLine();
		if (wrapped)
		{
			dalamudYellow = ImGuiColors.DalamudYellow;
		ImGui.TextColoredWrapped(in dalamudYellow, new ImU8String(text));
		}
		else
		{
			dalamudYellow = ImGuiColors.DalamudYellow;
		ImGui.TextColored(in dalamudYellow, new ImU8String(text));
		}
	}

	public static void IconAlertText(string text, bool wrapped = false)
	{
		ImGui.PushFont(UiBuilder.IconFont);
		Vector4 dalamudRed = ImGuiColors.DalamudRed;
		ImGui.TextColored(in dalamudRed, new ImU8String(FontAwesomeExtensions.ToIconString((FontAwesomeIcon)61553)));
		ImGui.PopFont();
		ImGui.SameLine();
		if (wrapped)
		{
			dalamudRed = ImGuiColors.DalamudRed;
		ImGui.TextColoredWrapped(in dalamudRed, new ImU8String(text));
		}
		else
		{
			dalamudRed = ImGuiColors.DalamudRed;
		ImGui.TextColored(in dalamudRed, new ImU8String(text));
		}
	}

	public static bool IconButton(FontAwesomeIcon icon, string id)
	{
		ImGui.PushFont(UiBuilder.IconFont);
		ImU8String val = default(ImU8String);
		val = new ImU8String(2, 2);
		val.AppendFormatted<string>(FontAwesomeExtensions.ToIconString(icon));
		val.AppendLiteral("##");
		val.AppendFormatted<string>(id);
		bool result = ImGui.Button(val, default(Vector2));
		ImGui.PopFont();
		return result;
	}

	public static float GetIconButtonWidth(FontAwesomeIcon icon)
	{
		ImGui.PushFont(UiBuilder.IconFont);
		Vector2 vector = ImGui.CalcTextSize(new ImU8String(FontAwesomeExtensions.ToIconString(icon)), false, -1f);
		ImGui.PopFont();
		float x = vector.X;
		ImGuiStylePtr style = ImGui.GetStyle();
		return x + style.FramePadding.X * 4f;
	}

	public static bool IconSelectable(FontAwesomeIcon icon)
	{
		ImGui.PushFont(UiBuilder.IconFont);
		bool result = ImGui.Selectable(new ImU8String(FontAwesomeExtensions.ToIconString(icon)), false, (ImGuiSelectableFlags)0, default(Vector2));
		ImGui.PopFont();
		return result;
	}

	public static void IconCheckbox(bool isChecked)
	{
		ImGui.PushFont(UiBuilder.IconFont);
		if (isChecked)
		{
			ImGuiStylePtr style = ImGui.GetStyle();
		ImGui.TextColored(in style.Colors[18], new ImU8String(FontAwesomeExtensions.ToIconString((FontAwesomeIcon)61452)));
		}
		else
		{
			Vector2 size = ImGui.CalcTextSize(new ImU8String(FontAwesomeExtensions.ToIconString((FontAwesomeIcon)61452)), false, -1f);
			ImGui.Dummy(new Vector2(size.X, size.Y));
		}
		ImGui.PopFont();
	}

	public static void IconTriState(TriState state)
	{
		ImGui.PushFont(UiBuilder.IconFont);
		switch (state)
		{
		case TriState.Allow:
		{
			ImGuiStylePtr style = ImGui.GetStyle();
		ImGui.TextColored(in style.Colors[18], new ImU8String(FontAwesomeExtensions.ToIconString((FontAwesomeIcon)61452)));
			break;
		}
		case TriState.Disallow:
		{
			Vector4 dalamudRed = ImGuiColors.DalamudRed;
		ImGui.TextColored(in dalamudRed, new ImU8String(FontAwesomeExtensions.ToIconString((FontAwesomeIcon)61453)));
			break;
		}
		default:
		{
			Vector2 size = ImGui.CalcTextSize(new ImU8String(FontAwesomeExtensions.ToIconString((FontAwesomeIcon)61452)), false, -1f);
			ImGui.Dummy(new Vector2(size.X, size.Y));
			break;
		}
		}
		ImGui.PopFont();
	}

	public static bool TreeNode(string text, Action? contextMenu = null, Vector4 col = default(Vector4), ImGuiTreeNodeFlags flags = (ImGuiTreeNodeFlags)0)
	{
		uint id = ImGui.GetID(new ImU8String(text));
		ImGui.PushID((IntPtr)(int)id);
		TreeOpenStates.TryGetValue(id, out var wasOpen);
		Vector4 obj;
		if (wasOpen)
		{
			obj = ((col == default(Vector4)) ? ChatterboxTheme.Accent : col);
		}
		else if (!(col == default(Vector4)))
		{
			obj = col;
		}
		else
		{
			ImGuiStylePtr style = ImGui.GetStyle();
			obj = style.Colors[0];
		}
		Vector4 color = obj;
		ImGui.PushStyleColor((ImGuiCol)0, color);
		bool isNowOpen = ImGui.TreeNodeEx(new ImU8String(text), flags, default(ImU8String));
		TreeOpenStates[id] = isNowOpen;
		ImGui.PopStyleColor();
		if (contextMenu != null && ImGui.BeginPopupContextItem(new ImU8String("##treeContext"), (ImGuiPopupFlags)1))
		{
			contextMenu();
			ImGui.EndPopup();
		}
		ImGui.PopID();
		return isNowOpen;
	}

	public static bool HonorificGlowPicker(string label, string id, Vector3? color, int? gradientColorSet, GradientAnimationStyle? gradientAnimationStyle, Action<Vector3, int?, GradientAnimationStyle?> setter)
	{
		if (!color.HasValue)
		{
			color = Vector3.One;
		}
		Vector4 colResult = color.Value.AsVector4();
		colResult.W = 1f;
		Vector4 colGradient = Vector4.One;
		if (gradientColorSet.HasValue)
		{
			GradientStyle style = GradientSystem.GetStyle(gradientColorSet.Value, gradientAnimationStyle);
			colGradient = ((style == null) ? Vector4.One : new Vector4(GradientSystem.GetColourVec3(style, 0, 3), 1f));
		}
		ImU8String val = default(ImU8String);
		val = new ImU8String(8, 1);
		val.AppendLiteral("##");
		val.AppendFormatted<string>(id);
		val.AppendLiteral("Button");
		ImU8String val2 = val;
		Vector4 vector = ((colGradient != Vector4.One) ? colGradient : colResult);
		if (ImGui.ColorButton(val2, in vector, (ImGuiColorEditFlags)32, default(Vector2)))
		{
			ImGui.OpenPopup(new ImU8String(id), (ImGuiPopupFlags)0);
		}
		if (!string.IsNullOrWhiteSpace(label))
		{
			ImGui.SameLine();
			ImGui.Text(new ImU8String(label));
		}
		bool r = false;
		if (ImGui.BeginPopup(new ImU8String(id), (ImGuiWindowFlags)0))
		{
			r |= HonorificGradientPicker(colGradient.AsVector3(), ref gradientColorSet, ref gradientAnimationStyle);
			if (!gradientColorSet.HasValue)
			{
				ImGui.SetColorEditOptions((ImGuiColorEditFlags)177209344);
				bool num = r;
				ImU8String val3 = default(ImU8String);
				val3 = new ImU8String(2, 2);
				val3.AppendFormatted<string>(label);
				val3.AppendLiteral("##");
				val3.AppendFormatted<string>(id);
				r = num | ImGui.ColorPicker4(val3, ref colResult, (ImGuiColorEditFlags)181404032);
			}
			ImGui.EndPopup();
		}
		if (r)
		{
			setter(colResult.AsVector3(), gradientColorSet, gradientAnimationStyle);
		}
		return r;
	}

	public static bool HonorificGradientPicker(Vector3 curColor, ref int? gradientColorSet, ref GradientAnimationStyle? gradientAnimationStyle)
	{
		bool r = false;
		float w = ImGui.CalcItemWidth();
		ImU8String val = default(ImU8String);
		val = new ImU8String(19, 0);
		val.AppendLiteral("##rainbowModeSelect");
		if (ImGui.BeginCombo(val, new ImU8String((!gradientColorSet.HasValue) ? "Default Glow" : ""), (ImGuiComboFlags)16))
		{
			if (ImGui.Selectable(new ImU8String("Default Glow"), !gradientColorSet.HasValue, (ImGuiSelectableFlags)1, default(Vector2)))
			{
				ImGui.CloseCurrentPopup();
				gradientColorSet = null;
				gradientAnimationStyle = null;
				r = true;
			}
			if (ImGui.BeginTabBar(new ImU8String("gradientAnimations"), (ImGuiTabBarFlags)0))
			{
				if (ImGui.BeginTabItem(new ImU8String("Wave"), (ImGuiTabItemFlags)0))
				{
					DrawTab(curColor, ref gradientColorSet, ref gradientAnimationStyle, GradientAnimationStyle.Wave);
				}
				if (ImGui.BeginTabItem(new ImU8String("Pulse"), (ImGuiTabItemFlags)0))
				{
					DrawTab(curColor, ref gradientColorSet, ref gradientAnimationStyle, GradientAnimationStyle.Pulse);
				}
				if (ImGui.BeginTabItem(new ImU8String("Static"), (ImGuiTabItemFlags)0))
				{
					DrawTab(curColor, ref gradientColorSet, ref gradientAnimationStyle, GradientAnimationStyle.Static);
				}
				ImGui.EndTabBar();
			}
			ImGui.EndCombo();
		}
		if (gradientColorSet.HasValue)
		{
			GradientStyle style = GradientSystem.GetStyle(gradientColorSet.Value, gradientAnimationStyle);
			Counter obj = new Counter
			{
				TitleColour = curColor,
				TitleGradientAnimationStyle = gradientAnimationStyle,
				TitleGradientColorSet = gradientColorSet,
				TitleTemplate = (style?.Name ?? "Invalid Style")
			};
			Vector2 itemRectMin = ImGui.GetItemRectMin();
			ImGuiStylePtr style2 = ImGui.GetStyle();
			ImGui.SetCursorScreenPos(itemRectMin + style2.FramePadding);
			ReadOnlySpan<byte> readOnlySpan = obj.ToSeString(includeQuotes: false).Encode();
			SeStringDrawParams val2 = default(SeStringDrawParams);
			val2.Color = uint.MaxValue;
			val2.WrapWidth = float.MaxValue;
			val2.TargetDrawList = ImGui.GetWindowDrawList();
			val2.Font = UiBuilder.DefaultFont;
			val2.FontSize = UiBuilder.DefaultFontSizePx;
			val2.ScreenOffset = ImGui.GetCursorScreenPos();
		ImGuiHelpers.SeStringWrapped(readOnlySpan, in val2, default(ImGuiId), (ImGuiButtonFlags)1);
		}
		return r;
		void DrawTab(Vector3 titleColour, ref int? reference, ref GradientAnimationStyle? reference2, GradientAnimationStyle animationStyleTab)
		{
			if (ImGui.BeginChild(new ImU8String("gradientPicker"), new Vector2(w), false, (ImGuiWindowFlags)0))
			{
				ImU8String val3 = default(ImU8String);
				for (int i = 0; i < GradientSystem.NumColourSets; i++)
				{
					GradientStyle style3 = GradientSystem.GetStyle(i, animationStyleTab);
					if (style3 != null && style3.AnimationStyle == animationStyleTab)
					{
						val3 = new ImU8String(14, 1);
						val3.AppendLiteral("##rainbowMode_");
						val3.AppendFormatted<int>(i);
						if (ImGui.Selectable(val3, reference == i && reference2 == animationStyleTab, (ImGuiSelectableFlags)1, default(Vector2)))
						{
							ImGui.CloseCurrentPopup();
							reference = style3.ColourSet;
							reference2 = style3.AnimationStyle;
							r = true;
						}
						Vector2 itemRectMin2 = ImGui.GetItemRectMin();
						ImGuiStylePtr style4 = ImGui.GetStyle();
						ImGui.SetCursorScreenPos(itemRectMin2 + style4.FramePadding);
						ImDrawListPtr dl = ImGui.GetWindowDrawList();
						ReadOnlySpan<byte> readOnlySpan2 = new Counter
						{
							TitleColour = titleColour,
							TitleTemplate = style3.Name,
							TitleGradientColorSet = i,
							TitleGradientAnimationStyle = animationStyleTab
						}.ToSeString(includeQuotes: false).Encode();
						SeStringDrawParams val4 = default(SeStringDrawParams);
						val4.Color = uint.MaxValue;
						val4.WrapWidth = float.MaxValue;
						val4.TargetDrawList = dl;
						val4.Font = UiBuilder.DefaultFont;
						val4.FontSize = UiBuilder.DefaultFontSizePx;
						val4.ScreenOffset = ImGui.GetCursorScreenPos();
		ImGuiHelpers.SeStringWrapped(readOnlySpan2, in val4, default(ImGuiId), (ImGuiButtonFlags)1);
						ImGui.NewLine();
					}
				}
			}
			ImGui.EndChild();
			ImGui.EndTabItem();
		}
	}

	public static bool ColorPicker3(string label, string id, Vector3? value, Action<Vector3> setter)
	{
		if (!value.HasValue)
		{
			value = new Vector3(255f, 255f, 255f);
		}
		Vector4 col = value.Value.AsVector4();
		col.W = 1f;
		ImU8String val = default(ImU8String);
		val = new ImU8String(8, 1);
		val.AppendLiteral("##");
		val.AppendFormatted<string>(id);
		val.AppendLiteral("Button");
		if (ImGui.ColorButton(val, in col, (ImGuiColorEditFlags)32, default(Vector2)))
		{
			ImGui.OpenPopup(new ImU8String(id), (ImGuiPopupFlags)0);
		}
		if (!string.IsNullOrWhiteSpace(label))
		{
			ImGui.SameLine();
			ImGui.Text(new ImU8String(label));
		}
		bool r = false;
		if (ImGui.BeginPopup(new ImU8String(id), (ImGuiWindowFlags)0))
		{
			ImGui.SetColorEditOptions((ImGuiColorEditFlags)177209344);
			ImU8String val2 = default(ImU8String);
			val2 = new ImU8String(2, 2);
			val2.AppendFormatted<string>(label);
			val2.AppendLiteral("##");
			val2.AppendFormatted<string>(id);
			r = ImGui.ColorPicker4(val2, ref col, (ImGuiColorEditFlags)181404032);
			ImGui.EndPopup();
		}
		if (r)
		{
			setter(col.AsVector3());
		}
		return r;
	}

	public static bool DrawColorPicker(string label, Vector3 value, Action<Vector3> setter, Vector2 checkboxSize)
	{
		bool modified = false;
		ImGui.SetNextItemWidth(checkboxSize.X * 2f);
		bool comboOpen;
		if (value == default(Vector3))
		{
			ImGui.PushStyleColor((ImGuiCol)7, uint.MaxValue);
			ImGui.PushStyleColor((ImGuiCol)9, uint.MaxValue);
			ImGui.PushStyleColor((ImGuiCol)8, uint.MaxValue);
			Vector2 p = ImGui.GetCursorScreenPos();
			ImDrawListPtr dl = ImGui.GetWindowDrawList();
			comboOpen = ImGui.BeginCombo(new ImU8String(label), new ImU8String(" "), (ImGuiComboFlags)16);
			dl.AddLine(p, p + new Vector2(checkboxSize.X), 4278190335u, 3f * ImGuiHelpers.GlobalScale);
			ImGui.PopStyleColor(3);
		}
		else
		{
			ImGui.PushStyleColor((ImGuiCol)7, new Vector4(value, 1f));
			ImGui.PushStyleColor((ImGuiCol)9, new Vector4(value, 1f));
			ImGui.PushStyleColor((ImGuiCol)8, new Vector4(value, 1f));
			comboOpen = ImGui.BeginCombo(new ImU8String(label), new ImU8String("  "), (ImGuiComboFlags)16);
			ImGui.PopStyleColor(3);
		}
		if (comboOpen)
		{
			if (ImGui.IsWindowAppearing())
			{
				editingColour = value;
			}
			ImU8String val = default(ImU8String);
			val = new ImU8String(16, 0);
			val.AppendLiteral("##ColorPickClear");
			ImU8String val2 = val;
			Vector4 one = Vector4.One;
		if (ImGui.ColorButton(val2, in one, (ImGuiColorEditFlags)64, default(Vector2)))
			{
				value = default(Vector3);
				modified = true;
				setter(value);
				ImGui.CloseCurrentPopup();
			}
			if (ImGui.IsItemHovered())
			{
				ImGui.SetTooltip(new ImU8String("Clear selected colour"));
				ImGui.SetMouseCursor((ImGuiMouseCursor)7);
			}
			ImDrawListPtr dl2 = ImGui.GetWindowDrawList();
			dl2.AddLine(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), 4278190335u, 3f * ImGuiHelpers.GlobalScale);
			if (value != default(Vector3))
			{
				ImGui.SameLine();
				ImU8String val3 = default(ImU8String);
				val3 = new ImU8String(15, 0);
				val3.AppendLiteral("##ColorPick_old");
				ImU8String val4 = val3;
				one = new Vector4(value, 1f);
		if (ImGui.ColorButton(val4, in one, (ImGuiColorEditFlags)64, default(Vector2)))
				{
					ImGui.CloseCurrentPopup();
				}
				if (ImGui.IsItemHovered())
				{
					ImGui.SetTooltip(new ImU8String("Revert to previous selection"));
					ImGui.SetMouseCursor((ImGuiMouseCursor)7);
				}
			}
			ImGui.SameLine();
			ImU8String val5 = new ImU8String("Confirm");
			one = new Vector4(editingColour, 1f);
		if (ImGui.ColorButton(val5, in one, (ImGuiColorEditFlags)64, new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetItemRectSize().Y)))
			{
				value = editingColour;
				modified = true;
				setter(value);
				ImGui.CloseCurrentPopup();
			}
			Vector2 size = ImGui.GetItemRectSize();
			if (ImGui.IsItemHovered())
			{
				dl2.AddRectFilled(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), 858993459u);
				ImGui.SetMouseCursor((ImGuiMouseCursor)7);
			}
			Vector2 textSize = ImGui.CalcTextSize(new ImU8String("Confirm"), false, -1f);
			dl2.AddText(ImGui.GetItemRectMin() + size / 2f - textSize / 2f, ImGui.ColorConvertFloat4ToU32(new Vector4(editingColour, 1f)) ^ 0xFFFFFF, new ImU8String("Confirm"));
			ImU8String val6 = default(ImU8String);
			val6 = new ImU8String(11, 0);
			val6.AppendLiteral("##ColorPick");
			ImGui.ColorPicker3(val6, ref editingColour, (ImGuiColorEditFlags)272);
			ImGui.EndCombo();
		}
		return modified;
	}
}
