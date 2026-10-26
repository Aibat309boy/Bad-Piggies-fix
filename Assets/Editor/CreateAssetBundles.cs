using UnityEditor;
using System.IO;

public class CreateAssetBundles
{
	[MenuItem("Assets/Build AssetBundles")]
	static void BuildAllAssetBundles()
	{
		string outputDirectory = "Assets/AssetBundles";
		BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;
		BuildAssetBundleOptions flags = BuildAssetBundleOptions.None;
		if (buildTarget == BuildTarget.WebGL) flags |= BuildAssetBundleOptions.ChunkBasedCompression;

		if (!Directory.Exists(outputDirectory)) Directory.CreateDirectory(outputDirectory);

		BuildPipeline.BuildAssetBundles(outputDirectory, flags, buildTarget);

		// HACK: Rename asset bundles to have the correct casing (temporary solution for until I know why the filenames are lowercase)
		foreach (string filepath in Directory.EnumerateFiles(outputDirectory))
		{

			string extension = Path.GetExtension(filepath);
			if (extension != ".unity3d") continue;
			string name = Path.GetFileNameWithoutExtension(filepath);
			string[] parts = name.Split('_');
			for (int i = 0; i < parts.Length; i++) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1).ToLowerInvariant();
			string fixedName = string.Join("_", parts);
			if (name != fixedName)
			{
				string dirpath = Path.GetDirectoryName(filepath);
				File.Move(filepath, dirpath + "/" + fixedName + extension);
			}
		}
	}
}