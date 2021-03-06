using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Helpers;
using Sleet;
using Test.Common;
using Xunit;

namespace NuGet.CatalogReader.Tests
{
    public class CatalogReaderTests
    {
        [Fact]
        public async Task VerifyNoEntriesWhenReadingAnEmptyCatalogAsync()
        {
            // Arrange
            using (var cache = new LocalCache())
            using (var cacheContext = new SourceCacheContext())
            using (var workingDir = new TestFolder())
            {
                var log = new TestLogger();
                var baseUri = Sleet.UriUtility.CreateUri("https://localhost:8080/testFeed/");
                var feedFolder = Path.Combine(workingDir, "feed");
                var nupkgsFolder = Path.Combine(workingDir, "nupkgs");
                Directory.CreateDirectory(feedFolder);
                Directory.CreateDirectory(nupkgsFolder);

                await CatalogReaderTestHelpers.CreateCatalogAsync(workingDir, feedFolder, nupkgsFolder, baseUri, log);

                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");

                var httpSource = CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri);

                // Act
                using (var catalogReader = new CatalogReader(feedUri, httpSource, cacheContext, TimeSpan.FromMinutes(1), log))
                {
                    var entries = await catalogReader.GetEntriesAsync();
                    var flatEntries = await catalogReader.GetFlattenedEntriesAsync();
                    var set = await catalogReader.GetPackageSetAsync();

                    // Assert
                    Assert.Empty(entries);
                    Assert.Empty(flatEntries);
                    Assert.Empty(set);
                }
            }
        }

        [Fact]
        public async Task VerifySingleEntriesWhenReadingACatalogAsync()
        {
            // Arrange
            using (var cache = new LocalCache())
            using (var cacheContext = new SourceCacheContext())
            using (var workingDir = new TestFolder())
            {
                var log = new TestLogger();
                var baseUri = Sleet.UriUtility.CreateUri("https://localhost:8080/testFeed/");
                var feedFolder = Path.Combine(workingDir, "feed");
                var nupkgsFolder = Path.Combine(workingDir, "nupkgs");
                Directory.CreateDirectory(feedFolder);
                Directory.CreateDirectory(nupkgsFolder);

                var packageA = new TestNupkg("a", "1.0.0");
                TestNupkg.Save(nupkgsFolder, packageA);

                await CatalogReaderTestHelpers.CreateCatalogAsync(workingDir, feedFolder, nupkgsFolder, baseUri, log);

                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");

                var httpSource = CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri);

                // Act
                using (var catalogReader = new CatalogReader(feedUri, httpSource, cacheContext, TimeSpan.FromMinutes(1), log))
                {
                    var entries = await catalogReader.GetEntriesAsync();
                    var flatEntries = await catalogReader.GetFlattenedEntriesAsync();
                    var set = await catalogReader.GetPackageSetAsync();

                    var entry = entries.FirstOrDefault();

                    // Assert
                    Assert.Equal(1, entries.Count);
                    Assert.Equal(1, flatEntries.Count);
                    Assert.Equal(1, set.Count);

                    Assert.Equal("a", entry.Id);
                    Assert.Equal("1.0.0", entry.Version.ToNormalizedString());
                }
            }
        }

        [Fact]
        public async Task VerifyEditsAreIgnoredInFlattenedViewAsync()
        {
            // Arrange
            using (var cache = new LocalCache())
            using (var cacheContext = new SourceCacheContext())
            using (var workingDir = new TestFolder())
            {
                var log = new TestLogger();
                var baseUri = Sleet.UriUtility.CreateUri("https://localhost:8080/testFeed/");
                var feedFolder = Path.Combine(workingDir, "feed");
                var nupkgsFolder = Path.Combine(workingDir, "nupkgs");
                Directory.CreateDirectory(feedFolder);
                Directory.CreateDirectory(nupkgsFolder);

                var packageA = new TestNupkg("a", "1.0.0");
                TestNupkg.Save(nupkgsFolder, packageA);

                // Create and push
                await CatalogReaderTestHelpers.CreateCatalogAsync(workingDir, feedFolder, nupkgsFolder, baseUri, log);

                // 2nd push
                await CatalogReaderTestHelpers.PushPackagesAsync(workingDir, nupkgsFolder, baseUri, log);

                // 3rd push
                await CatalogReaderTestHelpers.PushPackagesAsync(workingDir, nupkgsFolder, baseUri, log);

                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");
                var httpSource = CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri);

                // Act
                using (var catalogReader = new CatalogReader(feedUri, httpSource, cacheContext, TimeSpan.FromMinutes(1), log))
                {
                    var entries = await catalogReader.GetEntriesAsync();
                    var flatEntries = await catalogReader.GetFlattenedEntriesAsync();
                    var set = await catalogReader.GetPackageSetAsync();

                    var entry = entries.FirstOrDefault();

                    // Assert
                    // 3 adds, 2 removes
                    Assert.Equal(5, entries.Count);
                    Assert.Equal(1, flatEntries.Count);
                    Assert.Equal(1, set.Count);

                    Assert.Equal("a", entry.Id);
                    Assert.Equal("1.0.0", entry.Version.ToNormalizedString());
                }
            }
        }

        [Fact]
        public async Task VerifyCatalogEntryPropertiesAsync()
        {
            // Arrange
            using (var cache = new LocalCache())
            using (var cacheContext = new SourceCacheContext())
            using (var workingDir = new TestFolder())
            {
                var log = new TestLogger();
                var baseUri = Sleet.UriUtility.CreateUri("https://localhost:8080/testFeed/");
                var feedFolder = Path.Combine(workingDir, "feed");
                var nupkgsFolder = Path.Combine(workingDir, "nupkgs");
                Directory.CreateDirectory(feedFolder);
                Directory.CreateDirectory(nupkgsFolder);

                var packageA = new TestNupkg("a", "1.0.0.1-RC.1.2.b0.1+meta.blah.1");
                TestNupkg.Save(nupkgsFolder, packageA);

                // Create and push
                await CatalogReaderTestHelpers.CreateCatalogAsync(workingDir, feedFolder, nupkgsFolder, baseUri, log);

                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");
                var httpSource = CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri);

                // Act
                using (var catalogReader = new CatalogReader(feedUri, httpSource, cacheContext, TimeSpan.FromMinutes(1), log))
                {
                    var entries = await catalogReader.GetEntriesAsync();
                    var entry = entries.FirstOrDefault();

                    // Assert
                    Assert.Equal("a", entry.Id);
                    Assert.Equal("1.0.0.1-RC.1.2.b0.1", entry.Version.ToNormalizedString());
                    Assert.NotEmpty(entry.CommitId);
                    Assert.True(DateTimeOffset.MinValue < entry.CommitTimeStamp);
                    Assert.Equal("a.1.0.0.1-rc.1.2.b0.1", entry.FileBaseName);
                    Assert.True(entry.IsAddOrUpdate);
                    Assert.False(entry.IsDelete);
                    Assert.True(await entry.IsListedAsync());
                    Assert.Equal("https://localhost:8080/testFeed/flatcontainer/a/1.0.0.1-rc.1.2.b0.1/a.1.0.0.1-rc.1.2.b0.1.nupkg", entry.NupkgUri.AbsoluteUri);
                    Assert.Equal("https://localhost:8080/testFeed/flatcontainer/a/1.0.0.1-rc.1.2.b0.1/a.nuspec", entry.NuspecUri.AbsoluteUri);
                    Assert.Equal("https://localhost:8080/testFeed/flatcontainer/a/index.json", entry.PackageBaseAddressIndexUri.AbsoluteUri);
                    Assert.Equal("https://localhost:8080/testFeed/registration/a/1.0.0.1-rc.1.2.b0.1.json", entry.PackageRegistrationUri.AbsoluteUri);
                    Assert.Equal("https://localhost:8080/testFeed/registration/a/index.json", entry.RegistrationIndexUri.AbsoluteUri);
                    Assert.Equal("nuget:PackageDetails", string.Join("|", entry.Types));
                    Assert.StartsWith("https://localhost:8080/testFeed/catalog/data/", entry.Uri.AbsoluteUri);
                }
            }
        }

        [Fact]
        public async Task GetCatalogEntryVerifyUrlsCanBeOpenedAsJsonAsync()
        {
            // Arrange
            using (var cache = new LocalCache())
            using (var cacheContext = new SourceCacheContext())
            using (var workingDir = new TestFolder())
            {
                var log = new TestLogger();
                var baseUri = Sleet.UriUtility.CreateUri("https://localhost:8080/testFeed/");
                var feedFolder = Path.Combine(workingDir, "feed");
                var nupkgsFolder = Path.Combine(workingDir, "nupkgs");
                Directory.CreateDirectory(feedFolder);
                Directory.CreateDirectory(nupkgsFolder);

                var packageA = new TestNupkg("a", "1.0.0.1-RC.1.2.b0.1+meta.blah.1");
                TestNupkg.Save(nupkgsFolder, packageA);

                // Create and push
                await CatalogReaderTestHelpers.CreateCatalogAsync(workingDir, feedFolder, nupkgsFolder, baseUri, log);

                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");
                var httpSource = CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri);

                // Act
                using (var catalogReader = new CatalogReader(feedUri, httpSource, cacheContext, TimeSpan.FromMinutes(1), log))
                {
                    var entries = await catalogReader.GetEntriesAsync();
                    var entry = entries.FirstOrDefault();

                    // Assert
                    (await entry.GetNupkgAsync()).Should().NotBeNull();
                    (await entry.GetNupkgAsync()).Should().NotBeNull();
                    (await entry.GetNuspecAsync()).Should().NotBeNull();
                    (await entry.GetPackageBaseAddressIndexUriAsync()).Should().NotBeNull();
                    (await entry.GetPackageDetailsAsync()).Should().NotBeNull();
                    (await entry.GetPackageRegistrationUriAsync()).Should().NotBeNull();
                    (await entry.GetRegistrationIndexUriAsync()).Should().NotBeNull();
                }
            }
        }
    }
}
