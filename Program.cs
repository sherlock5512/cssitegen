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

namespace csSiteGen;

class Program
{

	static int Main(string[] args)
	{

		// Get the current versiion number
		string? version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

		// Configure logger
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
		if (!OperatingSystem.IsLinux())
		{
			Log.Warning("This program has only been tested on linux and cannot be assumed to work on other Operating Systems");
			// AnsiConsole.MarkupLine("[[[yellow]Warning[/]]] This program is only tested on linux systems, it may not work on this Operating System.");
		}

		Stopwatch TotalExecutionTime = Stopwatch.StartNew();

		var inputDirectoryOption = new Option<DirectoryInfo>(
			name: "--input",
			description: "The directory that contains the site source.");
		inputDirectoryOption.IsRequired = true;
		inputDirectoryOption.AddValidator(result =>
				{
					if (!result.GetValueForOption(inputDirectoryOption)!.Exists)
					{
						result.ErrorMessage = $"Input directory {result.GetValueForOption(inputDirectoryOption)!.FullName} does not exist";
					}
				});

		var outputDirectoryOption = new Option<DirectoryInfo>(
			name: "--output",
			description: "The directory that the site should be output to.");
		outputDirectoryOption.IsRequired = true;

		var rootCommand = new RootCommand("csSiteGen");

		var cleanCommand = new Command("clean", "Clean the output directory");
		cleanCommand.AddOption(outputDirectoryOption);
		cleanCommand.SetHandler(async (directory) =>
				{
					await Task.Run(() =>
						{
							Clean(directory);
						});
				} ,outputDirectoryOption);

		var convertCommand = new Command("convert", "Convert the input directory and place the files in the output directory.");
		convertCommand.AddOption(inputDirectoryOption);
		convertCommand.AddOption(outputDirectoryOption);
		convertCommand.SetHandler(async (inputDir, outputDir) =>
				{
					await Task.Run(() =>
					{
						Convert(inputDir, outputDir);
					});
				}, inputDirectoryOption, outputDirectoryOption);

		rootCommand.AddCommand(cleanCommand);
		rootCommand.AddCommand(convertCommand);

		var parser = new CommandLineBuilder(rootCommand)
			.UseDefaults()
			.Build();

		parser.Invoke(args);


		TotalExecutionTime.Stop();
		Log.Information("TotalExecutionTime {time:000}ms", TotalExecutionTime.ElapsedMilliseconds);

		Log.CloseAndFlush();
		return 0;
	}

	static int Convert(DirectoryInfo inputDir, DirectoryInfo outputDir)
	{
		AnsiConsole.Console.Profile.Capabilities.Ansi = true;

		List<SiteFile> siteFiles = new();

		Utils.GetFiles(inputDir).ForEach(x => siteFiles.Add(new SiteFile(x)));
		Log.Information("SiteFiles: {@sf} {count}", siteFiles, siteFiles.Count);

		Console.WriteLine($"Converting {siteFiles.Count} files from {inputDir.FullName} to {outputDir.FullName}");
		RuntimeSettings settings = new(inputDir,outputDir);


		Dictionary<string,bool> fileStatus = new();
		Log.Debug("fileStatus {@fileStatus}",fileStatus);

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
					Log.Information("adding {@siteFile} conversion status to fileStatus", siteFiles[i]);
					Log.Debug("FileStatus {@fileStatus}",fileStatus);
					fileStatus.Add(siteFiles[i].FullName,res);
					overallTask.Increment(1);
				}
			});

		Log.Information("Conversion Status {@status}", fileStatus);

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

	static int Clean(DirectoryInfo outputDir)
	{
		if (!outputDir.Exists)
		{
			Log.Warning("Not deleting {dir} as it doesn't exist",outputDir.FullName);
			AnsiConsole.MarkupLineInterpolated($"[bold][[[yellow]Warning[/]]][/] Not cleaning [blue]\"{outputDir}\"[/] as it does not exist.");
			return 0; // success because it doesn't exist.
		}
		try
		{
			Log.Information("Cleaning {dir}",outputDir.FullName);
			AnsiConsole.MarkupInterpolated($"Cleaning [blue]\"{outputDir.FullName}\"[/]");
			outputDir.Delete(recursive: true);
			outputDir.Create();
			AnsiConsole.MarkupLine(" [bold][[[green]OK[/]]][/]");
			AnsiConsole.MarkupLineInterpolated($"\t[grey]>>[/] [green]All files in {outputDir.FullName} purged successfully[/]");
			return 0;
		}
		catch (System.Security.SecurityException e)
		{
			AnsiConsole.MarkupLine(" [bold][[[red]Fail[/]]][/]");
			AnsiConsole.MarkupLine("[orangered1]See log for more details about what went wrong.[/]");
			Log.Error(e, "Failed to delete directory {dir} due to permission error.", outputDir.FullName);
			return 1;
		}
		catch (Exception e)
		{
			AnsiConsole.MarkupLine(" [red][[[bold]Fail[/]]][/]");
			AnsiConsole.MarkupLine("[orangered1]See log for more details about what went wrong.[/]");
			Log.Error(e, "Failed to delete/create directory {dir}", outputDir.FullName);
			return 1;
		}
	}
}
