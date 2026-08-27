using Microsoft.Playwright;

namespace HealthTracker.Web.Tests;

public sealed class BrowserRegressionTests : IClassFixture<PlaywrightFixture>
{
    private readonly PlaywrightFixture fixture;

    public BrowserRegressionTests(PlaywrightFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Dashboard_shows_the_current_users_tracking_surface()
    {
        // Arrange
        var page = await fixture.Browser!.NewPageAsync();

        // Act
        await page.GotoAsync(fixture.BaseUrl + "/");

        // Assert
        await Assertions.Expect(
                page.GetByRole(AriaRole.Heading, new() { Name = "Your health, at a glance" })
            )
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Blood glucose", new() { Exact = true }))
            .ToBeVisibleAsync();
        await page.CloseAsync();
    }

    [Fact]
    public async Task Navigation_opens_the_templates_page()
    {
        // Arrange
        var page = await fixture.Browser!.NewPageAsync();
        await page.GotoAsync(fixture.BaseUrl + "/");

        // Act
        await page.GetByRole(AriaRole.Link, new() { Name = "Templates", Exact = true }).ClickAsync();

        // Assert
        await Assertions.Expect(
                page.GetByRole(
                    AriaRole.Heading,
                    new() { Name = "Measurement templates", Exact = true }
                )
            )
            .ToBeVisibleAsync();
        await page.CloseAsync();
    }

    [Fact]
    public async Task Templates_page_creates_a_custom_template()
    {
        // Arrange
        var page = await fixture.Browser!.NewPageAsync();
        await page.GotoAsync(fixture.BaseUrl + "/templates");
        await page.WaitForTimeoutAsync(1000);
        await page.GetByRole(
                AriaRole.Button,
                new() { Name = "Custom template", Exact = true }
            )
            .ClickAsync();
        await Assertions.Expect(page.GetByText("Create custom template", new() { Exact = true }))
            .ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Textbox).Nth(0).FillAsync("Sleep quality");
        await page.GetByRole(AriaRole.Textbox).Nth(1).FillAsync("Lifestyle");
        await page.GetByRole(AriaRole.Textbox).Nth(2).FillAsync("score");

        // Act
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        // Assert
        await Assertions.Expect(
                page.GetByRole(
                    AriaRole.Row,
                    new()
                    {
                        Name =
                            "Sleep quality Custom Lifestyle score Edit custom template Delete custom template",
                    }
                )
            )
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Template saved.", new() { Exact = true }))
            .ToBeVisibleAsync();
        await page.CloseAsync();
    }

    [Fact]
    public async Task Readings_page_shows_the_empty_history_state()
    {
        // Arrange
        var page = await fixture.Browser!.NewPageAsync();

        // Act
        await page.GotoAsync(fixture.BaseUrl + "/readings");

        // Assert
        await Assertions.Expect(
                page.GetByRole(AriaRole.Heading, new() { Name = "Readings", Exact = true })
            )
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("No readings match these filters.", new() { Exact = true }))
            .ToBeVisibleAsync();
        await page.CloseAsync();
    }

    [Fact]
    public async Task App_information_page_shows_the_configured_android_release()
    {
        // Arrange
        var page = await fixture.Browser!.NewPageAsync();

        // Act
        await page.GotoAsync(fixture.BaseUrl + "/settings");

        // Assert
        await Assertions.Expect(
                page.GetByRole(AriaRole.Heading, new() { Name = "App information", Exact = true })
            )
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Version 1.2.3", new() { Exact = true }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Download APK" }))
            .ToBeVisibleAsync();
        await page.CloseAsync();
    }

    [Fact]
    public async Task Application_logs_page_exposes_admin_diagnostics()
    {
        // Arrange
        var page = await fixture.Browser!.NewPageAsync();

        // Act
        await page.GotoAsync(fixture.BaseUrl + "/logs");

        // Assert
        await Assertions.Expect(
                page.GetByRole(AriaRole.Heading, new() { Name = "Application logs", Exact = true })
            )
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Open text view", new() { Exact = true }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Download .txt", new() { Exact = true }))
            .ToBeVisibleAsync();
        await page.CloseAsync();
    }

    [Fact]
    public async Task Narrow_view_can_open_the_navigation_menu()
    {
        // Arrange
        var page = await fixture.Browser!.NewPageAsync(
            new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = 375, Height = 800 } }
        );
        await page.GotoAsync(fixture.BaseUrl + "/");

        // Act
        await page.GetByRole(AriaRole.Button, new() { Name = "Open navigation menu" }).ClickAsync();

        // Assert
        await Assertions.Expect(
                page.GetByRole(AriaRole.Link, new() { Name = "Templates", Exact = true })
            )
            .ToBeVisibleAsync();
        await page.CloseAsync();
    }
}
