namespace csSiteGen;

public static partial class Utils {

	// Abstract Directory.GetFiles to get a List as it will be easier to handle later.
	public static List<FileInfo> GetFiles(DirectoryInfo dir){
		return dir.GetFiles("*",SearchOption.AllDirectories).Where(x => x.Name != ".template").ToList();
	}
}
