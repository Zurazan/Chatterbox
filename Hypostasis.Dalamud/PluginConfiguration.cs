using System;
using System.IO;
using Dalamud.Configuration;
using Dalamud.Interface.ImGuiNotification;

namespace Hypostasis.Dalamud;

public abstract class PluginConfiguration
{
	public Version PluginVersion;

	public static DirectoryInfo ConfigFolder => DalamudApi.PluginInterface.ConfigDirectory;

	public static FileInfo ConfigFile => DalamudApi.PluginInterface.ConfigFile;

	protected PluginConfiguration()
	{
		if (!(this is IPluginConfiguration))
		{
			throw new ApplicationException("A PluginConfiguration MUST implement IPluginConfiguration!");
		}
	}

	public virtual void Initialize()
	{
	}

	public static T LoadConfig<T>() where T : PluginConfiguration, new()
	{
		T config;
		try
		{
			config = (DalamudApi.PluginInterface.GetPluginConfig() as T) ?? new T();
		}
		catch (Exception exception)
		{
			DalamudApi.ShowNotification("Error loading config! Renaming old file and resetting...", (NotificationType)3, 10000u);
			DalamudApi.LogError("Error loading config! Renaming old file and resetting...", exception);
			config = ResetConfig<T>();
		}
		config.Initialize();
		config.UpdateVersion();
		return config;
	}

	public void Save()
	{
		PluginModuleManager.CheckModules();
		DalamudApi.PluginInterface.SavePluginConfig((IPluginConfiguration)this);
	}

	private static T ResetConfig<T>() where T : new()
	{
		ConfigFile.MoveTo(ConfigFile.FullName + ".CORRUPT", overwrite: true);
		return new T();
	}

	private void UpdateVersion()
	{
		Version pluginVersion = Util.AssemblyName.Version;
		if (!(PluginVersion == pluginVersion))
		{
			Version prevVersion = PluginVersion;
			PluginVersion = pluginVersion;
			if (prevVersion < pluginVersion)
			{
				OnUpdate(prevVersion);
			}
		}
	}

	protected virtual void OnUpdate(Version previousVersion)
	{
	}
}
