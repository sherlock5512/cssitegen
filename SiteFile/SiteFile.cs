using System.Text.Json;
using Serilog;
namespace csSiteGen;

/// <summary>
/// A <c>SiteFile</c> represents an individual file to be converted for the static site.
/// </summary>
public partial class SiteFile
{
	FileInfo info;
	Conversions.ConvertFunc ConverterFunction;
	static Dictionary<string, DateTime>? Metadata = null;

	/// <summary>
	/// The name of the file, Not Guaranteed to be unique.
	/// Use only for output and logging, never file operations,
	/// nor as a Dictionary key.
	/// </summary>
	public string Name => info.Name;

	/// <summary>
	/// The FullName (or Path) of the file, This is unique as each file can only be found once.
	/// Use for file operations and Dictionary keys, or anywhere else you need to avoid ambiguity.
	/// </summary>
	public string FullName => info.FullName;

	public SiteFile(FileInfo fileInfo)
	{
		info = fileInfo;

		Log.Information("{file} extension is {ext}",fileInfo.FullName, fileInfo.Extension);
		// Using this Ensures that the ConverterFunction is Always set.
		// ConverterFunctions ALWAYS accept just the FileInfo, and RuntimeSettings passed at convert time.
		ConverterFunction = Conversions.Mappings.GetValueOrDefault(info.Extension, Conversions.RawCpy);
	}

	/// <summary>
	///	Convert the file, placing it in the correct place in the output directory.
	/// If a filetype conversion is not needed, or specified, then the file is simply copied.
	/// </summary>
	public bool Convert(RuntimeSettings settings)
	{
		if (!NeedsUpdating(settings))
		{
			Log.Information("Ignoring {name}, as it has not been changed since last run.", info.FullName);
			return true;
		}
		bool res = ConverterFunction(info, settings);

		if (res)
		{
			Log.Information("Converted sucessfully, updating metadata");
			if (Metadata!.ContainsKey(info.FullName))
			{
			    Metadata[info.FullName] = info.LastWriteTimeUtc;
			}
			else
			{
			    Metadata.Add(info.FullName,info.LastWriteTimeUtc);
			}
			Log.Debug("Metadata Dictionary now {@Metadata}", Metadata);
			SaveMetadata(settings);
		}

		return res;
	}

	private bool NeedsUpdating(RuntimeSettings settings)
	{
		if (Metadata is null)
		{
			LoadMetadata(settings);
		}

		// NOTE: By this point Metadata CANNOT be null as LoadMetadata would either have loaded the JSON or instantiated blank MetaData
		// The compiler however is unaware of this as it uses side effects, so we tell it there is no possible null value here.
		Log.Debug("Attempting to check metadata for file {f}",info.FullName);
		Log.Debug("METADATA={MD}",Metadata!.GetValueOrDefault(info.FullName,DateTime.MinValue));
		Log.Debug("FILETIME={FT}",info.LastWriteTimeUtc);
		Log.Debug("RES={res}",(Metadata!.GetValueOrDefault(info.FullName, DateTime.MinValue) != info.LastWriteTimeUtc));
		return (Metadata!.GetValueOrDefault(info.FullName, DateTime.MinValue) != info.LastWriteTimeUtc);
	}


	/*
	 * NOTE: It may seem wrong to save the metadata in the output directory.
	 * But that ensures that if you remove the output directory the site will be
	 * Fully recreated.
	 */
	private void LoadMetadata(RuntimeSettings settings)
	{
		string metaFile = $"{settings.OutputDirectory}/.files";
		Log.Information("Loading Metadata from {file}",metaFile);
		try
		{
			string metaJson = File.ReadAllText(metaFile);
			Log.Debug("Read Json {metaJson}", metaJson);
			Metadata = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(metaJson);
			Log.Debug("Deserialized to {@Metadata}",Metadata);
		}
		catch (IOException e)
		{
			Log.Warning(e,"Error reading .files metafile");
			Metadata = new();
			return;
		}
		catch (JsonException e)
		{
			Log.Information(e, "Failed to deserialize .files Json data");
			Log.Information("The metadata file will be deleted as it is likely corrupted.");
			File.Delete(metaFile);
			Metadata = new();
		}
	}

	private void SaveMetadata(RuntimeSettings settings)
	{
		if (Metadata is null)
		{
			Log.Warning("Attempted to save null Metadata");
			return;
		}
		string metaFile = $"{settings.OutputDirectory}/.files";
		string metaJson;
		try
		{
			metaJson = JsonSerializer.Serialize<Dictionary<string, DateTime>>(Metadata);
			Log.Debug("metaJson: {metaJson}",metaJson);
		}
		catch (JsonException e)
		{
			Log.Warning(e, "Failed to serialize .files Json data");
			return;
		}
		Log.Information("Writing metadata");
		File.WriteAllText(metaFile, metaJson);
	}
}
