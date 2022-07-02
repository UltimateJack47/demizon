namespace DomProject.Core.Services.Borrowing;

public interface IBorrowingService
{
    Task<Dal.Entities.Borrowing> GetOneAsync(int id);
    IQueryable<Dal.Entities.Borrowing> GetAll();
    Task UpdateAsync(int id, Dal.Entities.Borrowing updatedBorrowing);
    Task<bool> CreateAsync(Dal.Entities.Borrowing borrowing);
    Task<bool> DeleteAsync(int id);
    Task<bool> SetReturned(int id);
}
