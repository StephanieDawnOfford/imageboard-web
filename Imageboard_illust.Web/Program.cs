using Imageboard_illust.Database;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSingleton<AppDatabase>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(1);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapGet("/boardimages/{boardId:int}/{fileName}", (int boardId, string fileName, HttpContext ctx, AppDatabase db) =>
{
    var userId = ctx.Session.GetInt32("UserId");
    if (userId is null || !db.CanUserAccessBoard(boardId, userId.Value))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    var safeFileName = Path.GetFileName(fileName);
    var path = Path.Combine(db.DataDir, "boards", boardId.ToString(), "images", safeFileName);
    if (!File.Exists(path)) return Results.NotFound();

    var contentType = Path.GetExtension(safeFileName).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".webp" => "image/webp",
        _ => "application/octet-stream",
    };
    return Results.File(path, contentType);
});

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
