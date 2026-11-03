using Imageboard_illust.Database;
using Imageboard_illust.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Imageboard_illust.Web.Pages;

public class BoardsModel : PageModel
{
    private readonly AppDatabase _db;

    public BoardsModel(AppDatabase db)
    {
        _db = db;
    }

    public bool IsAuthenticated { get; private set; }
    public int CurrentUserId { get; private set; }
    public List<Board> Boards { get; private set; } = new();

    [BindProperty]
    public string NewBoardName { get; set; } = string.Empty;

    [BindProperty]
    public string NewBoardType { get; set; } = "individual";

    [BindProperty]
    public string ShareCodeInput { get; set; } = string.Empty;

    [TempData]
    public string? FlashMessage { get; set; }

    public IActionResult OnGet()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
            return RedirectToPage("/Login");

        IsAuthenticated = true;
        CurrentUserId = userId.Value;
        Boards = _db.LoadBoardsByUser(userId.Value);
        return Page();
    }

    public IActionResult OnPost()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
            return RedirectToPage("/Login");

        if (string.IsNullOrWhiteSpace(NewBoardName))
        {
            ModelState.AddModelError(string.Empty, "Board name is required.");
            Boards = _db.LoadBoardsByUser(userId.Value);
            IsAuthenticated = true;
            CurrentUserId = userId.Value;
            return Page();
        }

        _db.InsertBoard(userId.Value, NewBoardName.Trim(), NewBoardType);
        return RedirectToPage();
    }

    public IActionResult OnPostJoin()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue) return RedirectToPage("/Login");

        if (string.IsNullOrWhiteSpace(ShareCodeInput))
        {
            FlashMessage = "Enter a share code to join a board.";
            return RedirectToPage();
        }

        var joined = _db.JoinBoard(ShareCodeInput.Trim().ToUpperInvariant(), userId.Value);
        FlashMessage = joined ? "Joined the board!" : "Invalid or already-joined share code.";
        return RedirectToPage();
    }

    public IActionResult OnPostShare(int boardId)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue) return RedirectToPage("/Login");

        var board = _db.LoadAllBoards().FirstOrDefault(b => b.Id == boardId);
        if (board is null || board.OwnerId != userId.Value)
        {
            FlashMessage = "Only the board owner can generate a share code.";
            return RedirectToPage();
        }

        var code = _db.GetBoardShareCode(boardId) ?? _db.CreateShareCode(boardId);
        FlashMessage = $"Share code for \"{board.Name}\": {code}";
        return RedirectToPage();
    }
}
