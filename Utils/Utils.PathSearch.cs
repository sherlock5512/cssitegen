using Serilog;
namespace csSiteGen;

public static partial class Utils {

	// As the PathSearch utility will be called for every convertible file
	// It has been memoized which mean subsequent calls for the same argument just return the result
	static Dictionary<string,string> PathSearchMemo = new();

	///<summary>
	///	Find executable in path
	///</summary>
	public static string PathSearch(string Program){

		if (PathSearchMemo.TryGetValue(Program, out string? result))
		{
			Log.Debug("Memo Hit for {program}", Program);
		    return result;
		}

		var path = System.Environment.GetEnvironmentVariable("PATH")?.Split(':');
		if (path is null)
		{
			Log.Error("Failed to read PATH environment variable.");
		    return string.Empty;
		}

		List<string> candidateExecutables = new();

		foreach (var dir in path)
		{
			// Directories do not need to exist for them to be in path
			// This check avoids attempting to look in directories that
			// Do not exist.
		    if (Directory.Exists(dir))
		    {
				Log.Information("Searching for {Program} in {dir}", Program, dir);
		        candidateExecutables.AddRange(Directory.GetFiles(dir,$"{Program}"));
		    }
		}

		if (candidateExecutables.Count == 0)
		{
			Log.Warning("Dependency {Program} not found",Program);
			return string.Empty;
		}

		result = candidateExecutables.First();
		PathSearchMemo.Add(Program,result);
		Log.Information("Found {program} at {path}",Program, result);
		Log.Debug("Adding {@entry} to PathSearchMemo", PathSearchMemo.Last());
		return result;
	}
}
