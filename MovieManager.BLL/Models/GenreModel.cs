namespace MovieManager.BLL.Models
{
    public class GenreModel : IModelWithId
    {
        public int Id               { get; set; }
        public string Name          { get; set; } = string.Empty;
        public string? Description  { get; set; }
    }
}
