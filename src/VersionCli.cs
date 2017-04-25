using System;
using Skarpdev.DotnetVersion.Vcs;
using Skarpdev.DotnetVersion.CsProj;
using Skarpdev.DotnetVersion.CsProj.FileSystem;

namespace Skarpdev.DotnetVersion
{
    public class VersionCli
    {
        private readonly IVcs _vcsTool;
        private readonly ProjectFileDetector _fileDetector;
        private readonly ProjectFileParser _fileParser;
        private readonly ProjectFileVersionPatcher _fileVersionPatcher;

        public VersionCli(
            IVcs vcsClient,
            ProjectFileDetector fileDetector,
            ProjectFileParser fileParser,
            ProjectFileVersionPatcher fileVersionPatcher
        )
        {
            _vcsTool = vcsClient;
            _fileDetector = fileDetector;
            _fileParser = fileParser;
            _fileVersionPatcher = fileVersionPatcher;
        }

        public void Execute(VersionBump bump, string commitMessage = "", string csProjFilePath = "")
        {
            if (!_vcsTool.IsVcsToolPresent())
            {
                Console.WriteLine($"Unable to find the vcs tool {_vcsTool.ToolName()} in your path");
                Environment.Exit(1);
            }

            if (!_vcsTool.IsRepositoryClean())
            {
                Console.WriteLine($"You currently have uncomitted changes in your repository, please commit these and try again");
                Environment.Exit(1);
            }

            var csProjXml = _fileDetector.FindAndLoadCsProj(csProjFilePath);
            _fileParser.Load(csProjXml);

            var semVer = SemVer.FromString(_fileParser.Version);
            semVer.Bump(bump);
            var newVersion = semVer.ToVersionString();
            var patchedCsProjXml = _fileVersionPatcher.Patch(
                csProjXml,
                _fileParser.Version,
                newVersion
            );
            _fileVersionPatcher.Flush(
                patchedCsProjXml,
                _fileDetector.ResolvedCsProjFile
            );

            // Run git commands
            _vcsTool.Commit(_fileDetector.ResolvedCsProjFile, $"v{newVersion}");
            _vcsTool.Tag($"v{newVersion}");

            Console.WriteLine(
                "Bumped {0} to version {1}",
                _fileDetector.ResolvedCsProjFile,
                newVersion);
        }

        public void DumpVersion(string csProjFilePath = "")
        {
            var csProjXml = _fileDetector.FindAndLoadCsProj(csProjFilePath);
            _fileParser.Load(csProjXml);

            Console.WriteLine("Project version is: {0}\t{1}", Environment.NewLine, _fileParser.Version);
        }
    }
}