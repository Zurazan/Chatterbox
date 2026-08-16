using System;
using System.Collections.Generic;
using System.Reflection;
using Dalamud.Game.Command;
using HandlerDelegate = Dalamud.Game.Command.IReadOnlyCommandInfo.HandlerDelegate;

namespace Hypostasis.Dalamud;

public sealed class PluginCommandManager : IDisposable
{
	private readonly HashSet<string> pluginCommands = new HashSet<string>();

	public PluginCommandManager(object o)
	{
		MethodInfo[] allMethods = o.GetType().GetAllMethods();
		foreach (MethodInfo method in allMethods)
		{
			AddPluginCommandMethod(o, method);
		}
	}

	private void AddPluginCommandMethod(object o, MethodInfo method)
	{
		PluginCommandAttribute attribute = method.GetCustomAttribute<PluginCommandAttribute>();
		if (attribute == null)
		{
			return;
		}
		CommandInfo commandInfo = new CommandInfo((HandlerDelegate)Delegate.CreateDelegate(typeof(HandlerDelegate), o, method))
		{
			HelpMessage = attribute.HelpMessage,
			ShowInHelp = attribute.ShowInHelp
		};
		string[] commands = attribute.Commands;
		foreach (string command in commands)
		{
			if (DalamudApi.CommandManager.AddHandler(command, commandInfo))
			{
				pluginCommands.Add(command);
			}
		}
	}

	public void Dispose()
	{
		foreach (string command in pluginCommands)
		{
			DalamudApi.CommandManager.RemoveHandler(command);
		}
	}
}
