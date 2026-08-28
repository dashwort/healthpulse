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
    public async Task Trend_shows_the_current_users_tracking_surface()
    {
        // Arrange
        var page = await fixture.Browser!.NewPageAsync();

        // Act
        await page.GotoAsync(fixture.BaseUrl + "/");

        // Assert
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Blood glucose", Exact = true }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("No readings in this range.", new() { Exact = true }))
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
                    new() { Name = "Templates", Exact = true }
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
        await Assertions.Expect(page.GetByText("Sleep quality", new() { Exact = true }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Template saved.", new() { Exact = true }))
            .ToBeVisibleAsync();
        await page.CloseAsync();
    }

    [Fact]
    public async Task History_page_exposes_measurement_and_date_filters()
    {
        // Arrange
        var page = await fixture.Browser!.NewPageAsync();

        // Act
        await page.GotoAsync(fixture.BaseUrl + "/readings");

        // Assert
        await Assertions.Expect(
                page.GetByRole(AriaRole.Heading, new() { Name = "History", Exact = true })
            )
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Combobox, new() { Name = "Measurement", Exact = true }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Textbox, new() { Name = "From", Exact = true }))
            .ToBeVisibleAsync();
        await page.CloseAsync();
    }

    [Fact]
    public async Task Quick_entry_creates_a_reading_visible_in_history()
    {
        // Arrange
        var page = await fixture.Browser!.NewPageAsync();
        await page.GotoAsync(fixture.BaseUrl + "/");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add reading", Exact = true })
            .ClickAsync();
        await page.GetByRole(AriaRole.Spinbutton, new() { Name = "Blood glucose", Exact = true })
            .FillAsync("6.4");

        // Act
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true })
            .ClickAsync();

        // Assert
        await Assertions.Expect(page.GetByText("Blood glucose saved.", new() { Exact = true }))
            .ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "History", Exact = true })
            .ClickAsync();
        await Assertions.Expect(page.GetByText("6.4 mmol/L", new() { Exact = true }))
            .ToBeVisibleAsync();
        await page.CloseAsync();
    }

    [Fact]
    public async Task Access_tokens_page_creates_and_revokes_a_token()
    {
        // Arrange
        var page = await fixture.Browser!.NewPageAsync();
        await page.GotoAsync(fixture.BaseUrl + "/tokens");
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Token name", Exact = true })
            .FillAsync("Browser test");

        // Act
        await page.GetByRole(AriaRole.Button, new() { Name = "Create token", Exact = true })
            .ClickAsync();

        // Assert
        await Assertions.Expect(page.GetByText("Copy this now", new() { Exact = true }))
            .ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Revoke", Exact = true })
            .ClickAsync();
        var dialog = page.GetByRole(AriaRole.Alertdialog);
        await Assertions.Expect(dialog.GetByRole(AriaRole.Heading, new() { Name = "Revoke token?" }))
            .ToBeVisibleAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Revoke", Exact = true })
            .ClickAsync();
        await Assertions.Expect(page.GetByText("Token revoked.", new() { Exact = true }))
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
