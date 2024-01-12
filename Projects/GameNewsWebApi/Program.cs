using System.Net.Http;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var MyAllowSpecificOrigin = "_myAllowSpecificOrigins";

builder.Services.AddCors(options => {
    options.AddPolicy(MyAllowSpecificOrigin,
                     policy =>{
                        policy.WithOrigins("http://localhost:5293")
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                     });
});

// Add HttpClient service
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(MyAllowSpecificOrigin); // Add this line to enable CORS for the entire application

app.MapGet("/steamgames", async () =>
{
    try
    {
        // Use HttpClient from the factory
        var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient();

        HttpResponseMessage response = await httpClient.GetAsync("https://api.steampowered.com/ISteamApps/GetAppList/v2/");
       
        if (response.IsSuccessStatusCode)
        {
            string responseData = await response.Content.ReadAsStringAsync();
            var steamGamesResponse =  JsonConvert.DeserializeObject<SteamGamesResponse>(responseData); // JsonSerializer.Deserialize<SteamGamesResponse>(responseData);

            if (steamGamesResponse != null && steamGamesResponse.applist?.apps != null)
            {
                // Limit the number of games to 100
                List<GameRecord> filteredGames = steamGamesResponse.applist.apps
                                .Where(app => !string.IsNullOrWhiteSpace(app.name))
                                .Take(100)
                                .Select(app => new GameRecord(app.appid, app.name,""))
                                .ToList();
           
                return Results.Json(filteredGames);
            }
        }

        // Log the error and return a more specific status code
        app.Logger.LogError($"Error fetching Steam games. Status code: {response.StatusCode}, Reason: {response.ReasonPhrase}");
        return Results.StatusCode((int)response.StatusCode);
    }
    catch (Exception ex)
    {
        // Log the exception
        app.Logger.LogError($"Function Error: {ex}");
        return Results.StatusCode(500);
    }
})
.WithName("GetSteamGames")
.WithOpenApi();

app.MapGet("/steamgamesinfo", async () =>
{
    try
    {
        // Use HttpClient from the factory
        var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient();

        HttpResponseMessage response = await httpClient.GetAsync("https://api.steampowered.com/ISteamApps/GetAppList/v2/");

        if (response.IsSuccessStatusCode)
        {
            string responseData = await response.Content.ReadAsStringAsync();
            var steamGamesResponse = JsonConvert.DeserializeObject<SteamGamesResponse>(responseData);// JsonSerializer.Deserialize<SteamGamesResponse>(responseData);
          
            if (steamGamesResponse != null && steamGamesResponse.applist?.apps != null)
            {
                // Limit the number of games to 30
                List<GameRecord> filteredGames = steamGamesResponse.applist.apps
                    .Where(app => !string.IsNullOrWhiteSpace(app.name))
                    .Take(30)
                    .Select(app => new GameRecord(app.appid, app.name,""))
                    .ToList();

               
                // Make concurrent requests for detailed information about each game
                    var tasks = filteredGames.Select(async game =>
                    {
                        try
                        {
                            var gameDetailsResponse = await httpClient.GetAsync($"http://store.steampowered.com/api/appdetails?appids={game.appid}");
                            if (gameDetailsResponse.IsSuccessStatusCode)
                            {
                                string gameDetailsData = await gameDetailsResponse.Content.ReadAsStringAsync();
                                string extractedData = "";

                                JObject jsonObject = JObject.Parse(gameDetailsData);

                                // Retrieve the dynamic first level key
                                var dynamicKey = jsonObject.Properties().FirstOrDefault();
                                if (dynamicKey != null)
                                {
                                    var dynamicKeyObject = dynamicKey.Value as JObject;

                                    // Navigate to the second level
                                    var level2Data = dynamicKeyObject?.GetValue("data");
                                    if (level2Data != null)
                                    {
                                        extractedData = level2Data.ToString(Formatting.Indented);
                                    }
                                    else
                                    {
                                        Console.WriteLine("Error: 'data' property not found in the second level.");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("Error: No dynamic first-level property found in the JSON.");
                                }

                                Root gameDetails = JsonConvert.DeserializeObject<Root>(extractedData);
                                // Console.WriteLine($"LEVEL 1 -->>>>>>>>>  {gameDetails.detailed_description}");
                                return new GameRecordDetails(game.name, gameDetails.short_description, gameDetails.header_image, gameDetails.website);
                            }
                            else
                            {
                                // Log error for individual request
                                app.Logger.LogError($"Error fetching details for AppId {game.appid}. Status code: {gameDetailsResponse.StatusCode}, Reason: {gameDetailsResponse.ReasonPhrase}");
                                return null; // Return null or a default GameRecordDetails as needed
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log error for individual request
                            app.Logger.LogError($"Error in concurrent request for AppId {game.appid}: {ex.Message}");
                            return null; // Return null or a default GameRecordDetails as needed
                        }
                    });

                    var results = await Task.WhenAll(tasks);

                    return Results.Json(results.Where(result => result != null).ToList());
            }
        }

        // Log the error and return a more specific status code
        app.Logger.LogError($"Error fetching Steam games. Status code: {response.StatusCode}, Reason: {response.ReasonPhrase}");
        return Results.StatusCode((int)response.StatusCode);
    }
    catch (Exception ex)
    {
        // Log the exception
        app.Logger.LogError($"Function Error: {ex}");
        return Results.StatusCode(500);
    }
})
.WithName("GetSteamInfo")
.WithOpenApi();

app.Run();


public record GameRecord(int appid, string name,string detailed_description);
public record GameRecordDetails(string name,string detailed_description,string header_image,string link);


public class SteamGamesResponse
{
    public AppList applist { get; set; }
}

public class AppList
{
    public List<App> apps { get; set; }
}

public class App
{
    public int appid { get; set; }
    public string name { get; set; }
    // public Data data { get; set; }
    public string detailed_description { get; set; }
    public string header_image { get; set; }
}

public class GameDetailsResponse
{
    // public Data data { get; set; }
}

    public class Category
    {
        public int id { get; set; }
        public string description { get; set; }
    }

    public class ContentDescriptors
    {
        public List<object> ids { get; set; }
        public object notes { get; set; }
    }

    public class Genre
    {
        public string id { get; set; }
        public string description { get; set; }
    }

    public class Movie
    {
        public int id { get; set; }
        public string name { get; set; }
        public string thumbnail { get; set; }
        public Webm webm { get; set; }
        public Mp4 mp4 { get; set; }
        public bool highlight { get; set; }
    }

    public class Mp4
    {
        [JsonProperty("480")]
        public string _480 { get; set; }
        public string max { get; set; }
    }

    public class PackageGroup
    {
        public string name { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public string selection_text { get; set; }
        public string save_text { get; set; }
        public int display_type { get; set; }
        public string is_recurring_subscription { get; set; }
        public List<Sub> subs { get; set; }
    }

    public class PcRequirements
    {
        public string minimum { get; set; }
    }

    public class Platforms
    {
        public bool windows { get; set; }
        public bool mac { get; set; }
        public bool linux { get; set; }
    }

    public class PriceOverview
    {
        public string currency { get; set; }
        public int initial { get; set; }
        public int final { get; set; }
        public int discount_percent { get; set; }
        public string initial_formatted { get; set; }
        public string final_formatted { get; set; }
    }

    public class ReleaseDate
    {
        public bool coming_soon { get; set; }
        public string date { get; set; }
    }

    public class Root
    {
        public string type { get; set; }
        public string name { get; set; }
        public int steam_appid { get; set; }
        public int required_age { get; set; }
        public bool is_free { get; set; }
        public string detailed_description { get; set; }
        public string about_the_game { get; set; }
        public string short_description { get; set; }
        public string supported_languages { get; set; }
        public string header_image { get; set; }
        public string capsule_image { get; set; }
        public string capsule_imagev5 { get; set; }
        public string website { get; set; }
        public PcRequirements pc_requirements { get; set; }
        public List<object> mac_requirements { get; set; }
        public List<object> linux_requirements { get; set; }
        public List<string> developers { get; set; }
        public List<string> publishers { get; set; }
        public PriceOverview price_overview { get; set; }
        public List<int> packages { get; set; }
        public List<PackageGroup> package_groups { get; set; }
        public Platforms platforms { get; set; }
        public List<Category> categories { get; set; }
        public List<Genre> genres { get; set; }
        public List<Screenshot> screenshots { get; set; }
        public List<Movie> movies { get; set; }
        public ReleaseDate release_date { get; set; }
        public SupportInfo support_info { get; set; }
        public string background { get; set; }
        public string background_raw { get; set; }
        public ContentDescriptors content_descriptors { get; set; }
    }

    public class Screenshot
    {
        public int id { get; set; }
        public string path_thumbnail { get; set; }
        public string path_full { get; set; }
    }

    public class Sub
    {
        public int packageid { get; set; }
        public string percent_savings_text { get; set; }
        public int percent_savings { get; set; }
        public string option_text { get; set; }
        public string option_description { get; set; }
        public string can_get_free_license { get; set; }
        public bool is_free_license { get; set; }
        public int price_in_cents_with_discount { get; set; }
    }

    public class SupportInfo
    {
        public string url { get; set; }
        public string email { get; set; }
    }

    public class Webm
    {
        [JsonProperty("480")]
        public string _480 { get; set; }
        public string max { get; set; }
    }
