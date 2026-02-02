using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Pinta.Core;

public static class RecentlyOpenedFilesManager
{
    internal static Action? RefreshOpenRecentListAction = null;
	private static readonly object recentHistoryLock = new ();

	public static void PrintRecentFiles ()
	{
		var recentFiles = GetRecentFiles ();
		if (recentFiles == null)
			return;

		foreach (var recentFile in recentFiles)
			Console.WriteLine ($"Recent File: ` {recentFile} `");
	}

	// may be null, always check when calling this method
	public static List<string>? GetRecentFiles ()
	{
		string recentHistoryFile =
			Path.Combine (GetUserSettingsDirectory (), "recentHistory.txt");

		lock (recentHistoryLock) {
			try {
				if (!File.Exists (recentHistoryFile)) {
					using (File.Create (recentHistoryFile)) { }
					return new List<string> ();
				}

				var files = File.ReadAllLines (recentHistoryFile)
					.Where (path => File.Exists (path))
					.Distinct ()
					.ToList ();

				return files;
			} catch (Exception e) {
				Console.WriteLine ($"Error: {e.Message}");
				Console.WriteLine (e.StackTrace);
				return null;
			}
		}
	}

	// Copied from SettingsManager.cs
	public static string GetUserSettingsDirectory ()
	{
		var appdataFolder = Environment.GetFolderPath (
			Environment.SpecialFolder.ApplicationData,
			Environment.SpecialFolderOption.Create);

		var settingsDirectory = Path.Combine (appdataFolder, "FamiliarPinta");
		Directory.CreateDirectory (settingsDirectory);

		return settingsDirectory;
	}

	public static void TryAddRecentFile (string? openedFilePath)
	{
		if (string.IsNullOrEmpty (openedFilePath) || !File.Exists (openedFilePath))
			return;

		string recentHistoryFile =
			Path.Combine (GetUserSettingsDirectory (), "recentHistory.txt");

		lock (recentHistoryLock) {
			try {
				var lines = File.Exists (recentHistoryFile)
					? File.ReadAllLines (recentHistoryFile).ToList ()
					: new List<string> ();

				// remove from list if paht is already there (will be re-added at the bottom)
				lines.RemoveAll (path => string.Equals (path, openedFilePath, StringComparison.Ordinal));

				// limit max lines so that the file size doesnt get rlly big
				while (lines.Count >= 10) {
					lines.RemoveAt (0);
				}

				lines.Add (openedFilePath);

				File.WriteAllLines (recentHistoryFile, lines);
                if (RefreshOpenRecentListAction != null)
                {
                    RefreshOpenRecentListAction.Invoke();
                }
			} catch (Exception e) {
				Console.WriteLine ($"Error: {e.Message}");
				Console.WriteLine (e.StackTrace);
			}
		}
	}
}
