using System.Collections.Generic;

namespace Hardlight
{
	public interface IModuleConfigurationWithFileList
	{
		IEnumerable<string> GetIncludedNativeFiles();

		string GetNativeCodePath();
	}
}
