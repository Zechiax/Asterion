using Asterion.Configuration;
using Microsoft.Extensions.Configuration;

namespace Asterion.Test.Configuration;

[TestFixture]
public class ConfigurationTests
{
    private string _dir = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "asterion-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_dir, true);

    private IConfiguration Load() =>
        new ConfigurationBuilder().AddTomlFile(Path.Combine(_dir, ConfigKeys.FileName)).Build();

    [Test]
    public void MigratesJsonWithBomAndRenamesOriginal()
    {
        File.WriteAllText(Path.Combine(_dir, "config.json"),
            ((char)0xFEFF) + "{ \"token\": \"a\\\"b\", \"testGuild\": \"123\", \"testChannel\": 456, \"TopGgToken\": \"tg\" }");

        var result = ConfigBootstrapper.EnsureConfigFile(_dir);

        Assert.That(result, Is.EqualTo(ConfigBootstrapResult.Migrated));
        Assert.That(File.Exists(Path.Combine(_dir, "config.json")), Is.False);
        Assert.That(File.Exists(Path.Combine(_dir, ConfigKeys.LegacyMigratedFileName)), Is.True);

        var config = Load();
        Assert.That(config[ConfigKeys.Token], Is.EqualTo("a\"b"));
        Assert.That(config.GetValue<ulong>(ConfigKeys.TestGuild), Is.EqualTo(123UL));
        Assert.That(config.GetValue<ulong>(ConfigKeys.TestChannel), Is.EqualTo(456UL));
        Assert.That(config[ConfigKeys.TopGgToken], Is.EqualTo("tg"));
    }

    [Test]
    public void LeavesExistingTomlUntouchedEvenWithJsonPresent()
    {
        var tomlPath = Path.Combine(_dir, ConfigKeys.FileName);
        File.WriteAllText(tomlPath, "token = \"keep\"");
        File.WriteAllText(Path.Combine(_dir, "config.json"), "{ \"token\": \"other\" }");

        var result = ConfigBootstrapper.EnsureConfigFile(_dir);

        Assert.That(result, Is.EqualTo(ConfigBootstrapResult.Existing));
        Assert.That(File.ReadAllText(tomlPath), Is.EqualTo("token = \"keep\""));
        Assert.That(File.Exists(Path.Combine(_dir, "config.json")), Is.True);
    }

    [Test]
    public void TemplateFailsValidation()
    {
        ConfigBootstrapper.EnsureConfigFile(_dir);

        var errors = ConfigValidator.Validate(Load(), isDebug: true);

        Assert.That(errors, Has.Count.EqualTo(2));
        Assert.That(ConfigValidator.Validate(Load(), isDebug: false), Has.Count.EqualTo(1));
    }

    [Test]
    public void EnvironmentTokenOverridesTemplate()
    {
        ConfigBootstrapper.EnsureConfigFile(_dir);
        var variable = ConfigKeys.EnvPrefix + "TOKEN";
        var previous = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, "from-env");
        try
        {
            var config = new ConfigurationBuilder()
                .AddTomlFile(Path.Combine(_dir, ConfigKeys.FileName))
                .AddEnvironmentVariables(ConfigKeys.EnvPrefix)
                .Build();

            Assert.That(config[ConfigKeys.Token], Is.EqualTo("from-env"));
            Assert.That(ConfigValidator.Validate(config, isDebug: false), Is.Empty);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    [Test]
    public void DataDirDefaultsToAppDirectory()
    {
        Assert.That(DataPaths.Resolve(null), Is.EqualTo(AppContext.BaseDirectory));
        Assert.That(DataPaths.Resolve(" "), Is.EqualTo(AppContext.BaseDirectory));
    }

    [Test]
    public void DataDirIsCreatedWhenSet()
    {
        var dataDir = Path.Combine(_dir, "data");

        var resolved = DataPaths.Resolve(dataDir);

        Assert.That(resolved, Is.EqualTo(Path.GetFullPath(dataDir)));
        Assert.That(Directory.Exists(dataDir), Is.True);
    }
}
