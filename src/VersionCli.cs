using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Skarp.Version.Cli.CsProj;
using Skarp.Version.Cli.Model;
using Skarp.Version.Cli.Vcs;
using Skarp.Version.Cli.Versioning;

namespace Skarp.Version.Cli
{
    public class VersionCli
    {
        private readonly IVcs _vcsTool;
        private readonly ProjectFileDetector _fileDetector;
        private readonly ProjectFileParser _fileParser;
        private readonly VcsParser _vcsParser;
        private readonly ProjectFileVersionPatcher _fileVersionPatcher;
        private readonly SemVerBumper _bumper;

        public VersionCli(
            IVcs vcsClient,
            ProjectFileDetector fileDetector,
            ProjectFileParser fileParser,
            VcsParser vcsParser,
            ProjectFileVersionPatcher fileVersionPatcher,
            SemVerBumper bumper
        )
        {
            _vcsTool = vcsClient;
            _fileDetector = fileDetector;
            _fileParser = fileParser;
            _vcsParser = vcsParser;
            _fileVersionPatcher = fileVersionPatcher;
            _bumper = bumper;
        }

        public VersionInfo Execute(VersionCliArgs args)
        {
            if (!args.DryRun && args.DoVcs && !_vcsTool.IsVcsToolPresent())
            {
                throw new OperationCanceledException(
                    $"Unable to find the vcs tool {_vcsTool.ToolName()} in your path");
            }

            if (!args.DryRun && args.DoVcs && !_vcsTool.IsRepositoryClean())
            {
                throw new OperationCanceledException(
                    "You currently have uncomitted changes in your repository, please commit these and try again");
            }

            string csProjXml = string.Empty;

            if (_fileParser is ProjectFileParserNETFramework)
            {
                _fileParser.Load(args.AssemblyInfoFilePath);
            }
            else
            {
                csProjXml = _fileDetector.FindAndLoadCsProj(args.CsProjFilePath);
                _fileParser.Load(csProjXml, ProjectFileProperty.Version, ProjectFileProperty.PackageVersion);
            }

            var semVer = _bumper.Bump(
                SemVer.FromString(_fileParser.PackageVersion),
                args.VersionBump,
                args.SpecificVersionToApply,
                args.BuildMeta,
                args.PreReleasePrefix
            );
            var versionString = semVer.ToSemVerVersionString();

            var theOutput = new VersionInfo
            {
                Product = new ProductOutputInfo
                {
                    Name = ProductInfo.Name,
                    Version = ProductInfo.Version
                },
                OldVersion = _fileParser.PackageVersion,
                NewVersion = versionString,
                ProjectFile = _fileDetector.ResolvedCsProjFile,
                VersionStrategy = args.VersionBump.ToString().ToLowerInvariant()
            };

            if (!args.DryRun) // if we are not in dry run mode, then we should go ahead
            {
                if (!string.IsNullOrEmpty(args.AssemblyInfoFilePath))
                {
                    bool assemblyInfoAggiornato = AggiornaAssemblyInfo(args.AssemblyInfoFilePath, theOutput.OldVersion, theOutput.NewVersion);

                    bool nuspecAggiornato = AggiornaNuspec(args.NuspecFilePath, theOutput.NewVersion);

                    if (args.DoVcs)
                    {
                        string commitFiles = string.Empty;
                        if (assemblyInfoAggiornato)
                            commitFiles += $" \"{args.AssemblyInfoFilePath.TrimStart('.','\\')}\"";
                        if (nuspecAggiornato)
                            commitFiles += $" \"{args.NuspecFilePath.TrimStart('.', '\\')}\"";

                        // Run git commands
                        _vcsTool.Commit(commitFiles, _vcsParser.Commit(theOutput, _fileParser, args.CommitMessage));

                        _vcsTool.Tag(_vcsParser.Tag(theOutput, _fileParser, args.VersionControlTag));
                    }
                }
                else
                {
                    _fileVersionPatcher.Load(csProjXml);

                    _fileVersionPatcher.PatchVersionField(
                        _fileParser.Version,
                        versionString
                    );

                    _fileVersionPatcher.Flush(
                        _fileDetector.ResolvedCsProjFile
                    );

                    if (args.DoVcs)
                    {
                        _fileParser.Load(csProjXml, ProjectFileProperty.Title);
                        // Run git commands
                        _vcsTool.Commit(_fileDetector.ResolvedCsProjFile, _vcsParser.Commit(theOutput, _fileParser, args.CommitMessage));
                        _vcsTool.Tag(_vcsParser.Tag(theOutput, _fileParser, args.VersionControlTag));
                    }
                }
            }

            if (args.OutputFormat == OutputFormat.Json)
            {
                WriteJsonToStdout(theOutput);
            }
            else if (args.OutputFormat == OutputFormat.Bare)
            {
                Console.WriteLine(versionString);
            }
            else
            {
                Console.WriteLine($"Bumped {_fileDetector.ResolvedCsProjFile} to version {versionString}");
            }

            return theOutput;
        }

        private bool AggiornaAssemblyInfo(string assemblyInfoFilePath, string oldVersion, string newVersion)
        {
            bool result = false;
            if (File.Exists(assemblyInfoFilePath))
            {
                try
                {
                    string[] assemblyInfoContent = File.ReadAllLines(assemblyInfoFilePath);

                    for (int i = 0; i < assemblyInfoContent.Length; i++)
                    {
                        if (assemblyInfoContent[i] == $"[assembly: AssemblyVersion(\"{oldVersion}.0\")]")
                            assemblyInfoContent[i] = $"[assembly: AssemblyVersion(\"{newVersion}.0\")]";

                        if (assemblyInfoContent[i] == $"[assembly: AssemblyFileVersion(\"{oldVersion}.0\")]")
                            assemblyInfoContent[i] = $"[assembly: AssemblyFileVersion(\"{newVersion}.0\")]";
                    }

                    result = true;
                    File.WriteAllLines(assemblyInfoFilePath, assemblyInfoContent);
                }
                catch { }
            }
            return result;
        }

        //Aggiorna il file .nuspec
        private static bool AggiornaNuspec(string nugetFilePath, string newVersion)
        {
            bool result = false;
            if (File.Exists(nugetFilePath))
            {
                try
                {
                    string[] nuspecContent = File.ReadAllLines(nugetFilePath);

                    Regex pattern = new Regex("<version>(?<oldValue>.*)</version>", RegexOptions.IgnoreCase);
                    Match match;

                    for (int i = 0; i < nuspecContent.Length; i++)
                    {
                        match = pattern.Match(nuspecContent[i]);
                        if (match.Length > 0)
                        {
                            var oldValue = match.Groups["oldValue"].Value;
                            nuspecContent[i] = nuspecContent[i].Replace($"<version>{oldValue}</version>", $"<version>{newVersion}</version>");
                            result = true;
                        }
                    }
                    File.WriteAllLines(nugetFilePath, nuspecContent);
                }
                catch
                {

                }
            }
            return result;
        }

        public void DumpVersion(VersionCliArgs args)
        {
            var csProjXml = _fileDetector.FindAndLoadCsProj(args.CsProjFilePath);
            _fileParser.Load(csProjXml, ProjectFileProperty.Version, ProjectFileProperty.PackageVersion);

            switch (args.OutputFormat)
            {
                case OutputFormat.Json:
                    var theOutput = new
                    {
                        Product = new
                        {
                            Name = ProductInfo.Name,
                            Version = ProductInfo.Version
                        },
                        CurrentVersion = _fileParser.PackageVersion,
                        ProjectFile = _fileDetector.ResolvedCsProjFile,
                    };
                    WriteJsonToStdout(theOutput);
                    break;
                case OutputFormat.Bare:
                    Console.WriteLine(_fileParser.PackageVersion);
                    break;
                case OutputFormat.Text:
                default:
                    Console.WriteLine("Project version is: {0}\t{1}", Environment.NewLine, _fileParser.PackageVersion);
                    break;
            }
        }

        private static void WriteJsonToStdout(object theOutput)
        {
            Console.WriteLine(
                JsonConvert.SerializeObject(
                    theOutput, new JsonSerializerSettings
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver()
                    }));
        }
    }
}