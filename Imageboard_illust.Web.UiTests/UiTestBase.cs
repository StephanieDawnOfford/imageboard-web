using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace Imageboard_illust.Web.UiTests;

public abstract class UiTestBase
{
    protected IWebDriver Driver { get; private set; } = null!;
    protected static string BaseUrl => WebAppFixture.BaseUrl;

    [SetUp]
    public void SetUpDriver()
    {
        var options = new ChromeOptions();
        if (Environment.GetEnvironmentVariable("HEADLESS") != "false")
            options.AddArgument("--headless=new");
        options.AddArgument("--window-size=1280,900");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--no-sandbox");

        Driver = new ChromeDriver(options);
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
    }

    [TearDown]
    public void TearDownDriver()
    {
        Driver.Quit();
        Driver.Dispose();
    }

    protected static string UniqueEmail(string label) => $"{label}.{Guid.NewGuid():N}@example.com";

    protected static void WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            Thread.Sleep(100);
        }
        throw new TimeoutException("Condition was not met in time.");
    }

    // Driver.PageSource is a raw snapshot with no implicit wait, unlike FindElement. A click that
    // triggers a full-page navigation can return just before the new page has finished rendering,
    // so poll instead of reading PageSource exactly once.
    protected void WaitUntilPageContains(string text, int timeoutMs = 5000)
    {
        try
        {
            WaitUntil(() => Driver.PageSource.Contains(text), timeoutMs);
        }
        catch (TimeoutException)
        {
            // fall through so the caller's Assert produces a readable diff
        }
    }

    protected void RegisterUser(string email, string password)
    {
        Driver.Navigate().GoToUrl(BaseUrl + "/Register");
        Driver.FindElement(By.Id("Email")).SendKeys(email);
        Driver.FindElement(By.Id("Password")).SendKeys(password);
        Driver.FindElement(By.Id("ConfirmPassword")).SendKeys(password);
        Driver.FindElement(By.CssSelector("button[type=submit]")).Click();
    }

    protected static string CreateTempPngFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"uitest_{Guid.NewGuid():N}.png");
        var bytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
