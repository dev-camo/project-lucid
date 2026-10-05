namespace Hardlight
{
	public interface IGradleFileConfig
	{
		string PathToGradleTemplateFile { get; }

		string PathToGradleFile { get; }

		string DefaultGradlePath => "/NativeAndroid~/build.gradle";

		string DefaultGradleTemplatePath => "/NativeAndroid~/build.2020gradle";
	}
}
