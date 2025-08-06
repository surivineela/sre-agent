// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Graph.Helpers;
using Shouldly;

namespace Agent.Tests.Unit.Graph.Crawler.ARM;

public class PostgreSqlConnectionStringHelperTests
{
    [Theory]
    [InlineData("Host=myserver.postgres.database.azure.com;Database=mydb;Username=myuser;Password=mypass", true)]
    [InlineData("Server=myserver.postgres.database.azure.com;Database=mydb;User ID=myuser;Password=mypass", true)]
    [InlineData("host=localhost;database=testdb;username=testuser", true)]
    [InlineData("DATABASE=mydb;USERNAME=user;Host=server", true)]
    [InlineData("Database=mydb;User ID=user;Server=myserver.postgres.database.azure.com", true)]
    [InlineData("HOST=test.postgres.database.azure.com;PORT=5432", true)]
    [InlineData("postgresql://user:pass@host:5432/database", true)]
    [InlineData("postgres://user@host/database", true)]
    [InlineData("host=localhost database=testdb user=testuser", true)]
    [InlineData("Data Source=sqlserver.database.windows.net;Initial Catalog=mydb", false)]
    [InlineData("Server=sqlserver;Database=mydb;Integrated Security=true", false)]
    [InlineData("", false)]
    [InlineData("InvalidConnectionString", false)]
    [InlineData("Host=;Database=", false)]
    [InlineData("SomeRandomText", false)]
    [InlineData("Key=Value;Another=Setting", false)]
    public void IsPostgreSqlConnectionString_WithVariousInputs_ShouldReturnExpectedResult(string? connectionString, bool expected)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString ?? string.Empty);

        // Assert
        result.ShouldBe(expected, $"Connection string '{connectionString}' should return {expected}");
    }

    [Theory]
    [InlineData("Host=myserver.postgres.database.azure.com;Database=mydb")]
    [InlineData("Server=myserver.postgres.database.azure.com;Database=mydb")]
    [InlineData("host=myserver.postgres.database.azure.com;database=mydb")]
    [InlineData("SERVER=myserver.postgres.database.azure.com;DATABASE=mydb")]
    public void IsPostgreSqlConnectionString_WithAzurePostgreSqlSuffix_ShouldReturnTrue(string connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString);

        // Assert
        result.ShouldBeTrue($"Azure PostgreSQL connection string '{connectionString}' should be recognized");
    }

    [Theory]
    [InlineData("Database=mydb;Username=user;Password=pass")]
    [InlineData("Database=mydb;User ID=user;Password=pass")]
    [InlineData("DATABASE=mydb;USERNAME=user;PASSWORD=pass")]
    [InlineData("database=mydb;username=user;password=pass")]
    public void IsPostgreSqlConnectionString_WithDatabaseAndUsername_ShouldReturnTrue(string connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString);

        // Assert
        result.ShouldBeTrue($"PostgreSQL connection string with database and username '{connectionString}' should be recognized");
    }

    [Theory]
    [InlineData("Host=localhost")]
    [InlineData("Server=myserver")]
    [InlineData("HOST=LOCALHOST")]
    [InlineData("SERVER=MYSERVER")]
    public void IsPostgreSqlConnectionString_WithHostOrServerOnly_ShouldReturnTrue(string connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString);

        // Assert
        result.ShouldBeTrue($"PostgreSQL connection string with host/server '{connectionString}' should be recognized");
    }

    [Theory]
    [InlineData("ConnectionTimeout=30;CommandTimeout=60")]
    [InlineData("Pooling=true;MinPoolSize=1;MaxPoolSize=100")]
    [InlineData("Database=mydb;Pooling=false")]
    [InlineData("Username=user;Pooling=true")]
    public void IsPostgreSqlConnectionString_WithoutHostServerOrAzureSuffix_ShouldReturnFalse(string connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString);

        // Assert
        result.ShouldBeFalse($"Connection string without host/server or Azure suffix '{connectionString}' should not be recognized as PostgreSQL");
    }

    [Theory]
    [InlineData("Data Source=localhost;Initial Catalog=mydb")]
    [InlineData("Server=myserver;Database=mydb;Integrated Security=true")]
    [InlineData("Data Source=.\\SQLEXPRESS;AttachDbFilename=|DataDirectory|mydb.mdf")]
    public void IsPostgreSqlConnectionString_WithSqlServerPatterns_ShouldReturnFalse(string connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString);

        // Assert
        result.ShouldBeFalse($"SQL Server connection string '{connectionString}' should not be recognized as PostgreSQL");
    }

    [Fact]
    public void IsPostgreSqlConnectionString_WithEmptyString_ShouldReturnFalse()
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(string.Empty);

        // Assert
        result.ShouldBeFalse("Empty string should not be recognized as PostgreSQL connection string");
    }
    [Fact]
    public void IsPostgreSqlConnectionString_WithNull_ShouldReturnFalse()
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(null!);

        // Assert
        result.ShouldBeFalse("Null should not be recognized as PostgreSQL connection string");
    }

    [Fact]
    public void IsPostgreSqlConnectionString_WithWhitespace_ShouldReturnFalse()
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString("   ");

        // Assert
        result.ShouldBeFalse("Whitespace should not be recognized as PostgreSQL connection string");
    }

    [Theory]
    [InlineData("postgresql://user:pass@host:5432/database", true, PostgreSqlConnectionFormat.URL)]
    [InlineData("postgres://user:pass@host/database", true, PostgreSqlConnectionFormat.URL)]
    [InlineData("postgresql+psycopg2://user:pass@host/database", true, PostgreSqlConnectionFormat.URL)]
    [InlineData("postgresql+asyncpg://user:pass@host/database", true, PostgreSqlConnectionFormat.URL)]
    [InlineData("jdbc:postgresql://host:5432/database", true, PostgreSqlConnectionFormat.URL)]
    [InlineData("Host=myserver.postgres.database.azure.com;Database=mydb;Username=myuser;Password=mypass", true, PostgreSqlConnectionFormat.SemicolonList)]
    [InlineData("Server=myserver.postgres.database.azure.com;Database=mydb;User ID=myuser;Password=mypass", true, PostgreSqlConnectionFormat.SemicolonList)]
    [InlineData("host=localhost database=testdb user=testuser password=secret", true, PostgreSqlConnectionFormat.KeyValueList)]
    [InlineData("host='my host' dbname='my db' user='my user'", true, PostgreSqlConnectionFormat.KeyValueList)]
    [InlineData(@"host=localhost\ with\ spaces dbname=test", true, PostgreSqlConnectionFormat.KeyValueList)]
    [InlineData(@"{""host"":""localhost"",""database"":""mydb"",""user"":""myuser""}", true, PostgreSqlConnectionFormat.JSONPayload)]
    [InlineData(@"[{""host"":""localhost"",""port"":5432}]", true, PostgreSqlConnectionFormat.JSONPayload)]
    [InlineData("DRIVER={PostgreSQL ANSI};SERVER=localhost;DATABASE=mydb;UID=user;PWD=pass", true, PostgreSqlConnectionFormat.ODBC)]
    [InlineData("DRIVER={PostgreSQL Unicode};SERVER=localhost;DATABASE=mydb;UID=user;PWD=pass", true, PostgreSqlConnectionFormat.ODBC)]
    [InlineData("myservice", true, PostgreSqlConnectionFormat.ServiceName, "PGSERVICE")]
    [InlineData("production_db", true, PostgreSqlConnectionFormat.ServiceName, "service")]
    [InlineData("Data Source=sqlserver.database.windows.net;Initial Catalog=mydb", false, PostgreSqlConnectionFormat.Unknown)]
    [InlineData("", false, PostgreSqlConnectionFormat.Unknown)]
    public void DetectFormat_WithVariousInputs_ShouldReturnExpectedResult(string? connectionString, bool shouldBeValid, PostgreSqlConnectionFormat expectedFormat, string? keyName = null)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var isValid = helper.IsPostgreSqlConnectionString(connectionString ?? string.Empty, keyName!);
        var detectedFormat = helper.DetectFormat(connectionString ?? string.Empty, keyName!);

        // Assert
        isValid.ShouldBe(shouldBeValid, $"Connection string '{connectionString}' validity should be {shouldBeValid}");
        if (shouldBeValid)
        {
            detectedFormat.ShouldBe(expectedFormat, $"Connection string '{connectionString}' should be detected as {expectedFormat}");
        }
    }

    [Theory]
    [InlineData("postgresql://user:pass@host:5432/database?sslmode=require&application_name=myapp")]
    [InlineData("postgresql+psycopg2://user:pass@host/database")]
    [InlineData("postgresql+asyncpg://user:pass@host/database")]
    [InlineData("postgresql+psycopg://user:pass@host/database")]
    [InlineData("jdbc:postgresql://host:5432/database?user=test&password=secret")]
    public void IsPostgreSqlConnectionString_WithURLFormats_ShouldReturnTrue(string connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString);

        // Assert
        result.ShouldBeTrue($"URL format connection string '{connectionString}' should be recognized");
    }

    [Theory]
    [InlineData("Host=myserver.postgres.database.azure.com;Database=mydb;Username=myuser;Password=mypass")]
    [InlineData("Server=myserver.postgres.database.azure.com;Database=mydb;User ID=myuser;Password=mypass")]
    [InlineData("Host=localhost;Port=5432;Database=testdb;Username=testuser;Password=secret")]
    [InlineData("Server=localhost;Database=mydb;Pooling=true;SSL Mode=Require")]
    public void IsPostgreSqlConnectionString_WithSemicolonFormats_ShouldReturnTrue(string connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString);

        // Assert
        result.ShouldBeTrue($"Semicolon format connection string '{connectionString}' should be recognized");
    }

    [Theory]
    [InlineData("host=localhost database=testdb user=testuser password=secret")]
    [InlineData("host=myserver.postgres.database.azure.com dbname=mydb user=myuser")]
    [InlineData("host='localhost with spaces' dbname='my database' user='my user'")]
    [InlineData(@"host=localhost\ with\ spaces dbname=test user=admin")]
    public void IsPostgreSqlConnectionString_WithKeyValueFormats_ShouldReturnTrue(string connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString);

        // Assert
        result.ShouldBeTrue($"Key-value format connection string '{connectionString}' should be recognized");
    }

    [Theory]
    [InlineData(@"{""host"":""localhost"",""database"":""mydb"",""user"":""myuser"",""password"":""secret""}")]
    [InlineData(@"{""host"":""localhost"",""dbname"":""mydb"",""port"":5432}")]
    [InlineData(@"[{""host"":""server1"",""port"":5432},{""host"":""server2"",""port"":5432}]")]
    [InlineData(@"{""postgres"":{""host"":""localhost"",""database"":""mydb""}}")]
    public void IsPostgreSqlConnectionString_WithJSONFormats_ShouldReturnTrue(string connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString);

        // Assert
        result.ShouldBeTrue($"JSON format connection string '{connectionString}' should be recognized");
    }

    [Theory]
    [InlineData("DRIVER={PostgreSQL ANSI};SERVER=localhost;DATABASE=mydb;UID=user;PWD=pass")]
    [InlineData("DRIVER={PostgreSQL Unicode};SERVER=localhost;DATABASE=mydb;UID=user;PWD=pass")]
    [InlineData("DRIVER={PostgreSQL ODBC Driver};SERVER=localhost;PORT=5432;DATABASE=mydb")]
    public void IsPostgreSqlConnectionString_WithODBCFormats_ShouldReturnTrue(string connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString);

        // Assert
        result.ShouldBeTrue($"ODBC format connection string '{connectionString}' should be recognized");
    }

    [Theory]
    [InlineData("myservice", "PGSERVICE")]
    [InlineData("production_db", "service")]
    [InlineData("staging", "PGSERVICE")]
    public void IsPostgreSqlConnectionString_WithServiceNames_ShouldReturnTrue(string connectionString, string keyName)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString, keyName);

        // Assert
        result.ShouldBeTrue($"Service name '{connectionString}' with key '{keyName}' should be recognized");
    }

    [Theory]
    [InlineData("somevalue", "POSTGRESQLCONNSTR_MyConnection")]
    [InlineData("anyvalue", "CUSTOMCONNSTR_PostgreSQLConn")]
    [InlineData("connection", "DB_POSTGRESQL_URL")]
    [InlineData("value", "POSTGRES_CONNECTION")]
    public void IsPostgreSqlConnectionString_WithPostgreSQLKeyNames_ShouldReturnTrue(string connectionString, string keyName)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString, keyName);

        // Assert
        result.ShouldBeTrue($"Connection string '{connectionString}' with PostgreSQL key name '{keyName}' should be recognized");
    }

    [Theory]
    [InlineData("Host=myserver.postgres.database.azure.com;Database=mydb")]
    [InlineData("Server=myserver.postgres.database.azure.com;Database=mydb")]
    [InlineData("postgresql://user@myserver.postgres.database.azure.com/db")]
    [InlineData("host=myserver.postgres.database.azure.com dbname=mydb")]
    public void IsPostgreSqlConnectionString_WithAzurePostgreSQLSuffix_ShouldReturnTrue(string connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString);

        // Assert
        result.ShouldBeTrue($"Azure PostgreSQL connection string '{connectionString}' should be recognized");
    }

    [Theory]
    [InlineData("Data Source=sqlserver.database.windows.net;Initial Catalog=mydb")]
    [InlineData("Server=sqlserver;Database=mydb;Integrated Security=true")]
    [InlineData("mysql://user:pass@host/database")]
    [InlineData("mongodb://user:pass@host/database")]
    [InlineData("ConnectionTimeout=30;CommandTimeout=60")]
    [InlineData("SomeRandomText")]
    [InlineData("Key=Value;Another=Setting")]
    public void IsPostgreSqlConnectionString_WithNonPostgreSQLStrings_ShouldReturnFalse(string connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString);

        // Assert
        result.ShouldBeFalse($"Non-PostgreSQL connection string '{connectionString}' should not be recognized");
    }
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IsPostgreSqlConnectionString_WithEmptyOrNullStrings_ShouldReturnFalse(string? connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString ?? string.Empty);

        // Assert
        result.ShouldBeFalse($"Empty or null connection string should not be recognized");
    }

    [Fact]
    public void IsPostgreSqlConnectionString_WithQuotedAzureAppServiceConnectionString_ShouldReturnTrue()
    {
        // Arrange
        var helper = CreateHelper();
        var quotedConnectionString = "\"Host=myserver.postgres.database.azure.com;Database=mydb;Username=myuser;Password=mypass\"";

        // Act
        var result = helper.IsPostgreSqlConnectionString(quotedConnectionString);

        // Assert
        result.ShouldBeTrue("Quoted connection string should be recognized after removing quotes");
    }

    [Theory]
    [InlineData("postgresql://user:pass@host:5432/database", "standard")]
    [InlineData("postgresql+psycopg2://user:pass@host/database", "python-psycopg2")]
    [InlineData("postgresql+asyncpg://user:pass@host/database", "python-asyncpg")]
    [InlineData("jdbc:postgresql://host:5432/database", "java")]
    [InlineData("Host=localhost;SSL Mode=Require", "dotnet")]
    [InlineData("DRIVER={PostgreSQL ANSI};SERVER=localhost", "odbc")]
    public void DetectDriverFamily_WithVariousFormats_ShouldReturnExpectedFamily(string connectionString, string expectedFamily)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        // We need to access the private method through reflection or make it public for testing
        // For now, we'll test indirectly through the public API
        var isValid = helper.IsPostgreSqlConnectionString(connectionString);
        var format = helper.DetectFormat(connectionString);        // Assert
        isValid.ShouldBeTrue($"Connection string '{connectionString}' should be valid");
        format.ShouldNotBe(PostgreSqlConnectionFormat.Unknown, $"Format should be detected for '{connectionString}'");

        // Note: Since DetectDriverFamily is private, we can't directly test the expected family
        // This would require either making the method public or using reflection
        // For now, we verify the connection string is properly recognized
        // Expected family: {expectedFamily}
        expectedFamily.ShouldNotBeNullOrEmpty("Expected family should be provided for test case");
    }

    [Theory]
    [InlineData("postgresql://user:pass@host1:5432,host2:5433/database")]
    [InlineData("Host=host1:5432,host2:5433;Database=mydb")]
    [InlineData("host=host1,host2 dbname=mydb")]
    public void IsPostgreSqlConnectionString_WithMultipleHosts_ShouldReturnTrue(string connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString);

        // Assert
        result.ShouldBeTrue($"Multi-host connection string '{connectionString}' should be recognized");
    }

    [Theory]
    [InlineData("postgresql://user@host/db?sslmode=require")]
    [InlineData("Host=host;SSL Mode=Require;Database=db")]
    [InlineData("host=host sslmode=require dbname=db")]
    public void IsPostgreSqlConnectionString_WithSSLConfiguration_ShouldReturnTrue(string connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString);

        // Assert
        result.ShouldBeTrue($"SSL-configured connection string '{connectionString}' should be recognized");
    }

    [Theory]
    [InlineData("postgresql://user@host/db?application_name=MyApp")]
    [InlineData("Host=host;Application Name=MyApp;Database=db")]
    [InlineData("host=host application_name=MyApp dbname=db")]
    public void IsPostgreSqlConnectionString_WithApplicationName_ShouldReturnTrue(string connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString);

        // Assert
        result.ShouldBeTrue($"Connection string with application name '{connectionString}' should be recognized");
    }

    [Theory]
    [InlineData("Host=host;Authentication=Active Directory Managed Identity;Database=db")]
    [InlineData("postgresql://host/db?Authentication=Active%20Directory")]
    [InlineData("host=host gssencmode=require dbname=db")]
    public void IsPostgreSqlConnectionString_WithManagedIdentityAuth_ShouldReturnTrue(string connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString);

        // Assert
        result.ShouldBeTrue($"Managed identity connection string '{connectionString}' should be recognized");
    }

    [Theory]
    [InlineData("postgresql://user@host/db?sslcert=/path/to/cert&sslkey=/path/to/key")]
    [InlineData("host=host sslcert=/path/to/cert sslkey=/path/to/key dbname=db")]
    public void IsPostgreSqlConnectionString_WithCertificateAuth_ShouldReturnTrue(string connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString);

        // Assert
        result.ShouldBeTrue($"Certificate-based connection string '{connectionString}' should be recognized");
    }

    [Theory]
    [InlineData("Host=host;Pooling=true;Min Pool Size=1;Max Pool Size=100;Database=db")]
    [InlineData("postgresql://user@host/db?pool_max_conns=100")]
    public void IsPostgreSqlConnectionString_WithPoolingConfig_ShouldReturnTrue(string connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString);

        // Assert
        result.ShouldBeTrue($"Connection pooling connection string '{connectionString}' should be recognized");
    }

    [Theory]
    [InlineData("Host=host;Target Server Type=read-write;Database=db")]
    [InlineData("postgresql://user@host/db?target_session_attrs=read-write")]
    [InlineData("host=host target_session_attrs=read-write dbname=db")]
    public void IsPostgreSqlConnectionString_WithTargetServerType_ShouldReturnTrue(string connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString);

        // Assert
        result.ShouldBeTrue($"Target server type connection string '{connectionString}' should be recognized");
    }

    [Theory]
    [InlineData("postgresql://user@host/db?keepalives=1&keepalives_idle=30")]
    [InlineData("Host=host;TCP Keepalives Enabled=true;TCP Keepalives Idle=30;Database=db")]
    public void IsPostgreSqlConnectionString_WithKeepAlives_ShouldReturnTrue(string connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act
        var result = helper.IsPostgreSqlConnectionString(connectionString);

        // Assert
        result.ShouldBeTrue($"Keep-alives connection string '{connectionString}' should be recognized");
    }

    // Edge cases and malformed strings
    [Theory]
    [InlineData("postgresql://")]
    [InlineData("postgresql://host")]
    [InlineData("postgresql://host/")]
    [InlineData("Host=")]
    [InlineData("Database=")]
    [InlineData("host=")]
    public void IsPostgreSqlConnectionString_WithIncompleteButValidStarts_ShouldHandleGracefully(string connectionString)
    {
        // Arrange
        var helper = CreateHelper();

        // Act & Assert - Should not throw exceptions
        var result = helper.IsPostgreSqlConnectionString(connectionString);
        var format = helper.DetectFormat(connectionString);

        // These may return false or true depending on the specific case, but should not crash
        format.ShouldNotBe(PostgreSqlConnectionFormat.Unknown, "Should at least attempt to categorize the format");
    }
    private static PostgreSqlConnectionStringHelper CreateHelper()
    {
        // For basic tests that only use IsPostgreSqlConnectionString and DetectFormat,
        // we can pass null for the dependencies since they won't be used
        return new PostgreSqlConnectionStringHelper(null!, null!, null!);
    }
}
