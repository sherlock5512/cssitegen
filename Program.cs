/*
 * csSiteGen - A static site generator written in c#
 * Copyright © 2022 Robert Morrison<sherlock5512>
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using Serilog.Events;
using Serilog;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.CommandLine;
using System.Diagnostics;
using System.Reflection;
using Spectre.Console;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace csSiteGen;

class Program
{

	static int Main(string[] args)
	{

		// Get the current version number
		string? version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

		// Only log to file
		// Logging to the console is BAD practice as it tends to be messy.
		// TODO: Log to a known location using environment to find the correct location.
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Debug()
			.WriteTo.File("log.log")
			.CreateLogger();

		Log.Information("Starting New Instance of csSiteGen");

		if (version is not null)
		{
			Log.Information("Version: {ver}", version);
		}
		else
		{
			Log.Warning("Cannot get Version Information");
		}

		// It is very likely that this program will only work on linux. As such it is worth warning the user about this.
		// With further testing and handling of any edge cases it _may_ be possible to have this work anywhere.
		if (!OperatingSystem.IsLinux())
		{
			Log.Warning("This program has only been tested on linux and cannot be assumed to work on other Operating Systems");
			// AnsiConsole.MarkupLine("[[[yellow]Warning[/]]] This program is only tested on linux systems, it may not work on this Operating System.");
		}

		Stopwatch TotalExecutionTime = Stopwatch.StartNew();

		/* !! IMPORTANT !!
			WARN:
		This code uses system.commandline which is still in pre-release
			the following section of code will contain comments to explain the intent of the programmer
			which may be useful if system.commandline has breaking changes
		*/


		// First the option for the project directory is created
		var ProjectDirectoryOption = new Option<DirectoryInfo>(
				name: "--project",
				description: "The Directory for the project");
		ProjectDirectoryOption.IsRequired = false; // it is not required as not providing it infers that the current directory is the project directory
		// If the option is used then the input is validated before control passes to any of the actual code.
		ProjectDirectoryOption.AddValidator(result =>
			{
				if (!result.GetValueForOption(ProjectDirectoryOption)!.Exists)
				{
					result.ErrorMessage = $"Project directory {result.GetValueForOption(ProjectDirectoryOption)} does not exist.";
				}
			}
		);

		// The root command is the entry point for commandline but otherwise does nothing.
		var rootCommand = new RootCommand("csSiteGen");


		// TODO: Verify if the use of async in these functions is necessary

		// This creates the command for cleaning a projects output directory.
		var cleanCommand = new Command("clean", "Clean the projects output directory");
		cleanCommand.AddOption(ProjectDirectoryOption); // This command can use the project directory option we created earlier
		cleanCommand.SetHandler(async (ProjectDirectory) =>
				{
					await Task.Run(() =>
						{
							Clean(ProjectDirectory);
						});
				},ProjectDirectoryOption);

		// This creates the command for actually converting the project.
		var convertCommand = new Command("convert", "Convert the projects input directory and place the files in the output directory.");
		convertCommand.AddOption(ProjectDirectoryOption); // This command can use the project directory option
		convertCommand.SetHandler(async (ProjectDirectory) =>
				{
					await Task.Run(() =>
					{
						Convert(ProjectDirectory);
					});
				},ProjectDirectoryOption);

		// Adding the commands to the root command makes them actually callable on the commandline
		rootCommand.AddCommand(cleanCommand);
		rootCommand.AddCommand(convertCommand);

		// The parser is what actually handles the arguments and dispatches them to the appropriate commands.
		// This is used instead of the simpler method of just Invoking the root command as it automatically creates usage statements.
		// It also makes a user aware that a subcommand needs to be used.
		var parser = new CommandLineBuilder(rootCommand)
			.UseDefaults()
			.Build();

		parser.Invoke(args);


		TotalExecutionTime.Stop();
		Log.Information("TotalExecutionTime {time:000}ms", TotalExecutionTime.ElapsedMilliseconds);

		// Providing there were no early exits it is best to properly close the log before we exit.
		Log.CloseAndFlush();
		return 0;
	}

	static int Convert(DirectoryInfo? ProjectDirectory)
	{
		Log.Information("Convert command was called, beginning conversion.");

		// WARN: This is only temporary as Spectre.Console does not recognise some Linux terminals
		// A better solution that checks the terminal value and sets this option should be added in the future.
		AnsiConsole.Console.Profile.Capabilities.Ansi = true;


		ProjectSettings settings = GetProjectSettings(ProjectDirectory);

		List<SiteFile> siteFiles = new();

		Utils.GetFiles(settings.InputDirectory).ForEach(x => {
				siteFiles.Add(new SiteFile(x));
				Log.Debug("Found file {file}",x.FullName);
				});
		Log.Information("SiteFiles Found {count}", siteFiles, siteFiles.Count);

		Console.WriteLine($"Converting {siteFiles.Count} files from {settings.InputDirectory.FullName} to {settings.OutputDirectory.FullName}");


		Dictionary<string,bool> fileStatus = new();

		AnsiConsole.Progress()
			.AutoRefresh(true)
			.Columns(new ProgressColumn[]
				{
					new TaskDescriptionColumn(),
					new ProgressBarColumn(),
					new PercentageColumn(),
					new RemainingTimeColumn(),
					new SpinnerColumn()
					})
		.Start(ctx =>
			{
				var tasks = siteFiles.Select(x => ctx.AddTask($"Converting {x.Name}")).ToList();
				var overallTask = ctx.AddTask("[bold]Converting Files[/]");
				overallTask.MaxValue = siteFiles.Count();

				for (int i = 0; i < siteFiles.Count; i++)
				{
				    tasks[i].MaxValue = 1;
					Log.Information("Converting file {name} {i}/{count}",siteFiles[i].Name,i+1,siteFiles.Count());
					bool res = siteFiles[i].Convert(settings);
					tasks[i].Increment(1);
					tasks[i].StopTask();
					if (!res)
					{
						Log.Warning("{name} Failed...",siteFiles[i].FullName);
					    tasks[i].Description += " [red]FAILED[/]";
					}
					Log.Information("adding {siteFile} conversion status to fileStatus", siteFiles[i].FullName);
					fileStatus.Add(siteFiles[i].FullName,res);
					overallTask.Increment(1);
				}
			});

		var Failed = fileStatus.Where(x => x.Value == false);
		if ( Failed.Count() > 0)
		{
			foreach (var fail in Failed)
			{
			    AnsiConsole.MarkupLineInterpolated(
					$"[red]File [blue]\"{fail.Key}\"[/] failed to convert[/]");
				AnsiConsole.MarkupLine("[yellow]See log for more details[/]");
			}
		}

		return 0;
	}

	static int Clean(DirectoryInfo? ProjectDirectory)
	{
		Log.Information("Clean command was called, Beginning cleaning");

		ProjectSettings settings = GetProjectSettings(ProjectDirectory);

		if (!settings.OutputDirectory.Exists)
		{
			Log.Warning("Not deleting {dir} as it doesn't exist",settings.OutputDirectory.FullName);
			AnsiConsole.MarkupLineInterpolated($"[bold][[[yellow]Warning[/]]][/] Not cleaning [blue]\"{settings.OutputDirectory.FullName}\"[/] as it does not exist.");
			return 0; // success because it doesn't exist.
		}
		try
		{
			Log.Information("Cleaning {dir}",settings.OutputDirectory.FullName);
			AnsiConsole.MarkupInterpolated($"Cleaning [blue]\"{settings.OutputDirectory.FullName}\"[/]");
			settings.OutputDirectory.Delete(recursive: true);
			settings.OutputDirectory.Create();
			AnsiConsole.MarkupLine(" [bold][[[green]OK[/]]][/]");
			AnsiConsole.MarkupLineInterpolated($"\t[grey]>>[/] [green]All files in {settings.OutputDirectory.FullName} purged successfully[/]");
			return 0;
		}
		catch (System.Security.SecurityException e)
		{
			AnsiConsole.MarkupLine(" [bold][[[red]Fail[/]]][/]");
			AnsiConsole.MarkupLine("[orangered1]See log for more details about what went wrong.[/]");
			Log.Error(e, "Failed to delete directory {dir} due to permission error.", settings.OutputDirectory.FullName);
			return 1;
		}
		catch (Exception e)
		{
			AnsiConsole.MarkupLine(" [red][[[bold]Fail[/]]][/]");
			AnsiConsole.MarkupLine("[orangered1]See log for more details about what went wrong.[/]");
			Log.Error(e, "Failed to delete/create directory {dir}", settings.OutputDirectory.FullName);
			return 1;
		}
	}

	static void EnforceConsistency(ProjectSettings projectSettings)
	{
		// Grab the metadata.

		// Read the metadata.

		// Find deleted files.

		// Figure out what the new name for those files would be.

		// Remove those files.

	}

	static ProjectSettings GetProjectSettings(DirectoryInfo? ProjectDirectory)
	{
		// TODO: implement proper error handling where file access is performed.

		if (ProjectDirectory is null)
		{
			// use the current directory if no project directory is passed.
		    ProjectDirectory = new DirectoryInfo(".");
		}
		Log.Information("{projectdir} => fullname {pdfn}",ProjectDirectory, ProjectDirectory.FullName);
		FileInfo projectFile = new (Path.Combine(ProjectDirectory.FullName,"cssitegen.json"));


		if (!projectFile.Exists)
		{
		    Log.Fatal("Cannot locate project file {pf} in {dir}",projectFile,ProjectDirectory);
			Environment.Exit(1);
		}

		Log.Information("Located Project File {pf}",projectFile.FullName);
		ProjectSettings? projectSettings = JsonSerializer
			.Deserialize<ProjectSettings>(
					projectFile
					.OpenText()
					.ReadToEnd(),
					SourceGenerationContext.Default.ProjectSettings
		);

		if (projectSettings is null)
		{
		    Log.Fatal("Cannot deserialize projectFile");
			Environment.Exit(1);
		}
		projectSettings.setProjectRoot(ProjectDirectory);

		return projectSettings;
	}

}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ProjectSettings))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}
