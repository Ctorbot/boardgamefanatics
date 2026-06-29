using System.Xml.Linq;
using BoardGameFanatics.Data;
using Microsoft.EntityFrameworkCore;

namespace BoardGameFanatics.Services;

public record BggSearchResult(int BggId, string Name, int? YearPublished);

public class BggService(HttpClient http, AppDbContext db)
{
    public async Task<List<BggSearchResult>> SearchGamesAsync(string query)
    {
        var xml = await http.GetStringAsync($"/xmlapi2/search?query={Uri.EscapeDataString(query)}&type=boardgame");
        var doc = XDocument.Parse(xml);

        return doc.Root?
            .Elements("item")
            .Select(item => new BggSearchResult(
                BggId: (int)item.Attribute("id")!,
                Name: PrimaryName(item.Elements("name")),
                YearPublished: (int?)item.Element("yearpublished")?.Attribute("value")))
            .ToList() ?? [];
    }

    public async Task<Game> GetGameDetailsAsync(int bggId)
    {
        var xml = await http.GetStringAsync($"/xmlapi2/thing?id={bggId}&stats=1");
        var doc = XDocument.Parse(xml);
        var item = doc.Root!.Element("item")!;

        return new Game
        {
            BggId = bggId,
            Name = PrimaryName(item.Elements("name")),
            YearPublished = (int?)item.Element("yearpublished")?.Attribute("value"),
            MinPlayers = (int?)item.Element("minplayers")?.Attribute("value"),
            MaxPlayers = (int?)item.Element("maxplayers")?.Attribute("value"),
            PlayingTimeMinutes = (int?)item.Element("playingtime")?.Attribute("value"),
            MinAge = (int?)item.Element("minage")?.Attribute("value"),
            Description = (string?)item.Element("description"),
            ThumbnailUrl = (string?)item.Element("thumbnail"),
            ImageUrl = (string?)item.Element("image"),
            CachedAt = DateTime.UtcNow,
        };
    }

    public async Task<Game> FindOrCacheGameAsync(int bggId)
    {
        var existing = await db.Games.FindAsync(bggId);
        if (existing is not null) return existing;

        var game = await GetGameDetailsAsync(bggId);
        db.Games.Add(game);
        await db.SaveChangesAsync();
        return game;
    }

    private static string PrimaryName(IEnumerable<XElement> names)
    {
        var list = names.ToList();
        var primary = list.FirstOrDefault(n => (string?)n.Attribute("type") == "primary");
        return (string?)((primary ?? list.FirstOrDefault())?.Attribute("value")) ?? "";
    }
}
