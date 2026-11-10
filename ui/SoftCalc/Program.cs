// Program.cs - SoftCalc entry point: locate the engine, open the main window

using SoftCalc.Engine;
using SoftCalc.Persistence;
using SoftCalc.UI;

namespace SoftCalc;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
		ApplicationConfiguration.Initialize();
		var folder = DataFolder.Default();
		Theme.Apply(SettingsStore.Load(folder.SettingsPath).Settings.Theme);
		System.Diagnostics.ProcessStartInfo startInfo;
		try
		{
			startInfo = EngineLocator.Locate(folder.EngineRoot);
		}
		catch (Exception ex) when (ex is FileNotFoundException or IOException or UnauthorizedAccessException
			or InvalidDataException)
		{
			MessageBox.Show("SoftCalc could not start its engine." + Environment.NewLine + Environment.NewLine + ex.Message, "SoftCalc",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
			return;
		}
		Application.AddMessageFilter(new HelpWindow.HelpKey());    // F1 opens Help from any window
		bool restart;
		using (var session = new EngineSession(startInfo))
		{
			var form = new MainForm(session, folder);
			Application.Run(form);
			restart = form.RestartRequested;
		}
		if (restart && Environment.ProcessPath is { } exe)
		{
			System.Diagnostics.Process.Start(exe);    // a new theme takes effect in a fresh process
		}
	}
}
