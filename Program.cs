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

using Serilog;
using Serilog.Events;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Diagnostics;
// using System.Text.Json;

namespace csSiteGen;

class Program
{

	static int Main(string[] args)
	{
		// Default values
		List<string> _verbatimFileTypes = new List<string>{ // file extenstions we want to have copied verbatim
			"html", // Premade html pages
			"css", // Style Sheets
			// IMAGE TYPES TODO: add webp conversion for most images.
			"jpg",
			"png",
			"webp",
			"gif"
		};
		// TODO: Make this a dictionary/tuple to show a FROM -> TO filetype relationship (Possibly with program)
		List<string> _conversionTypes = new List<string>{ // file extenstions we want to convert to html
			"md"
		};


		// Get the current versiion number
		string? version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

		// Configure logger
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Debug()
			.WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
			.WriteTo.File("log.log")
			.CreateLogger();

		Log.Information("Starting New Instance of csSiteGen");

		if (version is not null)
		{
			Log.Information("Version: {ver}", version);
		}
		else
		{
			Log.Error("Cannot get Version Information");
		}

		// Exit on argument errors
		if (args.Length < 2)
		{
			Log.Error("Too Few Args: Expected at least 2 but received {Count}",args.Length);
			return 1;
		}

		// It is very likely that this program will only work on linux. As such it is worth warning the user about this.
		if (!OperatingSystem.IsLinux())
		{
			Log.Warning("This program has only been tested on linux and cannot be assumed to work on other Operating Systems");
		}

		// Get and check the directories passed to the program
		string _inputDirectory = args[0];
		string _outputDirectory = args[1];

		if (!Directory.Exists(_inputDirectory))
		{
			Log.Error("Input directory '{i}' Does not exist or is not a directory you have access to" , _inputDirectory);
			Environment.Exit(1);
		}
		if (!Directory.Exists(_outputDirectory))
		{
			Log.Error("Output directory '{o}' Does not exist or is not a directory you have access to" , _outputDirectory);
			Environment.Exit(1);
		}

		// Test for dependencies
		List<string> deps = new List<string>{"pandoc"};
		var dep = CheckDeps(deps);
		Log.Debug("CheckDeps result: {@dep}",dep);

		// Deal with the dependency test results
		Dictionary<string,string> depsDict = new();
		if (dep.Item2 is null)
		{
			Log.Debug("Ignoring dependency check as no dependencies listed");
		}
		else
		{
			depsDict = dep.Item2;
		}
		if (!dep.Item1)
		{
			foreach (var dependency in depsDict)
			{
				Log.Error("Dependency {dependency} Cannot be found\nPlease install it or check it is in your PATH",dependency.Key);
			}
			Log.CloseAndFlush();
			Environment.Exit(1);
		}

		List<string> ConvertableInputFiles = new();
		foreach (var ext in _conversionTypes)
		{
			ConvertableInputFiles.AddRange(GetAllFilesMatching($".*{ext}",_inputDirectory));
		}
		Log.Debug("Files matching conversiontypes: {@conversionType} found {count} \n {files} ",_conversionTypes,ConvertableInputFiles.Count(),ConvertableInputFiles);


		Log.CloseAndFlush();
		return 0;
	}

/// <summary>
/// Returns a list of all the files in the given directory and all subdirectories recursive
///	</summary>
/// <remarks>
/// Currently this function ignores reparse points (symlinks) so as to avoid infinite loops.
/// It is probably a good idea to make the function aware of loops so it can read symlinks.
/// </remarks>
/// <param name="directory">
/// The directory to find files in.
/// <param>
/// <returns>
/// A string list of every file in the provided directory and all subdirectories
/// </returns>
	static List<string> GetAllFiles(string directory)
	{
		List<string> res = new();
		Stack<string> dirs = new();

		dirs.Push(directory);
		Log.Debug("Dirs Starting as: {DirStack}",dirs );
		Stopwatch timer = new();
		timer.Start();
		while (dirs.Count > 0)
		{
			var dir = dirs.Pop();
			res.AddRange(Directory.GetFiles(dir));
			foreach (string subdir in Directory.GetDirectories(dir))
			{
				if(File.GetAttributes(subdir).HasFlag(FileAttributes.ReparsePoint))
				{
					/*
					 * FIXME: This is only a temporary measure and should not be relied upon
					 * if possible there should be a depth detection system that allows for
					 * safe symlink following.
					 */
					Log.Debug("Directory {subdir} is a ReparsePoint(symlink) and has been ignored",subdir);
					continue;
				}
				dirs.Push(subdir);
				Log.Debug("Adding Directory {subdir} to dirs\nResult: {dirs}",subdir,dirs);
			}
		}
		timer.Stop();
		Log.Information("Crawled {directory} in {time}ms with {number} results",directory,timer.ElapsedMilliseconds,res.Count());
		return res;
	}

	static List<string> GetAllFilesMatching(string pattern, string directory)
	{
		List<string> res = new();
		List<string> files = GetAllFiles(directory);

		Regex expression = new Regex(pattern,
				RegexOptions.Compiled);

		res = files.FindAll( x => expression.IsMatch(x));  // Here a lambda function is used as a predicate to do regex across the whole file list

		return res;
	}

	void Usage()
	{
		/*
		 * This function will be called when argument errors happen. This will
		 * also be called if the user triggers the help flag currently this
		 * function throws a not implemented exception since it I cannot write
		 * the usage until I have other functionality
		 */
		throw new NotImplementedException();
	}

	static Tuple<bool,Dictionary<string,string>?> CheckDeps(List<string>? dependencies)
	{
		bool success = true;
		Stopwatch timer = new();
		if (! (dependencies?.Count() > 0)) // assume success as no dependencies
		{
			return new Tuple<bool,Dictionary<string,string>?>(true,null);
		}


		var path = System.Environment.GetEnvironmentVariable("PATH")?.Split(':');
		if (path is null)
		{
			Log.Error("Cannot read system path.");
			Environment.Exit(1);
		}
		Log.Debug("Path is: {path}",path);

		List<string> executables = new();
		timer = Stopwatch.StartNew();
		foreach (string dir in path)
		{
			if (Directory.Exists(dir)) // Sometimes People fuck up their path variables and include directories that don't exist.
			{
				executables.AddRange(GetAllFiles(dir));
			}
		}
		timer.Stop();
		Log.Verbose("Executables are: {executables}",executables);
		Log.Information("Got executables list in {timer:000}ms",timer.ElapsedMilliseconds);

		// Do some regex magic to find an executable in the path.
		Dictionary<string,string>? result = new();
		Dictionary<string,string>? failures = new();

		foreach (string dependency in dependencies)
		{
			timer = Stopwatch.StartNew();
			string regex = $"/(.*/)*{dependency}";
			Regex expr = new Regex(regex,RegexOptions.Compiled);
			Log.Debug("Compiled regex: {expr}",expr);
			string? match = executables.Find( x => expr.IsMatch(x));
			if (match is not null && match != "")
			{
				timer.Stop();
				Log.Information("Dependency {dep} found at {path} in {timer:000}ms",dependency,match,timer.ElapsedMilliseconds);
				result.Add(dependency,match); // Create a dictionary of paths to found executables
			}
			else
			{
				timer.Stop();
				Log.Information("Dependency {dep} cannot be found, search took {timer:000ms}",dependency,timer.ElapsedMilliseconds);
				success = false;
				failures.Add(dependency,dependency);
			}
		}

		if (success)
		{
			return new Tuple<bool,Dictionary<string,string>?>(success,result);
		}
		else
		{
			return new Tuple<bool,Dictionary<string,string>?>(success,failures);
		}
	}

	static bool Convert(Dictionary<string,string> files)
	{

		return false;
	}
}
