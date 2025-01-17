// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Microsoft.TestTemplates.Acceptance.Tests;

[TestClass]
public sealed partial class AcceptanceTests
{
    private static readonly ImmutableArray<string> SupportedTargetFrameworks =
    [
        "net8.0",
        "net9.0",
    ];
    private static readonly (string ProjectTemplateName, string ItemTemplateName, string[] Languages)[] AvailableItemTemplates =
    [
        ("nunit", "nunit-test", Languages.All),
    ];

    private static readonly (string ProjectTemplateName, string[] Languages, bool RunDotnetTest)[] AvailableProjectTemplates =
    [
        ("mstest", Languages.All, true),
        ("nunit", Languages.All, true),
        ("xunit", Languages.All, true),
        ("mstest-playwright", [Languages.CSharp], false),
        ("nunit-playwright", [Languages.CSharp], false),
    ];

    [AssemblyInitialize]
    public static void InstallTemplates(TestContext _)
    {
        foreach (var targetFramework in SupportedTargetFrameworks)
        {
            DotnetUtils.InvokeDotnetNewInstall(GetTestTemplatePath(targetFramework));
        }
    }

    [AssemblyCleanup]
    public static void UninstallTemplates()
    {
        foreach (var targetFramework in SupportedTargetFrameworks)
        {
            DotnetUtils.InvokeDotnetNewUninstall(GetTestTemplatePath(targetFramework));
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(GetTemplateItemsToTest), DynamicDataSourceType.Method)]
    public void ItemTemplate_CanBeInstalledAndTestArePassing(string targetFramework, string projectTemplate, string itemTemplate, string language)
    {
        string testProjectName = GenerateTestProjectName();
        string outputDirectory = Path.Combine(Constants.ArtifactsTempDirectory, testProjectName);

        // Create new test project: dotnet new <projectTemplate> -n <testProjectName> -f <targetFramework> -lang <language>
        DotnetUtils.InvokeDotnetNew(projectTemplate, testProjectName, targetFramework, language, outputDirectory);

        var itemName = "test";

        // Add test item to test project: dotnet new <itemTemplate> -n <test> -lang <language> -o <testProjectName>
        DotnetUtils.InvokeDotnetNew(itemTemplate, itemName, language: language, outputDirectory: outputDirectory);

        if (language == Languages.FSharp)
        {
            // f# projects don't include all files by default, so the file is created
            // but the project ignores it until you manually add it into the project
            // in the right order
            AddItemToFsproj(itemName, outputDirectory, testProjectName);
        }

        // Run tests: dotnet test <path>
        var result = DotnetUtils.InvokeDotnetTest(outputDirectory);

        // Verify the tests run as expected.
        result.ValidateSummaryStatus(2, 0, 0);

        Directory.Delete(outputDirectory, true);
    }

    [DataTestMethod]
    [DynamicData(nameof(GetTemplateProjectsToTest), DynamicDataSourceType.Method)]
    public void ProjectTemplate_CanBeInstalledAndTestsArePassing(string targetFramework, string projectTemplate, string language, bool runDotnetTest)
    {
        var testProjectName = GenerateTestProjectName();
        string outputDirectory = Path.Combine(Constants.ArtifactsTempDirectory, testProjectName);

        // Create new test project: dotnet new <projectTemplate> -n <testProjectName> -f <targetFramework> -lang <language> -o <outputDirectory>
        DotnetUtils.InvokeDotnetNew(projectTemplate, testProjectName, targetFramework, language, outputDirectory);

        if (runDotnetTest)
        {
            // Run tests: dotnet test <path>
            var result = DotnetUtils.InvokeDotnetTest(outputDirectory);

            // Verify the tests run as expected.
            result.ValidateSummaryStatus(1, 0, 0); 
        }

        Directory.Delete(outputDirectory, true);
    }

    private static IEnumerable<object[]> GetTemplateItemsToTest()
    {
        foreach (var targetFramework in SupportedTargetFrameworks)
        {
            foreach (var (projectTemplate, itemTemplate, languages) in AvailableItemTemplates)
            {
                foreach (var language in languages)
                {
                    yield return new string[] { targetFramework, projectTemplate, itemTemplate, language };
                }
            }
        }
    }

    private static IEnumerable<object[]> GetTemplateProjectsToTest()
    {
        foreach (var targetFramework in SupportedTargetFrameworks)
        {
            foreach (var (projectTemplate, languages, runDotnetTest) in AvailableProjectTemplates)
            {
                foreach (var language in languages)
                {
                    yield return new object[] { targetFramework, projectTemplate, language, runDotnetTest };
                }
            }
        }
    }

    private static string GenerateTestProjectName()
    {
        // Avoiding VB errors because root namespace must not start with number or contain dashes
        return "Test_" + Guid.NewGuid().ToString("N");
    }

    private void AddItemToFsproj(string itemName, string outputDirectory, string projectName)
    {
        var fsproj = Path.Combine(outputDirectory, $"{projectName}.fsproj");
        var lines = File.ReadAllLines(fsproj).ToList();

        lines.Insert(lines.IndexOf("  <ItemGroup>") + 1, $@"    <Compile Include=""{itemName}.fs""/>");
        File.WriteAllLines(fsproj, lines);
    }

    private static string GetTestTemplatePath(string targetFramework)
    {
        // Strip the "net" prefix from the target framework
        string version = targetFramework[3..];
        return Path.Combine("template_feed", "Microsoft.DotNet.Test.ProjectTemplates." + version, "content");
    }
}
