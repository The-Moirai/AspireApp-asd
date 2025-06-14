var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var apiService=builder.AddProject<Projects.WebApplication_Drone>("apisercie-drone")
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WaitFor(cache);

builder.AddProject<Projects.BlazorApp_Web>("blazorapp-web")
    .WithExternalHttpEndpoints().WithReference(apiService)
    .WaitFor(apiService)
    .WithReference(cache)
    .WaitFor(cache);

builder.Build().Run();
