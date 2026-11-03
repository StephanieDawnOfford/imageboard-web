using OpenQA.Selenium;

namespace Imageboard_illust.Web.UiTests;

public class AuthTests : UiTestBase
{
    [Test]
    public void HomePage_Loads()
    {
        Driver.Navigate().GoToUrl(BaseUrl + "/");
        Assert.That(Driver.Title, Does.Contain("Imageboard"));
    }

    [Test]
    public void Register_NewUser_LogsInAutomatically()
    {
        var email = UniqueEmail("register");
        RegisterUser(email, "Passw0rd!");

        WaitUntilPageContains(email);
        Assert.That(Driver.PageSource, Does.Contain(email));
        Assert.That(Driver.FindElements(By.LinkText("Logout")).Count, Is.GreaterThan(0));
    }

    [Test]
    public void Register_DuplicateEmail_ShowsError()
    {
        var email = UniqueEmail("dupe");
        RegisterUser(email, "Passw0rd!");
        Driver.FindElement(By.LinkText("Logout")).Click();

        RegisterUser(email, "Passw0rd!");

        WaitUntilPageContains("already exists");
        Assert.That(Driver.PageSource, Does.Contain("already exists"));
    }

    [Test]
    public void Login_WithWrongPassword_ShowsError()
    {
        var email = UniqueEmail("wrongpw");
        RegisterUser(email, "Correct123!");
        Driver.FindElement(By.LinkText("Logout")).Click();

        Driver.Navigate().GoToUrl(BaseUrl + "/Login");
        Driver.FindElement(By.Id("Email")).SendKeys(email);
        Driver.FindElement(By.Id("Password")).SendKeys("WrongPassword!");
        Driver.FindElement(By.CssSelector("button[type=submit]")).Click();

        WaitUntilPageContains("Invalid email or password");
        Assert.That(Driver.PageSource, Does.Contain("Invalid email or password"));
    }

    [Test]
    public void Login_WithCorrectPassword_Succeeds()
    {
        var email = UniqueEmail("rightpw");
        RegisterUser(email, "Correct123!");
        Driver.FindElement(By.LinkText("Logout")).Click();

        Driver.Navigate().GoToUrl(BaseUrl + "/Login");
        Driver.FindElement(By.Id("Email")).SendKeys(email);
        Driver.FindElement(By.Id("Password")).SendKeys("Correct123!");
        Driver.FindElement(By.CssSelector("button[type=submit]")).Click();

        WaitUntilPageContains(email);
        Assert.That(Driver.PageSource, Does.Contain(email));
    }

    [Test]
    public void Logout_ReturnsToLoggedOutNav()
    {
        var email = UniqueEmail("logout");
        RegisterUser(email, "Passw0rd!");

        Driver.FindElement(By.LinkText("Logout")).Click();

        Assert.That(Driver.FindElements(By.LinkText("Login")).Count, Is.GreaterThan(0));
    }
}
