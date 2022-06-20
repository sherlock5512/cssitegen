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
using System.Reflection;

namespace csSiteGen;

class Program
{

	static int Main(string[] args)
	{
		// Get the current versiion number
		string? version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Debug()
			.WriteTo.Console()
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

		if (args.Length < 2)
		{
			Log.Error("Too Few Args: Expected at least 2 but received {Count}",args.Length);
			return 1;
		}

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

		List<string> Input_files = GetAllFiles(_inputDirectory);
		Log.Information("Input File Count: {count}",Input_files.Count());
		foreach (string item in Input_files)
		{
			Console.WriteLine(item);
		}
		return 0;
	}



	static List<string> GetAllFiles(string directory)
	{
		List<string> res = new();
		Stack<string> dirs = new();

		dirs.Push(directory);
		Log.Debug("Dirs Starting as: {DirStack}",dirs );

		while (dirs.Count > 0)
		{
			var dir = dirs.Pop();
			res.AddRange(Directory.GetFiles(dir));
			foreach (string subdir in Directory.GetDirectories(dir))
			{
				if(File.GetAttributes(subdir).HasFlag(FileAttributes.ReparsePoint))
				{
					Log.Debug("Directory {subdir} is a ReparsePoint(symlink) and has been ignored",subdir);
					continue;
				}
				dirs.Push(subdir);
				Log.Debug("Adding Directory {subdir} to dirs\nResult: {dirs}",subdir,dirs);
			}
		}

		return res;
	}

	static List<string> GetAllFilesMatching(string pattern, string directory)
	{
		List<string> res = new List<string>();
		List<string> files = GetAllFiles(directory);

		throw new NotImplementedException();
	}
}
