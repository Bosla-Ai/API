namespace Domain.Entities;

public abstract class BaseEntity
{
    public int Id { get; set; }
}

public abstract class BaseAuditEntity : BaseEntity
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public abstract class BaseManyToManyEntity<TLeft, TLeftKey, TRight, TRightKey>
    where TLeft : class where TRight : class
{
    public TLeftKey LeftId { get; set; } = default!;
    public TLeft Left { get; set; } = default!;
    
    public TRightKey RightId { get; set; } = default!;
    public TRight Right { get; set; } = default!;
}