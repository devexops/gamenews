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
                List<GameRecord> filteredGames = new List<GameRecord>();

                // Add individual GameRecord instances
                GameRecord custom1 = new GameRecord { appid = 2426960, name = "Summoners War" , detailed_description ="" };
                GameRecord custom2 = new GameRecord { appid = 730, name = "Counter-Strike 2"  , detailed_description =""};
                GameRecord custom3 = new GameRecord { appid = 230410, name = "Warframe"  , detailed_description =""};
                GameRecord custom4 = new GameRecord { appid = 582010, name = "Monster Hunter: World"  , detailed_description =""};

                filteredGames.Add(custom1);
                filteredGames.Add(custom2);
                filteredGames.Add(custom3);
                filteredGames.Add(custom4);

                // Add a list of GameRecord instances
                List<GameRecord> newGames = steamGamesResponse.applist.apps
                    .Where(app => !string.IsNullOrWhiteSpace(app.name))
                    .Take(30)
                    .Select(app => new GameRecord{ appid= app.appid, name = app.name, detailed_description = ""})
                    .ToList();

                filteredGames.AddRange(newGames);

                int delayBetweenRequestsMilliseconds = 1000;
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
                                return new GameRecordDetails(game.appid,game.name, gameDetails.short_description, gameDetails.header_image, "");
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
                        finally
                        {
                             await Task.Delay(delayBetweenRequestsMilliseconds);
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

app.MapGet("/steamgamesnews/{appid}", async (int appid) =>
{
    try
    {
        // Use HttpClient from the factory
        var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient();

        // Replace the {} in the URL with the actual appid parameter
        HttpResponseMessage response = await httpClient.GetAsync($"https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid={appid}");

        if (response.IsSuccessStatusCode)
        {
            string responseData = await response.Content.ReadAsStringAsync();
             app.Logger.LogError($"JSON News:  { responseData}");
          
            var steamGamesResponse = JsonConvert.DeserializeObject<GamesNewsData>(responseData);

            if (steamGamesResponse != null)
            {
                // Create a new GameNews object with the relevant properties
                var gameNews = new InfoNews
                (
                    steamGamesResponse.appnews.newsitems
                );
                return Results.Json(gameNews);
            }
        }

        // Log the error and return a more specific status code
        app.Logger.LogError($"Error fetching Steam games news for AppId {appid}. Status code: {response.StatusCode}, Reason: {response.ReasonPhrase}");
        return Results.StatusCode((int)response.StatusCode);
    }
    catch (Exception ex)
    {
        // Log the exception
        app.Logger.LogError($"Function Error: {ex}");
        return Results.StatusCode(500);
    }
})
.WithName("GetSteamGamesNews")
.WithOpenApi();



app.Run();


public record GameRecord
{
    public int appid { get; init; }
    public string name { get; init; }
    public string detailed_description { get; init; }
}

public record GameNews
{
        public string title { get; init; }
        public string url { get; init; }
        public string author { get; init; }
        public string contents { get; init; }
        public string feedlabel { get; init; }
        public int date { get; init; }
        public string feedname { get; init; }
        public int feed_type { get; init; }
}

public record GameRecordDetails(int appid, string name,string detailed_description,string header_image,string link);
public record InfoNews(List<Newsitem> news);

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
        public string name { get; set; }
        public int steam_appid { get; set; }
        public string detailed_description { get; set; }
        public string short_description { get; set; }
        public string header_image { get; set; }
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


    public class Appnews
    {
        public int appid { get; set; }
        public List<Newsitem> newsitems { get; set; }
        public int count { get; set; }
    }

    public class Newsitem
    {
       
        public string title { get; set; }
  
        public string author { get; set; }
        public string contents { get; set; }
     
        public int date { get; set; }
        public string feedname { get; set; }
    
        public int appid { get; set; }
 
    }

    public class GamesNewsData
    {
        public Appnews appnews { get; set; }
    }
