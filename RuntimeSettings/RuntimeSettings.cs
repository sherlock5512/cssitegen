namespace csSiteGen;


/// <summary>
/// Class <c>RuntimeSettings</c> Contains all the settings that could be loaded from the commandline.
/// </summary>
public class RuntimeSettings {
	public DirectoryInfo InputDirectory {get; private set;}
	public DirectoryInfo OutputDirectory {get; private set;}
	public string? BaseUrl {get; private set;}


	public RuntimeSettings(string inputDirectory, string outputDirectory){
		InputDirectory = new DirectoryInfo(inputDirectory);
		OutputDirectory = new DirectoryInfo(outputDirectory);

		/* NOTE: it is the responisbility of the UI code to check the values passed are good.
		*/
	}


	public RuntimeSettings(DirectoryInfo inputDirectory, DirectoryInfo outputDirectory){
		InputDirectory = inputDirectory;
		OutputDirectory = outputDirectory;

		/* NOTE: it is the responisbility of the UI code to check the values passed are good.
		*/
	}

	public void setBaseUrl(string? baseurl) {
		BaseUrl = baseurl;
	}
}
