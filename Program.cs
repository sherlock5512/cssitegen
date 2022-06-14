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
			Log.Error("Too Few Args");
			return 1;
		}

		string _inputDirectory = args[0];
		string _outputDirectory = args[1];

		Log.Debug("{_inputDirectory} , {_outputDirectory}",_inputDirectory,_outputDirectory);
		Log.Debug("{a} , {b}",Directory.Exists(_inputDirectory),Directory.Exists(_outputDirectory));

		if (!Directory.Exists(_inputDirectory))
		{
			Log.Error("Input directory '{i}' Does not exist or is not a directory you have access to" , _inputDirectory);
			Environment.Exit(1);
		}
		if (!Directory.Exists(_outputDirectory))
		{
			Log.Error("Output directory '{o}' Does not exist or is not a directory you have access to" , _outputDirectory);

			Console.WriteLine("Do");
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
