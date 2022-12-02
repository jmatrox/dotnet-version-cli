using System.IO;
using System.Text.RegularExpressions;

namespace Skarp.Version.Cli.CsProj
{
    public class ProjectFileParserNETFramework : ProjectFileParser
    {
        public override void Load(string xmlDocument, params ProjectFileProperty[] properties)
        {
            string[] fileContent = File.ReadAllLines(xmlDocument);

            string version = string.Empty;
            string fileVersion = string.Empty;

            Regex patternVersion = new("^\\[assembly: AssemblyVersion\\(\"(?<AssemblyVersion>.*)\"\\)]$", RegexOptions.IgnoreCase);
            Regex patternFileVersion = new("^\\[assembly: AssemblyFileVersion\\(\"(?<AssemblyFileVersion>.*)\"\\)]$", RegexOptions.IgnoreCase);
            Regex patternTitle = new("^\\[assembly: AssemblyTitle\\(\"(?<AssemblyTitle>.*)\"\\)]$", RegexOptions.IgnoreCase);

            for (int i = 0; i < fileContent.Length; i++)
            {
                Match matchVersion = patternVersion.Match(fileContent[i]);
                Match matchFileVersion = patternFileVersion.Match(fileContent[i]);
                Match matchTitle = patternTitle.Match(fileContent[i]);
                if (matchVersion.Length > 0)
                {
                    version = matchVersion.Groups["AssemblyVersion"].Value;
                    var splitted = version.Split('.');
                    if (splitted.Length > 2)
                    {
                        version = $"{splitted[0]}.{splitted[1]}.{splitted[2]}";
                    }
                }

                if (matchFileVersion.Length > 0)
                {
                    fileVersion = matchFileVersion.Groups["AssemblyFileVersion"].Value;
                    var splitted = fileVersion.Split('.');
                    if (splitted.Length > 2)
                    {
                        fileVersion = $"{splitted[0]}.{splitted[1]}.{splitted[2]}";
                    }
                }

                if (matchTitle.Length > 0)
                {
                    PackageName = matchTitle.Groups["AssemblyTitle"].Value;
                }
            }

            if (!string.IsNullOrEmpty(version))
            {
                Version = version;
                PackageVersion = version;
            }
            else
            {
                Version = fileVersion;
                PackageVersion = fileVersion;
            }
        }

    }
}