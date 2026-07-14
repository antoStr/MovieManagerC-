namespace MovieManager.BLL.Models
{
    /// <summary>
    /// Vincolo comune ai model gestiti dal <c>GenericService</c>:
    /// garantisce a compile-time la presenza di una chiave singola <c>Id</c> di tipo int.
    /// </summary>
    public interface IModelWithId
    {
        int Id { get; set; }
    }
}
