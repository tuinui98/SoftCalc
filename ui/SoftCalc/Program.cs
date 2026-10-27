// Program.cs - SoftCalc entry point: locate the engine, open the main window

using SoftCalc.Engine;
using SoftCalc.UI;

namespace SoftCalc;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
		ApplicationConfiguration.Initialize();
		System.Diagnostics.ProcessStartInfo startInfo;
		try
		{
			startInfo = EngineLocator.Locate();
		}
		catch (FileNotFoundException ex)
		{
			MessageBox.Show(ex.Message, "SoftCalc", MessageBoxButtons.OK, MessageBoxIcon.Error);
			return;
		}
		using var session = new EngineSession(startInfo);
		Application.Run(new MainForm(session));
	}
}
