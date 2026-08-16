using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Chatterbox;

public static class ChatterboxTheme
{
	public static readonly Vector4 Accent = new Vector4(0.26f, 0.84f, 0.76f, 1f);
	public static readonly Vector4 AccentSoft = new Vector4(0.17f, 0.49f, 0.47f, 1f);
	public static readonly Vector4 Highlight = new Vector4(1f, 0.55f, 0.42f, 1f);
	public static readonly Vector4 Text = new Vector4(0.91f, 0.96f, 0.95f, 1f);
	public static readonly Vector4 Muted = new Vector4(0.57f, 0.68f, 0.67f, 1f);
	public static readonly Vector4 Panel = new Vector4(0.07f, 0.12f, 0.14f, 1f);
	public static readonly Vector4 PanelRaised = new Vector4(0.09f, 0.16f, 0.18f, 1f);
	public static readonly Vector4 Success = new Vector4(0.40f, 0.85f, 0.55f, 1f);

	public static void Push()
	{
		float scale = ImGuiHelpers.GlobalScale;
		ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 8f * scale);
		ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 8f * scale);
		ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5f * scale);
		ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 5f * scale);
		ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8f, 7f) * scale);
		ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8f, 5f) * scale);

		ImGui.PushStyleColor(ImGuiCol.Text, Text);
		ImGui.PushStyleColor(ImGuiCol.TextDisabled, Muted);
		ImGui.PushStyleColor(ImGuiCol.WindowBg, Panel);
		ImGui.PushStyleColor(ImGuiCol.ChildBg, PanelRaised);
		ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(0.06f, 0.11f, 0.13f, 0.98f));
		ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.20f, 0.35f, 0.36f, 0.72f));
		ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.11f, 0.21f, 0.23f, 1f));
		ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.15f, 0.29f, 0.31f, 1f));
		ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.18f, 0.38f, 0.39f, 1f));
		ImGui.PushStyleColor(ImGuiCol.CheckMark, Accent);
		ImGui.PushStyleColor(ImGuiCol.SliderGrab, AccentSoft);
		ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, Accent);
		ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.13f, 0.25f, 0.27f, 1f));
		ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.18f, 0.38f, 0.39f, 1f));
		ImGui.PushStyleColor(ImGuiCol.ButtonActive, AccentSoft);
		ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.12f, 0.25f, 0.27f, 0.92f));
		ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.17f, 0.34f, 0.35f, 1f));
		ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.20f, 0.42f, 0.41f, 1f));
		ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.21f, 0.40f, 0.40f, 0.70f));
	}

	public static void Pop()
	{
		ImGui.PopStyleColor(19);
		ImGui.PopStyleVar(6);
	}

	public static void DrawBanner(string kicker, string title, string subtitle)
	{
		float scale = ImGuiHelpers.GlobalScale;
		Vector2 origin = ImGui.GetCursorScreenPos();
		Vector2 size = new Vector2(ImGui.GetContentRegionAvail().X, 62f * scale);
		ImDrawListPtr drawList = ImGui.GetWindowDrawList();
		uint panelColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.07f, 0.17f, 0.19f, 1f));
		uint accentColor = ImGui.ColorConvertFloat4ToU32(Accent);
		drawList.AddRectFilled(origin, origin + size, panelColor, 8f * scale);
		drawList.AddRectFilled(origin, origin + new Vector2(4f * scale, size.Y), accentColor, 8f * scale);

		ImGui.SetCursorScreenPos(origin + new Vector2(16f, 10f) * scale);
		Vector4 kickerColor = Accent;
		ImGui.TextColored(in kickerColor, new ImU8String(kicker));
		ImGui.SameLine();
		Vector4 titleColor = Text;
		ImGui.TextColored(in titleColor, new ImU8String(title));
		ImGui.SetCursorScreenPos(origin + new Vector2(16f, 34f) * scale);
		Vector4 subtitleColor = Muted;
		ImGui.TextColored(in subtitleColor, new ImU8String(subtitle));
		ImGui.SetCursorScreenPos(origin + new Vector2(0f, size.Y + 10f * scale));
	}
}
