using System.Text.Json.Serialization;
// project settings is a user accessible config to set the Source and destination of a site
// This may include more scope in the future such as holding the site base address etc..

public class ProjectSettings
{
	// The Source and Destination need to be public for the json constructor to work properly.
	private DirectoryInfo? _ProjectRoot;
    public string Source {get; private set;}
	public string Destination {get; private set;}
	public string? BaseUrl {get; private set;}
	public string? SiteName {get; private set;}

	public DirectoryInfo InputDirectory {get {
		if (_ProjectRoot is null)
		{
		    return new(Source);
		}
		return new(Path.Combine(_ProjectRoot.FullName,Source));
	}}
	public DirectoryInfo OutputDirectory {get {
		if (_ProjectRoot is null)
		{
		    return new(Destination);
		}
		return new(Path.Combine(_ProjectRoot.FullName,Destination));
	}}

	[JsonConstructor]
	public ProjectSettings(string source, string destination, string baseUrl, string siteName) {
		Source = source;
		Destination = destination;
		BaseUrl = baseUrl;
		SiteName = siteName;
	}

	public void setProjectRoot(string projectRoot) {
		_ProjectRoot = new(projectRoot);
	}
	public void setProjectRoot(DirectoryInfo projectRoot) {
		_ProjectRoot = projectRoot;
	}

	/*
	 * public void setBaseUrl(string baseUrl) {
	 *     BaseUrl = baseUrl;
	 * }
	 */
}
