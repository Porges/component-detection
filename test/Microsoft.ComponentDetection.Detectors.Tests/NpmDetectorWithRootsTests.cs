namespace Microsoft.ComponentDetection.Detectors.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ComponentDetection.Contracts;
using Microsoft.ComponentDetection.Contracts.TypedComponent;
using Microsoft.ComponentDetection.Detectors.Npm;
using Microsoft.ComponentDetection.Detectors.Tests.Utilities;
using Microsoft.ComponentDetection.TestsUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using static Microsoft.ComponentDetection.Detectors.Tests.Utilities.TestUtilityExtensions;

[TestClass]
[TestCategory("Governance/All")]
[TestCategory("Governance/ComponentDetection")]
public class NpmDetectorWithRootsTests : BaseDetectorTest<NpmComponentDetectorWithRoots>
{
    private readonly string packageLockJsonFileName = "package-lock.json";
    private readonly string packageJsonFileName = "package.json";
    private readonly List<string> packageJsonSearchPattern = new() { "package.json" };
    private readonly List<string> packageLockJsonSearchPatterns = new() { "package-lock.json", "npm-shrinkwrap.json", "lerna.json" };
    private readonly Mock<IPathUtilityService> mockPathUtilityService;
    private readonly Mock<IEnvironmentVariableService> mockEnvService;

    public NpmDetectorWithRootsTests()
    {
        this.mockPathUtilityService = new Mock<IPathUtilityService>();
        this.mockEnvService = new Mock<IEnvironmentVariableService>();
        this.DetectorTestUtility.AddServiceMock(this.mockPathUtilityService);
        this.DetectorTestUtility.AddServiceMock(this.mockEnvService);
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockReturnsValidAsync()
    {
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();

        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2(this.packageLockJsonFileName, componentName0, version0);
        var (packageJsonName, packageJsonContents, packageJsonPath) = NpmTestUtilities.GetPackageJsonOneRoot(componentName0, version0);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns, fileLocation: packageLockPath)
            .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        Assert.AreEqual(4, detectedComponents.Count());
        foreach (var component in detectedComponents)
        {
            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                component.Component.Id,
                parentComponent0 => parentComponent0.Name == componentName0 && parentComponent0.Version == version0);
            Assert.IsFalse(string.IsNullOrWhiteSpace(((NpmComponent)component.Component).Hash));
        }
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockVersion3ReturnsValidAsync()
    {
        this.mockEnvService
            .Setup(x =>
                x.GetEnvironmentVariable(NpmComponentUtilities.LockFile3EnvFlag))
            .Returns("true");

        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();

        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock3(this.packageLockJsonFileName, componentName0, version0);
        var (packageJsonName, packageJsonContents, packageJsonPath) = NpmTestUtilities.GetPackageJsonOneRoot(componentName0, version0);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns, fileLocation: packageLockPath)
            .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        var detectedComponents = componentRecorder.GetDetectedComponents();
        Assert.AreEqual(4, detectedComponents.Count());
        foreach (var component in detectedComponents)
        {
            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                component.Component.Id,
                parentComponent0 => parentComponent0.Name == componentName0 && parentComponent0.Version == version0);
            Assert.IsFalse(string.IsNullOrWhiteSpace(((NpmComponent)component.Component).Hash));
        }
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockVersion3NestedReturnsValidAsync()
    {
        this.mockEnvService
            .Setup(x =>
                x.GetEnvironmentVariable(NpmComponentUtilities.LockFile3EnvFlag))
            .Returns("true");

        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var componentName1 = Guid.NewGuid().ToString("N");
        var version1 = NewRandomVersion();
        var componentName2 = Guid.NewGuid().ToString("N");

        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedNestedPackageLock3(this.packageLockJsonFileName, componentName0, version0, componentName1, version1, componentName2);

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName1, version1);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns, fileLocation: packageLockPath)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

        var detectedComponents = componentRecorder.GetDetectedComponents().ToList();
        Assert.AreEqual(4, detectedComponents.Count);

        var component0 = detectedComponents.First(x => x.Component.Id.Contains(componentName0));
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            component0.Component.Id,
            parentComponent0 => parentComponent0.Name == componentName0);

        var component1 = detectedComponents.First(x => x.Component.Id.Contains(componentName1));
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            component1.Component.Id,
            parentComponent0 => parentComponent0.Name == componentName1);

        var duplicate = detectedComponents.Where(x => x.Component.Id.Contains(componentName2)).ToList();
        duplicate.Should().HaveCount(2);

        foreach (var component in detectedComponents)
        {
            // check that either component0 or component1 is our parent
            componentRecorder.IsDependencyOfExplicitlyReferencedComponents<NpmComponent>(
                component.Component.Id,
                parentComponent0 => parentComponent0.Name == componentName0 || parentComponent0.Name == componentName1);
            Assert.IsFalse(string.IsNullOrWhiteSpace(((NpmComponent)component.Component).Hash));
        }
    }

    [TestMethod]
    public async Task TestNpmDetector_MismatchedFilesReturnsEmptyAsync()
    {
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();

        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2(this.packageLockJsonFileName);
        var (packageJsonName, packageJsonContents, packageJsonPath) = NpmTestUtilities.GetPackageJsonOneRoot(componentName0, version0);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns, fileLocation: packageLockPath)
            .WithFile(packageJsonName, packageJsonContents, this.packageJsonSearchPattern, fileLocation: packageJsonPath)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        Assert.AreEqual(0, componentRecorder.GetDetectedComponents().Count());
    }

    [TestMethod]
    public async Task TestNpmDetector_MissingPackageJsonReturnsEmptyAsync()
    {
        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2(this.packageLockJsonFileName);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns, fileLocation: packageLockPath)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        Assert.AreEqual(0, componentRecorder.GetDetectedComponents().Count());
    }

    [TestMethod]
    public async Task TestNpmDetector_PackageLockMultiRootAsync()
    {
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var componentName1 = Guid.NewGuid().ToString("N");
        var componentName2 = Guid.NewGuid().ToString("N");
        var version2 = NewRandomVersion();
        var componentName3 = Guid.NewGuid().ToString("N");

        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2(this.packageLockJsonFileName, componentName0, version0, componentName2, version2, packageName1: componentName1, packageName3: componentName3);

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName2, version2);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        Assert.AreEqual(4, detectedComponents.Count());

        var component0 = detectedComponents.FirstOrDefault(x => x.Component.Id.Contains(componentName0));

        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            component0.Component.Id,
            parentComponent0 => parentComponent0.Name == componentName0);

        var component1 = detectedComponents.FirstOrDefault(x => x.Component.Id.Contains(componentName1));
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            component1.Component.Id,
            parentComponent0 => parentComponent0.Name == componentName0);

        var component2 = detectedComponents.FirstOrDefault(x => x.Component.Id.Contains(componentName2));

        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            component2.Component.Id,
            parentComponent0 => parentComponent0.Name == componentName0,
            parentComponent2 => parentComponent2.Name == componentName2);

        var component3 = detectedComponents.FirstOrDefault(x => x.Component.Id.Contains(componentName3));
        componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
            component3.Component.Id,
            parentComponent0 => parentComponent0.Name == componentName0,
            parentComponent2 => parentComponent2.Name == componentName2);
    }

    [TestMethod]
    public async Task TestNpmDetector_VerifyMultiRoot_DependencyGraphAsync()
    {
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var componentName2 = Guid.NewGuid().ToString("N");
        var version2 = NewRandomVersion();

        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2(this.packageLockJsonFileName, componentName0, version0, componentName2, version2);

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName2, version2);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns, fileLocation: packageLockPath)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        var graphsByLocation = componentRecorder.GetDependencyGraphsByLocation();

        var graph = graphsByLocation[packageLockPath];

        var npmComponent0Id = new NpmComponent(componentName0, version0).Id;
        var npmComponent2Id = new NpmComponent(componentName2, version2).Id;

        var dependenciesFor0 = graph.GetDependenciesForComponent(npmComponent0Id);
        Assert.AreEqual(dependenciesFor0.Count(), 2);
        var dependenciesFor2 = graph.GetDependenciesForComponent(npmComponent2Id);
        Assert.AreEqual(dependenciesFor2.Count(), 1);

        Assert.IsTrue(dependenciesFor0.Contains(npmComponent2Id));
    }

    [TestMethod]
    public async Task TestNpmDetector_EmptyVersionSkippedAsync()
    {
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var componentName2 = Guid.NewGuid().ToString("N");
        var version2 = NewRandomVersion();

        var packageLockJson = @"{{
                ""name"": ""test"",
                ""version"": """",
                ""dependencies"": {{
                    ""{0}"": {{
                        ""version"": ""{1}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k="",
                        ""requires"": {{
                                ""{2}"": ""{3}""
                        }}
                    }},
                    ""{4}"": {{
                        ""version"": ""{5}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg="",
                        ""requires"": {{
                                ""{6}"": ""{7}""
                        }}
                    }}
                }}
            }}";

        var packageLockTemplate = string.Format(packageLockJson, componentName0, version0, componentName2, version2, componentName2, version2, componentName0, version0);

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName2, version2);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockTemplate, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        Assert.AreEqual(0, componentRecorder.GetDetectedComponents().Count());
    }

    [TestMethod]
    public async Task TestNpmDetector_InvalidNameSkippedAsync()
    {
        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var componentName2 = Guid.NewGuid().ToString("N");
        var version2 = NewRandomVersion();

        var packageLockJson = @"{{
                ""name"": """",
                ""version"": ""1.0.0"",
                ""dependencies"": {{
                    ""{0}"": {{
                        ""version"": ""{1}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k="",
                        ""requires"": {{
                                ""{2}"": ""{3}""
                        }}
                    }},
                    ""{4}"": {{
                        ""version"": ""{5}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg="",
                        ""requires"": {{
                                ""{6}"": ""{7}""
                        }}
                    }}
                }}
            }}";

        var packageLockTemplate = string.Format(packageLockJson, componentName0, version0, componentName2, version2, componentName2, version2, componentName0, version0);

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName2, version2);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockTemplate, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        Assert.AreEqual(0, componentRecorder.GetDetectedComponents().Count());
    }

    [TestMethod]
    public async Task TestNpmDetector_LernaDirectoryAsync()
    {
        var lockFileLocation = Path.Combine(Path.GetTempPath(), Path.Combine("belowLerna", this.packageLockJsonFileName));
        var packageJsonFileLocation = Path.Combine(Path.GetTempPath(), Path.Combine("belowLerna", this.packageJsonFileName));
        var lernaFileLocation = Path.Combine(Path.GetTempPath(), "lerna.json");

        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var componentName1 = Guid.NewGuid().ToString("N");
        var version1 = NewRandomVersion();
        var componentName2 = Guid.NewGuid().ToString("N");
        var version2 = NewRandomVersion();

        var packageLockJson = @"{{
                ""name"": """",
                ""version"": """",
                ""dependencies"": {{
                    ""{0}"": {{
                        ""version"": ""{1}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k="",
                        ""requires"": {{
                                ""{2}"": ""{3}""
                        }}
                    }},
                    ""{4}"": {{
                        ""version"": ""{5}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg="",
                        ""requires"": {{
                                ""{6}"": ""{7}""
                        }}
                    }}
                }}
            }}";

        var packageLockTemplate = string.Format(packageLockJson, componentName0, version0, componentName2, version2, componentName2, version2, componentName0, version0);

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}"",
                    ""{4}"": ""{5}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName1, version1, componentName2, version2);

        this.mockPathUtilityService.Setup(x => x.IsFileBelowAnother(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile("lerna.json", "unused string", this.packageLockJsonSearchPatterns, fileLocation: lernaFileLocation)
            .WithFile(this.packageLockJsonFileName, packageLockTemplate, this.packageLockJsonSearchPatterns, fileLocation: lockFileLocation)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern, fileLocation: packageJsonFileLocation)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);
        Assert.AreEqual(2, componentRecorder.GetDetectedComponents().Count());
    }

    [TestMethod]
    public async Task TestNpmDetector_CircularRequirementsResolveAsync()
    {
        var packageJsonComponentPath = Path.Combine(Path.GetTempPath(), this.packageLockJsonFileName);

        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var componentName2 = Guid.NewGuid().ToString("N");
        var version2 = NewRandomVersion();

        var packageLockJson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": {{
                        ""version"": ""{1}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k="",
                        ""requires"": {{
                                ""{2}"": ""{3}""
                        }}
                    }},
                    ""{4}"": {{
                        ""version"": ""{5}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg="",
                        ""requires"": {{
                                ""{6}"": ""{7}""
                        }}
                    }}
                }}
            }}";

        var packageLockTemplate = string.Format(packageLockJson, componentName0, version0, componentName2, version2, componentName2, version2, componentName0, version0);

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0, componentName2, version2);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockTemplate, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        Assert.AreEqual(2, detectedComponents.Count());

        foreach (var component in detectedComponents)
        {
            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                component.Component.Id,
                parentComponent0 => parentComponent0.Name == componentName0,
                parentComponent2 => parentComponent2.Name == componentName2);
        }
    }

    [TestMethod]
    public async Task TestNpmDetector_ShrinkwrapLockReturnsValidAsync()
    {
        var lockFileName = "npm-shrinkwrap.json";
        var packageJsonComponentPath = Path.Combine(Path.GetTempPath(), this.packageJsonFileName);

        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();

        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2(lockFileName, componentName0, version0);
        var (packageJsonName, packageJsonContents, packageJsonPath) = NpmTestUtilities.GetPackageJsonOneRoot(componentName0, version0);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns, fileLocation: packageLockPath)
            .WithFile(this.packageJsonFileName, packageJsonContents, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        Assert.AreEqual(4, detectedComponents.Count());
        foreach (var component in detectedComponents)
        {
            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                component.Component.Id,
                parentComponent0 => parentComponent0.Name == componentName0 && parentComponent0.Version == version0);
        }
    }

    [TestMethod]
    public async Task TestNpmDetector_IgnoresPackageLocksInSubFoldersAsync()
    {
        var pathRoot = Path.GetTempPath();

        var packageLockUnderNodeModules = Path.Combine(pathRoot, Path.Combine("node_modules", this.packageLockJsonFileName));
        var packageJsonUnderNodeModules = Path.Combine(pathRoot, Path.Combine("node_modules", this.packageJsonFileName));

        var componentName0 = Guid.NewGuid().ToString("N");
        var version0 = NewRandomVersion();
        var componentName2 = Guid.NewGuid().ToString("N");
        var version2 = NewRandomVersion();

        var (packageLockName, packageLockContents, packageLockPath) = NpmTestUtilities.GetWellFormedPackageLock2(this.packageLockJsonFileName, componentName0, version0);
        var (packageLockName2, packageLockContents2, packageLockPath2) = NpmTestUtilities.GetWellFormedPackageLock2(this.packageLockJsonFileName, componentName2, version2, packageName0: "test2");

        var packagejson = @"{{
                ""name"": ""{2}"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentName0, version0, "test");

        var packageJsonTemplate2 = string.Format(packagejson, componentName2, version2, "test2");

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            /* Top level */
            .WithFile(packageLockName, packageLockContents, this.packageLockJsonSearchPatterns, fileLocation: packageLockPath)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            /* Under node_modules */
            .WithFile(packageLockName2, packageLockContents2, this.packageLockJsonSearchPatterns, fileLocation: packageLockUnderNodeModules)
            .WithFile(this.packageJsonFileName, packageJsonTemplate2, this.packageJsonSearchPattern, fileLocation: packageJsonUnderNodeModules)
            .ExecuteDetectorAsync();

        Assert.AreEqual(ProcessingResultCode.Success, scanResult.ResultCode);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        Assert.AreEqual(4, detectedComponents.Count());
        foreach (var component in detectedComponents)
        {
            componentRecorder.AssertAllExplicitlyReferencedComponents<NpmComponent>(
                component.Component.Id,
                parentComponent0 => parentComponent0.Name == componentName0);
        }
    }

    [TestMethod]
    public async Task TestNpmDetector_DependencyGraphIsCreatedAsync()
    {
        var packageJsonComponentPath = Path.Combine(Path.GetTempPath(), this.packageLockJsonFileName);

        var componentA = (Name: "componentA", Version: "1.0.0");
        var componentB = (Name: "componentB", Version: "1.0.0");
        var componentC = (Name: "componentC", Version: "1.0.0");

        var packageLockJson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": {{
                        ""version"": ""{1}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k="",
                        ""requires"": {{
                                ""{2}"": ""{3}""
                        }}
                    }},
                    ""{4}"": {{
                        ""version"": ""{5}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg="",
                        ""dependencies"": {{
                            ""{6}"": {{
                                ""version"": ""{7}"",
                                ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                                ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg=""
                            }}
                        }}
                    }}
                }}
            }}";

        var packageLockTemplate = string.Format(
            packageLockJson,
            componentA.Name,
            componentA.Version,
            componentB.Name,
            componentB.Version,
            componentB.Name,
            componentB.Version,
            componentC.Name,
            componentC.Version);

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                    ""{2}"": ""{3}""
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentA.Name, componentA.Version, componentB.Name, componentB.Version);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockTemplate, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(3);

        var componentAId = detectedComponents.First(c => ((NpmComponent)c.Component).Name.Equals(componentA.Name)).Component.Id;
        var componentBId = detectedComponents.First(c => ((NpmComponent)c.Component).Name.Equals(componentB.Name)).Component.Id;
        var componentCId = detectedComponents.First(c => ((NpmComponent)c.Component).Name.Equals(componentC.Name)).Component.Id;

        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();

        dependencyGraph.GetDependenciesForComponent(componentAId).Should().HaveCount(1);
        dependencyGraph.GetDependenciesForComponent(componentAId).Should().Contain(componentBId);
        dependencyGraph.GetDependenciesForComponent(componentBId).Should().HaveCount(1);
        dependencyGraph.GetDependenciesForComponent(componentBId).Should().Contain(componentCId);
        dependencyGraph.GetDependenciesForComponent(componentCId).Should().HaveCount(0);
    }

    [TestMethod]
    public async Task TestNpmDetector_NestedNodeModulesV3Async()
    {
        this.mockEnvService
            .Setup(x =>
                x.GetEnvironmentVariable(NpmComponentUtilities.LockFile3EnvFlag))
            .Returns("true");

        var componentA = (Name: "componentA", Version: "1.0.0");
        var componentB = (Name: "componentB", Version: "1.0.0");

        var packageLockJson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""lockfileVersion"": 3,
                ""requires"": true,
                ""packages"": {{
                    """": {{
                        ""name"": ""test"",
                        ""version"": ""0.0.0"",
                        ""dependencies"": {{
                            ""{0}"": ""{1}""
                        }}
                    }},
                    ""node_modules/{0}"": {{
                        ""version"": ""{1}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-EBPRBRBH3TIP4k5JTVxm7K9hR9k="",
                        ""dependencies"": {{
                                ""{2}"": ""{3}""
                        }}
                    }},
                    ""node_modules/{0}/node_modules/{2}"": {{
                        ""version"": ""{3}"",
                        ""resolved"": ""https://mseng.pkgs.visualstudio.com/_packaging/VsoMicrosoftExternals/npm/registry/"",
                        ""integrity"": ""sha1-PRT306DRK/NZUaVL07iuqH7nWPg=""
                    }}
                }}
            }}";

        var packageLockTemplate = string.Format(packageLockJson, componentA.Name, componentA.Version, componentB.Name, componentB.Version);

        var packagejson = @"{{
                ""name"": ""test"",
                ""version"": ""0.0.0"",
                ""dependencies"": {{
                    ""{0}"": ""{1}"",
                }}
            }}";

        var packageJsonTemplate = string.Format(packagejson, componentA.Name, componentA.Version);

        var (scanResult, componentRecorder) = await this.DetectorTestUtility
            .WithFile(this.packageLockJsonFileName, packageLockTemplate, this.packageLockJsonSearchPatterns)
            .WithFile(this.packageJsonFileName, packageJsonTemplate, this.packageJsonSearchPattern)
            .ExecuteDetectorAsync();

        scanResult.ResultCode.Should().Be(ProcessingResultCode.Success);

        var detectedComponents = componentRecorder.GetDetectedComponents();
        detectedComponents.Should().HaveCount(2);

        var componentAId = detectedComponents.First(c => ((NpmComponent)c.Component).Name.Equals(componentA.Name)).Component.Id;
        var componentBId = detectedComponents.First(c => ((NpmComponent)c.Component).Name.Equals(componentB.Name)).Component.Id;

        var dependencyGraph = componentRecorder.GetDependencyGraphsByLocation().Values.First();

        dependencyGraph.GetDependenciesForComponent(componentAId).Should().HaveCount(1);
        dependencyGraph.GetDependenciesForComponent(componentAId).Should().Contain(componentBId);
        dependencyGraph.GetDependenciesForComponent(componentBId).Should().HaveCount(0);
    }
}
