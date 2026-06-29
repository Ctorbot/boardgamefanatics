namespace BoardGameFanatics.Data;

public class Player
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public PlayerStatus Status { get; set; } = PlayerStatus.Pending;
    public PlayerRole Role { get; set; } = PlayerRole.Player;
    public DateTime CreatedAt { get; set; }

    public ICollection<CollectionEntry> CollectionEntries { get; set; } = [];
}
