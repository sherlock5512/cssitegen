using System.Text.Json.Serialization;
// project settings is a user accessible config to set the Source and destination of a site
// This may include more scope in the future such as holding the site base address etc..

class ProjectSettings
{
    private string _Source;
	private string _Destination;
	private DirectoryInfo? _ProjectRoot;
	public string? BaseUrl {get; private set;}

	public string Source {get {
		if (_ProjectRoot is null)
		{
		    return _Source;
		}
		return Path.Combine(_ProjectRoot.FullName,_Source);
	}}
	public string Destination {get {
		if (_ProjectRoot is null)
		{
		    return _Destination;
		}
		return Path.Combine(_ProjectRoot.FullName,_Destination);
	}}

	[JsonConstructor]
	public ProjectSettings(String source, String destination, string baseUrl) {
		_Source = source;
		_Destination = destination;
		BaseUrl = baseUrl;
	}

	public void setProjectRoot(string projectRoot) {
		_ProjectRoot = new(projectRoot);
	}
	public void setProjectRoot(DirectoryInfo projectRoot) {
		_ProjectRoot = projectRoot;
	}

	public void setBaseUrl(string baseUrl) {
		BaseUrl = baseUrl;
	}
}
