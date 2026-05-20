var builder = WebApplication.CreateBuilder(args);


// Load YARP configuration from yarp.json
builder.Configuration.AddJsonFile("yarp.json", optional: false, reloadOnChange: true);

// Add YARP reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

// Map YARP reverse proxy endpoints
app.MapReverseProxy();

app.Run();
