using OpenQA.Selenium;

namespace Imageboard_illust.Web.UiTests;

public class BoardTests : UiTestBase
{
    [SetUp]
    public void RegisterAndLogin()
    {
        RegisterUser(UniqueEmail("board"), "Passw0rd!");
        WaitUntil(() => Driver.FindElements(By.LinkText("Logout")).Count > 0);
    }

    [Test]
    public void CreateBoard_AppearsInBoardsList()
    {
        var name = "Test Board " + Guid.NewGuid().ToString("N")[..6];

        Driver.Navigate().GoToUrl(BaseUrl + "/Boards");
        Driver.FindElement(By.Id("NewBoardName")).SendKeys(name);
        Driver.FindElement(By.CssSelector("button.btn-success")).Click();

        Assert.That(Driver.FindElements(By.LinkText(name)).Count, Is.EqualTo(1));
    }

    [Test]
    public void UploadImage_CardAppearsOnCanvas()
    {
        var boardId = CreateBoardAndGetId("Upload Test Board");
        Driver.Navigate().GoToUrl($"{BaseUrl}/Board/{boardId}");

        Driver.FindElement(By.Name("files")).SendKeys(CreateTempPngFile());
        Driver.FindElement(By.CssSelector("button.btn-primary.btn-sm")).Click();

        Assert.That(Driver.FindElements(By.ClassName("board-card")).Count, Is.EqualTo(1));
    }

    [Test]
    public void RenameImage_UpdatesTitleViaAjax()
    {
        var boardId = CreateBoardAndGetId("Rename Test Board");
        Driver.Navigate().GoToUrl($"{BaseUrl}/Board/{boardId}");
        Driver.FindElement(By.Name("files")).SendKeys(CreateTempPngFile());
        Driver.FindElement(By.CssSelector("button.btn-primary.btn-sm")).Click();

        var jsExecutor = (IJavaScriptExecutor)Driver;
        var nameEl = Driver.FindElement(By.ClassName("board-card-name"));

        // Rename uses a native prompt() dialog; drive it by overriding window.prompt before the double-click.
        // Dispatching the 'dblclick' DOM event directly (rather than a WebDriver action chain) avoids
        // headless-mode flakiness around synthetic double-click targeting.
        jsExecutor.ExecuteScript("window.prompt = function() { return 'Renamed Image'; };");
        jsExecutor.ExecuteScript(
            "arguments[0].dispatchEvent(new MouseEvent('dblclick', { bubbles: true, cancelable: true, view: window }));",
            nameEl);

        WaitUntil(() => Driver.FindElement(By.ClassName("board-card-name")).Text == "Renamed Image");
        Assert.That(Driver.FindElement(By.ClassName("board-card-name")).Text, Is.EqualTo("Renamed Image"));
    }

    [Test]
    public void DeleteImage_RemovesCardFromCanvas()
    {
        var boardId = CreateBoardAndGetId("Delete Test Board");
        Driver.Navigate().GoToUrl($"{BaseUrl}/Board/{boardId}");
        Driver.FindElement(By.Name("files")).SendKeys(CreateTempPngFile());
        Driver.FindElement(By.CssSelector("button.btn-primary.btn-sm")).Click();

        Driver.FindElement(By.ClassName("board-card-close")).Click();
        Driver.SwitchTo().Alert().Accept();

        WaitUntil(() => Driver.FindElements(By.ClassName("board-card")).Count == 0);
        Assert.That(Driver.FindElements(By.ClassName("board-card")).Count, Is.EqualTo(0));
    }

    [Test]
    public void DeleteBoard_RemovesItFromBoardsList()
    {
        var name = "ToDelete " + Guid.NewGuid().ToString("N")[..6];
        var boardId = CreateBoardAndGetId(name);
        Driver.Navigate().GoToUrl($"{BaseUrl}/Board/{boardId}");

        Driver.FindElement(By.CssSelector("button.btn-outline-danger")).Click();
        Driver.SwitchTo().Alert().Accept();

        WaitUntil(() => Driver.Url.Contains("/Boards"));
        Assert.That(Driver.PageSource, Does.Not.Contain(name));
    }

    private int CreateBoardAndGetId(string name)
    {
        Driver.Navigate().GoToUrl(BaseUrl + "/Boards");
        Driver.FindElement(By.Id("NewBoardName")).SendKeys(name);
        Driver.FindElement(By.CssSelector("button.btn-success")).Click();

        var link = Driver.FindElement(By.LinkText(name));
        var href = link.GetDomAttribute("href")!;
        return int.Parse(href.TrimEnd('/').Split('/').Last());
    }
}
