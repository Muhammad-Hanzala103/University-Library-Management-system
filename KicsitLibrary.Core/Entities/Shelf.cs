namespace KicsitLibrary.Core.Entities
{
    public class Shelf : EntityBase
    {
        public string Name { get; set; } = string.Empty;
        public int RackId { get; set; }
        public virtual Rack Rack { get; set; } = null!;
    }
}
