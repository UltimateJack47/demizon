using Demizon.Dal.Entities;
using Demizon.Mvc.Services.Rehearsal;
using MudBlazor;

namespace Demizon.Mvc.Pages.Admin.Attendance.Components;

/// <summary>
/// Zrušení a obnovení zkoušek z admin UI. Sdílí ho stránka výjimek a rychlá
/// akce v tabulce docházky, aby obě ukazovaly stejné dialogy a hlášky.
/// </summary>
internal static class RehearsalAdminActions
{
    /// <returns><c>true</c>, když se zkoušky zrušily (volající si má přenačíst data).</returns>
    public static async Task<bool> CancelAsync(
        IDialogService dialogs, ISnackbar snackbar, RehearsalWorkflow workflow,
        int byMemberId, DateTime? day = null)
    {
        var parameters = new DialogParameters { { nameof(CancelRehearsalDialog.Day), day } };
        var dialog = await dialogs.ShowAsync<CancelRehearsalDialog>(
            null, parameters, new DialogOptions { CloseOnEscapeKey = true });
        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not CancelRehearsalDialog.CancelRehearsalInput input)
            return false;

        var cancelled = await workflow.CancelAsync(input.From, input.To, input.Reason, byMemberId, input.Notify);
        if (!cancelled.IsSuccess)
        {
            snackbar.Add(cancelled.Error ?? "Zkoušky se nepodařilo zrušit.", Severity.Error);
            return false;
        }

        snackbar.Add(
            input.Notify
                ? $"Zkoušky zrušeny, upozornění dostalo {cancelled.Value!.NotifiedCount} členů."
                : "Zkoušky zrušeny.",
            Severity.Success);
        return true;
    }

    /// <returns><c>true</c>, když se výjimka smazala.</returns>
    public static async Task<bool> RestoreAsync(
        IDialogService dialogs, ISnackbar snackbar, RehearsalWorkflow workflow,
        RehearsalCancellation cancellation)
    {
        var range = cancellation.DateFrom == cancellation.DateTo
            ? $"{cancellation.DateFrom:d. M. yyyy}"
            : $"{cancellation.DateFrom:d. M.} – {cancellation.DateTo:d. M. yyyy}";
        var confirmed = await dialogs.ShowMessageBoxAsync(
            "Obnovit zkoušky?",
            $"Zkoušky {range} se vrátí i s docházkou, kterou na ně členové vyplnili.",
            "Obnovit", "Zpět");
        if (confirmed != true)
            return false;

        var restored = await workflow.RestoreAsync(cancellation.Id);
        if (!restored.IsSuccess)
        {
            snackbar.Add(restored.Error ?? "Zkoušky se nepodařilo obnovit.", Severity.Error);
            return false;
        }

        snackbar.Add("Zkoušky obnoveny.", Severity.Success);
        return true;
    }
}
