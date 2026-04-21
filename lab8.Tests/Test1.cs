using lab8;
using lab8.Services;

namespace lab8.Tests;

/// <summary>
/// In-memory implementation of ILibraryService for testing.
/// No file system or DI setup is needed.
/// </summary>
public class FakeLibraryService : ILibraryService
{
    private readonly List<Book> books = [];
    private readonly List<User> users = [];
    private readonly Dictionary<int, List<Book>> borrowed = [];
    private int nextBookId = 1;
    private int nextUserId = 1;

    public IReadOnlyList<Book> GetBooks() => books.OrderBy(b => b.Id).ToList();

    public IReadOnlyList<User> GetUsers() => users.OrderBy(u => u.Id).ToList();

    public IReadOnlyList<BorrowedBookRecord> GetBorrowedBooks() =>
        borrowed.SelectMany(entry =>
        {
            var user = users.FirstOrDefault(u => u.Id == entry.Key);
            if (user is null)
            {
                return Enumerable.Empty<BorrowedBookRecord>();
            }

            return entry.Value.Select(book =>
                new BorrowedBookRecord(user.Id, user.Name, user.Email, book.Id, book.Title, book.Author, book.ISBN));
        }).ToList();

    public LibraryStats GetStats() =>
        new(books.Count, users.Count, borrowed.Sum(entry => entry.Value.Count));

    public OperationResult AddBook(Book input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            return OperationResult.Failure("Enter a book title.");
        }

        if (string.IsNullOrWhiteSpace(input.Author))
        {
            return OperationResult.Failure("Enter an author.");
        }

        if (string.IsNullOrWhiteSpace(input.ISBN))
        {
            return OperationResult.Failure("Enter an ISBN.");
        }

        books.Add(new Book
        {
            Id = nextBookId++,
            Title = input.Title.Trim(),
            Author = input.Author.Trim(),
            ISBN = input.ISBN.Trim()
        });

        return OperationResult.Success("Book added successfully.");
    }

    public OperationResult UpdateBook(Book input)
    {
        var existing = books.FirstOrDefault(book => book.Id == input.Id);
        if (existing is null)
        {
            return OperationResult.Failure("Book not found.");
        }

        existing.Title = input.Title.Trim();
        existing.Author = input.Author.Trim();
        existing.ISBN = input.ISBN.Trim();
        return OperationResult.Success("Book updated successfully.");
    }

    public OperationResult DeleteBook(int bookId)
    {
        var existing = books.FirstOrDefault(book => book.Id == bookId);
        if (existing is null)
        {
            return OperationResult.Failure("Book not found.");
        }

        books.Remove(existing);
        return OperationResult.Success("Book deleted successfully.");
    }

    public OperationResult AddUser(User input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            return OperationResult.Failure("Enter a user name.");
        }

        if (string.IsNullOrWhiteSpace(input.Email))
        {
            return OperationResult.Failure("Enter an email address.");
        }

        users.Add(new User
        {
            Id = nextUserId++,
            Name = input.Name.Trim(),
            Email = input.Email.Trim()
        });

        return OperationResult.Success("User added successfully.");
    }

    public OperationResult UpdateUser(User input)
    {
        var existing = users.FirstOrDefault(user => user.Id == input.Id);
        if (existing is null)
        {
            return OperationResult.Failure("User not found.");
        }

        existing.Name = input.Name.Trim();
        existing.Email = input.Email.Trim();
        return OperationResult.Success("User updated successfully.");
    }

    public OperationResult DeleteUser(int userId)
    {
        var existing = users.FirstOrDefault(user => user.Id == userId);
        if (existing is null)
        {
            return OperationResult.Failure("User not found.");
        }

        if (borrowed.TryGetValue(userId, out var loans) && loans.Count > 0)
        {
            return OperationResult.Failure("Return this user's borrowed books before deleting the account.");
        }

        users.Remove(existing);
        return OperationResult.Success("User deleted successfully.");
    }

    public OperationResult BorrowBook(int bookId, int userId)
    {
        var book = books.FirstOrDefault(candidate => candidate.Id == bookId);
        if (book is null)
        {
            return OperationResult.Failure("Book not found.");
        }

        var user = users.FirstOrDefault(candidate => candidate.Id == userId);
        if (user is null)
        {
            return OperationResult.Failure("User not found.");
        }

        if (!borrowed.TryGetValue(userId, out var loans))
        {
            loans = [];
            borrowed[userId] = loans;
        }

        loans.Add(book);
        books.Remove(book);
        return OperationResult.Success($"{book.Title} was borrowed by {user.Name}.");
    }

    public OperationResult ReturnBook(int userId, int bookId)
    {
        if (!borrowed.TryGetValue(userId, out var loans))
        {
            return OperationResult.Failure("No borrowed books.");
        }

        var book = loans.FirstOrDefault(candidate => candidate.Id == bookId);
        if (book is null)
        {
            return OperationResult.Failure("Borrowed book not found.");
        }

        loans.Remove(book);
        books.Add(book);
        return OperationResult.Success($"{book.Title} was returned successfully.");
    }
}

[TestClass]
public sealed class Test1
{
    private static ILibraryService CreateService() => new FakeLibraryService();

    private static Book MakeBook() =>
        new() { Title = "Clean Code", Author = "Robert Martin", ISBN = "978-0132350884" };

    private static User MakeUser() =>
        new() { Name = "Alice", Email = "alice@example.com" };

    [TestMethod]
    public void AddBook_ValidBook_Succeeds()
    {
        var result = CreateService().AddBook(MakeBook());
        Assert.IsTrue(result.Succeeded);
    }

    [TestMethod]
    public void AddBook_MissingTitle_Fails()
    {
        var book = MakeBook();
        book.Title = string.Empty;
        Assert.IsFalse(CreateService().AddBook(book).Succeeded);
    }

    [TestMethod]
    public void DeleteBook_NonExistentId_Fails()
    {
        Assert.IsFalse(CreateService().DeleteBook(999).Succeeded);
    }

    [TestMethod]
    public void AddUser_ValidUser_Succeeds()
    {
        Assert.IsTrue(CreateService().AddUser(MakeUser()).Succeeded);
    }

    [TestMethod]
    public void AddUser_MissingEmail_Fails()
    {
        var user = MakeUser();
        user.Email = string.Empty;
        Assert.IsFalse(CreateService().AddUser(user).Succeeded);
    }

    [TestMethod]
    public void DeleteUser_WithActiveLoans_Fails()
    {
        var service = CreateService();
        service.AddBook(MakeBook());
        service.AddUser(MakeUser());
        service.BorrowBook(service.GetBooks().First().Id, service.GetUsers().First().Id);
        Assert.IsFalse(service.DeleteUser(service.GetUsers().First().Id).Succeeded);
    }

    [TestMethod]
    public void BorrowBook_RemovesFromInventory()
    {
        var service = CreateService();
        service.AddBook(MakeBook());
        service.AddUser(MakeUser());
        service.BorrowBook(service.GetBooks().First().Id, service.GetUsers().First().Id);
        Assert.AreEqual(0, service.GetBooks().Count);
    }

    [TestMethod]
    public void ReturnBook_RestoresToInventory()
    {
        var service = CreateService();
        service.AddBook(MakeBook());
        service.AddUser(MakeUser());
        service.BorrowBook(service.GetBooks().First().Id, service.GetUsers().First().Id);
        service.ReturnBook(service.GetUsers().First().Id, service.GetBorrowedBooks().First().BookId);
        Assert.AreEqual(1, service.GetBooks().Count);
    }

    [TestMethod]
    public void GetStats_EmptyService_AllZeroes()
    {
        var stats = CreateService().GetStats();
        Assert.AreEqual(0, stats.AvailableBooks);
        //edited this line
        Assert.AreEqual(1, stats.TotalUsers);
        Assert.AreEqual(0, stats.BorrowedBooks);
    }
}
