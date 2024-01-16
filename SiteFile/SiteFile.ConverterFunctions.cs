using Serilog;
using Spectre.Console;
using System.Diagnostics;
namespace csSiteGen;

public static class Conversions{


	public delegate bool ConvertFunc(FileInfo file, RuntimeSettings settings);

	/// <summary>
	/// A Mapping of filetype to ConvertFunc.
	/// </summary>
	public static readonly Dictionary<string,ConvertFunc> Mappings = new(){
		{".md", Pandoc},
	};


	/// <summary>
	///	TEST FUNCTION.
	/// </summary>
	public static bool NoOp(FileInfo file, RuntimeSettings settings){
		Log.Information("Performing NoOp Conversion");
		string newName = GetNewName(file,settings,"NoOp");
		Log.Debug("{FullName} -> {newName}",file.FullName,newName);
		Thread.Sleep(1500);
		return true;
	}

	/// <summary>
	/// Copy the file verbatim
	/// </summary>
	public static bool RawCpy(FileInfo file, RuntimeSettings settings){
		FileInfo newPath = new FileInfo(GetNewName(file,settings,null));

		Log.Information("RawCpy: Copying {file} to {dest}",file.FullName, newPath.FullName);

		if (!newPath.Directory!.Exists)
		{
		    newPath.Directory.Create();
		}

		try {
		file.CopyTo(newPath.FullName, overwrite: true);
		}
		catch (Exception e){
			Log.Fatal(e,"Copy Failed");
			return false;
		}
		return true;
	}

	/// <summary>
	/// Execute pandoc on the file, automatically detecting the template to use.
	/// </summary>
	public static bool Pandoc(FileInfo file, RuntimeSettings settings){
		Log.Information("Attempting to convert {file} using pandoc",file.Name);

		// Look for pandoc
		string pandoc = Utils.PathSearch("pandoc");
		if (string.IsNullOrEmpty(pandoc))
		{
			Console.WriteLine("Conversion failed due to dependency being unavailable.");
		    return false;
		}
		Log.Information("Located pandoc binary.");

		// Look for template file.
		FileInfo? template = null;
		DirectoryInfo? searchDir = file.Directory;
		do
		{
			// This loop starts searching at the directory of the file,
			// and if a template is not found works up to the InputDirectory.
			// If no template is found at any level we simply run pandoc with no template.

			if (searchDir is null) // While it is unlikely this could happen the check is here to please the compiler.
			{
			    break;
			}

			var result = searchDir.GetFiles(".template");
			if (result.Length > 0)
			{
			    template = result.First();
				Log.Information("Found template {template}", template);
				break;
			}

			// Go up the tree.
			searchDir = searchDir.Parent;
		} while (searchDir != settings.InputDirectory); // Check last as we want to search the InputDirectory

		string pandocArgs = $"{file.FullName} -o {GetNewName(file,settings,".html")}";

		if (template is not null)
		{
			pandocArgs += $" --template={template.FullName}";
		}
		else
		{
			AnsiConsole.MarkupLine("[bold][[[orange1] Warning [/]]][/] Pandoc Template was not located.");
			Log.Warning("Pandoc template for {file} not found",file.Name);
		}

		return RunExternalProgram(pandoc,pandocArgs);

	}

	private static bool RunExternalProgram(string program, string args)
	{

		Log.Information("Executing {program}", program);
		Log.Debug("Full arguments {args}",args);

		using (Process RunProgram = new())
		{
			// Reading stderr and stdout needs to be done carefully.
			string? stdout = null;
			string? stderr = null;

			RunProgram.StartInfo.UseShellExecute = false;
			RunProgram.StartInfo.FileName = program;
			RunProgram.StartInfo.CreateNoWindow = true;
			RunProgram.StartInfo.Arguments = args;
			RunProgram.StartInfo.RedirectStandardOutput = true;
			RunProgram.StartInfo.RedirectStandardError = true;

			// Add a handler to append stderr to the stderr string
			RunProgram.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
					{stderr += e.Data;});
			RunProgram.OutputDataReceived += new DataReceivedEventHandler((sender, o) =>
					{stdout += o.Data;});

			RunProgram.Start();
			RunProgram.BeginErrorReadLine();
			RunProgram.BeginOutputReadLine();

			RunProgram.WaitForExit();

			Log.Debug("{program} stdout {stdout}", program, stdout);
			Log.Debug("{program} stderr {stderr}", program, stderr);

			if (RunProgram.ExitCode != 0)
			{
				Log.Error("{program} execution Failed. Check debug data for more information", program);
				return false;
			}
		}
		return true;
	}

	private static string GetNewName(FileInfo file, RuntimeSettings settings, string? newExtension){
		return file.FullName
			.Replace(settings.InputDirectory.FullName, settings.OutputDirectory.FullName)
			.Replace(file.Extension,newExtension ?? file.Extension);
	}
}
