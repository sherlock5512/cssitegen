using Serilog;
using Spectre.Console;
using System.Diagnostics;
namespace csSiteGen;

public static class Conversions{


	public delegate bool ConvertFunc(FileInfo file, ProjectSettings settings);

	/// <summary>
	/// A Mapping of filetype to ConvertFunc.
	/// </summary>
	public static readonly Dictionary<string,ConvertFunc> Mappings = new(){
		{".md", Pandoc},
	};

	private static readonly string[] StringReplaceFiletypes = {
		".md",
		".html"
	};


	/// <summary>
	///	TEST FUNCTION.
	/// </summary>
	public static bool NoOp(FileInfo file, ProjectSettings settings){
		Log.Information("Performing NoOp Conversion");
		string newName = GetNewName(file,settings,"NoOp");
		Log.Debug("{FullName} -> {newName}",file.FullName,newName);
		Thread.Sleep(1500);
		return true;
	}

	/// <summary>
	/// Copy the file verbatim (doing any baseurl replacements if needed)
	/// </summary>
	public static bool RawCpy(FileInfo file, ProjectSettings settings){
		FileInfo newPath = new FileInfo(GetNewName(file,settings,null));

		Log.Information("RawCpy: Copying {file} to {dest}",file.FullName, newPath.FullName);

		if (!newPath.Directory!.Exists)
		{
		    newPath.Directory.Create();
		}

		try {
			if (StringReplaceFiletypes.Contains(file.Extension))
			{
				File.WriteAllText(newPath.FullName, StringReplace(file, settings));
			}
			else
			{
				file.CopyTo(newPath.FullName, overwrite: true);
			}
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
	public static bool Pandoc(FileInfo file, ProjectSettings settings){
		// NOTE: Some of the code later where the tmpfile is created for baseurl replacement may be too safe.
		// the extension checks may be unnecessary, but this depends on if this function will be retooled to run pandoc for different conversions.
		// for now I have take the safer approach, but the leaner approach may be used in the future when the project is more mature


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

		// the empty string is used as it has a defined identity
		string tmpFile = string.Empty;
		if (StringReplaceFiletypes.Contains(file.Extension))
		{
			Log.Information("calling string replace for file {f}",file.FullName);
		    tmpFile = Path.Join(Path.GetTempPath(),"pandoc",file.Name);
			Directory.CreateDirectory(Path.GetDirectoryName(tmpFile)!); // NOTE: It is practially impossible that this would actually return null
			File.Create(tmpFile).Close(); // TODO: Use the filestream provided by File.Create within a using block to write the text
			File.WriteAllText(tmpFile,StringReplace(file,settings));

			if (template is not null)
			{
				Log.Information("calling string replace for template file");
				string tmpTemplateFile = Path.Join(Path.GetTempPath(),"pandoc",template.Name);
				Directory.CreateDirectory(Path.GetDirectoryName(tmpTemplateFile)!); // NOTE: It is practially impossible that this would actually return null
				File.Create(tmpTemplateFile).Close(); // TODO: Use the filestream provided by File.Create within a using block to write the text
				File.WriteAllText(tmpTemplateFile,StringReplace(template,settings));
				template = new(tmpTemplateFile);
			}
		}

		string pandocArgs;
		// If we have created a temporary file we need to ensure that we use it.
		if (!string.IsNullOrEmpty(tmpFile))
		{
			pandocArgs = $"{tmpFile} -o {GetNewName(file,settings,".html")}";
		}
		else
		{
			pandocArgs = $"{file.FullName} -o {GetNewName(file,settings,".html")}";
		}

		if (template is not null)
		{
			pandocArgs += $" --template={template.FullName}";
		}
		else
		{
			AnsiConsole.MarkupLine("[bold][[[orange1] Warning [/]]][/] Pandoc Template was not located.");
			Log.Warning("Pandoc template for {file} not found",file.Name);
		}

		pandocArgs += " --from markdown+raw_attribute ";
		if (!string.IsNullOrEmpty(settings.SiteName))
		{
			// This is used in my template to add the sitename to the title element.
			pandocArgs += $" --metadata=\"SiteName:{settings.SiteName}\"";
		}
		if (!Directory.Exists(Path.GetDirectoryName(GetNewName(file,settings,".html"))))
		{
		   Directory.CreateDirectory(Path.GetDirectoryName(GetNewName(file,settings,".html"))!);
		}

		bool pandocReturn = RunExternalProgram(pandoc,pandocArgs);
		// If we made a tmpfile delete it after running pandoc against it.
		if (!string.IsNullOrEmpty(tmpFile))
		{
		    File.Delete(tmpFile);
		}
		return pandocReturn;

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

			Log.Debug("{program} STDOUT:\n{stdout}", program, stdout);
			Log.Debug("{program} STDERR:\n{stderr}", program, stderr);

			if (RunProgram.ExitCode != 0)
			{
				Log.Error("{program} execution Failed. Check debug data for more information", program);
				return false;
			}
		}
		return true;
	}

	private static string GetNewName(FileInfo file, ProjectSettings settings, string? newExtension){
		return file.FullName
			.Replace(settings.InputDirectory.FullName, settings.OutputDirectory.FullName)
			.Replace(file.Extension,newExtension ?? file.Extension);
	}

	private static string StringReplace(FileInfo file, ProjectSettings settings){
		Log.Information("Doing StringReplace for {f}", file.FullName);
		// Read the file
		using (StreamReader FileReader = file.OpenText())
		{
			string filestring = FileReader.ReadToEnd();

			if (settings.BaseUrl is null)
			{
				Log.Warning("BaseUrl is null, replacing %BASEURL% with nothing.");
				filestring = filestring.Replace("%BASEURL%","");
			}
			else
			{
				Log.Information("Replacing %BASEURL% with {BaseUrl}",settings.BaseUrl);
				filestring = filestring.Replace("%BASEURL%",settings.BaseUrl);
			}

			if (settings.SiteName is null)
			{
			    Log.Warning("SiteName is null, replacing %SITENAME% with nothing.");
				filestring = filestring.Replace("%SITENAME%","");
			}
			else
			{
				Log.Information("Replacing %SITENAME% with {SiteName}",settings.SiteName);
				filestring = filestring.Replace("%SITENAME%",settings.SiteName);
			}

			return filestring;
		}
	}

}
