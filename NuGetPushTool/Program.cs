using AngleSharp.Xml.Parser;
using AngleSharp;
using System.IO;
using AngleSharp.Dom;
using System.Diagnostics;
using AngleSharp.Xml;

namespace NuGetPushTool
{
	internal class Program
	{
		static void Main(string[] args)
		{
			// Usage: NuGetPushTool 

			if (args.Length < 2)
			{
				Console.WriteLine("Usage: NuGetPushTool <build config> <project 1 directory> [<project 2 directory> [<project 3 directory> [etc.]]]");
				return;
			}
			else if (args[0] != "Release")
			{
				Console.WriteLine($"NuGetPushTool will only run on a 'Release' build. This is a '{args[0]}' build, so NuGetPushTool will be skipped.");
				return;
			}
			else
			{
				foreach (var arg in args.Skip(1))
					NuGetPush(arg);
			}
		}

		static void NuGetPush(in string ProjectDirectory)
		{
			foreach(var F in Directory.GetFiles(ProjectDirectory))
			{
				if(string.Compare(Path.GetExtension(F),".csproj",true)==0)
				{
					Console.WriteLine($"NuGetPushTool running for {F}...");
					NuGetPush2(ProjectDirectory, Path.Combine(ProjectDirectory, F));
					return;
				}
			}
			Console.WriteLine($"NuGetPushTool couldn't find a project file in {ProjectDirectory} :(");
		}
		static void NuGetPush2(in string ProjectDirectory, in string Csproj)
		{
			IConfiguration configXML = Configuration.Default.WithXml();
			IBrowsingContext contextXML = BrowsingContext.New(configXML);
			var MyXmlParser = contextXML.GetService<IXmlParser>();

			var XDoc = MyXmlParser.ParseDocument(File.ReadAllText(Csproj));

			IElement GlobElVersion = null, GlobPropGroup=null;

			// Things to configure
			string Version = "1.0.0"; // default value
			string Project = Path.GetFileNameWithoutExtension(Csproj);
			string OutputDir = "bin\\release";

			foreach(var ElPropertyGroup in XDoc.Descendents<IElement>().Where(M => M.TagName== "PropertyGroup"))
			{
				GlobPropGroup = ElPropertyGroup;

				if(ElPropertyGroup.GetElementsByTagName("Version")?.FirstOrDefault() is IElement ElVersion)
				{
					Version = ElVersion.InnerHtml;
					GlobElVersion = ElVersion;
				}
				if(ElPropertyGroup.GetElementsByTagName("Title")?.FirstOrDefault() is IElement ElTitle)
				{
					Project = ElTitle.InnerHtml;
				}
			}

			// Enviro variable
			string ApiKey = Environment.GetEnvironmentVariable("GITHUB_NUGET_KEY");
			if(string.IsNullOrEmpty(ApiKey))
			{
				Console.WriteLine("No API key found. API key should be set in the system environmental variable GITHUB_NUGET_KEY");
				return;
			}

			// Check if local NuGet package exists
			string NuGetFile = $"{Path.Combine(ProjectDirectory, OutputDir, Project)}.{Version}.nupkg";
			if(!File.Exists(NuGetFile))
			{
				Console.WriteLine($"NuGet file '{NuGetFile}' doesn't exist - skipping.");
				Console.WriteLine("| If this is surprising, then make sure that NuGetPushTool is running AFTER build & packaging.");
				Console.WriteLine("| For example, don't NuGetPushTool as a post-build event. Call it as a CopyPackage event:");
				Console.WriteLine("|    <Target Name=\"CopyPackage\" AfterTargets=\"Pack\">\r\n|       <Exec Command=\"$(SolutionDir)\\NuGetPushTool\\bin\\Release\\net6.0\\NuGetPushTool.exe $(Configuration) $(ProjectDir)\" />\r\n|    </Target>");
				return;
			}

			// Execute
			Process.Start("dotnet", $"nuget push \"{NuGetFile}\" --source \"github\" -k {ApiKey}");

			// Update the version number
			// From my own experiments, it seems possible to overwrite the csproj file while it's open in VS
			var V = Version.Split('.');
			var Inc = int.Parse(V.Last());
			++Inc;
			V[V.Length - 1] = Inc.ToString();
			if(!(GlobPropGroup is object))
			{
				GlobPropGroup = XDoc.CreateElement("PropertyGroup");
				XDoc.FirstChild.AppendChild(GlobPropGroup);
			}
			if(!(GlobElVersion is object))
			{
				GlobElVersion = XDoc.CreateElement("Version");
				GlobPropGroup.AppendChild(GlobElVersion);
			}
			GlobElVersion.TextContent = string.Join('.', V);

			File.WriteAllText(Csproj, XDoc.ToXml());
		}
	}
}