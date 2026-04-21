namespace lab8.Services;

public interface ILibraryService
{
    IReadOnlyList<Book> GetBooks();
    IReadOnlyList<User> GetUsers();
    IReadOnlyList<BorrowedBookRecord> GetBorrowedBooks();
    LibraryStats GetStats();
    OperationResult AddBook(Book input);
    OperationResult UpdateBook(Book input);
    OperationResult DeleteBook(int bookId);
    OperationResult AddUser(User input);
    OperationResult UpdateUser(User input);
    OperationResult DeleteUser(int userId);
    OperationResult BorrowBook(int bookId, int userId);
    OperationResult ReturnBook(int userId, int bookId);
}
