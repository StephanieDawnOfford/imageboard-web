using Imageboard_illust.Database;
using Imageboard_illust.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Imageboard_illust.Web.Pages;

public class BoardModel : PageModel
{
    private static readonly string[] ValidExts =
        { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };

    private readonly AppDatabase _db;

    public BoardModel(AppDatabase db) => _db = db;

    public Board CurrentBoard { get; private set; } = null!;
    public List<BoardItem> Items { get; private set; } = new();
    public bool CanEdit { get; private set; }

    public IActionResult OnGet(int id)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId is null) return RedirectToPage("/Login");

        if (!_db.CanUserAccessBoard(id, userId.Value))
            return RedirectToPage("/Boards");

        var board = _db.LoadAllBoards().FirstOrDefault(b => b.Id == id);
        if (board is null) return RedirectToPage("/Boards");

        CurrentBoard = board;
        Items = _db.LoadItemsByBoardOrderById(id);
        CanEdit = _db.GetUserPermissionForBoard(id, userId.Value, board.OwnerId) == "edit";
        return Page();
    }

    public IActionResult OnPostUpload(int id, List<IFormFile> files)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId is null) return RedirectToPage("/Login");
        if (!CanEditBoard(id, userId.Value)) return StatusCode(403);

        var board = _db.LoadAllBoards().FirstOrDefault(b => b.Id == id);
        if (board is null) return RedirectToPage("/Boards");

        var imagesDir = board.ImagesDir(_db.DataDir);
        Directory.CreateDirectory(imagesDir);

        int offset = 0;
        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!ValidExts.Contains(ext) || file.Length == 0) continue;

            var item = new BoardItem
            {
                BoardId = id,
                Name = Path.GetFileNameWithoutExtension(file.FileName),
                FileName = "tmp",
                X = 24 + offset,
                Y = 24 + offset,
                Width = 300,
                Height = 278,
                ZOrder = 0,
            };
            int itemId = _db.InsertItem(item);
            var fileName = $"{itemId}{ext}";
            using (var stream = new FileStream(Path.Combine(imagesDir, fileName), FileMode.Create))
                file.CopyTo(stream);
            _db.UpdateItemFileName(itemId, fileName);
            offset += 28;
        }

        return RedirectToPage(new { id });
    }

    public IActionResult OnPostLayout(int id, int itemId, int x, int y, int width, int height)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId is null || !CanEditBoard(id, userId.Value))
            return new JsonResult(new { ok = false }) { StatusCode = 403 };

        _db.UpdateItemLayout(itemId, Math.Max(0, x), Math.Max(0, y), Math.Max(80, width), Math.Max(68, height), 0);
        return new JsonResult(new { ok = true });
    }

    public IActionResult OnPostRename(int id, int itemId, string name)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId is null || !CanEditBoard(id, userId.Value))
            return new JsonResult(new { ok = false }) { StatusCode = 403 };

        var trimmed = string.IsNullOrWhiteSpace(name) ? $"Image #{itemId}" : name.Trim();
        _db.UpdateItemName(itemId, trimmed);
        return new JsonResult(new { ok = true, name = trimmed });
    }

    public IActionResult OnPostDeleteItem(int id, int itemId)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId is null || !CanEditBoard(id, userId.Value))
            return new JsonResult(new { ok = false }) { StatusCode = 403 };

        var item = _db.LoadItemsByBoard(id).FirstOrDefault(i => i.Id == itemId);
        _db.DeleteItem(itemId);
        if (item is not null)
        {
            var path = item.FullPath(_db.DataDir, id);
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
        return new JsonResult(new { ok = true });
    }

    public IActionResult OnPostDeleteBoard(int id)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId is null) return RedirectToPage("/Login");

        var board = _db.LoadAllBoards().FirstOrDefault(b => b.Id == id);
        if (board is null || board.OwnerId != userId.Value) return RedirectToPage("/Boards");

        var dir = Path.Combine(_db.DataDir, "boards", id.ToString());
        _db.DeleteBoard(id);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        return RedirectToPage("/Boards");
    }

    private bool CanEditBoard(int boardId, int userId)
    {
        var board = _db.LoadAllBoards().FirstOrDefault(b => b.Id == boardId);
        if (board is null) return false;
        return _db.GetUserPermissionForBoard(boardId, userId, board.OwnerId) == "edit";
    }
}
