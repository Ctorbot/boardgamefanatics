namespace BoardGameFanatics.Data;

public class Game
{
    public int BggId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? YearPublished { get; set; }
    public int? MinPlayers { get; set; }
    public int? MaxPlayers { get; set; }
    public int? PlayingTimeMinutes { get; set; }
    public int? MinAge { get; set; }
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CachedAt { get; set; }

    public ICollection<CollectionEntry> CollectionEntries { get; set; } = [];
}
