using Microsoft.VisualBasic.FileIO;

namespace lab8.Services;

public sealed class LibraryService : ILibraryService
{
    private readonly object syncRoot = new();
    private readonly List<Book> books = [];
    private readonly List<User> users = [];
    private readonly Dictionary<int, List<Book>> borrowedBooks = [];
    private readonly string booksPath;
    private readonly string usersPath;

    public LibraryService(IWebHostEnvironment environment)
    {
        booksPath = Path.Combine(environment.ContentRootPath, "Data", "Books.csv");
        usersPath = Path.Combine(environment.ContentRootPath, "Data", "Users.csv");
        LoadData();
    }

    public IReadOnlyList<Book> GetBooks()
    {
        lock (syncRoot)
        {
            return books.OrderBy(book => book.Id).Select(CloneBook).ToList();
        }
    }

    public IReadOnlyList<User> GetUsers()
    {
        lock (syncRoot)
        {
            return users.OrderBy(user => user.Id).Select(CloneUser).ToList();
        }
    }

    public IReadOnlyList<BorrowedBookRecord> GetBorrowedBooks()
    {
        lock (syncRoot)
        {
            var userLookup = users.ToDictionary(user => user.Id);

            return borrowedBooks
                .OrderBy(entry => entry.Key)
                .SelectMany(entry =>
                {
                    if (!userLookup.TryGetValue(entry.Key, out var user))
                    {
                        return [];
                    }

                    return entry.Value.Select(book => new BorrowedBookRecord(
                        user.Id,
                        user.Name,
                        user.Email,
                        book.Id,
                        book.Title,
                        book.Author,
                        book.ISBN));
                })
                .OrderBy(entry => entry.UserName)
                .ThenBy(entry => entry.Title)
                .ToList();
        }
    }

    public LibraryStats GetStats()
    {
        lock (syncRoot)
        {
            var borrowedCount = borrowedBooks.Sum(entry => entry.Value.Count);
            return new LibraryStats(books.Count, users.Count, borrowedCount);
        }
    }

    public OperationResult AddBook(Book input)
    {
        lock (syncRoot)
        {
            var validationMessage = ValidateBook(input);
            if (validationMessage is not null)
            {
                return OperationResult.Failure(validationMessage);
            }

            var nextId = books.Count == 0 ? 1 : books.Max(book => book.Id) + 1;
            books.Add(new Book
            {
                Id = nextId,
                Title = Clean(input.Title),
                Author = Clean(input.Author),
                ISBN = Clean(input.ISBN)
            });

            return OperationResult.Success("Book added successfully.");
        }
    }

    public OperationResult UpdateBook(Book input)
    {
        lock (syncRoot)
        {
            var validationMessage = ValidateBook(input);
            if (validationMessage is not null)
            {
                return OperationResult.Failure(validationMessage);
            }

            var existingBook = books.FirstOrDefault(book => book.Id == input.Id);
            if (existingBook is null)
            {
                return OperationResult.Failure("Book not found.");
            }

            existingBook.Title = Clean(input.Title);
            existingBook.Author = Clean(input.Author);
            existingBook.ISBN = Clean(input.ISBN);

            return OperationResult.Success("Book updated successfully.");
        }
    }

    public OperationResult DeleteBook(int bookId)
    {
        lock (syncRoot)
        {
            var existingBook = books.FirstOrDefault(book => book.Id == bookId);
            if (existingBook is null)
            {
                return OperationResult.Failure("Book not found.");
            }

            books.Remove(existingBook);
            return OperationResult.Success("Book deleted successfully.");
        }
    }

    public OperationResult AddUser(User input)
    {
        lock (syncRoot)
        {
            var validationMessage = ValidateUser(input);
            if (validationMessage is not null)
            {
                return OperationResult.Failure(validationMessage);
            }

            var nextId = users.Count == 0 ? 1 : users.Max(user => user.Id) + 1;
            users.Add(new User
            {
                Id = nextId,
                Name = Clean(input.Name),
                Email = Clean(input.Email)
            });

            return OperationResult.Success("User added successfully.");
        }
    }

    public OperationResult UpdateUser(User input)
    {
        lock (syncRoot)
        {
            var validationMessage = ValidateUser(input);
            if (validationMessage is not null)
            {
                return OperationResult.Failure(validationMessage);
            }

            var existingUser = users.FirstOrDefault(user => user.Id == input.Id);
            if (existingUser is null)
            {
                return OperationResult.Failure("User not found.");
            }

            existingUser.Name = Clean(input.Name);
            existingUser.Email = Clean(input.Email);

            return OperationResult.Success("User updated successfully.");
        }
    }

    public OperationResult DeleteUser(int userId)
    {
        lock (syncRoot)
        {
            var existingUser = users.FirstOrDefault(user => user.Id == userId);
            if (existingUser is null)
            {
                return OperationResult.Failure("User not found.");
            }

            if (borrowedBooks.TryGetValue(userId, out var activeLoans) && activeLoans.Count > 0)
            {
                return OperationResult.Failure("Return this user's borrowed books before deleting the account.");
            }

            users.Remove(existingUser);
            borrowedBooks.Remove(userId);

            return OperationResult.Success("User deleted successfully.");
        }
    }

    public OperationResult BorrowBook(int bookId, int userId)
    {
        lock (syncRoot)
        {
            var book = books.FirstOrDefault(candidate => candidate.Id == bookId);
            if (book is null)
            {
                return OperationResult.Failure("Book not found or no available copies remain.");
            }

            var user = users.FirstOrDefault(candidate => candidate.Id == userId);
            if (user is null)
            {
                return OperationResult.Failure("User not found.");
            }

            if (!borrowedBooks.TryGetValue(userId, out var userLoans))
            {
                userLoans = [];
                borrowedBooks[userId] = userLoans;
            }

            userLoans.Add(CloneBook(book));
            books.Remove(book);

            return OperationResult.Success($"{book.Title} was borrowed by {user.Name}.");
        }
    }

    public OperationResult ReturnBook(int userId, int bookId)
    {
        lock (syncRoot)
        {
            var user = users.FirstOrDefault(candidate => candidate.Id == userId);
            if (user is null)
            {
                return OperationResult.Failure("User not found.");
            }

            if (!borrowedBooks.TryGetValue(userId, out var userLoans) || userLoans.Count == 0)
            {
                return OperationResult.Failure("This user has no borrowed books to return.");
            }

            var book = userLoans.FirstOrDefault(candidate => candidate.Id == bookId);
            if (book is null)
            {
                return OperationResult.Failure("Selected borrowed book was not found.");
            }

            userLoans.Remove(book);
            books.Add(CloneBook(book));

            if (userLoans.Count == 0)
            {
                borrowedBooks.Remove(userId);
            }

            return OperationResult.Success($"{book.Title} was returned successfully.");
        }
    }

    private void LoadData()
    {
        lock (syncRoot)
        {
            books.Clear();
            users.Clear();
            borrowedBooks.Clear();
            books.AddRange(ReadBooks());
            users.AddRange(ReadUsers());
        }
    }

    private IEnumerable<Book> ReadBooks()
    {
        foreach (var fields in ReadCsv(booksPath))
        {
            if (fields.Length < 4 || !int.TryParse(fields[0], out var id))
            {
                continue;
            }

            yield return new Book
            {
                Id = id,
                Title = Clean(fields[1]),
                Author = Clean(fields[2]),
                ISBN = Clean(fields[3])
            };
        }
    }

    private IEnumerable<User> ReadUsers()
    {
        foreach (var fields in ReadCsv(usersPath))
        {
            if (fields.Length < 3 || !int.TryParse(fields[0], out var id))
            {
                continue;
            }

            yield return new User
            {
                Id = id,
                Name = Clean(fields[1]),
                Email = Clean(fields[2])
            };
        }
    }

    private static IEnumerable<string[]> ReadCsv(string path)
    {
        if (!File.Exists(path))
        {
            yield break;
        }

        using var parser = new TextFieldParser(path);
        parser.TextFieldType = FieldType.Delimited;
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;
        parser.TrimWhiteSpace = true;

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is not null && fields.Length > 0)
            {
                yield return fields;
            }
        }
    }

    private static string? ValidateBook(Book input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            return "Enter a book title.";
        }

        if (string.IsNullOrWhiteSpace(input.Author))
        {
            return "Enter an author.";
        }

        if (string.IsNullOrWhiteSpace(input.ISBN))
        {
            return "Enter an ISBN.";
        }

        return null;
    }

    private static string? ValidateUser(User input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            return "Enter a user name.";
        }

        if (string.IsNullOrWhiteSpace(input.Email))
        {
            return "Enter an email address.";
        }

        return null;
    }

    private static string Clean(string? value) => value?.Trim() ?? string.Empty;

    private static Book CloneBook(Book book) =>
        new()
        {
            Id = book.Id,
            Title = book.Title,
            Author = book.Author,
            ISBN = book.ISBN
        };

    private static User CloneUser(User user) =>
        new()
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email
        };
}

public sealed record LibraryStats(int AvailableBooks, int TotalUsers, int BorrowedBooks);

public sealed record BorrowedBookRecord(
    int UserId,
    string UserName,
    string UserEmail,
    int BookId,
    string Title,
    string Author,
    string ISBN);

public sealed record OperationResult(bool Succeeded, string Message)
{
    public static OperationResult Success(string message) => new(true, message);

    public static OperationResult Failure(string message) => new(false, message);
}
