var builder = DistributedApplication.CreateBuilder(args);

// Add a SQL Server container
var sqlPassword = builder.AddParameter("sql-password");
var sqlServer = builder
    .AddSqlServer("sql", sqlPassword);
var sqlDatabase = sqlServer.AddDatabase("Agency");

sqlServer.WithLifetime(ContainerLifetime.Persistent);

// Populate the database with the schema and data
sqlServer
    .WithBindMount("./sql-server", target: "/usr/config")
    .WithBindMount("../../database", target: "/docker-entrypoint-initdb.d")
    .WithEntrypoint("/usr/config/entrypoint.sh");

var dab = builder.AddExecutable("dab", "dab", "../../dab/", "start")
    .WithReference(sqlDatabase)
    .WithHttpEndpoint(targetPort: 5000)
    .WaitFor(sqlServer)
    .WithOtlpExporter();
var dabEndpoint = dab.GetEndpoint("http");

var apiService = builder.AddProject<Projects.DBChat_ApiService>("api")
    .WithReference(dabEndpoint);

var frontend = builder.AddNpmApp("frontend", "../frontend", "dev")
    .WithNpmPackageInstallation()
    .WithReference(apiService)
    .WithHttpEndpoint(env: "PORT");
    
builder.Build().Run();

// Add Data API Builder using dab-config.json 
// var dab = builder.AddDataAPIBuilder("dab", "../../dab/dab-config.json")
//     .WithReference(sqlDatabase)
//     .WaitFor(sqlServer);

// var dab = builder.AddContainer("dab", "dabtest1", "1.0")
//     .WithBindMount("../../dab/dab-config.json", target: "/App/dab-config.json")
//     .WithHttpEndpoint(targetPort: 5000,
//         name: "http")
//     .WithOtlpExporter()
//     .WithReference(sqlDatabase)
//     .WaitFor(sqlServer);
