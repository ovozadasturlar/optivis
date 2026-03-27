using Avalonia;
using Avalonia.ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace OptiVis.Desktop;

class Program
{
	private const uint MessageBoxIconError = 0x00000010;
	private const uint MessageBoxIconWarning = 0x00000030;

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

	[STAThread]
	public static void Main(string[] args)
	{
		try
		{
			BuildAvaloniaApp()
				.StartWithClassicDesktopLifetime(args);
		}
		catch (Exception ex)
		{
			var logPath = WriteStartupErrorLog(ex);
			ShowStartupMessage(
				$"OptiVis ishga tushishda xatolik berdi.\n\n{ex.Message}\n\nLog fayl:\n{logPath}",
				isError: true);
		}
	}

	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.WithInterFont()
			.UseReactiveUI()
			.LogToTrace();

	internal static void ShowStartupMessage(string message, bool isError)
	{
		try
		{
			Debug.WriteLine(message);
			Console.WriteLine(message);

			if (OperatingSystem.IsWindows())
			{
				var type = isError ? MessageBoxIconError : MessageBoxIconWarning;
				MessageBoxW(IntPtr.Zero, message, "OptiVis", type);
			}
		}
		catch
		{
		}
	}

	private static string WriteStartupErrorLog(Exception ex)
	{
		try
		{
			var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			var folder = Path.Combine(appData, "OptiVis");
			Directory.CreateDirectory(folder);

			var logPath = Path.Combine(folder, "startup-errors.log");
			var payload = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n";
			File.AppendAllText(logPath, payload);
			return logPath;
		}
		catch
		{
			return "startup-errors.log yozilmadi";
		}
	}
}
