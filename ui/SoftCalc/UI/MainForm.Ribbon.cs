// MainForm.Ribbon.cs - The main window's ribbon: commands from its menus, Customize Ribbon
//
// The ribbon is rebuilt when the menus change (catalog loaded, user
// functions added or removed) and when Customize Ribbon applies a new
// layout. OK / Apply writes the layout to settings.json at once
// (design/ribbon_plan.md R3).

using SoftCalc.Logic;
using SoftCalc.Persistence;

namespace SoftCalc.UI;

internal sealed partial class MainForm
{
	// Every command the ribbon can show, from the current menus
	internal RibbonCommands RibbonCommandList()
	{
		var precision = new RibbonCommand
		{
			Id = RibbonLayout.PrecisionId, Text = "Precision", Path = "Precision (Advanced Options)",
			Children = () => _settingItems.TryGetValue("precision", out var items) ? items.Values : Enumerable.Empty<ToolStripItem>(),
			DynamicText = () => $"Prec {(int?)_session.Settings["precision"] ?? _prefs.Precision}",
		};
		var dynamicTexts = new Dictionary<string, Func<string>>
		{
			["View/Word Size"] = () => $"Word {(int?)_session.Settings["word_size"] ?? _prefs.WordSize}",
		};
		return RibbonCommands.FromMenu(_menu, new[] { precision }, new HashSet<ToolStripMenuItem> { _userFunctionsMenu }, dynamicTexts);
	}

	private void RebuildRibbon()
	{
		_ribbon.Build(_ribbonLayout, RibbonCommandList());
		_ribbon.ShowSettings(_session.Settings);
	}

	private void CustomizeRibbon()
	{
		var before = RibbonLayout.Clone(_ribbonLayout);
		using var dialog = new CustomizeRibbonDialog(RibbonCommandList(), _ribbonLayout, ApplyRibbon);
		if (dialog.ShowDialog(this) != DialogResult.OK && !RibbonLayout.SameAs(before, _ribbonLayout))
		{
			ApplyRibbon(before);    // Cancel after Apply: back to the layout from before the window
		}
	}

	private void ApplyRibbon(List<RibbonGroup> layout)
	{
		_ribbonLayout = RibbonLayout.Clone(layout);
		_prefs.Ribbon = RibbonLayout.ToSaved(_ribbonLayout);
		_ownRibbon = true;
		RebuildRibbon();
		try
		{
			_folder.WithLock(() => SettingsStore.SaveRibbon(_folder.SettingsPath, _prefs.Ribbon));
		}
		catch (IOException ex)
		{
			AddNotice("(ribbon layout not saved)", ex.Message);
		}
	}
}
