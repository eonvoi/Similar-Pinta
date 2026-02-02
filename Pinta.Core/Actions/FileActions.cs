// 
// FileActions.cs
//  
// Author:
//       Jonathan Pobst <monkey@jpobst.com>
// 
// Copyright (c) 2010 Jonathan Pobst
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Linq;
using System.Threading.Tasks;

namespace Pinta.Core;

public sealed class FileActions
{
	public Command New { get; }
	//public Command NewScreenshot { get; }
	public Command Open { get; }
	public Command OpenRecent { get; }
	public Command Close { get; }
	public Command Save { get; }
	public Command SaveAs { get; }
	public Command SaveAll { get; }
	public Command Print { get; }

	public event EventHandler<ModifyCompressionEventArgs>? ModifyCompression;

	/// <remarks>
	/// The returned value is
	/// <see langword="true" /> if was save succeeded
	/// and
	/// <see langword="false" /> otherwise
	/// </remarks>
	public event AsyncEventHandler<FileActions, DocumentSaveEventArgs>.Returning<bool>? SaveDocument;

	private readonly SystemManager system;
	private readonly AppActions app;
	public FileActions (SystemManager system, AppActions app)
	{
		New = new Command (
			"new",
			Translations.GetString ("New..."),
			null,
			Resources.StandardIcons.DocumentNew,
			shortcuts: ["<Primary>N"]
		) { ShortLabel = Translations.GetString ("New") };

		/* NewScreenshot = new Command (
			"NewScreenshot",
			Translations.GetString ("New Screenshot..."),
			null,
			Resources.StandardIcons.ViewFullscreen); */

		Open = new Command (
			"open",
			Translations.GetString ("Open..."),
			null,
			Resources.StandardIcons.DocumentOpen,
			shortcuts: ["<Primary>O"]
		) { ShortLabel = Translations.GetString ("Open") };

		OpenRecent = new Command (
			"recent",
			Translations.GetString ("Open Recent"),
			null,
			Resources.StandardIcons.DocumentOpen
		) { ShortLabel = Translations.GetString ("Recent") };

		Close = new Command (
			"close",
			Translations.GetString ("Close"),
			null,
			Resources.StandardIcons.WindowClose,
			shortcuts: ["<Primary>W"]);

		Save = new Command (
			"save",
			Translations.GetString ("Save"),
			null,
			Resources.StandardIcons.DocumentSave,
			shortcuts: ["<Primary>S"]);

		SaveAs = new Command (
			"saveAs",
			Translations.GetString ("Save As..."),
			null,
			Resources.StandardIcons.DocumentSaveAs,
			shortcuts: ["<Primary><Shift>S"]);

		SaveAll = new Command (
			"SaveAll",
			Translations.GetString ("Save All"),
			null,
			Resources.StandardIcons.DocumentSave,
			shortcuts: ["<Ctrl><Alt>A"]);

		Print = new Command (
			"print",
			Translations.GetString ("Print"),
			null,
			Resources.StandardIcons.DocumentPrint);

		this.system = system;
		this.app = app;
	}

	Gtk.Application? application = null;
	Gio.Menu? recent_files_menu = null;

	public void RegisterActions (Gtk.Application app, Gio.Menu menu)
	{
		// store a reference to the application
		application = app;
		bool isMac = system.OperatingSystem == OS.Mac;

		Gio.Menu save_section = Gio.Menu.New ();
		save_section.AppendItem (Save.CreateMenuItem ());
		save_section.AppendItem (SaveAs.CreateMenuItem ());
		save_section.AppendItem (SaveAll.CreateMenuItem ());

		Gio.Menu close_section = Gio.Menu.New ();
		close_section.AppendItem (Close.CreateMenuItem ());

		menu.AppendItem (New.CreateMenuItem ());
		// Removing this since it is not in Paint.NET 5.1.11
		//menu.AppendItem (NewScreenshot.CreateMenuItem ());
		menu.AppendItem (Open.CreateMenuItem ());

		// open recent
		// store as variable to reuse later in `RefreshRecentFilesList`
		recent_files_menu = Gio.Menu.New ();
		menu.AppendSubmenu (Translations.GetString ("Open Recent"), recent_files_menu);
		#region i'm king terry the terrible (it's static)
		RecentlyOpenedFilesManager.RefreshOpenRecentListAction = RefreshRecentFilesList;
		#endregion
		RefreshRecentFilesList ();

		menu.AppendSection (null, save_section);
		menu.AppendSection (null, close_section);

		// Already in Mac's app menu (apparently)
		if (!isMac) {
			Gio.Menu exit_section = Gio.Menu.New ();
			exit_section.AppendItem (this.app.Exit.CreateMenuItem ());
			menu.AppendSection (null, exit_section);
		}
#if false
		// Printing is disabled for now until it is fully functional.
		menu.Append (Print.CreateAcceleratedMenuItem (Gdk.Key.P, Gdk.ModifierType.ControlMask));
		menu.AppendSeparator ();
#endif
		app.AddCommands ([
			New,
			//NewScreenshot,
			Open,
			OpenRecent,

			Save,
			SaveAs,
			SaveAll,

			Close]);

		if (!isMac)
			app.AddCommand (this.app.Exit); // This is part of the application menu on macOS
	}

	public void RefreshRecentFilesList ()
	{
		if (application == null || recent_files_menu == null)
		{
			return;
		}

		var recentFiles = RecentlyOpenedFilesManager.GetRecentFiles ();
		recent_files_menu.RemoveAll();

		if (recentFiles == null || recentFiles.Count == 0) {
			var item = new Gio.MenuItem ();
			item.SetLabel ("None");
			item.SetAttributeValue ("enabled", GLib.Variant.NewBoolean (false));
			recent_files_menu.AppendItem (item);
		} else {
			foreach (string filePath in recentFiles) {
				var item = new Gio.MenuItem ();
				item.SetLabel (filePath);

				// all filePaths get assigned to the same action
				// targetValue is the "parameter" we later get in `action.OnActivate`
				item.SetActionAndTargetValue ("app.open_recent", GLib.Variant.NewString (filePath));

				recent_files_menu.AppendItem (item);
			}

			// create "open_recent" if it is null
			// (im too stupid to add it to ActionHandlers lol)
			if (application.LookupAction ("open_recent") == null) {
				var action = Gio.SimpleAction.New ("open_recent", GLib.VariantType.String);

				// listen for "open_recent" activations (like the user clicking the menu item)
				action.OnActivate += (sender, e) => {
					string path = e.Parameter?.GetString (out _) ?? ""; // length is discarded with "out _" since its not needed
					if (!string.IsNullOrEmpty (path)) {
						PintaCore.Workspace.OpenFile (Gio.FileHelper.NewForPath (path));
					}
				};

				application.AddAction (action);
			}
		}
	}

	public void RegisterHandlers () { }

	/// <returns>
	/// <see langword="true"/> if the save succeeded,
	/// <see langword="false"/> otherwise (for example, if it was canceled)
	/// </returns>
	internal async Task<bool> RaiseSaveDocument (Document document, bool saveAs)
	{
		if (SaveDocument is null)
			throw new InvalidOperationException ("GUI is not handling Workspace.SaveDocument");

		DocumentSaveEventArgs e = new (document, saveAs);
		var results = await SaveDocument.InvokeSequential (this, e);
		return results.All (succeeded => succeeded);
	}

	internal int RaiseModifyCompression (int defaultCompression, Gtk.Window parent)
	{
		ModifyCompressionEventArgs e = new (defaultCompression, parent);
		ModifyCompression?.Invoke (this, e);
		return
			e.Cancel
			? -1
			: e.Quality;
	}
}
