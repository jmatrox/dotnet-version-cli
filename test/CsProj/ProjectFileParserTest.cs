using System;
using Skarp.Version.Cli.CsProj;
using Xunit;

namespace Skarp.Version.Cli.Test.CsProj
{
    public class ProjectFileParserTest
    {
        private readonly ProjectFileParser parser;

        public ProjectFileParserTest()
        {
            parser = new ProjectFileParser();
            
        }
        
        [Fact]
        public void CanParseWellFormedProjectFilesWithVersionTag()
        {
            const string csProjXml = "<Project Sdk=\"Microsoft.NET.Sdk\">"+
                                     "<PropertyGroup>" + 
                                     "<TargetFramework>netstandard1.6</TargetFramework>" +
                                     "<RootNamespace>Unit.For.The.Win</RootNamespace>" +
                                     "<PackageId>Unit.Testing.Library</PackageId>" +
                                     "<Version>1.0.0</Version>" +
                                     "<PackageVersion>1.0.0-1+master</PackageVersion>" +
                                     "</PropertyGroup>" +
                                     "</Project>";

            parser.Load(csProjXml, ProjectFileProperty.Version, ProjectFileProperty.PackageVersion);
            Assert.Equal("1.0.0", parser.Version);
            Assert.Equal("1.0.0-1+master", parser.PackageVersion);
        } 
        [Fact]
        public void CanParse_when_version_and_package_version_missing()
        {
            const string csProjXml = "<Project Sdk=\"Microsoft.NET.Sdk\">"+
                                     "<PropertyGroup>" + 
                                     "<TargetFramework>netstandard1.6</TargetFramework>" +
                                     "<RootNamespace>Unit.For.The.Win</RootNamespace>" +
                                     "<PackageId>Unit.Testing.Library</PackageId>" +
                                     "</PropertyGroup>" +
                                     "</Project>";

            parser.Load(csProjXml, ProjectFileProperty.Version, ProjectFileProperty.PackageVersion);
            Assert.Equal("0.0.0", parser.Version);
            Assert.Equal("0.0.0", parser.PackageVersion);
        }
        
         [Fact]
        public void BailsOnMalformedProjectFile()
        {
            const string csProjXml = "<Projectttttt Sdk=\"Microsoft.NET.Sdk\">"+
                                     "<PropertyGroup>" + 
                                     "<TargetFramework>netstandard1.6</TargetFramework>" +
                                     "<RootNamespace>Unit.For.The.Win</RootNamespace>" +
                                     "<PackageId>Unit.Testing.Library</PackageId>" +
                                     "</PropertyGroup>" +
                                     "</Projectttttt>";

            var ex = Assert.Throws<ArgumentException>(() => 
                parser.Load(csProjXml)
            );
            
            Assert.Contains($"The provided csproj file seems malformed - no <Project> in the root", ex.Message);
            Assert.Equal("xmlDocument", ex.ParamName);
        }

        [Fact]
        public void Works_when_no_packageId_or_title()
        {
            const string csProjXml = "<Project Sdk=\"Microsoft.NET.Sdk\">"+
                                     "<PropertyGroup>" + 
                                     "<TargetFramework>netstandard1.6</TargetFramework>" +
                                     "<RootNamespace>Unit.For.The.Win</RootNamespace>" +
                                     "</PropertyGroup>" +
                                     "</Project>";
            
            parser.Load(csProjXml);
            Assert.Empty(parser.PackageName);
        }
    }
}
