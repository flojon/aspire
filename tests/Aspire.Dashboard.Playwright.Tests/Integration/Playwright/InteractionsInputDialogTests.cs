// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Dashboard.Tests.Integration.Playwright.Infrastructure;
using Aspire.DashboardService.Proto.V1;
using Aspire.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Xunit;

namespace Aspire.Dashboard.Tests.Integration.Playwright;

[RequiresFeature(TestFeature.Playwright)]
public sealed class InteractionsInputDialogTests : PlaywrightTestsBase<DashboardServerFixture>
{
    public InteractionsInputDialogTests(DashboardServerFixture dashboardServerFixture)
        : base(dashboardServerFixture)
    {
    }

    [Fact]
    [OuterloopTest("Resource-intensive Playwright browser test")]
    public async Task ChoiceFirstDialog_FocusesDropdownAndSubmissionKeepsCircuitUsable()
    {
        var client = (MockDashboardClient)DashboardServerFixture.DashboardApp.Services.GetRequiredService<IDashboardClient>();

        await RunTestAsync(async page =>
        {
            await page.GotoAsync("/");

            client.AddInteraction(CreateInputsInteraction(
                interactionId: 1,
                new InteractionInput
                {
                    Name = "choice",
                    Label = "Choice",
                    InputType = InputType.Choice,
                    Required = true,
                    Options = { ["a"] = "Option A", ["b"] = "Option B" }
                },
                new InteractionInput { Name = "text", Label = "Text", InputType = InputType.Text }));

            var dialog = page.Locator("#interactions-input-dialog");
            var dropdown = dialog.Locator("fluent-dropdown");
            var control = dropdown.Locator("button[slot='control']");

            await Assertions.Expect(control).ToBeFocusedAsync();

            await control.ClickAsync();
            await dropdown.Locator("fluent-option[text='Option B']").ClickAsync();
            await page.Locator("fluent-button", new() { HasText = "Submit" }).ClickAsync();

            await Assertions.Expect(dialog).ToBeHiddenAsync();
            var request = Assert.Single(client.InteractionRequests);
            Assert.Equal(1, request.InteractionId);

            // A second dialog proves the circuit and interactions provider survived the first submission.
            client.AddInteraction(CreateInputsInteraction(
                interactionId: 2,
                new InteractionInput { Name = "text", Label = "Text", InputType = InputType.Text }));

            await Assertions.Expect(dialog.Locator("fluent-text-input input")).ToBeFocusedAsync();
        });
    }

    private static WatchInteractionsResponseUpdate CreateInputsInteraction(int interactionId, params InteractionInput[] inputs)
    {
        var interaction = new WatchInteractionsResponseUpdate
        {
            InteractionId = interactionId,
            Title = "Inputs",
            PrimaryButtonText = "Submit",
            InputsDialog = new InteractionInputsDialog()
        };
        interaction.InputsDialog.InputItems.AddRange(inputs);
        return interaction;
    }
}
