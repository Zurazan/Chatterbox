using System;
using Dalamud.Game.Gui.Toast;

namespace Chatterbox;

public class CounterManager
{
	private Plugin plugin;

	private Trigger? LastTrigger { get; set; }

	private DateTime? LastTriggerStartTime { get; set; }

	private DateTime? LastTriggerEndTime { get; set; }

	public CounterManager(Plugin plugin)
	{
		this.plugin = plugin;
	}

	public void Update()
	{
		try
		{
			if (!Plugin.Config.Enabled || PlayerManager.LocalPlayer == null)
			{
				Dispose();
			}
			else if (LastTrigger != null && LastTriggerEndTime.HasValue)
			{
				if (DateTime.Now > LastTriggerEndTime.Value)
				{
					ClearTitle();
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.Warning(ex, "CounterManager Update Exception", Array.Empty<object>());
		}
	}

	public void UpdateCounter(Trigger trigger, EntityInfo instigator, EntityInfo? receiver)
	{
		Counter counter = trigger.GetCounter();
		if (counter != null)
		{
			counter.Amount++;
			Plugin.Config.Save();
			if (counter.CanDisplayTitle())
			{
				SetTitle(trigger, counter, instigator.Forename, instigator.Surname, receiver?.Forename, receiver?.Surname);
			}
			if (counter.CanDisplayToast())
			{
				SetToast(counter, instigator.Forename, instigator.Surname, receiver?.Forename, receiver?.Surname);
			}
			if (counter.CanDisplayEcho())
			{
				SetEcho(counter, instigator.Forename, instigator.Surname, receiver?.Forename, receiver?.Surname);
			}
		}
	}

	public void UpdateCounter(Trigger trigger, string instigatorName)
	{
		Counter counter = trigger.GetCounter();
		if (counter != null)
		{
			counter.Amount++;
			Plugin.Config.Save();
			string forename = instigatorName.GetForename();
			string surname = instigatorName.GetSurnameWorld().Item1;
			if (counter.CanDisplayTitle())
			{
				SetTitle(trigger, counter, forename, surname);
			}
			if (counter.CanDisplayToast())
			{
				SetToast(counter, forename, surname);
			}
			if (counter.CanDisplayEcho())
			{
				SetEcho(counter, forename, surname);
			}
		}
	}

	public void SetTitle(Trigger trigger, Counter? counter, string instForename, string instSurname, string? recForename = "", string? recSurname = "")
	{
		try
		{
			if (Plugin.Config.Enabled && PlayerManager.LocalPlayer != null && counter != null && !plugin.HasInvalidConditionForTitle() && (!LastTriggerStartTime.HasValue || !(DateTime.Now < LastTriggerStartTime.Value.AddMilliseconds(Plugin.Config.CounterCooldown))))
			{
				string template = GetTemplate(counter.TitleTemplate, counter.Amount, instForename, instSurname, recForename, recSurname);
				if (!string.IsNullOrWhiteSpace(template))
				{
					LastTrigger = trigger;
					LastTriggerStartTime = DateTime.Now;
					LastTriggerEndTime = DateTime.Now.AddMilliseconds(counter.GetDuration());
					plugin.Honorific?.SetTitle(template, counter.TitlePrefix, counter.TitleColour, counter.TitleGlow, counter.TitleGradientColorSet, counter.TitleGradientAnimationStyle);
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.Warning(ex, "Honorific IPC Exception", Array.Empty<object>());
		}
	}

	public void ClearTitle()
	{
		if (LastTrigger == null)
		{
			return;
		}
		LastTrigger = null;
		LastTriggerStartTime = null;
		LastTriggerEndTime = null;
		plugin.Honorific?.ClearTitle();
	}

	public void Dispose()
	{
		if (LastTrigger != null)
		{
			LastTrigger = null;
			LastTriggerStartTime = null;
			LastTriggerEndTime = null;
			plugin.Honorific?.ClearTitle();
		}
	}

	public void SetToast(Counter? counter, string instForename, string instSurname, string? recForename = "", string? recSurname = "")
	{
		if (!Plugin.Config.Enabled || PlayerManager.LocalPlayer == null || counter == null)
		{
			return;
		}
		string template = GetTemplate(counter.ToastTemplate, counter.Amount, instForename, instSurname, recForename, recSurname);
		if (string.IsNullOrWhiteSpace(template))
		{
			return;
		}
		Plugin.Framework.RunOnFrameworkThread((Action)delegate
		{
			if (counter.ToastDisplayType == ToastDisplayType.Normal)
			{
				Plugin.ToastGui.ShowNormal(template, new ToastOptions
				{
					Speed = counter.ToastDisplaySpeed == ToastDisplaySpeed.Fast ? ToastSpeed.Fast : ToastSpeed.Slow,
					Position = counter.ToastDisplayPosition == ToastDisplayPosition.Bottom ? ToastPosition.Bottom : ToastPosition.Top
				});
			}
			else if (counter.ToastDisplayType == ToastDisplayType.Quest)
			{
				Plugin.ToastGui.ShowQuest(template, new QuestToastOptions
				{
					Position = (QuestToastPosition)0
				});
			}
			else
			{
				Plugin.ToastGui.ShowError(template);
			}
		});
	}

	public void SetEcho(Counter? counter, string instForename, string instSurname, string? recForename = "", string? recSurname = "")
	{
		if (Plugin.Config.Enabled && PlayerManager.LocalPlayer != null && counter != null)
		{
			string template = GetTemplate(counter.EchoTemplate, counter.Amount, instForename, instSurname, recForename, recSurname);
			if (!string.IsNullOrWhiteSpace(template))
			{
				plugin.Chat.SendEcho(template);
			}
		}
	}

	private string GetTemplate(string template, int amount, string instForename, string instSurname, string? recForename = "", string? recSurname = "")
	{
		if (string.IsNullOrWhiteSpace(template))
		{
			return string.Empty;
		}
		return template.Replace("%n%", $"{amount}").Replace("%ifn%", instForename).Replace("%isn%", instSurname)
			.Replace("%rfn%", recForename)
			.Replace("%rsn%", recSurname);
	}
}
